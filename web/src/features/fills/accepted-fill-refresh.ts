import type { QueryClient } from '@tanstack/react-query'

import {
  getGetPositionsQueryKey,
  getPositions,
  type getPositionsResponse,
} from '../../api/generated/positions/positions'
import { ApiError } from '../../api/http/api-fetch'

export const acceptedFillPollDurationMs = 10_000
export const acceptedFillPollIntervalMs = 1_000

type PositionsFetcher = (signal: AbortSignal) => Promise<getPositionsResponse>

interface AcceptedFillPollOptions {
  durationMs?: number
  fetchPositions?: PositionsFetcher
  intervalMs?: number
  now?: () => number
}

interface PositionsRequestSnapshot {
  dataUpdateCount: number
  sequence: number
}

interface PositionsRequestCoordinator {
  latestSequence: number
}

const positionsRequestCoordinators = new WeakMap<QueryClient, PositionsRequestCoordinator>()

class PollDeadlineReached extends Error {
  constructor() {
    super('The accepted-fill refresh deadline was reached.')
    this.name = 'PollDeadlineReached'
  }
}

const abortError = (): DOMException => new DOMException('Polling cancelled.', 'AbortError')

const isAbortError = (error: unknown): boolean =>
  (error instanceof DOMException && error.name === 'AbortError') ||
  (typeof error === 'object' && error !== null && 'name' in error && error.name === 'AbortError')

const isTransientError = (error: unknown): boolean =>
  error instanceof ApiError &&
  (error.status === 0 || error.status === 408 || error.status === 429 || error.status >= 500)

const beginPositionsRequest = (queryClient: QueryClient): PositionsRequestSnapshot => {
  const coordinator = positionsRequestCoordinators.get(queryClient) ?? { latestSequence: 0 }
  coordinator.latestSequence += 1
  positionsRequestCoordinators.set(queryClient, coordinator)

  return {
    dataUpdateCount:
      queryClient.getQueryState<getPositionsResponse>(getGetPositionsQueryKey())?.dataUpdateCount ?? 0,
    sequence: coordinator.latestSequence,
  }
}

const publishCurrentPositionsResponse = (
  queryClient: QueryClient,
  snapshot: PositionsRequestSnapshot,
  response: getPositionsResponse,
): void => {
  const coordinator = positionsRequestCoordinators.get(queryClient)
  const state = queryClient.getQueryState<getPositionsResponse>(getGetPositionsQueryKey())

  if (
    coordinator?.latestSequence !== snapshot.sequence ||
    (state?.dataUpdateCount ?? 0) !== snapshot.dataUpdateCount ||
    state?.fetchStatus === 'fetching'
  ) {
    return
  }

  queryClient.setQueryData(getGetPositionsQueryKey(), response)
}

export function positionFingerprint(
  response: getPositionsResponse | undefined,
  symbol: string,
): string | undefined {
  if (!response || response.status !== 200 || !Array.isArray(response.data)) return undefined

  const position = response.data.find(
    (candidate) => candidate.symbol?.toUpperCase() === symbol.toUpperCase(),
  )
  return position ? JSON.stringify(position) : 'absent'
}

const wait = (duration: number, signal: AbortSignal): Promise<void> =>
  new Promise((resolve, reject) => {
    if (signal.aborted) {
      reject(abortError())
      return
    }

    const onAbort = () => {
      window.clearTimeout(timeout)
      reject(abortError())
    }
    const timeout = window.setTimeout(() => {
      signal.removeEventListener('abort', onAbort)
      resolve()
    }, duration)
    signal.addEventListener('abort', onAbort, { once: true })
  })

const fetchBeforeDeadline = async (
  fetchPositions: PositionsFetcher,
  signal: AbortSignal,
  remainingMs: number,
): Promise<getPositionsResponse> => {
  if (signal.aborted) throw abortError()

  const requestController = new AbortController()
  let deadlineReached = false
  let callerAborted = false
  let rejectBoundary: (error: Error) => void = () => undefined
  const boundary = new Promise<never>((_resolve, reject) => {
    rejectBoundary = reject
  })
  const onAbort = () => {
    callerAborted = true
    requestController.abort()
    rejectBoundary(abortError())
  }
  const deadlineTimer = window.setTimeout(() => {
    deadlineReached = true
    requestController.abort()
    rejectBoundary(new PollDeadlineReached())
  }, remainingMs)

  signal.addEventListener('abort', onAbort, { once: true })

  try {
    return await Promise.race([fetchPositions(requestController.signal), boundary])
  } catch (error) {
    if (callerAborted || signal.aborted) throw abortError()
    if (deadlineReached) throw new PollDeadlineReached()
    throw error
  } finally {
    window.clearTimeout(deadlineTimer)
    signal.removeEventListener('abort', onAbort)
  }
}

export async function observeAcceptedFill(
  queryClient: QueryClient,
  symbol: string,
  baseline: string | undefined,
  signal: AbortSignal,
  options: AcceptedFillPollOptions = {},
): Promise<'observed' | 'timed-out'> {
  const durationMs = options.durationMs ?? acceptedFillPollDurationMs
  const intervalMs = options.intervalMs ?? acceptedFillPollIntervalMs
  const now = options.now ?? Date.now
  const fetchPositions =
    options.fetchPositions ?? ((requestSignal: AbortSignal) => getPositions({ signal: requestSignal }))
  const deadline = now() + durationMs

  while (now() < deadline) {
    if (signal.aborted) throw abortError()

    let response: getPositionsResponse
    const requestSnapshot = beginPositionsRequest(queryClient)
    try {
      response = await fetchBeforeDeadline(fetchPositions, signal, deadline - now())
    } catch (error) {
      if (error instanceof PollDeadlineReached) return 'timed-out'
      if (isAbortError(error)) throw error
      if (!isTransientError(error)) throw error

      const remainingAfterFailure = deadline - now()
      if (remainingAfterFailure <= 0) return 'timed-out'
      await wait(Math.min(intervalMs, remainingAfterFailure), signal)
      continue
    }

    if (response.status === 200) {
      // Publish only when neither a newer poll request nor the screen's
      // canonical query has superseded this direct response.
      publishCurrentPositionsResponse(queryClient, requestSnapshot, response)
    }
    const current = positionFingerprint(response, symbol)

    if (baseline !== undefined && current !== undefined && current !== baseline) {
      return 'observed'
    }

    const remaining = deadline - now()
    if (remaining <= 0) return 'timed-out'
    await wait(Math.min(intervalMs, remaining), signal)
  }

  return 'timed-out'
}
