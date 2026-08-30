#!/usr/bin/env bash
set -Eeuo pipefail

root_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd)"
script="$root_dir/scripts/reader-content-runtime-smoke.sh"
fixture_curl="$root_dir/scripts/tests/reader-content-runtime-smoke.fixture-curl.sh"

chmod +x "$script" "$fixture_curl"
output="$(
  INKFLOW_READER_CONTENT_SMOKE_CURL_BIN="$fixture_curl" \
    bash "$script" http://fixture.invalid 11111111-1111-4111-8111-111111111111
  )"

grep -Fq 'reader-content-runtime-smoke: PASS' <<< "$output"
printf 'reader-content-runtime-smoke.test: PASS\n'
