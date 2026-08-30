#!/usr/bin/env bash
set -Eeuo pipefail

root_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd)"
script="$root_dir/scripts/collection-package-runtime-smoke.sh"

bash -n "$script"
for required in \
  '/api/v1/admin/collection-runs' \
  'source-url.scheme' \
  'pause' \
  'resume' \
  'stop' \
  'cancel' \
  '/api/v1/admin/books/' \
  '/api/v1/admin/packages/' \
  'mimetype' \
  'formatVersion' \
  'book.package.create' \
  'collection-package-runtime-smoke: PASS'; do
  grep -Fq -- "$required" "$script"
done

printf 'collection-package-runtime-smoke.test: PASS\n'
