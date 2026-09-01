# Local infrastructure

The local environment runs Postgres and LocalStack from the root
`docker-compose.yml`. Terraform in `infra/envs/local` creates:

- `trade-ledger-fills.fifo`, with content-based deduplication disabled so the
  producer must use the fill ID as `MessageDeduplicationId`.
- `trade-ledger-fills-dlq.fifo`, with a redrive policy after three receives.

The queue is defined in `infra/modules/fill-queue` so later AWS environments can
reuse the same queue semantics.

Postgres is available at `localhost:55432` (database/user/password:
`trade_ledger`). Override the host port with `TRADE_LEDGER_POSTGRES_PORT`; inside
the Compose network it remains `postgres:5432`.

## Start

From the repository root:

```bash
deploy/scripts/bootstrap-all.sh
```

This starts both containers, initializes and applies Terraform, then sends and
receives a smoke-test message using an explicit fill ID for deduplication.
Terraform state and Postgres data are local and ignored by Git/Docker.

## Stop

```bash
deploy/scripts/local-down.sh
```

To remove the Postgres data volume as well, run `docker compose down --volumes`.
