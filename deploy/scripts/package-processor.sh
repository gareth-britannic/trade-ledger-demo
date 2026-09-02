#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
ARTIFACT_DIR="${REPO_ROOT}/artifacts"
ARTIFACT_PATH="${ARTIFACT_DIR}/trade-ledger-processor.zip"
PUBLISH_ROOT="$(mktemp -d)"
trap 'rm -rf "${PUBLISH_ROOT}"' EXIT

for command_name in dotnet zip; do
  if ! command -v "${command_name}" >/dev/null 2>&1; then
    echo "ERROR: ${command_name} is required but was not found." >&2
    exit 1
  fi
done

mkdir -p "${ARTIFACT_DIR}"

dotnet publish "${REPO_ROOT}/src/TradeLedger.Processor/TradeLedger.Processor.csproj" \
  --configuration Release \
  --runtime linux-arm64 \
  --self-contained true \
  --output "${PUBLISH_ROOT}/publish" \
  -p:PublishSingleFile=true \
  -p:DebugType=None \
  -p:DebugSymbols=false

if [[ ! -x "${PUBLISH_ROOT}/publish/bootstrap" ]]; then
  echo "ERROR: publish did not produce an executable custom-runtime bootstrap." >&2
  exit 1
fi

(
  cd "${PUBLISH_ROOT}/publish"
  zip -q -r "${ARTIFACT_PATH}" .
)

echo "Packaged .NET 10 Lambda: ${ARTIFACT_PATH}"
