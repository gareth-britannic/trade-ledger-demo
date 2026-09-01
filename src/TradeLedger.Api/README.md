# Trade Ledger API

The API uses a controller → application service → repository/publisher flow. Controllers bind HTTP
contracts, call one service method, and map application results to explicit response contracts.
Application owns the models, business rules, interfaces, and use cases; EF Core and AWS types are
confined to Infrastructure.

## Configuration

Set configuration with the standard .NET double-underscore environment-variable convention:

| Variable | Purpose |
| --- | --- |
| `Database__ConnectionString` | Optional complete PostgreSQL connection string |
| `Database__Host`, `Database__Name` | PostgreSQL endpoint and database when not using a complete connection string |
| `Database__Username`, `Database__Password` | PostgreSQL credentials when not using a complete connection string |
| `FillQueue__Url` | Full FIFO queue URL (required) |
| `FillProcessing__Enabled` | Enables SQS polling and asynchronous FIFO processing (defaults to `true`) |
| `Authentication__Cognito__Authority` | HTTPS Cognito user-pool authority (required) |
| `Authentication__Cognito__Audience` | Cognito app-client audience (required) |
| `AWS_REGION` or `AWS_DEFAULT_REGION` | AWS SDK region resolution outside Development |

The AWS SDK uses its standard credential chain. LocalStack's service URL and test credentials exist
only in `appsettings.Development.json`; the queue URL remains configuration supplied by Terraform.

## Local run and migrations

Start PostgreSQL and LocalStack, provision the local queue, then export its URL and Cognito test
configuration:

```bash
deploy/scripts/bootstrap-all.sh
dotnet tool restore
export FillQueue__Url="$(terraform -chdir=infra/envs/local output -raw fill_queue_url)"
export Authentication__Cognito__Authority="https://cognito-idp.REGION.amazonaws.com/USER_POOL_ID"
export Authentication__Cognito__Audience="COGNITO_APP_CLIENT_ID"
ASPNETCORE_ENVIRONMENT=Development dotnet ef database update --project src/TradeLedger.Infrastructure --startup-project src/TradeLedger.Api
dotnet run --project src/TradeLedger.Api
```

Swagger UI is available at `/swagger` only in Development. `/health` is anonymous and reports only
the aggregate PostgreSQL health status.

## Position source of truth and reliability

Positions are derived from persisted open `lots` plus dated `realised_pnl_entries`. The asynchronous
FIFO processor consumes accepted fills from SQS, replaces the symbol's open-lot state, records one
realised P&L entry for each processed sell, and marks the fill with `processed_at`. The timestamp on
the P&L entry is the fill's UTC execution time, so period queries such as "this month" are accurate.
`processed_at` makes repeated SQS delivery idempotent.

The processing service lives in Application. `SqsFillProcessor` is only its current host, so the same
use case can move to Lambda later without moving FIFO behaviour into AWS-specific code.

Fill acceptance deliberately persists before publishing. This required sequence is not atomic: a
queue failure can leave a persisted fill unpublished. Unique fill IDs and FIFO deduplication make
retries safer, but the recommended follow-up is a complete transactional outbox with an atomic
fill/outbox write, observable dispatcher, retry/backoff policy, delivery semantics, and cleanup.

Current observability is structured compact-JSON logs, correlation IDs, PostgreSQL health, and native
CloudWatch/SQS infrastructure metrics. Custom application metrics and distributed tracing are not yet
implemented.
