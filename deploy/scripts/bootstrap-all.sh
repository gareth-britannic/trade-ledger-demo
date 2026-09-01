#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"

for command_name in curl docker terraform; do
  if ! command -v "${command_name}" >/dev/null 2>&1; then
    echo "ERROR: ${command_name} is required but was not found." >&2
    exit 1
  fi
done

echo "Starting the LocalStack services used by Terraform..."
docker compose -f "${REPO_ROOT}/docker-compose.Local.yml" up -d --wait localstack

"${SCRIPT_DIR}/bootstrap-terraform.sh"
