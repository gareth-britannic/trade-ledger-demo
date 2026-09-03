import type {
  CreateFillResponse,
  LotResponse,
  PositionResponse,
  ProblemDetails,
} from '../api/generated/model'

export const positionsFixture = [
  {
    symbol: 'AAPL',
    openQuantity: 125,
    averageUnitCost: 172.35,
    realisedPnl: 1240.5,
  },
  {
    symbol: 'MSFT',
    openQuantity: 20,
    averageUnitCost: 310,
    realisedPnl: -125.75,
  },
] satisfies PositionResponse[]

export const appleLotsFixture = [
  {
    id: 'e591b278-1b31-4898-8952-62db1aa48134',
    symbol: 'AAPL',
    remainingQuantity: 100,
    unitCost: 172,
    openedAt: '2026-03-04T10:30:00Z',
  },
  {
    id: '13649f24-8c11-44f9-9774-9915c5596e66',
    symbol: 'AAPL',
    remainingQuantity: 25,
    unitCost: 173.75,
    openedAt: '2026-05-19T14:45:00Z',
  },
] satisfies LotResponse[]

export const acceptedFillFixture = {
  fillId: 'ba79eb72-e3ae-4785-b31c-3299811a436f',
} satisfies CreateFillResponse

export const problemDetailsFixture = {
  type: 'https://trade-ledger.local/problems/unavailable',
  title: 'Ledger query failed',
  status: 500,
  detail: 'The positions store could not be reached.',
  correlationId: 'corr-positions-42',
} satisfies ProblemDetails
