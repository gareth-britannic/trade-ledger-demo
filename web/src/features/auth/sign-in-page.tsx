import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { useLocation, useNavigate } from 'react-router-dom'
import { z } from 'zod'

import { Button, Field, InlineNotice } from '../../components/ui'
import { AuthError } from './cognito-auth-client'
import { useAuth } from './use-auth'

const signInSchema = z.object({
  email: z.string().trim().min(1, 'Enter the demo email.').email('Enter a valid email address.'),
  password: z.string().min(1, 'Enter the demo password.'),
})

type SignInValues = z.infer<typeof signInSchema>

const safeReturnPath = (state: unknown): string => {
  if (typeof state !== 'object' || state === null || !('returnTo' in state)) return '/positions'
  const returnTo = state.returnTo
  return typeof returnTo === 'string' &&
    returnTo.startsWith('/') &&
    !returnTo.startsWith('//') &&
    !returnTo.startsWith('/sign-in')
    ? returnTo
    : '/positions'
}

const sessionEndedMessage = (state: unknown): string | null => {
  if (typeof state !== 'object' || state === null || !('reason' in state)) return null
  return state.reason === 'expired' || state.reason === 'unauthorized'
    ? 'Your session ended. Sign in again to continue.'
    : null
}

const signInErrorMessage = (error: unknown): string =>
  error instanceof AuthError
    ? error.message
    : 'Sign-in could not be completed. Check the local services and try again.'

export function SignInPage() {
  const { signIn } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const sessionNotice = sessionEndedMessage(location.state)
  const [submissionError, setSubmissionError] = useState<string | null>(null)
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<SignInValues>({
    resolver: zodResolver(signInSchema),
    defaultValues: {
      email: 'demo@trade-ledger.local',
      password: '',
    },
  })

  const submit = handleSubmit(async (values) => {
    setSubmissionError(null)
    try {
      await signIn(values)
      void navigate(safeReturnPath(location.state), { replace: true })
    } catch (error) {
      setSubmissionError(signInErrorMessage(error))
    }
  })

  return (
    <main className="flex min-h-screen flex-col bg-ground text-ink">
      <header className="border-b border-line px-6 py-5 sm:px-10">
        <span className="font-wordmark">Trade Ledger</span>
      </header>

      <div className="flex flex-1 items-center justify-center px-5 py-12 sm:px-8">
        <section className="w-full max-w-[430px]" aria-labelledby="sign-in-title">
          <p className="font-mono text-label font-medium uppercase text-ink-3">
            Local demo access
          </p>
          <h1 id="sign-in-title" className="mt-3 text-3xl font-semibold tracking-[-0.035em]">
            Sign in to the ledger
          </h1>
          <p className="mt-3 text-body leading-6 text-ink-3">
            Use the single demo user created by the local bootstrap. There is no registration flow.
          </p>

          <InlineNotice className="mt-7 leading-6" tone="neutral">
            The demo password stays in the ignored <code className="font-mono text-xs">.generated</code>{' '}
            environment file and is never bundled into this web app. Every authenticated user shares the
            same demonstration ledger.
          </InlineNotice>

          {sessionNotice ? (
            <InlineNotice className="mt-4" tone="warning">
              {sessionNotice}
            </InlineNotice>
          ) : null}

          <form className="mt-8 space-y-5" onSubmit={(event) => void submit(event)} noValidate>
            <Field
              id="email"
              type="email"
              label="Email"
              autoComplete="username"
              spellCheck={false}
              required
              error={errors.email?.message}
              {...register('email')}
            />

            <Field
              id="password"
              type="password"
              label="Password"
              autoComplete="current-password"
              required
              error={errors.password?.message}
              {...register('password')}
            />

            {submissionError ? (
              <InlineNotice tone="error">
                {submissionError}
              </InlineNotice>
            ) : null}

            <Button
              type="submit"
              className="w-full"
              isLoading={isSubmitting}
              loadingLabel="Signing in…"
            >
              Sign in
            </Button>
          </form>
        </section>
      </div>
    </main>
  )
}
