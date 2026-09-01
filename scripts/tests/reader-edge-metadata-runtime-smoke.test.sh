#!/usr/bin/env bash
set -Eeuo pipefail

root_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd)"
fixture_curl="$root_dir/scripts/tests/reader-edge-metadata-runtime-smoke.fixture-curl.sh"

chmod +x "$root_dir/scripts/reader-edge-metadata-runtime-smoke.sh" "$fixture_curl"
output="$(
  INKFLOW_READER_EDGE_SMOKE_CURL_BIN="$fixture_curl" \
    bash "$root_dir/scripts/reader-edge-metadata-runtime-smoke.sh" \
      http://fixture.invalid \
      33333333-3333-4333-8333-333333333333
  )" 2>&1

grep -Fq 'reader-edge-metadata-runtime-smoke: PASS' <<< "$output"
printf 'reader-edge-metadata-runtime-smoke.test: PASS\n'
