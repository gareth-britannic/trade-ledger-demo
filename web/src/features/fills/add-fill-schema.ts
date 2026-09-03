import { z } from 'zod'

const decimalPattern = /^(?:0|[1-9]\d{0,19})(?:\.\d{1,8})?$/u
const symbolPattern = /^[A-Za-z0-9][A-Za-z0-9._/-]{0,31}$/u

const canonicalDecimal = (value: string): string | undefined => {
  const match = /^(\d+)(?:\.(\d+))?(?:e([+-]?\d+))?$/iu.exec(value)
  if (!match?.[1]) return undefined

  const fraction = match[2] ?? ''
  const exponent = Number(match[3] ?? '0')
  let coefficient = BigInt(`${match[1]}${fraction}`)
  let scale = fraction.length - exponent

  if (scale < 0) {
    coefficient *= 10n ** BigInt(-scale)
    scale = 0
  }

  while (scale > 0 && coefficient % 10n === 0n) {
    coefficient /= 10n
    scale -= 1
  }

  return `${coefficient}:${scale}`
}

const isJsonNumberExact = (value: string): boolean => {
  const serialized = JSON.stringify(Number(value))
  return typeof serialized === 'string' && canonicalDecimal(value) === canonicalDecimal(serialized)
}

const positiveDecimal = (label: string) =>
  z
    .string()
    .trim()
    .min(1, `Enter a ${label.toLowerCase()}.`)
    .regex(decimalPattern, `${label} must be a positive number with up to 8 decimal places.`)
    .refine((value) => Number.isFinite(Number(value)) && Number(value) > 0, `${label} must be greater than zero.`)
    .refine(isJsonNumberExact, `${label} is too precise to submit safely from this browser.`)

export const addFillSchema = z.object({
  symbol: z
    .string()
    .trim()
    .min(1, 'Enter a symbol.')
    .max(32, 'Symbol must be 32 characters or fewer.')
    .regex(
      symbolPattern,
      'Start with a letter or number and use only letters, numbers, dots, dashes, underscores, or slashes.',
    )
    .transform((value) => value.toUpperCase()),
  side: z.enum(['Buy', 'Sell'], { message: 'Choose Buy or Sell.' }),
  quantity: positiveDecimal('Quantity'),
  price: positiveDecimal('Price'),
  executedAt: z
    .string()
    .min(1, 'Enter the execution date and time.')
    .refine((value) => !Number.isNaN(new Date(value).getTime()), 'Enter a valid execution date and time.'),
})

export type AddFillInput = z.input<typeof addFillSchema>
export type AddFillValues = z.output<typeof addFillSchema>
