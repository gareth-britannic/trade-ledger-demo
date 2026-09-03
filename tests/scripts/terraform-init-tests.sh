#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
HELPER="${REPO_ROOT}/deploy/scripts/terraform-init.sh"
FAKE_TERRAFORM="${SCRIPT_DIR}/support/fake-terraform.sh"
TEST_ROOT="$(mktemp -d)"
trap 'rm -rf -- "${TEST_ROOT}"' EXIT

fail() {
  echo "FAIL: $1" >&2
  exit 1
}

assert_equals() {
  local expected="$1"
  local actual="$2"
  local message="$3"
  [[ "${actual}" == "${expected}" ]] || fail "${message}: expected '${expected}', got '${actual}'"
}

STACK_DIR="${TEST_ROOT}/stack"
mkdir -p "${STACK_DIR}"

export TERRAFORM_BIN="${FAKE_TERRAFORM}"
export TERRAFORM_INIT_RETRY_DELAY_SECONDS=0
export TF_PLUGIN_CACHE_DIR="${TEST_ROOT}/plugin-cache"
export FAKE_TERRAFORM_STATE_FILE="${TEST_ROOT}/success-state"
export FAKE_TERRAFORM_ARGUMENT_FILE="${TEST_ROOT}/success-arguments"
export FAKE_TERRAFORM_FAILURES_BEFORE_SUCCESS=1

"${HELPER}" "${STACK_DIR}" -backend=false -input=false -lockfile=readonly

assert_equals "2" "$(<"${FAKE_TERRAFORM_STATE_FILE}")" "transient failure retry count"
assert_equals \
  "-chdir=${STACK_DIR} init -backend=false -input=false -lockfile=readonly" \
  "$(head -n 1 "${FAKE_TERRAFORM_ARGUMENT_FILE}")" \
  "terraform arguments"
[[ -d "${TF_PLUGIN_CACHE_DIR}" ]] || fail "plugin cache directory was not created"

export FAKE_TERRAFORM_STATE_FILE="${TEST_ROOT}/failure-state"
export FAKE_TERRAFORM_ARGUMENT_FILE="${TEST_ROOT}/failure-arguments"
export FAKE_TERRAFORM_FAILURES_BEFORE_SUCCESS=10
export TERRAFORM_INIT_MAX_ATTEMPTS=3

set +e
"${HELPER}" "${STACK_DIR}" -backend=false >/dev/null 2>&1
status=$?
set -e

assert_equals "42" "${status}" "final Terraform exit status"
assert_equals "3" "$(<"${FAKE_TERRAFORM_STATE_FILE}")" "maximum retry count"

echo "Terraform init retry helper tests passed."
