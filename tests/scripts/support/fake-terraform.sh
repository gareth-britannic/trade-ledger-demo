#!/usr/bin/env bash
set -euo pipefail

: "${FAKE_TERRAFORM_STATE_FILE:?FAKE_TERRAFORM_STATE_FILE is required}"
: "${FAKE_TERRAFORM_ARGUMENT_FILE:?FAKE_TERRAFORM_ARGUMENT_FILE is required}"

invocation_count=0
if [[ -f "${FAKE_TERRAFORM_STATE_FILE}" ]]; then
  invocation_count="$(<"${FAKE_TERRAFORM_STATE_FILE}")"
fi
invocation_count=$((invocation_count + 1))
printf '%s\n' "${invocation_count}" > "${FAKE_TERRAFORM_STATE_FILE}"
printf '%s\n' "$*" >> "${FAKE_TERRAFORM_ARGUMENT_FILE}"

if (( invocation_count <= ${FAKE_TERRAFORM_FAILURES_BEFORE_SUCCESS:-0} )); then
  exit "${FAKE_TERRAFORM_FAILURE_STATUS:-42}"
fi
