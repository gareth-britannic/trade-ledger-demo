import { zodResolver } from '@hookform/resolvers/zod'
import { useQueryClient } from '@tanstack/react-query'
import { useEffect, useRef, useState } from 'react'
import { useForm } from 'react-hook-form'

import type { CreateFillRequest } from '../../api/generated/model'
import { useCreateFill } from '../../api/generated/fills/fills'
import {
  getGetPositionsQueryOptions,
  getGetPositionsQueryKey,
  type getPositionsResponse,
} from '../../api/generated/positions/positions'
import { ApiError } from '../../api/http/api-fetch'
import { Button, Dialog, Field, InlineNotice } from '../../components/ui'
import { formatLocalDateTime, localDateTimeToIso, toDateTimeLocalValue } from '../../lib/date'
import {
  observeAcceptedFill,
  positionFingerprint,
} from './accepted-fill-refresh'
import {
  addFillSchema,
  type AddFillInput,
  type AddFillValues,
} from './add-fill-schema'

type AcceptancePhase = 'editing' | 'accepted' | 'observed' | 'timed-out'

export interface AddFillDialogProps {
  onOpenChange: (open: boolean) => void
  open: boolean
}

const defaults = (): AddFillInput => ({
  symbol: '',
  side: 'Buy',
  quantity: '',
  price: '',
  executedAt: toDateTimeLocalValue(),
})

const getApiErrorMessage = (error: ApiError): string =>
  error.detail ?? Object.values(error.fieldErrors).flat()[0] ?? error.message

export function AddFillDialog({ onOpenChange, open }: AddFillDialogProps) {
  const queryClient = useQueryClient()
  const mutation = useCreateFill()
  const pollControllers = useRef(new Set<AbortController>())
  const pollSequence = useRef(0)
  const [attemptFillId, setAttemptFillId] = useState<string>()
  const [phase, setPhase] = useState<AcceptancePhase>('editing')
  const [acceptedSymbol, setAcceptedSymbol] = useState('')
  const [acceptedExecution, setAcceptedExecution] = useState('')
  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<AddFillInput, unknown, AddFillValues>({
    resolver: zodResolver(addFillSchema),
    defaultValues: defaults(),
  })

  useEffect(
    () => () => {
      pollSequence.current += 1
      for (const controller of pollControllers.current) controller.abort()
      pollControllers.current.clear()
    },
    [],
  )

  const resetAttempt = () => {
    pollSequence.current += 1
    mutation.reset()
    setAttemptFillId(undefined)
    setPhase('editing')
    setAcceptedSymbol('')
    setAcceptedExecution('')
    reset(defaults())
  }

  const dismissAttempt = () => {
    // Keep the bounded cache refresh running after the dialog closes so the
    // positions screen can converge without requiring the user to keep a
    // modal open. Ignore its later status result because the dialog is reset.
    pollSequence.current += 1
    mutation.reset()
    setAttemptFillId(undefined)
    setPhase('editing')
    setAcceptedSymbol('')
    setAcceptedExecution('')
    reset(defaults())
  }

  const changeOpen = (nextOpen: boolean) => {
    if (!nextOpen) dismissAttempt()
    onOpenChange(nextOpen)
  }

  const submitFill = async (values: AddFillValues) => {
    const stableFillId = attemptFillId ?? crypto.randomUUID()
    setAttemptFillId(stableFillId)
    let cachedResponse = queryClient.getQueryData<getPositionsResponse>(getGetPositionsQueryKey())
    if (!cachedResponse) {
      try {
        cachedResponse = await queryClient.fetchQuery({
          ...getGetPositionsQueryOptions(),
          staleTime: 0,
        })
      } catch {
        // Fill submission remains available if the optional baseline read fails.
      }
    }
    const baseline = positionFingerprint(cachedResponse, values.symbol)
    const request: CreateFillRequest = {
      fillId: stableFillId,
      symbol: values.symbol,
      side: values.side,
      quantity: Number(values.quantity),
      price: Number(values.price),
      executedAt: localDateTimeToIso(values.executedAt),
    }

    try {
      const response = await mutation.mutateAsync({ data: request })
      if (response.status !== 202) return

      setAttemptFillId(undefined)
      setAcceptedSymbol(values.symbol)
      setAcceptedExecution(request.executedAt ?? '')
      setPhase('accepted')

      const controller = new AbortController()
      pollControllers.current.add(controller)
      const sequence = ++pollSequence.current

      void observeAcceptedFill(queryClient, values.symbol, baseline, controller.signal)
        .then((result) => {
          if (pollSequence.current === sequence) setPhase(result)
        })
        .catch((error: unknown) => {
          if (
            pollSequence.current === sequence &&
            !(error instanceof DOMException && error.name === 'AbortError')
          ) {
            setPhase('timed-out')
          }
        })
        .finally(() => pollControllers.current.delete(controller))
    } catch {
      // The mutation exposes the normalized error while the same fill ID is retained for retry.
    }
  }

  const pending = phase !== 'editing'
  const apiError = mutation.isError && mutation.error instanceof ApiError ? mutation.error : undefined

  return (
    <Dialog
      description="Queue an executed trade for asynchronous FIFO processing."
      onOpenChange={changeOpen}
      open={open}
      title="Add fill"
    >
      <form
        noValidate
        onSubmit={(event) => {
          void handleSubmit(submitFill)(event)
        }}
      >
        <fieldset className="space-y-[18px]" disabled={isSubmitting || pending}>
          <legend className="sr-only">Executed fill details</legend>
          <Field
            {...register('symbol')}
            autoCapitalize="characters"
            autoComplete="off"
            className="uppercase"
            error={errors.symbol?.message}
            id="fill-symbol"
            label="Symbol"
            placeholder="AAPL"
            required
          />

          <div>
            <span className="font-mono text-label font-medium uppercase text-ink-2" id="fill-side-label">
              Side <span aria-hidden="true" className="text-warn">*</span>
            </span>
            <div
              aria-describedby={errors.side ? 'fill-side-error' : undefined}
              aria-labelledby="fill-side-label"
              className="mt-2 grid grid-cols-2 rounded-control border border-line bg-surface-2 p-1"
              role="radiogroup"
            >
              {(['Buy', 'Sell'] as const).map((side) => (
                <label className="relative" key={side}>
                  <input className="peer sr-only" type="radio" value={side} {...register('side')} />
                  <span className="flex min-h-8 cursor-pointer items-center justify-center rounded-control text-body text-ink-2 transition-colors peer-checked:bg-accent-soft peer-checked:font-medium peer-checked:text-accent peer-focus-visible:outline-2 peer-focus-visible:outline-offset-2 peer-focus-visible:outline-accent">
                    {side}
                  </span>
                </label>
              ))}
            </div>
            {errors.side ? (
              <p className="mt-2 text-xs font-medium text-warn" id="fill-side-error" role="alert">
                {errors.side.message}
              </p>
            ) : null}
          </div>

          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
            <Field
              {...register('quantity')}
              autoComplete="off"
              error={errors.quantity?.message}
              id="fill-quantity"
              inputMode="decimal"
              label="Quantity"
              placeholder="150"
              required
            />
            <Field
              {...register('price')}
              autoComplete="off"
              error={errors.price?.message}
              id="fill-price"
              inputMode="decimal"
              label="Price (GBP)"
              placeholder="191.40"
              required
            />
          </div>

          <Field
            {...register('executedAt')}
            error={errors.executedAt?.message}
            hint="Entered in your local time and sent to the ledger as UTC."
            id="fill-executed-at"
            label="Execution date & time"
            required
            type="datetime-local"
          />

          <Button className="w-full" isLoading={isSubmitting} loadingLabel="Queuing fill…" type="submit">
            Submit fill
          </Button>
        </fieldset>

        {apiError ? (
          <InlineNotice className="mt-[18px]" title="Fill was not accepted" tone="error">
            <p>{getApiErrorMessage(apiError)}</p>
            <p className="mt-1 text-xs">Correct the details or retry. A retry uses the same fill ID.</p>
            {apiError.correlationId ? (
              <p className="mt-2 font-mono text-xs">Correlation ID: {apiError.correlationId}</p>
            ) : null}
          </InlineNotice>
        ) : null}

        {pending ? (
          <InlineNotice
            aria-live="polite"
            className="mt-[18px]"
            title={phase === 'observed' ? 'Position update observed' : '202 Accepted'}
            tone={phase === 'observed' ? 'success' : 'warning'}
          >
            {phase === 'accepted' ? (
              <p>Queued — waiting for {acceptedSymbol} to appear in positions once applied.</p>
            ) : null}
            {phase === 'observed' ? (
              <p>The positions API now reflects a change for {acceptedSymbol}.</p>
            ) : null}
            {phase === 'timed-out' ? (
              <p>
                The fill remains accepted, but no position change was observed during the bounded refresh.
                Refresh positions manually to check again.
              </p>
            ) : null}
            {acceptedExecution ? (
              <p className="mt-2 text-xs text-ink-2">Execution: {formatLocalDateTime(acceptedExecution)}</p>
            ) : null}
            <div className="mt-3 flex flex-wrap gap-2">
              <Button onClick={resetAttempt} size="sm" variant="secondary">
                Queue another fill
              </Button>
              {phase === 'timed-out' ? (
                <Button
                  onClick={() => void queryClient.invalidateQueries({ queryKey: getGetPositionsQueryKey() })}
                  size="sm"
                  variant="ghost"
                >
                  Refresh positions
                </Button>
              ) : null}
            </div>
          </InlineNotice>
        ) : null}
      </form>
    </Dialog>
  )
}
