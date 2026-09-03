const quantityFormatter = new Intl.NumberFormat('en-GB', {
  maximumFractionDigits: 8,
  useGrouping: true,
})

const moneyFormatter = new Intl.NumberFormat('en-GB', {
  style: 'currency',
  currency: 'GBP',
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
})

const signedMoneyFormatter = new Intl.NumberFormat('en-GB', {
  style: 'currency',
  currency: 'GBP',
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
  signDisplay: 'always',
})

export function formatQuantity(value: number | null | undefined): string {
  return value == null ? '—' : quantityFormatter.format(value)
}

export function formatMoney(value: number | null | undefined): string {
  return value == null ? '—' : moneyFormatter.format(value)
}

export function formatSignedMoney(value: number | null | undefined): string {
  return value == null ? '—' : signedMoneyFormatter.format(value)
}
