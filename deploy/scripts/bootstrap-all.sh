#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"

for command_name in aws curl docker terraform; do
  if ! command -v "${command_name}" >/dev/null 2>&1; then
    echo "ERROR: ${command_name} is required but was not found." >&2
    exit 1
  fi
done

echo "Starting LocalStack and Postgres..."
docker compose -f "${REPO_ROOT}/docker-compose.yml" up -d --wait

"${SCRIPT_DIR}/migrate-local-database.sh"
"${SCRIPT_DIR}/package-processor.sh"
"${SCRIPT_DIR}/bootstrap-terraform.sh"
