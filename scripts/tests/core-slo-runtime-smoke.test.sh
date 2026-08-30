#!/usr/bin/env bash
set -Eeuo pipefail

root_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd)"
fixture_curl="$root_dir/scripts/tests/core-slo-runtime-smoke.fixture-curl.sh"
evidence_file="$(mktemp "${TMPDIR:-/tmp}/inkflow-core-slo-test-evidence.XXXXXX.json")"
default_evidence_dir=""
default_stale_file=""

cleanup() {
  local status=$?
  set +e
  rm -f -- "$evidence_file"
  if [[ -n "$default_stale_file" ]]; then
    chmod u+w "$default_stale_file" 2>/dev/null || true
  fi
  if [[ -n "$default_evidence_dir" ]]; then
    rm -rf -- "$default_evidence_dir"
  fi
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

default_evidence_dir="$(mktemp -d "${TMPDIR:-/tmp}/inkflow-core-slo-default-test.XXXXXX")"
default_stale_file="$default_evidence_dir/inkflow-core-slo-evidence.json"
default_output="$default_evidence_dir/run-output.txt"

printf 'stale evidence\n' > "$default_stale_file"
chmod 444 "$default_stale_file"
TMPDIR="$default_evidence_dir" \
INKFLOW_SLO_CURL_BIN="$fixture_curl" \
INKFLOW_SLO_PROBE_COUNT=1 \
INKFLOW_SLO_CURL_MAX_TIME=3 \
  bash "$root_dir/scripts/core-slo-runtime-smoke.sh" http://fixture.invalid > "$default_output"

default_evidence_files=("$default_evidence_dir"/inkflow-core-slo-evidence.*.json)
test "${#default_evidence_files[@]}" = 1
test -s "${default_evidence_files[0]}"
grep -q '"evidenceSource": "ci-core-slo-runtime-smoke"' "${default_evidence_files[0]}"

chmod u+w "$default_stale_file"
rm -rf -- "$default_evidence_dir"
default_evidence_dir=""
default_stale_file=""

printf 'core-slo-runtime-smoke.test: PASS\n'
