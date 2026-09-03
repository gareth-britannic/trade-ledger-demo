import type {
  InitiateAuthCommand,
  InitiateAuthCommandOutput,
} from '@aws-sdk/client-cognito-identity-provider'

import type { AuthSession } from './session-bridge'

export type AuthErrorCode =
  | 'invalid-credentials'
  | 'misconfigured'
  | 'unavailable'
  | 'invalid-response'

export class AuthError extends Error {
  readonly code: AuthErrorCode

  constructor(code: AuthErrorCode, message: string, options?: ErrorOptions) {
    super(message, options)
    this.name = 'AuthError'
    this.code = code
  }
}

export interface SignInCredentials {
  email: string
  password: string
}

interface CognitoCommandSender {
  send(command: InitiateAuthCommand, options?: { abortSignal?: AbortSignal }): Promise<InitiateAuthCommandOutput>
}

export interface CognitoAuthClientOptions {
  clientId?: string
  region?: string
  endpoint?: string
  sender?: CognitoCommandSender
  now?: () => number
}

interface AccessTokenClaims {
  client_id?: unknown
  exp?: unknown
  token_use?: unknown
}

const invalidCredentialErrors = new Set([
  'InvalidPasswordException',
  'NotAuthorizedException',
  'UserNotFoundException',
])

const configurationErrors = new Set([
  'InvalidParameterException',
  'PasswordResetRequiredException',
  'ResourceNotFoundException',
  'UserNotConfirmedException',
])

// This must remain a runtime import: Cognito is only needed on sign-in, so it lives outside the
// initial authenticated-app bundle. The static import above deliberately supplies types only.
// eslint-disable-next-line @typescript-eslint/consistent-type-imports -- This module type documents the intentional runtime split point.
type CognitoSdkModule = typeof import('@aws-sdk/client-cognito-identity-provider')
const loadCognitoSdk = (): Promise<CognitoSdkModule> =>
  import('@aws-sdk/client-cognito-identity-provider')

const browserEndpoint = (): string => {
  if (typeof window === 'undefined') return 'http://localhost/cognito/'
  return new URL('/cognito/', window.location.origin).toString()
}

const errorName = (error: unknown): string | undefined => {
  if (typeof error !== 'object' || error === null || !('name' in error)) return undefined
  return typeof error.name === 'string' ? error.name : undefined
}

const decodeAccessToken = (token: string): AccessTokenClaims => {
  const segments = token.split('.')
  const payload = segments[1]
  if (segments.length !== 3 || !payload) {
    throw new AuthError(
      'invalid-response',
      'The sign-in service returned an invalid access token. Rerun the local bootstrap and try again.',
    )
  }

  try {
    const base64 = payload.replace(/-/gu, '+').replace(/_/gu, '/')
    const padded = base64.padEnd(Math.ceil(base64.length / 4) * 4, '=')
    const binary = atob(padded)
    const bytes = Uint8Array.from(binary, (character) => character.charCodeAt(0))
    const parsed: unknown = JSON.parse(new TextDecoder().decode(bytes))
    if (typeof parsed !== 'object' || parsed === null || Array.isArray(parsed)) throw new Error()
    return parsed
  } catch (error) {
    if (error instanceof AuthError) throw error
    throw new AuthError(
      'invalid-response',
      'The sign-in service returned an invalid access token. Rerun the local bootstrap and try again.',
      { cause: error },
    )
  }
}

const normalizeCognitoError = (error: unknown): AuthError => {
  if (error instanceof AuthError) return error

  const name = errorName(error)
  if (name && invalidCredentialErrors.has(name)) {
    return new AuthError('invalid-credentials', 'The email or password is incorrect.', { cause: error })
  }

  if (name && configurationErrors.has(name)) {
    return new AuthError(
      'misconfigured',
      'Local sign-in is not configured. Rerun the backend bootstrap and regenerate the web configuration.',
      { cause: error },
    )
  }

  return new AuthError(
    'unavailable',
    'The local sign-in service could not be reached. Check that cognito-local is running and try again.',
    { cause: error },
  )
}

export class CognitoAuthClient {
  private readonly clientId: string
  private readonly now: () => number
  private readonly sender: CognitoCommandSender | undefined
  private readonly region: string
  private readonly endpoint: string

  constructor(options: CognitoAuthClientOptions = {}) {
    this.clientId = options.clientId?.trim() ?? import.meta.env.VITE_COGNITO_CLIENT_ID?.trim() ?? ''
    this.region = options.region?.trim() ?? import.meta.env.VITE_COGNITO_REGION?.trim() ?? ''
    this.endpoint = options.endpoint ?? browserEndpoint()
    this.sender = options.sender
    this.now = options.now ?? Date.now
  }

  async signIn(credentials: SignInCredentials, signal?: AbortSignal): Promise<AuthSession> {
    if (!this.clientId || !this.region) {
      throw new AuthError(
        'misconfigured',
        'Local sign-in is not configured. Run npm run config:local after bootstrapping the backend.',
      )
    }

    let output: InitiateAuthCommandOutput
    try {
      const { CognitoIdentityProviderClient, InitiateAuthCommand } = await loadCognitoSdk()
      const sender =
        this.sender ??
        new CognitoIdentityProviderClient({
          endpoint: this.endpoint,
          region: this.region,
        })
      output = await sender.send(
        new InitiateAuthCommand({
          AuthFlow: 'USER_PASSWORD_AUTH',
          ClientId: this.clientId,
          AuthParameters: {
            USERNAME: credentials.email.trim(),
            PASSWORD: credentials.password,
          },
        }),
        signal ? { abortSignal: signal } : undefined,
      )
    } catch (error) {
      if (signal?.aborted) throw error
      throw normalizeCognitoError(error)
    }

    const accessToken = output.AuthenticationResult?.AccessToken
    if (!accessToken) {
      throw new AuthError(
        'invalid-response',
        'The sign-in service did not return an access token. Rerun the local bootstrap and try again.',
      )
    }

    const claims = decodeAccessToken(accessToken)
    if (
      claims.token_use !== 'access' ||
      claims.client_id !== this.clientId ||
      typeof claims.exp !== 'number' ||
      !Number.isFinite(claims.exp)
    ) {
      throw new AuthError(
        'invalid-response',
        'The sign-in service returned an invalid access token. Rerun the local bootstrap and try again.',
      )
    }

    const expiresAt = claims.exp * 1_000
    if (expiresAt <= this.now()) {
      throw new AuthError('invalid-response', 'The sign-in service returned an expired access token.')
    }

    return {
      accessToken,
      email: credentials.email.trim(),
      expiresAt,
    }
  }
}
