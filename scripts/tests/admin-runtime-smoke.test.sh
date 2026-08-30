#!/usr/bin/env bash
set -Eeuo pipefail

root_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd)"
script="$root_dir/scripts/admin-runtime-smoke.sh"
fixture_launcher="$root_dir/scripts/run-acceptance-fixture.sh"

bash -n "$script"
bash -n "$fixture_launcher"
for required in \
  '/api/v1/admin/plans' \
  '/api/v1/admin/operations/overview' \
  '/api/v1/admin/operations/alerts/history' \
  '/api/v1/admin/audit/events' \
  '/permissions' \
  'source.manage' \
  '/health/search/disable' \
  '/credential-binding' \
  '/api/v1/admin/content/takedowns' \
  '/entitlement' \
  'admin-runtime-smoke: PASS'; do
  grep -Fq -- "$required" "$script"
done

grep -Fq -- 'tar --no-same-owner' "$fixture_launcher"
grep -Fq -- 'UseAppHost=false' "$fixture_launcher"
grep -Fq -- '"$@"' "$fixture_launcher"

printf 'admin-runtime-smoke.test: PASS\n'
