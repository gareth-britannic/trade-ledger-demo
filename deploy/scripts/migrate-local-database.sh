#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
POSTGRES_PORT="${TRADE_LEDGER_POSTGRES_PORT:-55432}"

for command_name in dotnet docker; do
  if ! command -v "${command_name}" >/dev/null 2>&1; then
    echo "ERROR: ${command_name} is required but was not found." >&2
    exit 1
  fi
done

if ! docker compose -f "${REPO_ROOT}/docker-compose.yml" exec -T postgres \
  pg_isready -U trade_ledger -d trade_ledger >/dev/null; then
  echo "ERROR: local Postgres is not ready." >&2
  exit 1
fi

Database__ConnectionString="Host=localhost;Port=${POSTGRES_PORT};Database=trade_ledger;Username=trade_ledger;Password=trade_ledger" \
  dotnet ef database update \
  --project "${REPO_ROOT}/src/TradeLedger.Database/TradeLedger.Database.csproj" \
  --startup-project "${REPO_ROOT}/src/TradeLedger.Api/TradeLedger.Api.csproj"

echo "Local Trade Ledger database migrations applied."
