#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
COGNITO_ENV_FILE="${REPO_ROOT}/.generated/local-cognito.env"
COGNITO_ENDPOINT_DOCKER="http://cognito-local:9229"
COGNITO_REGION="eu-west-2"

if [[ ! -f "${COGNITO_ENV_FILE}" ]]; then
  echo "ERROR: ${COGNITO_ENV_FILE} does not exist. Run deploy/scripts/bootstrap-all.sh first." >&2
  exit 1
fi

# shellcheck disable=SC1090
source "${COGNITO_ENV_FILE}"

docker compose -f "${REPO_ROOT}/docker-compose.yml" exec -T localstack \
  aws --endpoint-url "${COGNITO_ENDPOINT_DOCKER}" --region "${COGNITO_REGION}" \
    cognito-idp initiate-auth \
    --auth-flow USER_PASSWORD_AUTH \
    --client-id "${TRADE_LEDGER_LOCAL_COGNITO_CLIENT_ID}" \
    --auth-parameters \
      USERNAME="${TRADE_LEDGER_LOCAL_USER_EMAIL}",PASSWORD="${TRADE_LEDGER_LOCAL_USER_PASSWORD}" \
    --query "AuthenticationResult.AccessToken" \
    --output text
