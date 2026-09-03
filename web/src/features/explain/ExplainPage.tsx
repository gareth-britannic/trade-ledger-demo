import { zodResolver } from '@hookform/resolvers/zod'
import { useForm, useWatch } from 'react-hook-form'

import { useExplainLedger } from '../../api/generated/explain/explain'
import { ApiError } from '../../api/http/api-fetch'
import { Button, InlineNotice, Input } from '../../components/ui'
import {
  explainQuestionSchema,
  maximumQuestionLength,
  type ExplainQuestionInput,
  type ExplainQuestionValues,
} from './explain-schema'

const fieldErrorMessage = (error: ApiError): string | undefined =>
  Object.values(error.fieldErrors).flat().find((message) => message.trim().length > 0)

const getErrorDetails = (
  error: unknown,
): { title: string; message: string; correlationId?: string } => {
  if (error instanceof ApiError) {
    return {
      title: error.title || 'The ledger could not be explained',
      message:
        error.detail ??
        fieldErrorMessage(error) ??
        'The explanation request failed. Check the question and try again.',
      ...(error.correlationId ? { correlationId: error.correlationId } : {}),
    }
  }

  return {
    title: 'The ledger could not be explained',
    message: 'An unexpected error occurred. Try asking again.',
  }
}

export function ExplainPage() {
  const explain = useExplainLedger()
  const {
    register,
    handleSubmit,
    control,
    formState: { errors },
  } = useForm<ExplainQuestionInput, unknown, ExplainQuestionValues>({
    resolver: zodResolver(explainQuestionSchema),
    defaultValues: { question: '' },
  })

  const question = useWatch({ control, name: 'question' }) ?? ''
  const response = explain.data?.status === 200 ? explain.data.data : undefined
  const toolCalls = response?.toolCalls ?? []
  const answer = response?.answer?.trim()
  const apiError = explain.isError ? getErrorDetails(explain.error) : undefined

  const submitQuestion = ({ question: trimmedQuestion }: ExplainQuestionValues) => {
    explain.mutate({ data: { question: trimmedQuestion } })
  }
  const submitForm = handleSubmit(submitQuestion)

  return (
    <main
      aria-labelledby="explain-page-title"
      className="mx-auto w-full max-w-screen-2xl px-4 py-6 sm:px-6 lg:px-8"
    >
      <h1
        id="explain-page-title"
        className="mb-5 font-mono text-label font-medium uppercase text-ink-3"
      >
        Explain
      </h1>

      <form
        aria-busy={explain.isPending}
        className="rounded-control border border-line bg-surface p-3"
        noValidate
        onSubmit={(event) => void submitForm(event)}
      >
        <div className="flex flex-col gap-3 sm:flex-row sm:items-start">
          <div className="min-w-0 flex-1">
            <label className="sr-only" htmlFor="ledger-question">
              Question about the ledger
            </label>
            <Input
              {...register('question', {
                onChange: () => {
                  if (explain.isError) explain.reset()
                },
              })}
              aria-describedby={`ledger-question-help ledger-question-count${errors.question ? ' ledger-question-error' : ''}`}
              aria-invalid={errors.question ? 'true' : 'false'}
              autoComplete="off"
              className="min-h-8 border-transparent bg-transparent px-1 py-1 shadow-none hover:border-transparent focus:border-transparent focus:ring-0"
              id="ledger-question"
              placeholder="What’s my realised P&amp;L on AAPL this month?"
              type="text"
            />
            <div className="mt-2 flex items-start justify-between gap-4 px-1 text-label text-ink-3">
              <p id="ledger-question-help">Ask about positions, realised P&amp;L, or open lots.</p>
              <p
                className={question.length > maximumQuestionLength ? 'text-warn' : undefined}
                id="ledger-question-count"
              >
                {question.length}/{maximumQuestionLength}
              </p>
            </div>
          </div>

          <Button
            isLoading={explain.isPending}
            loadingLabel="Asking…"
            size="sm"
            type="submit"
          >
            Ask
          </Button>
        </div>

        {errors.question ? (
          <p className="mt-2 px-1 text-body text-warn" id="ledger-question-error" role="alert">
            {errors.question.message}
          </p>
        ) : null}
      </form>

      <aside className="mt-3 border-l-2 border-line pl-3 text-body text-ink-2" aria-label="Explanation limitation">
        Answers are produced deterministically from repository-backed ledger data. Real Ollama narration
        is still work in progress.
      </aside>

      {explain.isPending ? (
        <InlineNotice
          aria-live="polite"
          className="mt-5"
          role="status"
        >
          Reading the ledger and preparing an answer…
        </InlineNotice>
      ) : null}

      {apiError ? (
        <InlineNotice className="mt-5" role="alert" title={apiError.title} tone="error">
          <p>{apiError.message}</p>
          {apiError.correlationId ? (
            <p className="mt-2 font-mono text-label">
              Correlation ID: <span className="select-all">{apiError.correlationId}</span>
            </p>
          ) : null}
        </InlineNotice>
      ) : null}

      {response ? (
        <div className="mt-5 space-y-5">
          <section aria-labelledby="tool-calls-title">
            <h2
              className="mb-2 font-mono text-label font-medium uppercase text-ink-3"
              id="tool-calls-title"
            >
              Tool calls
            </h2>

            {toolCalls.length > 0 ? (
              <ol className="space-y-2">
                {toolCalls.map((toolCall, index) => (
                  <li
                    className="flex min-h-9 items-center gap-3 rounded-control border border-line bg-surface px-3 py-2"
                    key={`${index}-${toolCall}`}
                  >
                    <span aria-hidden="true" className="size-1.5 shrink-0 rounded-full bg-accent" />
                    <code className="min-w-0 break-words font-data text-ink-2">{toolCall}</code>
                  </li>
                ))}
              </ol>
            ) : (
              <p className="rounded-control border border-line bg-surface px-4 py-3 text-body text-ink-3">
                No tool calls were reported for this answer.
              </p>
            )}
          </section>

          <section
            aria-labelledby="answer-title"
            aria-live="polite"
            className="rounded-control bg-accent-soft px-4 py-4"
          >
            <h2
              className="mb-2 font-mono text-label font-medium uppercase text-accent"
              id="answer-title"
            >
              Answer
            </h2>
            <p className="max-w-5xl text-body leading-6 text-ink">
              {answer || 'The API returned no explanation for this question.'}
            </p>
          </section>
        </div>
      ) : null}
    </main>
  )
}

export default ExplainPage
