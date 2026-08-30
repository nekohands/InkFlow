#!/usr/bin/env bash
set -Eeuo pipefail

root_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd)"
fixture_curl="$root_dir/scripts/tests/reader-frontend-runtime-smoke.fixture-curl.sh"

chmod +x "$root_dir/scripts/reader-frontend-runtime-smoke.sh" "$fixture_curl"
output="$({
  INKFLOW_FRONTEND_SMOKE_CURL_BIN="$fixture_curl" \
    bash "$root_dir/scripts/reader-frontend-runtime-smoke.sh" http://fixture.invalid
} 2>&1)"

grep -Fq 'reader-frontend-runtime-smoke: PASS' <<< "$output"
printf 'reader-frontend-runtime-smoke.test: PASS\n'
