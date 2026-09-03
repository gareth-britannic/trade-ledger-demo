import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { type ReactElement, useState } from 'react'
import { describe, expect, it, vi } from 'vitest'

import type { CreateFillRequest } from '../../api/generated/model'
import {
  getGetPositionsQueryKey,
  type getPositionsResponse,
} from '../../api/generated/positions/positions'
import { server } from '../../test/server'
import { acceptedFillFixture } from '../../test/fixtures'
import { AddFillDialog } from './AddFillDialog'
import { addFillSchema } from './add-fill-schema'

const createQueryClient = () =>
  new QueryClient({
    defaultOptions: {
      mutations: { retry: false },
      queries: { gcTime: Number.POSITIVE_INFINITY, retry: false },
    },
  })

function renderWithQueryClient(ui: ReactElement) {
  const queryClient = createQueryClient()
  return {
    queryClient,
    ...render(<QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>),
  }
}

async function completeFillForm(user: ReturnType<typeof userEvent.setup>) {
  await user.type(screen.getByRole('textbox', { name: 'Symbol' }), 'aapl')
  await user.type(screen.getByRole('textbox', { name: 'Quantity' }), '10.125')
  await user.type(screen.getByRole('textbox', { name: 'Price (GBP)' }), '191.40')
  fireEvent.change(screen.getByLabelText(/Execution date & time/i), {
    target: { value: '2026-09-03T14:30' },
  })
}

describe('AddFillDialog', () => {
  it('rejects a decimal that JSON number serialization would silently change', () => {
    const result = addFillSchema.safeParse({
      symbol: 'AAPL',
      side: 'Buy',
      quantity: '9007199254740993',
      price: '19.25',
      executedAt: '2026-09-03T14:30',
    })

    expect(result.success).toBe(false)
    expect(result.error?.issues).toContainEqual(
      expect.objectContaining({ message: 'Quantity is too precise to submit safely from this browser.' }),
    )
  })

  it('shows an inspectable execution date/time field with a local-time default', async () => {
    renderWithQueryClient(<AddFillDialog onOpenChange={vi.fn()} open />)

    const dialog = await screen.findByRole('dialog', { name: 'Add fill' })
    const execution = within(dialog).getByLabelText(/Execution date & time/i)

    expect(execution).toBeVisible()
    expect(execution).toHaveAttribute('type', 'datetime-local')
    expect((execution as HTMLInputElement).value).toMatch(/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}$/u)
    expect(execution).toHaveAccessibleDescription(/local time.*UTC/i)
  })

  it('validates symbol, decimal values, and execution time before submitting', async () => {
    const user = userEvent.setup()
    renderWithQueryClient(<AddFillDialog onOpenChange={vi.fn()} open />)

    fireEvent.change(screen.getByLabelText(/Execution date & time/i), {
      target: { value: '' },
    })
    await user.click(screen.getByRole('button', { name: 'Submit fill' }))

    expect(await screen.findByText('Enter a symbol.')).toBeInTheDocument()
    expect(screen.getByText('Enter a quantity.')).toBeInTheDocument()
    expect(screen.getByText('Enter a price.')).toBeInTheDocument()
    expect(screen.getByText('Enter the execution date and time.')).toBeInTheDocument()
  })

  it('reuses one fill ID after a transport failure and presents 202 as queued, not persisted', async () => {
    const user = userEvent.setup()
    const requests: CreateFillRequest[] = []
    const acceptedFillId = acceptedFillFixture.fillId
    if (!acceptedFillId) throw new Error('Expected a typed accepted-fill fixture ID.')
    const randomUuid = vi
      .spyOn(globalThis.crypto, 'randomUUID')
      .mockReturnValue(acceptedFillId as ReturnType<Crypto['randomUUID']>)
    server.use(
      http.post('/api/fills', async ({ request }) => {
        requests.push((await request.json()) as CreateFillRequest)
        if (requests.length === 1) return HttpResponse.error()
        return HttpResponse.json(acceptedFillFixture, { status: 202 })
      }),
      http.get('/api/positions', () => HttpResponse.json([])),
    )
    renderWithQueryClient(<AddFillDialog onOpenChange={vi.fn()} open />)

    await completeFillForm(user)
    await user.click(screen.getByRole('button', { name: 'Submit fill' }))

    const failure = await screen.findByRole('alert')
    expect(failure).toHaveTextContent('Fill was not accepted')
    expect(failure).toHaveTextContent('same fill ID')

    await user.click(screen.getByRole('button', { name: 'Submit fill' }))

    const acceptance = await screen.findByText('202 Accepted')
    const notice = acceptance.closest('[role="status"]')
    expect(notice).not.toBeNull()
    expect(notice).toHaveTextContent('Queued — waiting for AAPL to appear in positions once applied.')
    expect(notice).not.toHaveTextContent(/\b(?:saved|persisted|completed)\b/iu)

    expect(requests).toHaveLength(2)
    expect(randomUuid).toHaveBeenCalledOnce()
    expect(requests[0]?.fillId).toBe(acceptedFillId)
    expect(requests[1]?.fillId).toBe(requests[0]?.fillId)
    expect(requests[1]).toMatchObject({
      symbol: 'AAPL',
      side: 'Buy',
      quantity: 10.125,
      price: 191.4,
    })
    expect(requests[1]?.executedAt).toMatch(/^2026-09-03T/u)
  })

  it('keeps refreshing positions after the accepted-fill dialog is dismissed', async () => {
    const user = userEvent.setup()
    let positionRequests = 0
    server.use(
      http.post('/api/fills', () => HttpResponse.json(acceptedFillFixture, { status: 202 })),
      http.get('/api/positions', () => {
        positionRequests += 1
        return HttpResponse.json(
          positionRequests <= 2
            ? []
            : [
                {
                  symbol: 'AAPL',
                  openQuantity: 10.125,
                  averageUnitCost: 191.4,
                  realisedPnl: 0,
                },
              ],
        )
      }),
    )

    function ControlledDialog() {
      const [open, setOpen] = useState(true)
      return <AddFillDialog onOpenChange={setOpen} open={open} />
    }

    const { queryClient } = renderWithQueryClient(<ControlledDialog />)
    await completeFillForm(user)
    await user.click(screen.getByRole('button', { name: 'Submit fill' }))
    await screen.findByText('202 Accepted')
    await user.click(screen.getByRole('button', { name: 'Close dialog' }))

    expect(screen.queryByRole('dialog', { name: 'Add fill' })).not.toBeInTheDocument()
    await waitFor(() => expect(positionRequests).toBeGreaterThanOrEqual(3), { timeout: 2_500 })
    expect(
      queryClient.getQueryData<getPositionsResponse>(getGetPositionsQueryKey()),
    ).toMatchObject({ data: [{ symbol: 'AAPL' }] })
  })

  it('keeps an accepted fill refresh running when the form is intentionally reset', async () => {
    const user = userEvent.setup()
    let positionRequests = 0
    server.use(
      http.post('/api/fills', () => HttpResponse.json(acceptedFillFixture, { status: 202 })),
      http.get('/api/positions', () => {
        positionRequests += 1
        return HttpResponse.json(
          positionRequests <= 2
            ? []
            : [
                {
                  symbol: 'AAPL',
                  openQuantity: 10.125,
                  averageUnitCost: 191.4,
                  realisedPnl: 0,
                },
              ],
        )
      }),
    )

    const { queryClient } = renderWithQueryClient(
      <AddFillDialog onOpenChange={vi.fn()} open />,
    )
    await completeFillForm(user)
    await user.click(screen.getByRole('button', { name: 'Submit fill' }))
    await screen.findByText('202 Accepted')
    await user.click(screen.getByRole('button', { name: 'Queue another fill' }))

    expect(screen.getByRole('textbox', { name: 'Symbol' })).toHaveValue('')
    await waitFor(() => expect(positionRequests).toBeGreaterThanOrEqual(3), { timeout: 2_500 })
    expect(
      queryClient.getQueryData<getPositionsResponse>(getGetPositionsQueryKey()),
    ).toMatchObject({ data: [{ symbol: 'AAPL' }] })
  })
})
