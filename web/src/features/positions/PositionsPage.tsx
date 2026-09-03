import { useState } from 'react'

import type { PositionResponse } from '../../api/generated/model'
import { useGetPositions } from '../../api/generated/positions/positions'
import { ApiError } from '../../api/http/api-fetch'
import {
  Button,
  EmptyState,
  InlineNotice,
  Skeleton,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableHeader,
  TableRow,
} from '../../components/ui'
import { formatMoney, formatQuantity, formatSignedMoney } from '../../lib/format'
import { LotsDrawer } from './LotsDrawer'

interface PositionsPageProps {
  onAddFill?: () => void
}

function PositionLoadingState() {
  return (
    <div aria-label="Loading positions" className="rounded-modal border border-line" role="status">
      <span className="sr-only">Loading positions…</span>
      <Skeleton className="h-11 rounded-b-none" />
      {[0, 1, 2, 3].map((row) => (
        <div className="grid h-12 grid-cols-5 items-center gap-6 border-t border-line px-5" key={row}>
          <Skeleton className="h-3 w-16" />
          <Skeleton className="h-3 w-20" />
          <Skeleton className="h-3 w-20" />
          <Skeleton className="h-3 w-24" />
          <Skeleton className="ml-auto h-3 w-20" />
        </div>
      ))}
    </div>
  )
}

function ErrorState({ error, onRetry }: { error: unknown; onRetry: () => void }) {
  const fieldMessage = error instanceof ApiError ? Object.values(error.fieldErrors).flat()[0] : undefined
  const message =
    error instanceof ApiError
      ? (error.detail ?? fieldMessage ?? error.message)
      : 'Positions could not be loaded. Try again.'

  return (
    <InlineNotice title="Positions unavailable" tone="error">
      <p>{message}</p>
      {error instanceof ApiError && error.correlationId ? (
        <p className="mt-2 font-mono text-xs">Correlation ID: {error.correlationId}</p>
      ) : null}
      <Button className="mt-3" onClick={onRetry} size="sm" variant="secondary">
        Try again
      </Button>
    </InlineNotice>
  )
}

function PnlValue({ value }: { value: number | undefined }) {
  const tone = value == null || value === 0 ? 'text-ink-2' : value > 0 ? 'text-accent' : 'text-warn'
  const meaning = value == null || value === 0 ? 'No gain or loss' : value > 0 ? 'Profit' : 'Loss'

  return (
    <span className={`font-data ${tone}`}>
      <span className="sr-only">{meaning}: </span>
      {formatSignedMoney(value)}
    </span>
  )
}

export function PositionsPage({ onAddFill }: PositionsPageProps) {
  const [selectedPosition, setSelectedPosition] = useState<PositionResponse | null>(null)
  const positionsQuery = useGetPositions<PositionResponse[]>({
    query: {
      staleTime: 20_000,
      select: (response): PositionResponse[] =>
        response.status === 200 && Array.isArray(response.data) ? response.data : [],
    },
  })
  const positions = positionsQuery.data ?? []

  return (
    <main className="mx-auto w-full max-w-screen-2xl px-4 py-6 sm:px-6 lg:px-8">
      <div className="mb-[18px] flex min-h-6 items-center justify-between gap-4">
        <h1 className="font-mono text-label font-medium uppercase tracking-[0.09375rem] text-ink-3">
          Positions
        </h1>
        <div aria-live="polite" className="flex items-center gap-3 text-xs text-ink-3">
          {positionsQuery.isFetching && !positionsQuery.isPending ? (
            <span role="status">Refreshing positions…</span>
          ) : null}
          {!positionsQuery.isPending ? (
            <Button
              aria-label="Refresh positions"
              disabled={positionsQuery.isFetching}
              onClick={() => void positionsQuery.refetch()}
              size="sm"
              variant="ghost"
            >
              Refresh
            </Button>
          ) : null}
        </div>
      </div>

      {positionsQuery.isPending ? <PositionLoadingState /> : null}

      {positionsQuery.isError ? (
        <ErrorState error={positionsQuery.error} onRetry={() => void positionsQuery.refetch()} />
      ) : null}

      {!positionsQuery.isPending && !positionsQuery.isError && positions.length === 0 ? (
        <EmptyState
          action={
            onAddFill ? (
              <Button onClick={onAddFill} size="sm">
                + Add fill
              </Button>
            ) : undefined
          }
          title="No positions yet"
          description="Queue the first executed fill. It will appear here after the asynchronous processor applies it."
        />
      ) : null}

      {!positionsQuery.isPending && !positionsQuery.isError && positions.length > 0 ? (
        <>
          <div className="hidden md:block">
            <TableContainer className="bg-surface">
              <Table>
                <TableHeader className="bg-surface-2">
                  <TableRow className="h-11 hover:bg-surface-2">
                    <TableHead className="w-[22%] px-5">Symbol</TableHead>
                    <TableHead className="w-[22%] px-5">Quantity</TableHead>
                    <TableHead className="w-[22%] px-5">Avg cost</TableHead>
                    <TableHead className="w-[22%] px-5">Realised P&amp;L</TableHead>
                    <TableHead className="px-5"><span className="sr-only">Actions</span></TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody className="bg-ground">
                  {positions.map((position, index) => {
                    const symbol = position.symbol?.toUpperCase() ?? '—'
                    return (
                      <TableRow className="h-12" key={`${symbol}-${index}`}>
                        <TableCell className="px-5 font-data font-semibold text-ink">{symbol}</TableCell>
                        <TableCell className="px-5 font-data text-ink">
                          {formatQuantity(position.openQuantity)}
                        </TableCell>
                        <TableCell className="px-5 font-data text-ink">
                          {formatMoney(position.averageUnitCost)}
                        </TableCell>
                        <TableCell className="px-5"><PnlValue value={position.realisedPnl} /></TableCell>
                        <TableCell className="px-5 text-right">
                          <Button
                            disabled={!position.symbol}
                            onClick={() => setSelectedPosition(position)}
                            size="sm"
                            variant="ghost"
                          >
                            View lots →
                          </Button>
                        </TableCell>
                      </TableRow>
                    )
                  })}
                </TableBody>
              </Table>
            </TableContainer>
          </div>

          <ul className="space-y-3 md:hidden">
            {positions.map((position, index) => {
              const symbol = position.symbol?.toUpperCase() ?? '—'
              return (
                <li className="rounded-modal border border-line bg-ground p-4" key={`${symbol}-${index}`}>
                  <div className="flex items-center justify-between gap-4 border-b border-line pb-3">
                    <span className="font-data font-semibold text-ink">{symbol}</span>
                    <PnlValue value={position.realisedPnl} />
                  </div>
                  <dl className="mt-3 grid grid-cols-2 gap-4">
                    <div>
                      <dt className="font-mono text-label uppercase tracking-wider text-ink-3">Quantity</dt>
                      <dd className="mt-1 font-data text-ink">{formatQuantity(position.openQuantity)}</dd>
                    </div>
                    <div>
                      <dt className="font-mono text-label uppercase tracking-wider text-ink-3">Avg cost</dt>
                      <dd className="mt-1 font-data text-ink">{formatMoney(position.averageUnitCost)}</dd>
                    </div>
                  </dl>
                  <Button
                    className="mt-3 w-full"
                    disabled={!position.symbol}
                    onClick={() => setSelectedPosition(position)}
                    size="sm"
                    variant="secondary"
                  >
                    View lots →
                  </Button>
                </li>
              )
            })}
          </ul>
        </>
      ) : null}

      <LotsDrawer
        onOpenChange={(open) => {
          if (!open) setSelectedPosition(null)
        }}
        open={selectedPosition !== null}
        position={selectedPosition}
      />
    </main>
  )
}
