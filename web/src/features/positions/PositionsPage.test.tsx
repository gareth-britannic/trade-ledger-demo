import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { act, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { delay, http, HttpResponse } from 'msw'
import type { ReactElement } from 'react'
import { describe, expect, it, vi } from 'vitest'

import {
  getGetPositionsQueryKey,
  type getPositionsResponse,
} from '../../api/generated/positions/positions'
import {
  appleLotsFixture,
  positionsFixture,
  problemDetailsFixture,
} from '../../test/fixtures'
import { server } from '../../test/server'
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
    expect(screen.getByTestId('positions-loading-desktop')).toHaveClass('hidden', 'md:block')
    expect(screen.getByTestId('positions-loading-mobile')).toHaveClass('md:hidden')
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

  it('distinguishes zero realised P&L from an unavailable value', async () => {
    server.use(
      http.get('/api/positions', () =>
        HttpResponse.json([
          { symbol: 'ZERO', openQuantity: 1, averageUnitCost: 10, realisedPnl: 0 },
          { symbol: 'UNKNOWN', openQuantity: 1, averageUnitCost: 10 },
        ]),
      ),
    )
    renderWithQueryClient(<PositionsPage />)

    const table = await screen.findByRole('table')
    const zeroRow = within(table).getByText('ZERO').closest('tr')
    const unavailableRow = within(table).getByText('UNKNOWN').closest('tr')

    expect(zeroRow).toHaveTextContent('No gain or loss: £0.00')
    expect(zeroRow).not.toHaveTextContent('+£0.00')
    expect(unavailableRow).toHaveTextContent('P&L unavailable: —')
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

  it('keeps cached positions visible when a background refresh fails and recovers on retry', async () => {
    const user = userEvent.setup()
    let requestCount = 0
    server.use(
      http.get('/api/positions', () => {
        requestCount += 1
        return requestCount === 2
          ? HttpResponse.json(problemDetailsFixture, { status: 500 })
          : HttpResponse.json(positionsFixture)
      }),
    )

    renderWithQueryClient(<PositionsPage />)

    const table = await screen.findByRole('table')
    await user.click(screen.getByRole('button', { name: 'Refresh positions' }))

    const warningTitle = await screen.findByText('Positions could not be refreshed')
    const warning = warningTitle.closest<HTMLElement>('[role="status"]')
    expect(warning).not.toBeNull()
    expect(warning).toHaveTextContent('The last loaded positions are still shown.')
    expect(warning).toHaveTextContent('Correlation ID: corr-positions-42')
    expect(table).toBeInTheDocument()
    expect(within(table).getByText('AAPL')).toBeInTheDocument()

    if (!warning) throw new Error('Expected the nonblocking refresh warning.')
    await user.click(within(warning).getByRole('button', { name: 'Try again' }))
    await waitFor(() => expect(screen.queryByText('Positions could not be refreshed')).not.toBeInTheDocument())
    expect(requestCount).toBe(3)
  })
})

describe('Lots drawer through PositionsPage', () => {
  it('shows a loading state and returns focus to View lots after Escape', async () => {
    const user = userEvent.setup()
    usePositions()
    server.use(
      http.get('/api/positions/lots', async () => {
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
      http.get('/api/positions/lots', () => HttpResponse.json(appleLotsFixture)),
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

  it('uses a query parameter for slash symbols and keeps maximum-length labels wrappable', async () => {
    const user = userEvent.setup()
    const symbol = 'BRK/B-ABCDEFGHIJKLMNOPQRSTUVWXYZ'
    let requestedSymbol: string | null = null
    server.use(
      http.get('/api/positions', () =>
        HttpResponse.json([{ symbol, openQuantity: 5, averageUnitCost: 200, realisedPnl: 0 }]),
      ),
      http.get('/api/positions/lots', ({ request }) => {
        requestedSymbol = new URL(request.url).searchParams.get('symbol')
        return HttpResponse.json(appleLotsFixture.map((lot) => ({ ...lot, symbol })))
      }),
    )
    renderWithQueryClient(<PositionsPage />)

    const mobileList = await screen.findByRole('list')
    const mobileSymbol = within(mobileList).getByText(symbol)
    expect(mobileSymbol).toHaveClass('min-w-0', 'break-all')
    await user.click(within(mobileList).getByRole('button', { name: 'View lots →' }))

    const drawer = await screen.findByRole('dialog', { name: `${symbol} — Open lots` })
    await within(drawer).findByRole('table')
    expect(requestedSymbol).toBe(symbol)
    expect(within(drawer).getByRole('heading', { name: `${symbol} — Open lots` }).firstElementChild)
      .toHaveClass('break-all')
  })

  it('updates the open drawer aggregate from the latest positions query data', async () => {
    const user = userEvent.setup()
    usePositions()
    server.use(http.get('/api/positions/lots', () => HttpResponse.json(appleLotsFixture)))
    const { queryClient } = renderWithQueryClient(<PositionsPage />)

    const trigger = (await screen.findAllByRole('button', { name: 'View lots →' }))[0]
    if (!trigger) throw new Error('Expected the first View lots button.')
    await user.click(trigger)

    const drawer = await screen.findByRole('dialog', { name: 'AAPL — Open lots' })
    expect(await within(drawer).findByText('125 remaining · avg cost £172.35')).toBeInTheDocument()

    const updatedPositions = positionsFixture.map((position) =>
      position.symbol === 'AAPL'
        ? { ...position, openQuantity: 130, averageUnitCost: 175 }
        : position,
    )
    act(() => {
      queryClient.setQueryData<getPositionsResponse>(getGetPositionsQueryKey(), {
        data: updatedPositions,
        headers: new Headers(),
        status: 200,
      })
    })

    expect(await within(drawer).findByText('130 remaining · avg cost £175.00')).toBeInTheDocument()
  })

  it('distinguishes a missing position from a generic lots failure', async () => {
    const user = userEvent.setup()
    usePositions()
    server.use(
      http.get('/api/positions/lots', () =>
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
    server.use(http.get('/api/positions/lots', () => HttpResponse.json([])))
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
      http.get('/api/positions/lots', () => {
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
