#!/usr/bin/env bash
set -Eeuo pipefail

root_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd)"
script="$root_dir/scripts/quality-failure-runtime-smoke.sh"
fixture_curl="$root_dir/scripts/tests/quality-failure-runtime-smoke.fixture-curl.sh"
state_dir="$(mktemp -d "${TMPDIR:-/tmp}/inkflow-quality-failure-fixture-test.XXXXXX")"
trap 'rm -rf -- "$state_dir"' EXIT

chmod +x "$script" "$fixture_curl"

output="$(
  INKFLOW_QUALITY_FAILURE_RUNTIME_SMOKE_CURL_BIN="$fixture_curl" \
  INKFLOW_QUALITY_FAILURE_RUNTIME_SMOKE_BOOK_ID=11111111-1111-4111-8111-111111111111 \
  INKFLOW_QUALITY_FAILURE_RUNTIME_SMOKE_CHAPTER_ID=22222222-2222-4222-8222-222222222222 \
  INKFLOW_QUALITY_FAILURE_RUNTIME_SMOKE_SOURCE_ID=inkflow-quality-a \
  INKFLOW_QUALITY_FAILURE_RUNTIME_SMOKE_GOOD_MARKER='InkFlow quality fixture good marker' \
  INKFLOW_QUALITY_FAILURE_RUNTIME_SMOKE_LOW_MARKER='InkFlow quality fixture truncated marker' \
  bash "$script" http://fixture.invalid
)"

grep -Fq 'quality-failure-runtime-smoke: PASS' <<< "$output"
printf 'quality-failure-runtime-smoke.test: PASS\n'
