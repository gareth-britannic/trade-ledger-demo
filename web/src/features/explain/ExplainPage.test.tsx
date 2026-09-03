import '@testing-library/jest-dom/vitest'

import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import type { ExplainResponse } from '../../api/generated/model'
import { ApiError } from '../../api/http/api-fetch'
import { ExplainPage } from './ExplainPage'

const useExplainLedgerMock = vi.hoisted(() => vi.fn())
const mutate = vi.hoisted(() => vi.fn())
const reset = vi.hoisted(() => vi.fn())

vi.mock('../../api/generated/explain/explain', () => ({
  useExplainLedger: useExplainLedgerMock,
}))

const idleMutation = (overrides: Record<string, unknown> = {}) => ({
  data: undefined,
  error: null,
  isError: false,
  isPending: false,
  isSuccess: false,
  mutate,
  reset,
  ...overrides,
})

describe('ExplainPage', () => {
  beforeEach(() => {
    useExplainLedgerMock.mockReturnValue(idleMutation())
  })

  afterEach(() => {
    cleanup()
  })

  it('validates the required question before calling the API', async () => {
    const user = userEvent.setup()
    render(<ExplainPage />)

    await user.click(screen.getByRole('button', { name: 'Ask' }))

    expect(await screen.findByText('Enter a question about the ledger.')).toBeInTheDocument()
    expect(mutate).not.toHaveBeenCalled()
  })

  it('rejects questions longer than 500 characters', async () => {
    const user = userEvent.setup()
    render(<ExplainPage />)

    fireEvent.change(screen.getByRole('textbox', { name: 'Question about the ledger' }), {
      target: { value: 'x'.repeat(501) },
    })
    await user.click(screen.getByRole('button', { name: 'Ask' }))

    expect(
      await screen.findByText('Keep the question to 500 characters or fewer.'),
    ).toBeInTheDocument()
    expect(mutate).not.toHaveBeenCalled()
  })

  it('trims a valid question and submits through the generated mutation', async () => {
    const user = userEvent.setup()
    render(<ExplainPage />)

    await user.type(
      screen.getByRole('textbox', { name: 'Question about the ledger' }),
      '   What is my AAPL P&L?   ',
    )
    await user.click(screen.getByRole('button', { name: 'Ask' }))

    expect(mutate).toHaveBeenCalledWith({ data: { question: 'What is my AAPL P&L?' } })
  })

  it('renders each reported tool call as a separate row and shows the answer as text', () => {
    const explanation = {
      toolCalls: [
        'get_positions()',
        'get_realised_pnl("AAPL", "month")',
        'get_lots("AAPL")',
      ],
      answer: 'Your realised P&L on AAPL this month is +£1,240.50.',
    } satisfies ExplainResponse

    useExplainLedgerMock.mockReturnValue(
      idleMutation({
        data: { data: explanation, headers: new Headers(), status: 200 },
        isSuccess: true,
      }),
    )

    render(<ExplainPage />)

    expect(screen.getAllByRole('listitem')).toHaveLength(3)
    expect(screen.getByText('get_positions()')).toBeInTheDocument()
    expect(screen.getByText('get_realised_pnl("AAPL", "month")')).toBeInTheDocument()
    expect(screen.getByText('get_lots("AAPL")')).toBeInTheDocument()
    expect(screen.getByText(explanation.answer)).toBeInTheDocument()
  })

  it('displays the API detail and correlation ID', () => {
    useExplainLedgerMock.mockReturnValue(
      idleMutation({
        error: new ApiError({
          status: 500,
          title: 'Explanation failed',
          detail: 'The ledger query could not be completed.',
          correlationId: 'corr-explain-42',
        }),
        isError: true,
      }),
    )

    render(<ExplainPage />)

    expect(screen.getByRole('alert')).toHaveTextContent('Explanation failed')
    expect(screen.getByRole('alert')).toHaveTextContent('The ledger query could not be completed.')
    expect(screen.getByRole('alert')).toHaveTextContent('Correlation ID: corr-explain-42')
  })

  it('shows a safe unexpected-error message and clears it when the question changes', () => {
    useExplainLedgerMock.mockReturnValue(
      idleMutation({
        error: new Error('internal provider detail'),
        isError: true,
      }),
    )

    render(<ExplainPage />)

    expect(screen.getByRole('alert')).toHaveTextContent(
      'An unexpected error occurred. Try asking again.',
    )
    fireEvent.change(screen.getByRole('textbox', { name: 'Question about the ledger' }), {
      target: { value: 'What are my positions?' },
    })
    expect(reset).toHaveBeenCalledOnce()
  })

  it('makes an empty successful response explicit instead of rendering blank sections', () => {
    useExplainLedgerMock.mockReturnValue(
      idleMutation({
        data: {
          data: { answer: '   ', toolCalls: [] } satisfies ExplainResponse,
          headers: new Headers(),
          status: 200,
        },
        isSuccess: true,
      }),
    )

    render(<ExplainPage />)

    expect(screen.getByText('No tool calls were reported for this answer.')).toBeVisible()
    expect(screen.getByText('The API returned no explanation for this question.')).toBeVisible()
  })
})
