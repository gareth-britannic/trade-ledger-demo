import { afterEach, describe, expect, it, vi } from 'vitest'

import {
  endAuthSession,
  endAuthSessionIfCurrent,
  establishAuthSession,
  getAccessToken,
  getAuthSession,
  subscribeToAuthSession,
} from './session-bridge'

afterEach(() => {
  vi.useRealTimers()
  endAuthSession('logout')
  window.sessionStorage.clear()
  window.localStorage.clear()
})

describe('session bridge', () => {
  it('keeps the access token in session storage rather than persistent local storage', () => {
    establishAuthSession({
      accessToken: 'session-only-token',
      email: 'demo@trade-ledger.local',
      expiresAt: Date.now() + 60_000,
    })

    expect(getAccessToken()).toBe('session-only-token')
    expect(window.sessionStorage.length).toBe(1)
    expect(window.localStorage.length).toBe(0)

    endAuthSession('logout')
    expect(getAuthSession()).toBeNull()
    expect(window.sessionStorage.length).toBe(0)
  })

  it('clears an expired token and announces the expiry reason', () => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date('2026-09-03T00:00:00Z'))
    const listener = vi.fn()
    const unsubscribe = subscribeToAuthSession(listener)
    establishAuthSession({
      accessToken: 'short-lived-token',
      email: 'demo@trade-ledger.local',
      expiresAt: Date.now() + 1_000,
    })
    listener.mockClear()

    vi.advanceTimersByTime(1_001)

    expect(getAccessToken()).toBeNull()
    expect(listener).toHaveBeenCalledWith(null, 'expired')
    unsubscribe()
  })

  it('treats each session establishment as a distinct identity', () => {
    const session = {
      accessToken: 'reused-token',
      email: 'demo@trade-ledger.local',
      expiresAt: Date.now() + 60_000,
    }
    establishAuthSession(session)
    const firstEstablishment = getAuthSession()
    establishAuthSession(session)
    const secondEstablishment = getAuthSession()

    expect(firstEstablishment).not.toBe(secondEstablishment)
    expect(firstEstablishment).not.toBeNull()
    expect(secondEstablishment).not.toBeNull()
    if (!firstEstablishment || !secondEstablishment) {
      throw new Error('Expected both authentication sessions to be established.')
    }

    expect(endAuthSessionIfCurrent(firstEstablishment, 'unauthorized')).toBe(false)
    expect(getAuthSession()).toBe(secondEstablishment)
    expect(endAuthSessionIfCurrent(secondEstablishment, 'unauthorized')).toBe(true)
    expect(getAuthSession()).toBeNull()
  })
})
