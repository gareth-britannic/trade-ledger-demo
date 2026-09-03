const utcDateFormatter = new Intl.DateTimeFormat('en-GB', {
  year: 'numeric',
  month: '2-digit',
  day: '2-digit',
  timeZone: 'UTC',
})

const localDateTimeFormatter = new Intl.DateTimeFormat('en-GB', {
  dateStyle: 'medium',
  timeStyle: 'short',
})

export function formatUtcDate(value: string | null | undefined): string {
  if (!value) return '—'

  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? '—' : `${utcDateFormatter.format(date)} UTC`
}

export function formatLocalDateTime(value: string): string {
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? value : `${localDateTimeFormatter.format(date)} local time`
}

export function toDateTimeLocalValue(date = new Date()): string {
  const localOffset = date.getTimezoneOffset() * 60_000
  return new Date(date.getTime() - localOffset).toISOString().slice(0, 16)
}

export function localDateTimeToIso(value: string): string {
  return new Date(value).toISOString()
}
