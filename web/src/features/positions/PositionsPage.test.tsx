import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { act, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { delay, http, HttpResponse } from 'msw'
import type { ReactElement } from 'react'
import { describe, expect, it, vi } from 'vitest'

import { server } from '../../test/server'
import {
  appleLotsFixture,
  positionsFixture,
  problemDetailsFixture,
} from '../../test/fixtures'
import { PositionsPage } from './PositionsPage'

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

const usePositions = () => {
  server.use(
    http.get('/api/positions', () => HttpResponse.json(positionsFixture)),
  )
}

describe('PositionsPage', () => {
  it('shows a labelled loading state while positions are pending', () => {
    server.use(
      http.get('/api/positions', async () => {
        await delay('infinite')
        return HttpResponse.json(positionsFixture)
      }),
    )

    renderWithQueryClient(<PositionsPage />)

    expect(screen.getByRole('status', { name: 'Loading positions' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Refresh positions' })).not.toBeInTheDocument()
  })

  it('shows an actionable empty state without implying synchronous processing', async () => {
    const onAddFill = vi.fn()
    const user = userEvent.setup()
    server.use(http.get('/api/positions', () => HttpResponse.json([])))

    renderWithQueryClient(<PositionsPage onAddFill={onAddFill} />)

    expect(await screen.findByRole('heading', { name: 'No positions yet' })).toBeInTheDocument()
    expect(screen.getByText(/after the asynchronous processor applies it/i)).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: '+ Add fill' }))
    expect(onAddFill).toHaveBeenCalledOnce()
  })

  it('renders generated position data and exposes profit and loss in text', async () => {
    usePositions()
    renderWithQueryClient(<PositionsPage />)

    const table = await screen.findByRole('table')
    const appleRow = within(table).getByText('AAPL').closest('tr')
    const microsoftRow = within(table).getByText('MSFT').closest('tr')

    expect(appleRow).toHaveTextContent('125')
    expect(appleRow).toHaveTextContent('£172.35')
    expect(appleRow).toHaveTextContent('Profit: +£1,240.50')
    expect(microsoftRow).toHaveTextContent('Loss: -£125.75')
  })

  it('shows Problem Details and the correlation ID, then allows retry', async () => {
    const user = userEvent.setup()
    let requestCount = 0
    server.use(
      http.get('/api/positions', () => {
        requestCount += 1
        return requestCount === 1
          ? HttpResponse.json(problemDetailsFixture, { status: 500 })
          : HttpResponse.json(positionsFixture)
      }),
    )

    renderWithQueryClient(<PositionsPage />)

    const error = await screen.findByRole('alert')
    expect(error).toHaveTextContent('The positions store could not be reached.')
    expect(error).toHaveTextContent('Correlation ID: corr-positions-42')

    await user.click(within(error).getByRole('button', { name: 'Try again' }))
    expect(await screen.findByRole('table')).toBeInTheDocument()
  })

  it('announces background refetching without replacing the current data', async () => {
    const user = userEvent.setup()
    let requestCount = 0
    let releaseRefresh: (() => void) | undefined
    const refreshGate = new Promise<void>((resolve) => {
      releaseRefresh = resolve
    })

    server.use(
      http.get('/api/positions', async () => {
        requestCount += 1
        if (requestCount > 1) await refreshGate
        return HttpResponse.json(positionsFixture)
      }),
    )

    renderWithQueryClient(<PositionsPage />)

    const table = await screen.findByRole('table')
    await user.click(screen.getByRole('button', { name: 'Refresh positions' }))

    expect(await screen.findByText('Refreshing positions…')).toBeInTheDocument()
    expect(table).toBeInTheDocument()
    expect(within(table).getByText('AAPL')).toBeInTheDocument()

    act(() => releaseRefresh?.())
    await waitFor(() => {
      expect(screen.queryByText('Refreshing positions…')).not.toBeInTheDocument()
    })
  })
})

describe('Lots drawer through PositionsPage', () => {
  it('shows a loading state and returns focus to View lots after Escape', async () => {
    const user = userEvent.setup()
    usePositions()
    server.use(
      http.get('/api/positions/AAPL/lots', async () => {
        await delay('infinite')
        return HttpResponse.json(appleLotsFixture)
      }),
    )
    renderWithQueryClient(<PositionsPage />)

    const trigger = (await screen.findAllByRole('button', { name: 'View lots →' }))[0]
    if (!trigger) throw new Error('Expected the first View lots button.')
    await user.click(trigger)

    const drawer = await screen.findByRole('dialog', { name: 'AAPL — Open lots' })
    expect(within(drawer).getByRole('status', { name: 'Loading open lots' })).toBeInTheDocument()
    expect(drawer).toContainElement(document.activeElement as HTMLElement)

    await user.keyboard('{Escape}')
    await waitFor(() => expect(drawer).not.toBeInTheDocument())
    expect(trigger).toHaveFocus()
  })

  it('renders FIFO lots and the position aggregate', async () => {
    const user = userEvent.setup()
    usePositions()
    server.use(
      http.get('/api/positions/AAPL/lots', () => HttpResponse.json(appleLotsFixture)),
    )
    renderWithQueryClient(<PositionsPage />)

    const trigger = (await screen.findAllByRole('button', { name: 'View lots →' }))[0]
    if (!trigger) throw new Error('Expected the first View lots button.')
    await user.click(trigger)

    const drawer = await screen.findByRole('dialog', { name: 'AAPL — Open lots' })
    const lotsTable = await within(drawer).findByRole('table')
    const rows = within(lotsTable).getAllByRole('row')

    expect(rows).toHaveLength(3)
    expect(within(lotsTable).getByText('04/03/2026 UTC')).toBeInTheDocument()
    expect(within(lotsTable).getByText('19/05/2026 UTC')).toBeInTheDocument()
    expect(within(drawer).getByText('125 remaining · avg cost £172.35')).toBeInTheDocument()
    expect(within(drawer).getByText(/oldest open lot first/i)).toBeInTheDocument()
  })

  it('distinguishes a missing position from a generic lots failure', async () => {
    const user = userEvent.setup()
    usePositions()
    server.use(
      http.get('/api/positions/AAPL/lots', () =>
        HttpResponse.json(
          { title: 'Not found', status: 404, detail: 'No position exists for AAPL.' },
          { status: 404 },
        ),
      ),
    )
    renderWithQueryClient(<PositionsPage />)

    const trigger = (await screen.findAllByRole('button', { name: 'View lots →' }))[0]
    if (!trigger) throw new Error('Expected the first View lots button.')
    await user.click(trigger)

    const drawer = await screen.findByRole('dialog', { name: 'AAPL — Open lots' })
    expect(await within(drawer).findByRole('heading', { name: 'Position not found' })).toBeInTheDocument()
    expect(within(drawer).queryByText('Open lots unavailable')).not.toBeInTheDocument()
  })

  it('shows an explicit empty-lots state for a fully closed position', async () => {
    const user = userEvent.setup()
    usePositions()
    server.use(http.get('/api/positions/AAPL/lots', () => HttpResponse.json([])))
    renderWithQueryClient(<PositionsPage />)

    const trigger = (await screen.findAllByRole('button', { name: 'View lots →' }))[0]
    if (!trigger) throw new Error('Expected the first View lots button.')
    await user.click(trigger)

    const drawer = await screen.findByRole('dialog', { name: 'AAPL — Open lots' })
    expect(await within(drawer).findByRole('heading', { name: 'No open lots' })).toBeVisible()
    expect(within(drawer).getByText(/fully closed/i)).toBeVisible()
  })

  it('shows a generic lots failure with correlation ID and recovers on retry', async () => {
    const user = userEvent.setup()
    let lotsRequests = 0
    usePositions()
    server.use(
      http.get('/api/positions/AAPL/lots', () => {
        lotsRequests += 1
        return lotsRequests === 1
          ? HttpResponse.json(problemDetailsFixture, { status: 500 })
          : HttpResponse.json(appleLotsFixture)
      }),
    )
    renderWithQueryClient(<PositionsPage />)

    const trigger = (await screen.findAllByRole('button', { name: 'View lots →' }))[0]
    if (!trigger) throw new Error('Expected the first View lots button.')
    await user.click(trigger)

    const drawer = await screen.findByRole('dialog', { name: 'AAPL — Open lots' })
    const error = await within(drawer).findByRole('alert')
    expect(error).toHaveTextContent('The positions store could not be reached.')
    expect(error).toHaveTextContent('Correlation ID: corr-positions-42')

    await user.click(within(error).getByRole('button', { name: 'Try again' }))
    expect(await within(drawer).findByRole('table')).toBeVisible()
    expect(lotsRequests).toBe(2)
  })
})
