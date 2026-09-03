import { useGetPositionLots } from '../../api/generated/positions/positions'
import type { LotResponse, PositionResponse } from '../../api/generated/model'
import { ApiError } from '../../api/http/api-fetch'
import {
  Button,
  Drawer,
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
import { formatUtcDate } from '../../lib/date'
import { formatMoney, formatQuantity } from '../../lib/format'

interface LotsDrawerProps {
  onOpenChange: (open: boolean) => void
  open: boolean
  position: PositionResponse | null
  symbol: string | null
}

function LotsLoadingState() {
  return (
    <div aria-label="Loading open lots" className="space-y-2" role="status">
      <span className="sr-only">Loading open lots…</span>
      <Skeleton className="h-10 w-full" />
      <Skeleton className="h-11 w-full" />
      <Skeleton className="h-11 w-full" />
    </div>
  )
}

function LotsErrorState({ error, onRetry }: { error: unknown; onRetry: () => void }) {
  const isNotFound = error instanceof ApiError && error.status === 404

  if (isNotFound) {
    return (
      <EmptyState
        title="Position not found"
        description="This position may have changed since the positions list was loaded. Close the drawer and refresh the ledger."
      />
    )
  }

  const detail =
    error instanceof ApiError
      ? (error.detail ?? Object.values(error.fieldErrors).flat()[0] ?? error.message)
      : 'Open lots could not be loaded. Try again.'

  return (
    <InlineNotice title="Open lots unavailable" tone="error">
      <p>{detail}</p>
      {error instanceof ApiError && error.correlationId ? (
        <p className="mt-2 font-mono text-xs">Correlation ID: {error.correlationId}</p>
      ) : null}
      <Button className="mt-3" onClick={onRetry} size="sm" variant="secondary">
        Try again
      </Button>
    </InlineNotice>
  )
}

export function LotsDrawer({ onOpenChange, open, position, symbol }: LotsDrawerProps) {
  const normalizedSymbol = symbol?.trim().toUpperCase() ?? ''
  const lotsQuery = useGetPositionLots({ symbol: normalizedSymbol }, {
    query: {
      enabled: open && normalizedSymbol.length > 0,
      select: (response): LotResponse[] =>
        response.status === 200 && Array.isArray(response.data) ? response.data : [],
    },
  })
  const lots = lotsQuery.data ?? []

  return (
    <Drawer
      description="FIFO order — oldest first"
      onOpenChange={onOpenChange}
      open={open}
      title={<span className="block break-all">{normalizedSymbol || 'Position'} — Open lots</span>}
    >
      {lotsQuery.isPending ? <LotsLoadingState /> : null}

      {lotsQuery.isError ? (
        <LotsErrorState error={lotsQuery.error} onRetry={() => void lotsQuery.refetch()} />
      ) : null}

      {!lotsQuery.isPending && !lotsQuery.isError && lots.length === 0 ? (
        <EmptyState
          title="No open lots"
          description="This position has no remaining FIFO lots. It may have been fully closed."
        />
      ) : null}

      {!lotsQuery.isPending && !lotsQuery.isError && lots.length > 0 ? (
        <>
          <TableContainer>
            <Table>
              <caption className="sr-only">FIFO-ordered open lots for {normalizedSymbol}</caption>
              <TableHeader>
                <TableRow className="hover:bg-surface">
                  <TableHead>Opened</TableHead>
                  <TableHead>Qty remaining</TableHead>
                  <TableHead>Unit cost</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {lots.map((lot, index) => (
                  <TableRow key={lot.id ?? `${lot.openedAt ?? 'lot'}-${index}`}>
                    <TableCell className="whitespace-nowrap font-mono text-data text-ink">
                      {formatUtcDate(lot.openedAt)}
                    </TableCell>
                    <TableCell className="font-data text-ink">
                      {formatQuantity(lot.remainingQuantity)}
                    </TableCell>
                    <TableCell className="font-data text-ink">
                      {formatMoney(lot.unitCost)}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>

          {position ? (
            <div className="mt-4 rounded-control bg-accent-soft px-4 py-3">
              <p className="break-words font-data font-semibold text-accent">
                {formatQuantity(position.openQuantity)} remaining · avg cost{' '}
                {formatMoney(position.averageUnitCost)}
              </p>
              <p className="mt-1 text-body text-ink-2">
                A sell consumes the oldest open lot first.
              </p>
            </div>
          ) : null}
        </>
      ) : null}
    </Drawer>
  )
}
