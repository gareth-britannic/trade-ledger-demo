import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import { useNavigate } from 'react-router-dom'

import { AuthContext, type AuthContextValue } from './auth-context'
import { CognitoAuthClient, type SignInCredentials } from './cognito-auth-client'
import {
  endAuthSession,
  establishAuthSession,
  getAuthSessionSnapshot,
  subscribeToAuthSession,
} from './session-bridge'

export interface AuthProviderProps {
  children: ReactNode
  client?: Pick<CognitoAuthClient, 'signIn'>
}

const defaultClient = new CognitoAuthClient()

export function AuthProvider({ children, client = defaultClient }: AuthProviderProps) {
  const navigate = useNavigate()
  const [{ session, endReason: sessionEndReason }, setAuthSessionSnapshot] = useState(() =>
    getAuthSessionSnapshot(),
  )

  useEffect(
    () =>
      subscribeToAuthSession((nextSession, reason) => {
        setAuthSessionSnapshot({ session: nextSession, endReason: reason ?? null })
      }),
    [],
  )

  useEffect(() => {
    if (!session) return undefined

    let timeout: number | undefined
    const expireWhenDue = () => {
      const remaining = session.expiresAt - Date.now()
      if (remaining <= 0) {
        endAuthSession('expired')
        return
      }

      timeout = window.setTimeout(expireWhenDue, Math.min(remaining, 2_147_483_647))
    }

    expireWhenDue()
    return () => {
      if (timeout !== undefined) window.clearTimeout(timeout)
    }
  }, [session])

  const signIn = useCallback(
    async (credentials: SignInCredentials) => {
      const nextSession = await client.signIn(credentials)
      establishAuthSession(nextSession)
    },
    [client],
  )

  const signOut = useCallback(() => {
    endAuthSession('logout')
    void navigate('/sign-in', { replace: true })
  }, [navigate])

  const value = useMemo<AuthContextValue>(
    () => ({
      status: session ? 'authenticated' : 'anonymous',
      isAuthenticated: session !== null,
      email: session?.email ?? null,
      sessionEndReason,
      signIn,
      signOut,
    }),
    [session, sessionEndReason, signIn, signOut],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
