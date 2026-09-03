import type { ReactNode } from 'react'
import { Navigate, useLocation } from 'react-router-dom'

import { useAuth } from './use-auth'

export interface AuthRouteProps {
  children: ReactNode
}

const safeReturnPath = (state: unknown): string => {
  if (typeof state !== 'object' || state === null || !('returnTo' in state)) return '/positions'
  const returnTo = state.returnTo
  return typeof returnTo === 'string' &&
    returnTo.startsWith('/') &&
    !returnTo.startsWith('//') &&
    !returnTo.startsWith('/sign-in')
    ? returnTo
    : '/positions'
}

export function RequireAuth({ children }: AuthRouteProps) {
  const { isAuthenticated, sessionEndReason } = useAuth()
  const location = useLocation()

  if (!isAuthenticated) {
    if (sessionEndReason === 'logout') {
      return <Navigate to="/sign-in" replace />
    }

    const returnTo = `${location.pathname}${location.search}${location.hash}`
    const reason =
      sessionEndReason === 'expired' || sessionEndReason === 'unauthorized'
        ? sessionEndReason
        : undefined
    return <Navigate to="/sign-in" replace state={{ returnTo, reason }} />
  }

  return children
}

export function PublicOnlyRoute({ children }: AuthRouteProps) {
  const { isAuthenticated } = useAuth()
  const location = useLocation()
  return isAuthenticated ? <Navigate to={safeReturnPath(location.state)} replace /> : children
}
