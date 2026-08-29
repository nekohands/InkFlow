#!/usr/bin/env bash
set -Eeuo pipefail

root_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd)"
fixture_curl="$root_dir/scripts/tests/core-slo-runtime-smoke.fixture-curl.sh"
evidence_file="$(mktemp "${TMPDIR:-/tmp}/inkflow-core-slo-test-evidence.XXXXXX.json")"

cleanup() {
  local status=$?
  set +e
  rm -f -- "$evidence_file"
  exit "$status"
}
trap cleanup EXIT

chmod +x "$fixture_curl"
INKFLOW_SLO_CURL_BIN="$fixture_curl" \
INKFLOW_SLO_PROBE_COUNT=3 \
INKFLOW_SLO_CURL_MAX_TIME=3 \
INKFLOW_SLO_EVIDENCE_FILE="$evidence_file" \
  bash "$root_dir/scripts/core-slo-runtime-smoke.sh" http://fixture.invalid

test -s "$evidence_file"
test "$(grep -o '"requestCount"' "$evidence_file" | wc -l | tr -d '[:space:]')" = 4
test "$(grep -o '"durationSampleCount"' "$evidence_file" | wc -l | tr -d '[:space:]')" = 4
test "$(grep -o '"p95LatencyMilliseconds"' "$evidence_file" | wc -l | tr -d '[:space:]')" = 4
grep -q '"evidenceSource": "ci-core-slo-runtime-smoke"' "$evidence_file"
grep -q '"public_api"' "$evidence_file"
grep -q '"legado_api"' "$evidence_file"
grep -q '"developer_api"' "$evidence_file"
grep -q '"reader"' "$evidence_file"

printf 'core-slo-runtime-smoke.test: PASS\n'
