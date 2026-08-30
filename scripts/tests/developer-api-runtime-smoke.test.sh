#!/usr/bin/env bash
set -Eeuo pipefail

root_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd)"
script="$root_dir/scripts/developer-api-runtime-smoke.sh"

bash -n "$script"
for required in \
  '/api/v1/me/developer-applications/' \
  '/api/developer/v1/search?q=' \
  'X-InkFlow-Api-Key' \
  'private,[[:space:]]*no-store' \
  'has("apiKey")' \
  'rotate' \
  'developer-api-runtime-smoke: PASS'; do
  grep -Fq -- "$required" "$script"
done

printf 'developer-api-runtime-smoke.test: PASS\n'
