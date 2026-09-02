# Processor Lambda module

This module is used only by `infra/envs/local`. It deploys the self-contained
.NET 10 ARM64 `bootstrap` executable on Lambda's `provided.al2023` custom runtime,
adds queue-scoped receive permissions, and connects the existing FIFO fill
queue with `ReportBatchItemFailures` enabled.

The package must be built first with `deploy/scripts/package-processor.sh`.
No production AWS environment consumes this module in this ticket.
