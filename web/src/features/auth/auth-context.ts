import { createContext } from 'react'

import type { SignInCredentials } from './cognito-auth-client'

export type AuthStatus = 'authenticated' | 'anonymous'

export interface AuthContextValue {
  status: AuthStatus
  isAuthenticated: boolean
  email: string | null
  signIn: (credentials: SignInCredentials) => Promise<void>
  signOut: () => void
}

export const AuthContext = createContext<AuthContextValue | undefined>(undefined)
