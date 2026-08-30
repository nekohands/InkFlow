#!/usr/bin/env bash
set -Eeuo pipefail

root_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd)"
script="$root_dir/scripts/private-library-runtime-smoke.sh"

bash -n "$script"
for required in \
  '/api/v1/me/private-library/import' \
  'Cache-Control' \
  'cross-user.json' \
  'private paragraph one' \
  'private-library-runtime-smoke: PASS'; do
  grep -Fq -- "$required" "$script"
done

printf 'private-library-runtime-smoke.test: PASS\n'
