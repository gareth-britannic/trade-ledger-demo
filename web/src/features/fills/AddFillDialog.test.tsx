import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
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

async function completeFillForm(
  user: ReturnType<typeof userEvent.setup>,
  symbol = 'aapl',
) {
  await user.type(screen.getByRole('textbox', { name: 'Symbol' }), symbol)
  await user.type(screen.getByRole('textbox', { name: 'Quantity' }), '10.125')
  await user.type(screen.getByRole('textbox', { name: 'Price (GBP)' }), '191.40')
  fireEvent.change(screen.getByLabelText(/Execution date & time/i), {
    target: { value: '2026-09-03T14:30' },
  })
}

function deferred<T>() {
  let resolve: (value: T | PromiseLike<T>) => void = () => undefined
  const promise = new Promise<T>((resolvePromise) => {
    resolve = resolvePromise
  })
  return { promise, resolve }
}

function ReopenableDialog() {
  const [open, setOpen] = useState(true)
  return (
    <>
      <button onClick={() => setOpen(true)} type="button">
        Open fill form
      </button>
      <AddFillDialog onOpenChange={setOpen} open={open} />
    </>
  )
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

  it('starts a new fill identity when retry details change after an ambiguous failure', async () => {
    const user = userEvent.setup()
    const requests: CreateFillRequest[] = []
    const generatedIds = [
      '10000000-0000-4000-8000-000000000011',
      '10000000-0000-4000-8000-000000000012',
    ] as const
    const randomUuid = vi
      .spyOn(globalThis.crypto, 'randomUUID')
      .mockReturnValueOnce(generatedIds[0])
      .mockReturnValueOnce(generatedIds[1])
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
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Changing details starts a new fill attempt.',
    )

    const price = screen.getByRole('textbox', { name: 'Price (GBP)' })
    await user.clear(price)
    await user.type(price, '200.25')
    await user.click(screen.getByRole('button', { name: 'Submit fill' }))

    expect(await screen.findByText('202 Accepted')).toBeVisible()
    expect(requests).toHaveLength(2)
    expect(requests[0]).toMatchObject({ fillId: generatedIds[0], price: 191.4 })
    expect(requests[1]).toMatchObject({ fillId: generatedIds[1], price: 200.25 })
    expect(randomUuid).toHaveBeenCalledTimes(2)
  })

  it('allows only one in-flight submission for a rapid duplicate submit', async () => {
    const user = userEvent.setup()
    const responseGate = deferred<void>()
    const requests: CreateFillRequest[] = []
    const acceptedFillId = acceptedFillFixture.fillId
    if (!acceptedFillId) throw new Error('Expected a typed accepted-fill fixture ID.')
    const randomUuid = vi
      .spyOn(globalThis.crypto, 'randomUUID')
      .mockReturnValue(acceptedFillId as ReturnType<Crypto['randomUUID']>)
    server.use(
      http.post('/api/fills', async ({ request }) => {
        requests.push((await request.json()) as CreateFillRequest)
        await responseGate.promise
        return HttpResponse.json(acceptedFillFixture, { status: 202 })
      }),
      http.get('/api/positions', () => HttpResponse.json([])),
    )
    renderWithQueryClient(<AddFillDialog onOpenChange={vi.fn()} open />)

    await completeFillForm(user)
    const submit = screen.getByRole('button', { name: 'Submit fill' })
    const form = submit.closest('form')
    if (!form) throw new Error('Expected the submit button to belong to the fill form.')
    fireEvent.submit(form)
    fireEvent.submit(form)

    await waitFor(() => expect(requests).toHaveLength(1))
    expect(randomUuid).toHaveBeenCalledOnce()

    act(() => responseGate.resolve())
    expect(await screen.findByText('202 Accepted')).toBeVisible()
  })

  it('keeps a newer retry UUID canonical when an older pending dialog completes after reopen', async () => {
    const user = userEvent.setup()
    const firstResponseGate = deferred<void>()
    const requests: CreateFillRequest[] = []
    let positionRequests = 0
    const generatedIds = [
      '10000000-0000-4000-8000-000000000001',
      '10000000-0000-4000-8000-000000000002',
    ] as const
    const randomUuid = vi
      .spyOn(globalThis.crypto, 'randomUUID')
      .mockReturnValueOnce(generatedIds[0])
      .mockReturnValueOnce(generatedIds[1])
    server.use(
      http.post('/api/fills', async ({ request }) => {
        requests.push((await request.json()) as CreateFillRequest)
        if (requests.length === 1) {
          await firstResponseGate.promise
          return HttpResponse.json(acceptedFillFixture, { status: 202 })
        }
        if (requests.length === 2) return HttpResponse.error()
        return HttpResponse.json(acceptedFillFixture, { status: 202 })
      }),
      http.get('/api/positions', () => {
        positionRequests += 1
        return HttpResponse.json([])
      }),
    )
    renderWithQueryClient(<ReopenableDialog />)

    await completeFillForm(user)
    await user.click(screen.getByRole('button', { name: 'Submit fill' }))
    await waitFor(() => expect(requests).toHaveLength(1))
    await user.click(screen.getByRole('button', { name: 'Close dialog' }))
    await user.click(screen.getByRole('button', { name: 'Open fill form' }))

    await completeFillForm(user, 'msft')
    await user.click(screen.getByRole('button', { name: 'Submit fill' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('Fill was not accepted')

    act(() => firstResponseGate.resolve())
    await waitFor(() => expect(positionRequests).toBeGreaterThan(0))
    expect(screen.getByRole('textbox', { name: 'Symbol' })).toHaveValue('msft')
    expect(screen.queryByText('202 Accepted')).not.toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Submit fill' }))
    expect(await screen.findByText('202 Accepted')).toBeVisible()
    expect(requests).toHaveLength(3)
    expect(requests[0]?.fillId).toBe(generatedIds[0])
    expect(requests[1]?.fillId).toBe(generatedIds[1])
    expect(requests[2]?.fillId).toBe(generatedIds[1])
    expect(randomUuid).toHaveBeenCalledTimes(2)
  })

  it('lets a stale accepted response refresh the cache without changing reopened UI', async () => {
    const user = userEvent.setup()
    const responseGate = deferred<void>()
    let fillRequests = 0
    let positionRequests = 0
    server.use(
      http.post('/api/fills', async () => {
        fillRequests += 1
        await responseGate.promise
        return HttpResponse.json(acceptedFillFixture, { status: 202 })
      }),
      http.get('/api/positions', () => {
        positionRequests += 1
        return HttpResponse.json([
          {
            symbol: 'AAPL',
            openQuantity: 10.125,
            averageUnitCost: 191.4,
            realisedPnl: 0,
          },
        ])
      }),
    )
    const { queryClient } = renderWithQueryClient(<ReopenableDialog />)

    await completeFillForm(user)
    await user.click(screen.getByRole('button', { name: 'Submit fill' }))
    await waitFor(() => expect(fillRequests).toBe(1))
    await user.click(screen.getByRole('button', { name: 'Close dialog' }))
    await user.click(screen.getByRole('button', { name: 'Open fill form' }))
    await user.type(screen.getByRole('textbox', { name: 'Symbol' }), 'msft')

    act(() => responseGate.resolve())
    await waitFor(() => expect(positionRequests).toBeGreaterThan(0))

    expect(queryClient.getQueryData<getPositionsResponse>(getGetPositionsQueryKey())).toMatchObject({
      data: [{ symbol: 'AAPL' }],
    })
    expect(screen.getByRole('textbox', { name: 'Symbol' })).toHaveValue('msft')
    expect(screen.queryByText('202 Accepted')).not.toBeInTheDocument()
  })

  it('does not continue into accepted-state polling after unmount', async () => {
    const user = userEvent.setup()
    const responseGate = deferred<void>()
    let fillRequests = 0
    let fillResponses = 0
    let positionRequests = 0
    server.use(
      http.post('/api/fills', async () => {
        fillRequests += 1
        await responseGate.promise
        fillResponses += 1
        return HttpResponse.json(acceptedFillFixture, { status: 202 })
      }),
      http.get('/api/positions', () => {
        positionRequests += 1
        return HttpResponse.json([])
      }),
    )
    const { unmount } = renderWithQueryClient(<AddFillDialog onOpenChange={vi.fn()} open />)

    await completeFillForm(user)
    await user.click(screen.getByRole('button', { name: 'Submit fill' }))
    await waitFor(() => expect(fillRequests).toBe(1))
    unmount()

    act(() => responseGate.resolve())
    await waitFor(() => expect(fillResponses).toBe(1))
    await new Promise((resolve) => window.setTimeout(resolve, 50))
    expect(positionRequests).toBe(0)
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

  it('reports when the bounded refresh observes the accepted position change', async () => {
    const user = userEvent.setup()
    let positionRequests = 0
    server.use(
      http.post('/api/fills', () => HttpResponse.json(acceptedFillFixture, { status: 202 })),
      http.get('/api/positions', () => {
        positionRequests += 1
        return HttpResponse.json(
          positionRequests === 1
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

    const { queryClient } = renderWithQueryClient(<AddFillDialog onOpenChange={vi.fn()} open />)
    queryClient.setQueryData<getPositionsResponse>(getGetPositionsQueryKey(), {
      data: [],
      headers: new Headers(),
      status: 200,
    })
    await completeFillForm(user)
    await user.click(screen.getByRole('button', { name: 'Submit fill' }))

    const observed = await screen.findByText('Position update observed')
    expect(observed.closest('[role="status"]')).toHaveTextContent(
      'The positions API now reflects a change for AAPL.',
    )
    expect(positionRequests).toBe(2)
  })
})
