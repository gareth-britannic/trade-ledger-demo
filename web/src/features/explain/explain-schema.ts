import { z } from 'zod'

export const maximumQuestionLength = 500

export const explainQuestionSchema = z.object({
  question: z
    .string()
    .trim()
    .min(1, 'Enter a question about the ledger.')
    .max(maximumQuestionLength, `Keep the question to ${maximumQuestionLength} characters or fewer.`),
})

export type ExplainQuestionInput = z.input<typeof explainQuestionSchema>
export type ExplainQuestionValues = z.output<typeof explainQuestionSchema>
