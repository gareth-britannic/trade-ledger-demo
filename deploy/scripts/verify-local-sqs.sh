#!/usr/bin/env bash
set -euo pipefail

LOCALSTACK_ENDPOINT="${LOCALSTACK_ENDPOINT:-http://localhost:4566}"
AWS_REGION="${AWS_REGION:-eu-west-2}"
QUEUE_NAME="trade-ledger-fills.fifo"
FUNCTION_NAME="trade-ledger-processor"

for command_name in aws curl; do
  if ! command -v "${command_name}" >/dev/null 2>&1; then
    echo "ERROR: ${command_name} is required but was not found." >&2
    exit 1
  fi
done

if ! curl -fsS "${LOCALSTACK_ENDPOINT}/_localstack/health" >/dev/null; then
  echo "ERROR: LocalStack is not ready at ${LOCALSTACK_ENDPOINT}." >&2
  exit 1
fi

export AWS_ACCESS_KEY_ID="test"
export AWS_SECRET_ACCESS_KEY="test"
export AWS_DEFAULT_REGION="${AWS_REGION}"
export AWS_EC2_METADATA_DISABLED="true"

QUEUE_URL="$(aws --endpoint-url "${LOCALSTACK_ENDPOINT}" sqs get-queue-url \
  --queue-name "${QUEUE_NAME}" --query QueueUrl --output text)"
ARCHITECTURE="$(aws --endpoint-url "${LOCALSTACK_ENDPOINT}" lambda get-function-configuration \
  --function-name "${FUNCTION_NAME}" --query 'Architectures[0]' --output text)"
MAPPING_STATE="$(aws --endpoint-url "${LOCALSTACK_ENDPOINT}" lambda list-event-source-mappings \
  --function-name "${FUNCTION_NAME}" --query 'EventSourceMappings[0].State' --output text)"

if [[ -z "${QUEUE_URL}" || "${ARCHITECTURE}" != "arm64" || "${MAPPING_STATE}" != "Enabled" ]]; then
  echo "ERROR: expected queue, ARM64 Lambda, and enabled event source mapping." >&2
  exit 1
fi

echo "Local SQS-to-Lambda wiring is ready."
