#!/usr/bin/env bash
set -Eeuo pipefail

root_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd)"
script="$root_dir/scripts/source-failover-runtime-smoke.sh"
fixture_curl="$root_dir/scripts/tests/source-failover-runtime-smoke.fixture-curl.sh"
state_dir="$(mktemp -d "${TMPDIR:-/tmp}/inkflow-failover-fixture-test.XXXXXX")"
state_file="$state_dir/state"

cleanup() {
  rm -rf -- "$state_dir"
}
trap cleanup EXIT

chmod +x "$script" "$fixture_curl"
output="$(
  INKFLOW_FAILOVER_FIXTURE_STATE_FILE="$state_file" \
  INKFLOW_FAILOVER_RUNTIME_SMOKE_CURL_BIN="$fixture_curl" \
  INKFLOW_FAILOVER_RUNTIME_SMOKE_ADMIN_TOKEN=fixture-admin-token \
  INKFLOW_FAILOVER_RUNTIME_SMOKE_BOOK_ID=11111111-1111-4111-8111-111111111111 \
  INKFLOW_FAILOVER_RUNTIME_SMOKE_CHAPTER_ID=22222222-2222-4222-8222-222222222222 \
    bash "$script" http://fixture.invalid
)"

grep -Fq 'source-failover-runtime-smoke: PASS' <<< "$output"
printf 'source-failover-runtime-smoke.test: PASS\n'
