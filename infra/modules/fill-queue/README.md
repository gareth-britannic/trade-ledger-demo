# Fill queue module

Creates the Trade Ledger FIFO fill queue and its FIFO dead-letter queue. The
source queue is redriven to the DLQ after a configurable number of receives.

Content-based deduplication is deliberately disabled. Producers must provide
the fill ID as `MessageDeduplicationId` on every `SendMessage` request.
Both queues use cost-free SQS-managed server-side encryption rather than a
customer-managed KMS key.

Provider configuration belongs to the calling root module so this module can be
used with either AWS or LocalStack.
