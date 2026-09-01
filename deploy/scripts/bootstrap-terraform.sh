#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
STACK_DIR="${REPO_ROOT}/infra/envs/local"
LOCALSTACK_ENDPOINT="http://localhost:4566"

for command_name in curl terraform; do
  if ! command -v "${command_name}" >/dev/null 2>&1; then
    echo "ERROR: ${command_name} is required but was not found." >&2
    exit 1
  fi
done

if ! curl -fsS "${LOCALSTACK_ENDPOINT}/_localstack/health" >/dev/null; then
  echo "ERROR: LocalStack is not ready at ${LOCALSTACK_ENDPOINT}." >&2
  echo "Run deploy/scripts/bootstrap-all.sh to start it first." >&2
  exit 1
fi

# Dummy credentials are mandatory for AWS clients but can only reach LocalStack
# because the Terraform provider overrides every service endpoint it uses.
export AWS_ACCESS_KEY_ID="test"
export AWS_SECRET_ACCESS_KEY="test"
export AWS_DEFAULT_REGION="eu-west-2"
export AWS_REGION="${AWS_DEFAULT_REGION}"
export AWS_ENDPOINT_URL="${LOCALSTACK_ENDPOINT}"
export AWS_EC2_METADATA_DISABLED="true"
export TF_VAR_aws_region="${AWS_REGION}"
export TF_VAR_localstack_endpoint="${LOCALSTACK_ENDPOINT}"

terraform -chdir="${STACK_DIR}" init
terraform -chdir="${STACK_DIR}" apply -auto-approve

echo
echo "LocalStack Terraform applied successfully."
echo "LocalStack endpoint: ${LOCALSTACK_ENDPOINT}"
