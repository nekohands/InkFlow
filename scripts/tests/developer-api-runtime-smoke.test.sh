#!/usr/bin/env bash
set -Eeuo pipefail

root_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd)"
script="$root_dir/scripts/developer-api-runtime-smoke.sh"
disable_user_helper="$root_dir/scripts/disable-acceptance-user.sh"

bash -n "$script"
bash -n "$disable_user_helper"
for required in \
  '/api/v1/me/developer-applications/' \
  '/api/developer/v1/search?q=' \
  'X-InkFlow-Api-Key' \
  'private,[[:space:]]*no-store' \
  'has("apiKey")' \
  'rotate' \
  'quota_exceeded' \
  'Retry-After' \
  'cross-account' \
  'disabled-user' \
  'disable_user_helper' \
  'developer-api-runtime-smoke: PASS'; do
  grep -Fq -- "$required" "$script"
done

grep -Fq -- 'acceptance-fixtures' "$disable_user_helper"
grep -Fq -- 'disable-user' "$disable_user_helper"
grep -Fq -- 'INKFLOW_ACCEPTANCE_FIXTURE_RUNNER' "$disable_user_helper"

printf 'developer-api-runtime-smoke.test: PASS\n'
