#!/usr/bin/env bash
set -Eeuo pipefail

if [[ "${INKFLOW_LIVE_TESTS:-}" != "1" ]]; then
  echo "INKFLOW_LIVE_TESTS=1 is required for the read-only live-source acceptance." >&2
  exit 2
fi

project="tests/InkFlow.IntegrationTests/InkFlow.IntegrationTests.csproj"
if [[ $# -gt 0 ]]; then
  echo "usage: INKFLOW_LIVE_TESTS=1 $0" >&2
  exit 2
fi

dotnet test "$project" \
  --configuration Release \
  --verbosity normal \
  --filter 'FullyQualifiedName~KanunuSourceAdapterLiveTests|FullyQualifiedName~EndToEndDataFlowTests'

echo "kanunu-live-acceptance: PASS (adapter, scheduler, worker, fetch artifact, content publish, public query)"
