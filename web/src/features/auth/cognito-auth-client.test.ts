import type { InitiateAuthCommand, InitiateAuthCommandOutput } from '@aws-sdk/client-cognito-identity-provider'
import { HttpResponse, http } from 'msw'
import { describe, expect, it, vi } from 'vitest'

import { server } from '../../test/server'
import { AuthError, CognitoAuthClient } from './cognito-auth-client'

type SendCommand = (
  command: InitiateAuthCommand,
  options?: { abortSignal?: AbortSignal },
) => Promise<InitiateAuthCommandOutput>

const jwt = (claims: Record<string, unknown>): string => {
  const encode = (value: unknown) =>
    btoa(JSON.stringify(value)).replace(/=/gu, '').replace(/\+/gu, '-').replace(/\//gu, '_')
  return `${encode({ alg: 'none' })}.${encode(claims)}.signature`
}

const output = (accessToken: string): InitiateAuthCommandOutput => ({
  $metadata: {},
  AuthenticationResult: {
    AccessToken: accessToken,
    IdToken: 'id-token-must-not-be-used',
    RefreshToken: 'refresh-token-must-not-be-stored',
  },
})

describe('CognitoAuthClient', () => {
  it('uses the modular SDK against the same-origin Cognito proxy', async () => {
    const expiresAtSeconds = 2_000_000_000
    const accessToken = jwt({
      token_use: 'access',
      client_id: 'public-client-id',
      exp: expiresAtSeconds,
    })
    let requestBody: unknown
    server.use(
      http.post('http://localhost:3000/cognito/', async ({ request }) => {
        requestBody = await request.json()
        expect(request.headers.get('x-amz-target')).toBe(
          'AWSCognitoIdentityProviderService.InitiateAuth',
        )
        return HttpResponse.json({ AuthenticationResult: { AccessToken: accessToken } })
      }),
    )
    const client = new CognitoAuthClient({
      clientId: 'public-client-id',
      region: 'eu-west-2',
      endpoint: 'http://localhost:3000/cognito/',
      now: () => 1_900_000_000_000,
    })

    await expect(
      client.signIn({ email: 'demo@trade-ledger.local', password: 'demo-password' }),
    ).resolves.toMatchObject({ accessToken, expiresAt: expiresAtSeconds * 1_000 })
    expect(requestBody).toEqual({
      AuthFlow: 'USER_PASSWORD_AUTH',
      ClientId: 'public-client-id',
      AuthParameters: {
        USERNAME: 'demo@trade-ledger.local',
        PASSWORD: 'demo-password',
      },
    })
  })

  it('uses USER_PASSWORD_AUTH and returns only an access-token session using the JWT expiry', async () => {
    const expiresAtSeconds = 2_000_000_000
    const accessToken = jwt({
      token_use: 'access',
      client_id: 'public-client-id',
      exp: expiresAtSeconds,
    })
    const send = vi.fn<SendCommand>()
    send.mockResolvedValue(output(accessToken))
    const client = new CognitoAuthClient({
      clientId: 'public-client-id',
      region: 'eu-west-2',
      endpoint: 'http://localhost/cognito/',
      sender: { send },
      now: () => 1_900_000_000_000,
    })

    const session = await client.signIn({
      email: ' demo@trade-ledger.local ',
      password: 'demo-password',
    })

    expect(session).toEqual({
      accessToken,
      email: 'demo@trade-ledger.local',
      expiresAt: expiresAtSeconds * 1_000,
    })
    const [command] = send.mock.calls[0]!
    expect(command.input).toEqual({
      AuthFlow: 'USER_PASSWORD_AUTH',
      ClientId: 'public-client-id',
      AuthParameters: {
        USERNAME: 'demo@trade-ledger.local',
        PASSWORD: 'demo-password',
      },
    })
    expect(session).not.toHaveProperty('idToken')
    expect(session).not.toHaveProperty('refreshToken')
  })

  it.each(['InvalidPasswordException', 'NotAuthorizedException', 'UserNotFoundException'])(
    'normalizes %s without revealing user existence',
    async (name) => {
      const send = vi.fn<SendCommand>()
      send.mockRejectedValue(Object.assign(new Error('provider detail'), { name }))
      const client = new CognitoAuthClient({
        clientId: 'public-client-id',
        region: 'eu-west-2',
        sender: { send },
      })

      const error = await client
        .signIn({ email: 'person@example.test', password: 'wrong' })
        .catch((caught: unknown) => caught)

      expect(error).toBeInstanceOf(AuthError)
      expect(error).toMatchObject({
        code: 'invalid-credentials',
        message: 'The email or password is incorrect.',
      })
    },
  )

  it('reports missing public configuration before making a request', async () => {
    const send = vi.fn<SendCommand>()
    const client = new CognitoAuthClient({ clientId: '', region: '', sender: { send } })

    await expect(
      client.signIn({ email: 'demo@trade-ledger.local', password: 'password' }),
    ).rejects.toMatchObject({ code: 'misconfigured' })
    expect(send).not.toHaveBeenCalled()
  })

  it('rejects an ID token even when it has a valid expiry', async () => {
    const send = vi.fn<SendCommand>()
    send.mockResolvedValue(
      output(
        jwt({
          token_use: 'id',
          client_id: 'public-client-id',
          exp: 2_000_000_000,
        }),
      ),
    )
    const client = new CognitoAuthClient({
      clientId: 'public-client-id',
      region: 'eu-west-2',
      sender: { send },
      now: () => 1_900_000_000_000,
    })

    await expect(
      client.signIn({ email: 'demo@trade-ledger.local', password: 'password' }),
    ).rejects.toMatchObject({ code: 'invalid-response' })
  })

  it('normalizes an unavailable emulator', async () => {
    const send = vi.fn<SendCommand>()
    send.mockRejectedValue(new TypeError('Failed to fetch'))
    const client = new CognitoAuthClient({
      clientId: 'public-client-id',
      region: 'eu-west-2',
      sender: { send },
    })

    await expect(
      client.signIn({ email: 'demo@trade-ledger.local', password: 'password' }),
    ).rejects.toMatchObject({ code: 'unavailable' })
  })

  it.each([
    'InvalidParameterException',
    'PasswordResetRequiredException',
    'ResourceNotFoundException',
    'UserNotConfirmedException',
  ])('normalizes %s as a local configuration problem', async (name) => {
    const send = vi.fn<SendCommand>()
    send.mockRejectedValue(Object.assign(new Error('provider detail'), { name }))
    const client = new CognitoAuthClient({
      clientId: 'public-client-id',
      region: 'eu-west-2',
      sender: { send },
    })

    await expect(
      client.signIn({ email: 'demo@trade-ledger.local', password: 'password' }),
    ).rejects.toMatchObject({ code: 'misconfigured' })
  })

  it('rejects a missing access token and an already-expired access token', async () => {
    const send = vi.fn<SendCommand>()
    const client = new CognitoAuthClient({
      clientId: 'public-client-id',
      region: 'eu-west-2',
      sender: { send },
      now: () => 1_900_000_000_000,
    })

    send.mockResolvedValueOnce({ $metadata: {}, AuthenticationResult: {} })
    await expect(
      client.signIn({ email: 'demo@trade-ledger.local', password: 'password' }),
    ).rejects.toMatchObject({ code: 'invalid-response' })

    send.mockResolvedValueOnce(
      output(jwt({ token_use: 'access', client_id: 'public-client-id', exp: 1_800_000_000 })),
    )
    await expect(
      client.signIn({ email: 'demo@trade-ledger.local', password: 'password' }),
    ).rejects.toMatchObject({
      code: 'invalid-response',
      message: 'The sign-in service returned an expired access token.',
    })
  })

  it('passes an already-aborted request through without replacing its error', async () => {
    const cancellation = new DOMException('Cancelled by the caller.', 'AbortError')
    const send = vi.fn<SendCommand>().mockRejectedValue(cancellation)
    const client = new CognitoAuthClient({
      clientId: 'public-client-id',
      region: 'eu-west-2',
      sender: { send },
    })
    const controller = new AbortController()
    controller.abort()

    await expect(
      client.signIn(
        { email: 'demo@trade-ledger.local', password: 'password' },
        controller.signal,
      ),
    ).rejects.toBe(cancellation)
  })
})
