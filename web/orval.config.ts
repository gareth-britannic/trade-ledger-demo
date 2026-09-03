import { defineConfig } from 'orval'

export default defineConfig({
  tradeLedger: {
    input: './openapi/trade-ledger.v1.json',
    output: {
      mode: 'tags-split',
      target: './src/api/generated/endpoints.ts',
      schemas: './src/api/generated/model',
      client: 'react-query',
      httpClient: 'fetch',
      clean: true,
      override: {
        mutator: {
          path: './src/api/http/api-fetch.ts',
          name: 'apiFetch',
        },
        query: {
          signal: true,
        },
      },
    },
  },
})
