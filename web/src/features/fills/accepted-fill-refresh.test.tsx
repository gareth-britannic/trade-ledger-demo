import type { QueryClient } from '@tanstack/react-query'
import { afterEach, describe, expect, it, vi } from 'vitest'

import type { PositionResponse } from '../../api/generated/model'
import type { getPositionsResponse } from '../../api/generated/positions/positions'
import {
  acceptedFillPollAttempts,
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

function pollingClient(fetchQuery: () => Promise<getPositionsResponse>) {
  const fetchQueryMock = vi.fn(fetchQuery)
  const queryClient = {
    fetchQuery: fetchQueryMock,
  } as unknown as QueryClient

  return { fetchQuery: fetchQueryMock, queryClient }
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
    const baselineResponse = responseFor([applePosition])
    const changedResponse = responseFor([
      { ...applePosition, openQuantity: 12 },
    ])
    const responses = [baselineResponse, changedResponse]
    const { fetchQuery, queryClient } = pollingClient(() => {
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
    )

    await vi.runAllTimersAsync()

    await expect(observation).resolves.toBe('observed')
    expect(fetchQuery).toHaveBeenCalledTimes(2)
  })

  it('performs only the bounded number of refresh attempts before timing out', async () => {
    vi.useFakeTimers()
    const baselineResponse = responseFor([applePosition])
    const { fetchQuery, queryClient } = pollingClient(() =>
      Promise.resolve(baselineResponse),
    )

    const observation = observeAcceptedFill(
      queryClient,
      'AAPL',
      positionFingerprint(baselineResponse, 'AAPL'),
      new AbortController().signal,
    )

    await vi.runAllTimersAsync()

    await expect(observation).resolves.toBe('timed-out')
    expect(fetchQuery).toHaveBeenCalledTimes(acceptedFillPollAttempts)
  })
})
