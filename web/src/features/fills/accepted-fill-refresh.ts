import type { QueryClient } from '@tanstack/react-query'

import {
  getGetPositionsQueryOptions,
  type getPositionsResponse,
} from '../../api/generated/positions/positions'

export const acceptedFillPollAttempts = 10
export const acceptedFillPollIntervalMs = 1_000

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
    const timeout = window.setTimeout(resolve, duration)
    signal.addEventListener(
      'abort',
      () => {
        window.clearTimeout(timeout)
        reject(new DOMException('Polling cancelled.', 'AbortError'))
      },
      { once: true },
    )
  })

export async function observeAcceptedFill(
  queryClient: QueryClient,
  symbol: string,
  baseline: string | undefined,
  signal: AbortSignal,
): Promise<'observed' | 'timed-out'> {
  for (let attempt = 0; attempt < acceptedFillPollAttempts; attempt += 1) {
    if (signal.aborted) throw new DOMException('Polling cancelled.', 'AbortError')

    const response = await queryClient.fetchQuery({
      ...getGetPositionsQueryOptions(),
      staleTime: 0,
    })
    const current = positionFingerprint(response, symbol)

    if (baseline !== undefined && current !== undefined && current !== baseline) {
      return 'observed'
    }

    if (attempt < acceptedFillPollAttempts - 1) {
      await wait(acceptedFillPollIntervalMs, signal)
    }
  }

  return 'timed-out'
}
