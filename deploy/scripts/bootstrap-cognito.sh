#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
GENERATED_DIR="${REPO_ROOT}/.generated"
COGNITO_ENV_FILE="${GENERATED_DIR}/local-cognito.env"
COGNITO_ENDPOINT_HOST="http://localhost:9229"
COGNITO_ENDPOINT_DOCKER="http://cognito-local:9229"
COGNITO_REGION="eu-west-2"
COGNITO_POOL_NAME="trade-ledger-local"
COGNITO_CLIENT_NAME="trade-ledger-local-client"
DEMO_EMAIL="${TRADE_LEDGER_LOCAL_USER_EMAIL:-demo@trade-ledger.local}"
DEMO_PASSWORD="${TRADE_LEDGER_LOCAL_USER_PASSWORD:-TradeLedgerDemo123!}"

cognito_aws() {
  docker compose -f "${REPO_ROOT}/docker-compose.yml" exec -T localstack \
    aws --endpoint-url "${COGNITO_ENDPOINT_DOCKER}" --region "${COGNITO_REGION}" "$@"
}

pool_id="$(
  cognito_aws cognito-idp list-user-pools \
    --max-results 60 \
    --query "UserPools[?Name=='${COGNITO_POOL_NAME}'].Id | [0]" \
    --output text 2>/dev/null || true
)"

if [[ -z "${pool_id}" || "${pool_id}" == "None" ]]; then
  pool_id="$(
    cognito_aws cognito-idp create-user-pool \
      --pool-name "${COGNITO_POOL_NAME}" \
      --auto-verified-attributes email \
      --username-attributes email \
      --query "UserPool.Id" \
      --output text
  )"
fi

client_id="$(
  cognito_aws cognito-idp list-user-pool-clients \
    --user-pool-id "${pool_id}" \
    --max-results 60 \
    --query "UserPoolClients[?ClientName=='${COGNITO_CLIENT_NAME}'].ClientId | [0]" \
    --output text 2>/dev/null || true
)"

if [[ -z "${client_id}" || "${client_id}" == "None" ]]; then
  client_id="$(
    cognito_aws cognito-idp create-user-pool-client \
      --user-pool-id "${pool_id}" \
      --client-name "${COGNITO_CLIENT_NAME}" \
      --no-generate-secret \
      --explicit-auth-flows ALLOW_USER_PASSWORD_AUTH ALLOW_REFRESH_TOKEN_AUTH \
      --query "UserPoolClient.ClientId" \
      --output text
  )"
fi

if ! cognito_aws cognito-idp admin-get-user \
  --user-pool-id "${pool_id}" \
  --username "${DEMO_EMAIL}" >/dev/null 2>&1; then
  cognito_aws cognito-idp sign-up \
    --client-id "${client_id}" \
    --username "${DEMO_EMAIL}" \
    --password "${DEMO_PASSWORD}" \
    --user-attributes Name=email,Value="${DEMO_EMAIL}" >/dev/null
  cognito_aws cognito-idp admin-confirm-sign-up \
    --user-pool-id "${pool_id}" \
    --username "${DEMO_EMAIL}" >/dev/null
fi

cognito_aws cognito-idp admin-set-user-password \
  --user-pool-id "${pool_id}" \
  --username "${DEMO_EMAIL}" \
  --password "${DEMO_PASSWORD}" \
  --permanent >/dev/null

mkdir -p "${GENERATED_DIR}"
{
  printf 'export Authentication__Cognito__Authority=%q\n' "${COGNITO_ENDPOINT_HOST}/${pool_id}"
  printf 'export Authentication__Cognito__Audience=%q\n' "${client_id}"
  printf 'export TRADE_LEDGER_LOCAL_COGNITO_POOL_ID=%q\n' "${pool_id}"
  printf 'export TRADE_LEDGER_LOCAL_COGNITO_CLIENT_ID=%q\n' "${client_id}"
  printf 'export TRADE_LEDGER_LOCAL_USER_EMAIL=%q\n' "${DEMO_EMAIL}"
  printf 'export TRADE_LEDGER_LOCAL_USER_PASSWORD=%q\n' "${DEMO_PASSWORD}"
} > "${COGNITO_ENV_FILE}"

echo "Local Cognito ready."
echo "  User pool: ${pool_id}"
echo "  App client: ${client_id}"
echo "  Registered user: ${DEMO_EMAIL}"
echo "  Environment: ${COGNITO_ENV_FILE}"
