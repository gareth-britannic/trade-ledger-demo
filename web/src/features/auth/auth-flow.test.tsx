import { act, cleanup, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { MemoryRouter, Route, Routes, type InitialEntry } from 'react-router-dom'

import { AuthProvider } from './auth-provider'
import { PublicOnlyRoute, RequireAuth } from './auth-routes'
import { AuthError, type CognitoAuthClient, type SignInCredentials } from './cognito-auth-client'
import { endAuthSession, establishAuthSession } from './session-bridge'
import { SignInPage } from './sign-in-page'

afterEach(() => {
  vi.useRealTimers()
  cleanup()
  endAuthSession('logout')
  window.sessionStorage.clear()
})

const renderFlow = (
  client: Pick<CognitoAuthClient, 'signIn'>,
  initialEntries: InitialEntry[] = ['/positions'],
) =>
  render(
    <MemoryRouter initialEntries={initialEntries}>
      <AuthProvider client={client}>
        <Routes>
          <Route
            path="/sign-in"
            element={
              <PublicOnlyRoute>
                <SignInPage />
              </PublicOnlyRoute>
            }
          />
          <Route
            path="/positions"
            element={
              <RequireAuth>
                <h1>Positions</h1>
              </RequireAuth>
            }
          />
        </Routes>
      </AuthProvider>
    </MemoryRouter>,
  )

describe('authentication flow', () => {
  it('redirects an anonymous user, signs in, and returns to the protected route', async () => {
    const signIn = vi.fn(async (credentials: SignInCredentials) =>
      Promise.resolve({
        accessToken: 'access-token',
        email: credentials.email,
        expiresAt: Date.now() + 60_000,
      }),
    )
    const user = userEvent.setup()
    renderFlow({ signIn })

    expect(await screen.findByRole('heading', { name: 'Sign in to the ledger' })).toBeVisible()
    await user.type(screen.getByLabelText(/Password/u), 'demo-password')
    await user.click(screen.getByRole('button', { name: 'Sign in' }))

    expect(await screen.findByRole('heading', { name: 'Positions' })).toBeVisible()
    expect(signIn).toHaveBeenCalledWith({
      email: 'demo@trade-ledger.local',
      password: 'demo-password',
    })
  })

  it('shows a normalized invalid-credentials error and keeps the password out of the message', async () => {
    const signIn = vi.fn(async () =>
      Promise.reject(new AuthError('invalid-credentials', 'The email or password is incorrect.')),
    )
    const user = userEvent.setup()
    renderFlow({ signIn })

    await user.type(await screen.findByLabelText(/Password/u), 'never-display-this')
    await user.click(screen.getByRole('button', { name: 'Sign in' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('The email or password is incorrect.')
    expect(screen.queryByText('never-display-this')).not.toBeInTheDocument()
  })

  it('validates required credentials before calling Cognito', async () => {
    const signIn = vi.fn(async () => Promise.reject(new Error('should not run')))
    const user = userEvent.setup()
    renderFlow({ signIn })

    await user.clear(await screen.findByLabelText(/Email/u))
    await user.click(screen.getByRole('button', { name: 'Sign in' }))

    expect(await screen.findByText('Enter the demo email.')).toBeVisible()
    expect(screen.getByText('Enter the demo password.')).toBeVisible()
    expect(signIn).not.toHaveBeenCalled()
  })

  it('shows a safe fallback when sign-in fails with an unexpected error', async () => {
    const signIn = vi.fn(async () => Promise.reject(new Error('provider internals')))
    const user = userEvent.setup()
    renderFlow({ signIn })

    await user.type(await screen.findByLabelText(/Password/u), 'never-display-this')
    await user.click(screen.getByRole('button', { name: 'Sign in' }))

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Sign-in could not be completed. Check the local services and try again.',
    )
    expect(screen.queryByText(/provider internals/u)).not.toBeInTheDocument()
  })

  it('expires an authenticated session at its deadline and explains the redirect', async () => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date('2026-09-03T00:00:00Z'))
    establishAuthSession({
      accessToken: 'short-lived-token',
      email: 'demo@trade-ledger.local',
      expiresAt: Date.now() + 1_000,
    })
    renderFlow({ signIn: vi.fn() })

    expect(screen.getByRole('heading', { name: 'Positions' })).toBeVisible()
    await act(async () => {
      await vi.advanceTimersByTimeAsync(1_001)
    })

    expect(screen.getByRole('heading', { name: 'Sign in to the ledger' })).toBeVisible()
    expect(screen.getByText('Your session ended. Sign in again to continue.')).toBeVisible()
  })

  it('preserves the unauthorized reason when the route guard redirects to sign-in', () => {
    establishAuthSession({
      accessToken: 'rejected-token',
      email: 'demo@trade-ledger.local',
      expiresAt: Date.now() + 60_000,
    })
    renderFlow({ signIn: vi.fn() })

    expect(screen.getByRole('heading', { name: 'Positions' })).toBeVisible()
    act(() => endAuthSession('unauthorized'))

    expect(screen.getByRole('heading', { name: 'Sign in to the ledger' })).toBeVisible()
    expect(screen.getByText('Your session ended. Sign in again to continue.')).toBeVisible()
  })

  it('rejects an unsafe return path after successful sign-in', async () => {
    const signIn = vi.fn(async (credentials: SignInCredentials) =>
      Promise.resolve({
        accessToken: 'access-token',
        email: credentials.email,
        expiresAt: Date.now() + 60_000,
      }),
    )
    const user = userEvent.setup()
    renderFlow(
      { signIn },
      [{ pathname: '/sign-in', state: { returnTo: '//malicious.example.test' } }],
    )

    await user.type(screen.getByLabelText(/Password/u), 'demo-password')
    await user.click(screen.getByRole('button', { name: 'Sign in' }))

    expect(await screen.findByRole('heading', { name: 'Positions' })).toBeVisible()
  })
})
