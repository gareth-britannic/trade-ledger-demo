import { QueryClient } from '@tanstack/react-query'
import { afterEach, describe, expect, it, vi } from 'vitest'

import type { PositionResponse } from '../../api/generated/model'
import {
  getGetPositionsQueryKey,
  type getPositionsResponse,
} from '../../api/generated/positions/positions'
import { ApiError } from '../../api/http/api-fetch'
import {
  observeAcceptedFill,
  positionFingerprint,
} from './accepted-fill-refresh'

const responseFor = (positions: PositionResponse[]): getPositionsResponse => ({
  data: positions,
  headers: new Headers(),
  status: 200,
})

const applePosition = {
  symbol: 'AAPL',
  openQuantity: 10,
  averageUnitCost: 190,
  realisedPnl: 0,
} satisfies PositionResponse

const pollingClient = () => new QueryClient({ defaultOptions: { queries: { retry: false } } })

function deferred<T>() {
  let resolve: (value: T | PromiseLike<T>) => void = () => undefined
  const promise = new Promise<T>((resolvePromise) => {
    resolve = resolvePromise
  })
  return { promise, resolve }
}

afterEach(() => {
  vi.useRealTimers()
})

describe('positionFingerprint', () => {
  it('finds symbols case-insensitively and distinguishes an absent position', () => {
    const response = responseFor([applePosition])

    expect(positionFingerprint(response, 'aapl')).toBe(JSON.stringify(applePosition))
    expect(positionFingerprint(response, 'MSFT')).toBe('absent')
    expect(positionFingerprint(undefined, 'AAPL')).toBeUndefined()
  })
})

describe('observeAcceptedFill', () => {
  it('stops polling as soon as the selected position changes', async () => {
    vi.useFakeTimers()
    const queryClient = pollingClient()
    const baselineResponse = responseFor([applePosition])
    const changedResponse = responseFor([
      { ...applePosition, openQuantity: 12 },
    ])
    const responses = [baselineResponse, changedResponse]
    const fetchPositions = vi.fn(() => {
      const response = responses.shift()
      return response
        ? Promise.resolve(response)
        : Promise.reject(new Error('Unexpected extra poll.'))
    })

    const observation = observeAcceptedFill(
      queryClient,
      'AAPL',
      positionFingerprint(baselineResponse, 'AAPL'),
      new AbortController().signal,
      { fetchPositions },
    )

    await vi.advanceTimersByTimeAsync(1_000)

    await expect(observation).resolves.toBe('observed')
    expect(fetchPositions).toHaveBeenCalledTimes(2)
    expect(queryClient.getQueryData(getGetPositionsQueryKey())).toEqual(changedResponse)
  })

  it('retries transient failures inside the deadline', async () => {
    vi.useFakeTimers()
    const queryClient = pollingClient()
    const baselineResponse = responseFor([applePosition])
    const changedResponse = responseFor([{ ...applePosition, openQuantity: 12 }])
    const fetchPositions = vi
      .fn()
      .mockRejectedValueOnce(new ApiError({ status: 0, title: 'Unavailable' }))
      .mockResolvedValueOnce(changedResponse)

    const observation = observeAcceptedFill(
      queryClient,
      'AAPL',
      positionFingerprint(baselineResponse, 'AAPL'),
      new AbortController().signal,
      { durationMs: 2_000, fetchPositions, intervalMs: 100 },
    )

    await vi.advanceTimersByTimeAsync(100)

    await expect(observation).resolves.toBe('observed')
    expect(fetchPositions).toHaveBeenCalledTimes(2)
  })

  it('does not let an older overlapping poll response overwrite a newer one', async () => {
    const queryClient = pollingClient()
    const baselineResponse = responseFor([applePosition])
    const olderResponse = responseFor([{ ...applePosition, openQuantity: 11 }])
    const newerResponse = responseFor([{ ...applePosition, openQuantity: 12 }])
    const olderGate = deferred<getPositionsResponse>()
    const newerGate = deferred<getPositionsResponse>()

    const olderObservation = observeAcceptedFill(
      queryClient,
      'AAPL',
      positionFingerprint(baselineResponse, 'AAPL'),
      new AbortController().signal,
      { fetchPositions: () => olderGate.promise },
    )
    const newerObservation = observeAcceptedFill(
      queryClient,
      'AAPL',
      positionFingerprint(baselineResponse, 'AAPL'),
      new AbortController().signal,
      { fetchPositions: () => newerGate.promise },
    )

    newerGate.resolve(newerResponse)
    await expect(newerObservation).resolves.toBe('observed')
    expect(queryClient.getQueryData(getGetPositionsQueryKey())).toEqual(newerResponse)

    olderGate.resolve(olderResponse)
    await expect(olderObservation).resolves.toBe('observed')
    expect(queryClient.getQueryData(getGetPositionsQueryKey())).toEqual(newerResponse)
  })

  it('does not overwrite a canonical screen query that completed after polling began', async () => {
    const queryClient = pollingClient()
    const baselineResponse = responseFor([applePosition])
    const pollResponse = responseFor([{ ...applePosition, openQuantity: 11 }])
    const screenResponse = responseFor([{ ...applePosition, openQuantity: 12 }])
    const pollGate = deferred<getPositionsResponse>()
    const screenGate = deferred<getPositionsResponse>()
    let screenSignal: AbortSignal | undefined
    queryClient.setQueryData(getGetPositionsQueryKey(), baselineResponse)

    const observation = observeAcceptedFill(
      queryClient,
      'AAPL',
      positionFingerprint(baselineResponse, 'AAPL'),
      new AbortController().signal,
      { fetchPositions: () => pollGate.promise },
    )
    const screenRefresh = queryClient.fetchQuery({
      queryKey: getGetPositionsQueryKey(),
      queryFn: ({ signal }) => {
        screenSignal = signal
        return screenGate.promise
      },
    })

    screenGate.resolve(screenResponse)
    await expect(screenRefresh).resolves.toEqual(screenResponse)
    expect(screenSignal?.aborted).toBe(false)
    expect(queryClient.getQueryData(getGetPositionsQueryKey())).toEqual(screenResponse)

    pollGate.resolve(pollResponse)
    await expect(observation).resolves.toBe('observed')
    expect(screenSignal?.aborted).toBe(false)
    expect(queryClient.getQueryData(getGetPositionsQueryKey())).toEqual(screenResponse)
  })

  it('stops at the elapsed-time deadline after unchanged responses', async () => {
    vi.useFakeTimers()
    const queryClient = pollingClient()
    const baselineResponse = responseFor([applePosition])
    const fetchPositions = vi.fn(() => Promise.resolve(baselineResponse))
    const controller = new AbortController()
    const observation = observeAcceptedFill(
      queryClient,
      'AAPL',
      positionFingerprint(baselineResponse, 'AAPL'),
      controller.signal,
      { durationMs: 2_500, fetchPositions, intervalMs: 1_000 },
    )

    await vi.advanceTimersByTimeAsync(2_500)

    await expect(observation).resolves.toBe('timed-out')
    expect(fetchPositions).toHaveBeenCalledTimes(3)
  })

  it('aborts the actual in-flight request when its caller cancels', async () => {
    const queryClient = pollingClient()
    const controller = new AbortController()
    let requestSignal: AbortSignal | undefined
    const fetchPositions = vi.fn(
      (signal: AbortSignal) =>
        new Promise<getPositionsResponse>((_resolve, reject) => {
          requestSignal = signal
          signal.addEventListener(
            'abort',
            () => reject(new DOMException('Request cancelled.', 'AbortError')),
            { once: true },
          )
        }),
    )
    const observation = observeAcceptedFill(
      queryClient,
      'AAPL',
      'absent',
      controller.signal,
      { fetchPositions },
    )

    await vi.waitFor(() => expect(requestSignal).toBeDefined())
    controller.abort()

    await expect(observation).rejects.toMatchObject({ name: 'AbortError' })
    expect(requestSignal?.aborted).toBe(true)
    expect(fetchPositions).toHaveBeenCalledOnce()
  })

  it('aborts a stalled request and resolves at the deadline', async () => {
    vi.useFakeTimers()
    const queryClient = pollingClient()
    let requestSignal: AbortSignal | undefined
    const fetchPositions = vi.fn(
      (signal: AbortSignal) =>
        new Promise<getPositionsResponse>((_resolve, reject) => {
          requestSignal = signal
          signal.addEventListener(
            'abort',
            () => reject(new DOMException('Request deadline reached.', 'AbortError')),
            { once: true },
          )
        }),
    )
    const observation = observeAcceptedFill(
      queryClient,
      'AAPL',
      'absent',
      new AbortController().signal,
      { durationMs: 250, fetchPositions },
    )

    await vi.advanceTimersByTimeAsync(250)

    await expect(observation).resolves.toBe('timed-out')
    expect(requestSignal?.aborted).toBe(true)
    expect(fetchPositions).toHaveBeenCalledOnce()
  })
})
