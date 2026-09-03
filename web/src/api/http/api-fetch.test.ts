import { afterEach, describe, expect, it, vi } from 'vitest'

import {
  endAuthSession,
  establishAuthSession,
  getAuthSession,
} from '../../features/auth/session-bridge'
import { createAppQueryClient } from '../../app/providers/query-client'
import { ApiError, apiFetch } from './api-fetch'

interface TestEnvelope<T> {
  data: T
  headers: Headers
  status: number
}

afterEach(() => {
  endAuthSession('logout')
  window.sessionStorage.clear()
  vi.unstubAllGlobals()
})

describe('apiFetch', () => {
  it('uses a relative URL, attaches the access token, and returns the Orval response envelope', async () => {
    establishAuthSession({
      accessToken: 'access-token',
      email: 'demo@trade-ledger.local',
      expiresAt: Date.now() + 60_000,
    })
    const fetchMock = vi.fn<typeof fetch>()
    fetchMock.mockResolvedValue(
      new Response(JSON.stringify([{ symbol: 'ACME' }]), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      }),
    )
    vi.stubGlobal('fetch', fetchMock)

    const response = await apiFetch<TestEnvelope<Array<{ symbol: string }>>>('/api/positions', {
      method: 'GET',
    })

    expect(response.data).toEqual([{ symbol: 'ACME' }])
    expect(response.status).toBe(200)
    expect(response.headers).toBeInstanceOf(Headers)
    const [requestUrl, requestInit] = fetchMock.mock.calls[0]!
    expect(requestUrl).toBe('/api/positions')
    expect(new Headers(requestInit?.headers).get('Authorization')).toBe('Bearer access-token')
  })

  it('normalizes validation extensions and prefers the body correlation ID', async () => {
    const fetchMock = vi.fn(async () =>
      Promise.resolve(
        new Response(
          JSON.stringify({
            type: 'urn:trade-ledger:problem:validation',
            title: 'One or more validation errors occurred.',
            status: 400,
            errors: {
              Symbol: ['Symbol is invalid.'],
              nested: { price: ['Price is invalid.'] },
            },
            correlationId: 'body-correlation',
          }),
          {
            status: 400,
            headers: {
              'Content-Type': 'application/problem+json',
              'X-Correlation-Id': 'header-correlation',
            },
          },
        ),
      ),
    )
    vi.stubGlobal('fetch', fetchMock)

    const error = await apiFetch('/api/fills', { method: 'POST' }).catch(
      (caught: unknown) => caught,
    )

    expect(error).toBeInstanceOf(ApiError)
    expect(error).toMatchObject({
      status: 400,
      correlationId: 'body-correlation',
      fieldErrors: {
        Symbol: ['Symbol is invalid.'],
        nested: ['Price is invalid.'],
      },
    })
  })

  it('takes a correlation ID from the response header and clears the current session on an empty 401', async () => {
    establishAuthSession({
      accessToken: 'rejected-token',
      email: 'demo@trade-ledger.local',
      expiresAt: Date.now() + 60_000,
    })
    vi.stubGlobal(
      'fetch',
      vi.fn(async () =>
        Promise.resolve(
          new Response(null, {
            status: 401,
            statusText: 'Unauthorized',
            headers: { 'X-Correlation-Id': 'header-only-correlation' },
          }),
        ),
      ),
    )

    const error = await apiFetch('/api/positions', { method: 'GET' }).catch(
      (caught: unknown) => caught,
    )

    expect(error).toMatchObject({
      status: 401,
      title: 'Unauthorized',
      correlationId: 'header-only-correlation',
    })
    expect(getAuthSession()).toBeNull()
  })

  it('does not let a delayed 401 clear a newer authentication session', async () => {
    establishAuthSession({
      accessToken: 'superseded-token',
      email: 'first@trade-ledger.local',
      expiresAt: Date.now() + 60_000,
    })
    let resolveResponse: ((response: Response) => void) | undefined
    const responsePending = new Promise<Response>((resolve) => {
      resolveResponse = resolve
    })
    const fetchMock = vi.fn<typeof fetch>(() => responsePending)
    vi.stubGlobal('fetch', fetchMock)

    const rejectedRequest = apiFetch('/api/positions', { method: 'GET' })
    establishAuthSession({
      accessToken: 'current-token',
      email: 'second@trade-ledger.local',
      expiresAt: Date.now() + 60_000,
    })

    resolveResponse?.(new Response(null, { status: 401, statusText: 'Unauthorized' }))

    await expect(rejectedRequest).rejects.toMatchObject({ status: 401 })
    expect(getAuthSession()).toMatchObject({
      accessToken: 'current-token',
      email: 'second@trade-ledger.local',
    })
    const requestHeaders = new Headers(fetchMock.mock.calls[0]?.[1]?.headers)
    expect(requestHeaders.get('Authorization')).toBe('Bearer superseded-token')
  })

  it('normalizes transport failures without exposing the original message', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => Promise.reject(new TypeError('sensitive transport detail'))))

    const error = await apiFetch('/api/positions', { method: 'GET' }).catch(
      (caught: unknown) => caught,
    )

    expect(error).toMatchObject({
      status: 0,
      title: 'Trade Ledger is unavailable',
    })
    expect((error as Error).message).not.toContain('sensitive transport detail')
  })

  it('preserves request cancellation instead of reporting the API as unavailable', async () => {
    const cancellation = new DOMException('The request was cancelled.', 'AbortError')
    vi.stubGlobal('fetch', vi.fn(async () => Promise.reject(cancellation)))

    await expect(apiFetch('/api/positions', { method: 'GET' })).rejects.toBe(cancellation)
  })

  it('falls back safely when an error response claims JSON but contains malformed text', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () =>
        Promise.resolve(
          new Response('{not-json', {
            status: 502,
            statusText: 'Bad Gateway',
            headers: { 'Content-Type': 'application/json' },
          }),
        ),
      ),
    )

    const error = await apiFetch('/api/positions', { method: 'GET' }).catch(
      (caught: unknown) => caught,
    )

    expect(error).toMatchObject({
      status: 502,
      title: 'Bad Gateway',
      body: '{not-json',
    })
  })

  it('rejects absolute URLs before a bearer token can be sent cross-origin', async () => {
    const fetchMock = vi.fn()
    vi.stubGlobal('fetch', fetchMock)

    await expect(apiFetch('https://example.test/api/positions', { method: 'GET' })).rejects.toMatchObject({
      status: 0,
      title: 'Invalid API request',
    })
    expect(fetchMock).not.toHaveBeenCalled()
  })
})

describe('app query client policy', () => {
  it('does not retry client errors or mutations, but retries transient query failures twice', () => {
    const queryClient = createAppQueryClient()
    const defaults = queryClient.getDefaultOptions()
    const retry = defaults.queries?.retry

    expect(defaults.queries?.refetchOnWindowFocus).toBe(true)
    expect(defaults.mutations?.retry).toBe(false)
    expect(retry).toBeTypeOf('function')
    if (typeof retry !== 'function') throw new Error('Expected a query retry function.')

    expect(retry(0, new ApiError({ status: 400, title: 'Bad request' }))).toBe(false)
    expect(retry(0, new ApiError({ status: 503, title: 'Unavailable' }))).toBe(true)
    expect(retry(1, new Error('transient'))).toBe(true)
    expect(retry(2, new Error('still failing'))).toBe(false)
  })
})
