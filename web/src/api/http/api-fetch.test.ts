import { afterEach, describe, expect, it, vi } from 'vitest'

import {
  endAuthSession,
  establishAuthSession,
  getAuthSession,
  registerUnauthorizedHandler,
} from '../../features/auth/session-bridge'
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

  it('takes a correlation ID from the response header and clears the session on an empty 401', async () => {
    establishAuthSession({
      accessToken: 'rejected-token',
      email: 'demo@trade-ledger.local',
      expiresAt: Date.now() + 60_000,
    })
    const redirect = vi.fn()
    const unregister = registerUnauthorizedHandler(redirect)
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
    expect(redirect).toHaveBeenCalledOnce()
    unregister()
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
