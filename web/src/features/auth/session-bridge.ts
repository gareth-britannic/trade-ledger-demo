export type AuthSessionEndReason = 'expired' | 'logout' | 'unauthorized'

export interface AuthSession {
  accessToken: string
  email: string
  expiresAt: number
}

type SessionListener = (session: AuthSession | null, reason?: AuthSessionEndReason) => void
type UnauthorizedHandler = () => void

const storageKey = 'trade-ledger.auth-session.v1'
const sessionListeners = new Set<SessionListener>()
let unauthorizedHandler: UnauthorizedHandler | undefined
let currentSession: AuthSession | null = null
let initialized = false

const getStorage = (): Storage | undefined => {
  try {
    return typeof window === 'undefined' ? undefined : window.sessionStorage
  } catch {
    return undefined
  }
}

const isAuthSession = (value: unknown): value is AuthSession => {
  if (typeof value !== 'object' || value === null) return false

  const candidate = value as Record<string, unknown>
  return (
    typeof candidate.accessToken === 'string' &&
    candidate.accessToken.length > 0 &&
    typeof candidate.email === 'string' &&
    typeof candidate.expiresAt === 'number' &&
    Number.isFinite(candidate.expiresAt)
  )
}

const readStoredSession = (): AuthSession | null => {
  const storage = getStorage()
  if (!storage) return null

  try {
    const serialized = storage.getItem(storageKey)
    if (!serialized) return null

    const parsed: unknown = JSON.parse(serialized)
    if (!isAuthSession(parsed) || parsed.expiresAt <= Date.now()) {
      try {
        storage.removeItem(storageKey)
      } catch {
        // An in-memory session remains usable when browser storage is unavailable.
      }
      return null
    }

    return parsed
  } catch {
    try {
      storage.removeItem(storageKey)
    } catch {
      // Ignore storage access failures and continue without persistence.
    }
    return null
  }
}

const getSessionValue = (): AuthSession | null => {
  if (!initialized) {
    currentSession = readStoredSession()
    initialized = true
  }

  if (currentSession && currentSession.expiresAt <= Date.now()) {
    endAuthSession('expired')
  }

  return currentSession
}

const emit = (reason?: AuthSessionEndReason) => {
  for (const listener of sessionListeners) listener(currentSession ?? null, reason)
}

export const getAuthSession = (): AuthSession | null => getSessionValue()

export const getAccessToken = (): string | null => getSessionValue()?.accessToken ?? null

export const establishAuthSession = (session: AuthSession): void => {
  if (!isAuthSession(session) || session.expiresAt <= Date.now()) {
    throw new Error('Cannot establish an expired or invalid authentication session.')
  }

  currentSession = session
  initialized = true
  try {
    getStorage()?.setItem(storageKey, JSON.stringify(session))
  } catch {
    // Storage can be disabled; the module-level session still satisfies memory-only auth.
  }
  emit()
}

export const endAuthSession = (reason: AuthSessionEndReason = 'logout'): void => {
  currentSession = null
  initialized = true
  try {
    getStorage()?.removeItem(storageKey)
  } catch {
    // Clearing the in-memory token is the security-critical operation.
  }
  emit(reason)

  if (reason === 'unauthorized') unauthorizedHandler?.()
}

export const subscribeToAuthSession = (listener: SessionListener): (() => void) => {
  sessionListeners.add(listener)
  return () => sessionListeners.delete(listener)
}

export const registerUnauthorizedHandler = (handler: UnauthorizedHandler): (() => void) => {
  unauthorizedHandler = handler
  return () => {
    if (unauthorizedHandler === handler) unauthorizedHandler = undefined
  }
}
