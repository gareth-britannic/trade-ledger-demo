import { createContext } from 'react'

import type { SignInCredentials } from './cognito-auth-client'
import type { AuthSessionEndReason } from './session-bridge'

export type AuthStatus = 'authenticated' | 'anonymous'

export interface AuthContextValue {
  status: AuthStatus
  isAuthenticated: boolean
  email: string | null
  sessionEndReason: AuthSessionEndReason | null
  signIn: (credentials: SignInCredentials) => Promise<void>
  signOut: () => void
}

export const AuthContext = createContext<AuthContextValue | undefined>(undefined)
