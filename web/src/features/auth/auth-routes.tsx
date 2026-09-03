import type { ReactNode } from 'react'
import { Navigate, useLocation } from 'react-router-dom'

import { useAuth } from './use-auth'

export interface AuthRouteProps {
  children: ReactNode
}

export function RequireAuth({ children }: AuthRouteProps) {
  const { isAuthenticated, sessionEndReason } = useAuth()
  const location = useLocation()

  if (!isAuthenticated) {
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
  return isAuthenticated ? <Navigate to="/positions" replace /> : children
}
