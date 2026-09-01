#!/usr/bin/env bash
set -euo pipefail

LOCALSTACK_ENDPOINT="${LOCALSTACK_ENDPOINT:-http://localhost:4566}"
AWS_REGION="${AWS_REGION:-eu-west-2}"
QUEUE_NAME="trade-ledger-fills.fifo"
FILL_ID="local-smoke-$(date +%s)-$$"
MESSAGE_BODY="{\"fillId\":\"${FILL_ID}\",\"source\":\"local-smoke-test\"}"

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
  --queue-name "${QUEUE_NAME}" \
  --query QueueUrl \
  --output text)"

aws --endpoint-url "${LOCALSTACK_ENDPOINT}" sqs send-message \
  --queue-url "${QUEUE_URL}" \
  --message-body "${MESSAGE_BODY}" \
  --message-group-id "${FILL_ID}" \
  --message-deduplication-id "${FILL_ID}" \
  >/dev/null

RECEIVED_MESSAGE="$(aws --endpoint-url "${LOCALSTACK_ENDPOINT}" sqs receive-message \
  --queue-url "${QUEUE_URL}" \
  --wait-time-seconds 5 \
  --max-number-of-messages 10 \
  --attribute-names MessageDeduplicationId \
  --query "Messages[?Attributes.MessageDeduplicationId=='${FILL_ID}'] | [0].[Body,ReceiptHandle]" \
  --output text)"

IFS=$'\t' read -r RECEIVED_BODY RECEIPT_HANDLE <<<"${RECEIVED_MESSAGE}"

if [[ "${RECEIVED_BODY}" != "${MESSAGE_BODY}" ]]; then
  echo "ERROR: SQS round trip failed. Expected ${MESSAGE_BODY}, received ${RECEIVED_BODY}." >&2
  exit 1
fi

aws --endpoint-url "${LOCALSTACK_ENDPOINT}" sqs delete-message \
  --queue-url "${QUEUE_URL}" \
  --receipt-handle "${RECEIPT_HANDLE}"

echo "SQS round trip succeeded for fillId ${FILL_ID}."
