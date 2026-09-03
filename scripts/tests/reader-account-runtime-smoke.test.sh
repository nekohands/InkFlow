#!/usr/bin/env bash
set -Eeuo pipefail

root_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd)"
script="$root_dir/scripts/reader-account-runtime-smoke.sh"

bash -n "$script"
for required in \
  '/api/v1/auth/register' \
  '/api/v1/auth/login' \
  '/api/v1/auth/refresh' \
  '/api/v1/auth/logout' \
  '/api/v1/me/reading/preferences' \
  '/api/v1/me/reading/shelf' \
  '/api/v1/me/reading/progress' \
  '/api/v1/me/reading/history' \
  '/api/v1/me/profile/avatar' \
  '/api/v1/admin/operations/overview?limit=50' \
  '/api/v1/admin/collection-runs?limit=1' \
  '/api/v1/admin/packages?limit=1' \
  '/api/v1/admin/crawler/dead-letters?limit=1' \
  'reader_source_id/health"' \
  'reader source operation denial smoke' \
  'invalid_package_format' \
  'has("crawler") | not' \
  'base64 --decode' \
  'uploaded avatar response was empty' \
  '/api/v1/me/legado/tokens' \
  '撤销后令牌记录未删除' \
  'refresh token rotation did not issue a new refresh token' \
  'reader-account-runtime-smoke: PASS'; do
  grep -Fq -- "$required" "$script"
done

printf 'reader-account-runtime-smoke.test: PASS\n'
