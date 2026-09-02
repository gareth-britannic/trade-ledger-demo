# Trade Ledger API

The API is the synchronous edge of the ledger. It validates fill commands, publishes a typed
`FillRequestMessage` through `ISqsClient`, and returns `202 Accepted` only after SQS accepts the
message. The SQS-triggered processor is the only host that persists fills and applies them to the
ledger. Database code lives in `TradeLedger.Database`; shared logging, correlation, and SQS code
lives in `TradeLedger.Common`.

## Authentication scope

`/api/fills`, `/api/positions`, position-lot queries, and `/api/explain` require a signed Cognito
access token. `/health` is anonymous. Validation covers the issuer, signing key, expiry,
`token_use=access`, and the expected Cognito app-client `client_id`.

This is intentionally one authenticated shared ledger. Local bootstrap registers one demo user,
but there is no application user table, tenant model, self-service registration UI, or row-level
ownership. Authentication demonstrates the identity and token boundary; it does not claim
multi-user data isolation.

## Configuration

Set configuration with the standard .NET double-underscore environment-variable convention:

| Variable | Purpose |
| --- | --- |
| `Database__ConnectionString` | Optional complete PostgreSQL connection string |
| `Database__Host`, `Database__Name` | PostgreSQL endpoint and database when not using a complete connection string |
| `Database__Username`, `Database__Password` | PostgreSQL credentials when not using a complete connection string |
| `FillQueue__Url` | Full FIFO queue URL (required) |
| `Authentication__Cognito__Authority` | Cognito user-pool issuer/authority (required; HTTPS except for loopback development) |
| `Authentication__Cognito__Audience` | Expected Cognito app-client ID (required; the historical key name is retained although access tokens carry this value in `client_id`) |
| `AWS_REGION` or `AWS_DEFAULT_REGION` | AWS SDK region resolution outside Development |

The AWS SDK uses its standard credential chain. LocalStack's service URL and test credentials exist
only in `appsettings.Development.json`; the queue URL remains configuration supplied by Terraform.

## Local run

From the repository root:

```bash
deploy/scripts/bootstrap-all.sh
source .generated/local-cognito.env
export FillQueue__Url="$(terraform -chdir=infra/envs/local output -raw fill_queue_url)"
dotnet run --project src/TradeLedger.Api --launch-profile http
```

In another terminal, authenticate the registered local demo user and call a protected endpoint:

```bash
TOKEN="$(deploy/scripts/get-local-token.sh)"
curl -fsS -H "Authorization: Bearer $TOKEN" http://localhost:5232/api/positions
```

The bootstrap credentials are `demo@trade-ledger.local` / `TradeLedgerDemo123!`; they are local-only
development values. Swagger UI is available at `/swagger` only in Development. Select **Authorize**
and paste the access token to call protected operations. `/health` reports only aggregate PostgreSQL
health and does not require a token.

## Position source of truth and reliability

Positions are derived from persisted open `lots` plus dated `realised_pnl_entries`. The asynchronous
FIFO processor persists first-seen fills, replays each symbol in execution-time order, replaces the
symbol's open-lot state, records realised P&L for processed sells, and marks fills with
`processed_at`, all within one transaction protected by a per-symbol advisory lock. The P&L entry
timestamp is the fill's UTC execution time, so period queries such as "this month" are accurate.
`processed_at` makes matching SQS redelivery a no-op.

The Lambda entry point delegates the SQS event directly to `SqsMessageHandler<FillRequestMessage>`.
The handler owns timeout handling, structured logs, per-message correlation and DI scopes, JSON
deserialization, FIFO group blocking, and partial-batch failures. It then calls `FillMessageHandler`,
which validates the message and invokes the application processor.

SQS is the acceptance boundary. The API performs one durable side effect—queue publication—and the
processor owns both persistence and ledger application. This avoids a database/queue dual write and
does not require a transactional outbox. The trade-off is eventual consistency: an accepted fill is
not queryable until the processor consumes it.

Current observability is structured compact-JSON logs, correlation IDs, PostgreSQL health, and
native CloudWatch/SQS infrastructure metrics. The AWS Terraform declares log groups, ECS Container
Insights, and WAF metrics, but custom application metrics, alarms, dashboards, and distributed
tracing are not yet implemented.
