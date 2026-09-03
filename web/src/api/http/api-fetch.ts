import {
  endAuthSessionIfCurrent,
  getAuthSession,
} from '../../features/auth/session-bridge'

export type ApiFieldErrors = Record<string, string[]>

export interface ApiErrorOptions<Body = unknown> {
  status: number
  title: string
  detail?: string
  type?: string
  instance?: string
  correlationId?: string
  traceId?: string
  fieldErrors?: ApiFieldErrors
  body?: Body
  cause?: unknown
}

export class ApiError<Body = unknown> extends Error {
  readonly status: number
  readonly title: string
  readonly detail: string | undefined
  readonly type: string | undefined
  readonly instance: string | undefined
  readonly correlationId: string | undefined
  readonly traceId: string | undefined
  readonly fieldErrors: ApiFieldErrors
  readonly body: Body | undefined

  constructor(options: ApiErrorOptions<Body>) {
    const firstFieldMessage = Object.values(options.fieldErrors ?? {}).flat()[0]
    super(options.detail ?? firstFieldMessage ?? options.title, { cause: options.cause })
    this.name = 'ApiError'
    this.status = options.status
    this.title = options.title
    this.detail = options.detail
    this.type = options.type
    this.instance = options.instance
    this.correlationId = options.correlationId
    this.traceId = options.traceId
    this.fieldErrors = options.fieldErrors ?? {}
    this.body = options.body
  }
}

type JsonRecord = Record<string, unknown>

const isRecord = (value: unknown): value is JsonRecord =>
  typeof value === 'object' && value !== null && !Array.isArray(value)

const readString = (record: JsonRecord | undefined, key: string): string | undefined => {
  const value = record?.[key]
  return typeof value === 'string' && value.trim().length > 0 ? value : undefined
}

const flattenMessages = (value: unknown): string[] => {
  if (typeof value === 'string') return value.trim() ? [value] : []
  if (Array.isArray(value)) return value.flatMap(flattenMessages)
  if (isRecord(value)) return Object.values(value).flatMap(flattenMessages)
  return []
}

const readFieldErrors = (record: JsonRecord | undefined): ApiFieldErrors => {
  const errors = record?.errors
  if (!isRecord(errors)) {
    const messages = flattenMessages(errors)
    return messages.length > 0 ? { request: messages } : {}
  }

  return Object.fromEntries(
    Object.entries(errors)
      .map(([field, value]) => [field, flattenMessages(value)] as const)
      .filter((entry) => entry[1].length > 0),
  )
}

const parseResponseBody = async (response: Response): Promise<unknown> => {
  const text = await response.text()
  if (!text.trim()) return undefined

  const contentType = response.headers.get('content-type')?.toLowerCase() ?? ''
  if (contentType.includes('json') || /^\s*(?:\{|\[)/u.test(text)) {
    try {
      return JSON.parse(text) as unknown
    } catch {
      // Preserve malformed upstream content as text for a safe generic error.
    }
  }

  return text
}

const toApiError = (response: Response, body: unknown): ApiError => {
  const problem = isRecord(body) ? body : undefined
  const fieldErrors = readFieldErrors(problem)
  const bodyMessage = readString(problem, 'message')
  const detail = readString(problem, 'detail') ?? bodyMessage
  const type = readString(problem, 'type')
  const instance = readString(problem, 'instance')
  const correlationId =
    readString(problem, 'correlationId') ?? response.headers.get('x-correlation-id') ?? undefined
  const traceId = readString(problem, 'traceId')
  const title =
    readString(problem, 'title') ?? (response.statusText || `Request failed (${response.status})`)

  return new ApiError({
    status: response.status,
    title,
    ...(detail ? { detail } : {}),
    ...(type ? { type } : {}),
    ...(instance ? { instance } : {}),
    ...(correlationId ? { correlationId } : {}),
    ...(traceId ? { traceId } : {}),
    ...(Object.keys(fieldErrors).length > 0 ? { fieldErrors } : {}),
    body,
  })
}

const isAbortError = (error: unknown): boolean =>
  (error instanceof DOMException && error.name === 'AbortError') ||
  (isRecord(error) && error.name === 'AbortError')

const relativeApiUrl = (url: string): string => {
  if (/^(?:[a-z][a-z\d+.-]*:)?\/\//iu.test(url)) {
    throw new ApiError({
      status: 0,
      title: 'Invalid API request',
      detail: 'The API client only permits same-origin relative URLs.',
    })
  }

  return url.startsWith('/') ? url : `/${url}`
}

export const apiFetch = async <T>(url: string, options: RequestInit): Promise<T> => {
  const requestUrl = relativeApiUrl(url)
  const headers = new Headers(options.headers)
  headers.set('Accept', 'application/json, application/problem+json')

  const requestSession = requestUrl.startsWith('/api/') ? getAuthSession() : null
  if (requestSession) {
    headers.set('Authorization', `Bearer ${requestSession.accessToken}`)
  } else {
    headers.delete('Authorization')
  }

  let response: Response
  try {
    response = await fetch(requestUrl, {
      ...options,
      headers,
      credentials: 'same-origin',
    })
  } catch (error) {
    if (isAbortError(error)) throw error
    throw new ApiError({
      status: 0,
      title: 'Trade Ledger is unavailable',
      detail: 'The API could not be reached. Check that the local services are running and try again.',
      cause: error,
    })
  }

  if (response.status === 401 && requestSession) {
    endAuthSessionIfCurrent(requestSession, 'unauthorized')
  }

  const body = await parseResponseBody(response)
  if (!response.ok) {
    throw toApiError(response, body)
  }

  return {
    data: body,
    status: response.status,
    headers: response.headers,
  } as T
}

export type ErrorType<ErrorBody> = ApiError<ErrorBody>
export type BodyType<BodyData> = BodyData
