#!/usr/bin/env bash
set -Eeuo pipefail

root_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd)"
script="$root_dir/scripts/legado-runtime-smoke.sh"
fixture_curl="$root_dir/scripts/tests/legado-runtime-smoke.fixture-curl.sh"

chmod +x "$fixture_curl"
output="$(
  INKFLOW_LEGADO_RUNTIME_SMOKE_CURL_BIN="$fixture_curl" \
  INKFLOW_LEGADO_RUNTIME_SMOKE_BOOK_ID=11111111-1111-4111-8111-111111111111 \
  INKFLOW_LEGADO_RUNTIME_SMOKE_CHAPTER_ID=22222222-2222-4222-8222-222222222222 \
    bash "$script" http://fixture.invalid
)"

grep -Fq 'legado-runtime-smoke: PASS' <<< "$output"
printf 'legado-runtime-smoke.test: PASS\n'
