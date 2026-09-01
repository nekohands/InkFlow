#!/usr/bin/env bash
set -Eeuo pipefail

root_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd)"
script="$root_dir/scripts/reader-navigation-runtime-smoke.sh"
fixture_curl="$root_dir/scripts/tests/reader-navigation-runtime-smoke.fixture-curl.sh"

chmod +x "$script" "$fixture_curl"
output="$({
  INKFLOW_READER_NAVIGATION_SMOKE_CURL_BIN="$fixture_curl" \
    bash "$script" \
      http://fixture.invalid \
      11111111-1111-4111-8111-111111111111 \
      22222222-2222-4222-8222-222222222222
} 2>&1)"

grep -Fq 'reader-navigation-runtime-smoke: PASS' <<< "$output"
printf 'reader-navigation-runtime-smoke.test: PASS\n'
