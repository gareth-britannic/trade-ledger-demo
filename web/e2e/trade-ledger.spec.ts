import { expect, test } from '@playwright/test'

const demoEmail = process.env.TRADE_LEDGER_LOCAL_USER_EMAIL
const demoPassword = process.env.TRADE_LEDGER_LOCAL_USER_PASSWORD

test('signs in and exercises the real asynchronous ledger flow', async ({ page }) => {
  if (!demoEmail || !demoPassword) {
    throw new Error(
      'Source ../.generated/local-cognito.env before running the real-stack Playwright test.',
    )
  }

  const symbol = `WEB${Date.now().toString(36).toUpperCase()}`

  await page.goto('/sign-in')
  await page.getByLabel('Email').fill(demoEmail)
  await page.getByLabel('Password').fill(demoPassword)
  await page.getByRole('button', { name: 'Sign in' }).click()

  await expect(page).toHaveURL(/\/positions$/u)
  await expect(page.getByRole('heading', { name: 'Positions' })).toBeVisible()

  await page.getByRole('button', { name: '+ Add fill' }).click()
  const fillDialog = page.getByRole('dialog', { name: 'Add fill' })
  await expect(fillDialog).toBeVisible()
  await fillDialog.getByLabel('Symbol').fill(symbol)
  await fillDialog.getByLabel('Buy').check()
  await fillDialog.getByLabel('Quantity').fill('7')
  await fillDialog.getByLabel('Price (GBP)').fill('19.25')
  await expect(fillDialog.getByLabel('Execution date & time')).not.toHaveValue('')
  await fillDialog.getByRole('button', { name: 'Submit fill' }).click()

  await expect(fillDialog.getByText('202 Accepted')).toBeVisible()
  await expect(fillDialog.getByText(/Queued|waiting/u)).toBeVisible()
  await fillDialog.getByRole('button', { name: 'Close dialog' }).click()

  const positionRow = page.getByRole('row').filter({ hasText: symbol })
  await expect(positionRow).toBeVisible({ timeout: 30_000 })
  await positionRow.getByRole('button', { name: 'View lots →' }).click()

  const lotsDrawer = page.getByRole('dialog', { name: `${symbol} — Open lots` })
  await expect(lotsDrawer).toBeVisible()
  await expect(lotsDrawer.getByText('7', { exact: true })).toBeVisible()
  await expect(lotsDrawer.getByText('£19.25', { exact: true })).toBeVisible()
  await lotsDrawer.getByRole('button', { name: 'Close drawer' }).click()

  await page.getByRole('link', { name: 'Explain' }).click()
  await expect(page).toHaveURL(/\/explain$/u)
  await page
    .getByLabel('Question about the ledger')
    .fill(`What is my realised P&L on ${symbol} this month?`)
  await page.getByRole('button', { name: 'Ask' }).click()

  await expect(page.getByRole('heading', { name: 'Tool calls' })).toBeVisible()
  await expect(page.getByText(`get_lots("${symbol}")`, { exact: true })).toBeVisible()
  await expect(page.getByRole('heading', { name: 'Answer' })).toBeVisible()
  await expect(page.getByText(new RegExp(symbol, 'u')).last()).toBeVisible()

  await page.getByRole('button', { name: 'Log out' }).click()
  await expect(page).toHaveURL(/\/sign-in$/u)
  await expect(page.getByRole('heading', { name: 'Sign in to the ledger' })).toBeVisible()
})
