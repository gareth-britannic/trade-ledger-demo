#!/usr/bin/env bash
set -euo pipefail

if [[ $# -lt 1 ]]; then
  echo "ERROR: usage: $0 <terraform-directory> [init-options...]" >&2
  exit 64
fi

STACK_DIR="$1"
shift

if [[ ! -d "${STACK_DIR}" ]]; then
  echo "ERROR: Terraform directory does not exist: ${STACK_DIR}" >&2
  exit 66
fi

TERRAFORM_BIN="${TERRAFORM_BIN:-terraform}"
MAX_ATTEMPTS="${TERRAFORM_INIT_MAX_ATTEMPTS:-3}"
RETRY_DELAY_SECONDS="${TERRAFORM_INIT_RETRY_DELAY_SECONDS:-5}"

if ! command -v "${TERRAFORM_BIN}" >/dev/null 2>&1; then
  echo "ERROR: Terraform executable was not found: ${TERRAFORM_BIN}" >&2
  exit 69
fi

if [[ ! "${MAX_ATTEMPTS}" =~ ^[1-9][0-9]*$ ]]; then
  echo "ERROR: TERRAFORM_INIT_MAX_ATTEMPTS must be a positive integer." >&2
  exit 64
fi

if [[ ! "${RETRY_DELAY_SECONDS}" =~ ^[0-9]+$ ]]; then
  echo "ERROR: TERRAFORM_INIT_RETRY_DELAY_SECONDS must be a non-negative integer." >&2
  exit 64
fi

if [[ -n "${TF_PLUGIN_CACHE_DIR:-}" ]]; then
  mkdir -p "${TF_PLUGIN_CACHE_DIR}"
fi

attempt=1
# Retry only the read-only initialization/download boundary. Apply remains a
# single explicit operation and is never hidden behind this helper.
while (( attempt <= MAX_ATTEMPTS )); do
  if "${TERRAFORM_BIN}" "-chdir=${STACK_DIR}" init "$@"; then
    exit 0
  else
    status=$?
  fi

  if (( attempt == MAX_ATTEMPTS )); then
    echo "ERROR: terraform init failed after ${MAX_ATTEMPTS} attempts." >&2
    exit "${status}"
  fi

  echo "WARNING: terraform init attempt ${attempt}/${MAX_ATTEMPTS} failed; retrying in ${RETRY_DELAY_SECONDS}s." >&2
  sleep "${RETRY_DELAY_SECONDS}"
  ((attempt += 1))
done
