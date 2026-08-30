#!/usr/bin/env bash
set -Eeuo pipefail

root_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd)"
script="$root_dir/scripts/admin-runtime-smoke.sh"

bash -n "$script"
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

printf 'admin-runtime-smoke.test: PASS\n'
