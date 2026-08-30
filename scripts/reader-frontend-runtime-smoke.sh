#!/usr/bin/env bash
set -Eeuo pipefail

export LC_ALL=C

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck disable=SC1091
source "$script_dir/load-local-env.sh"

base_url="${1:-${INKFLOW_FRONTEND_SMOKE_BASE_URL:-http://localhost:8080}}"
max_time="${INKFLOW_FRONTEND_SMOKE_CURL_MAX_TIME:-10}"
curl_bin="${INKFLOW_FRONTEND_SMOKE_CURL_BIN:-curl}"
work_dir=""

fail() {
  printf 'reader-frontend-runtime-smoke: %s\n' "$1" >&2
  exit 1
}

case "$base_url" in
  http://*|https://*) ;;
  *) fail 'base URL must use http or https' ;;
esac

case "$base_url" in
  *[[:space:]#?]*) fail 'base URL must not contain whitespace, a fragment, or a query' ;;
esac

base_url="${base_url%/}"
case "$base_url" in
  http://|https://) fail 'base URL must include a host' ;;
esac

if ! [[ "$max_time" =~ ^[1-9][0-9]*$ ]] || (( max_time > 60 )); then
  fail 'INKFLOW_FRONTEND_SMOKE_CURL_MAX_TIME must be an integer from 1 to 60'
fi

if ! command -v "$curl_bin" >/dev/null 2>&1; then
  fail "curl executable not found: $curl_bin"
fi

work_dir="$(mktemp -d "${TMPDIR:-/tmp}/inkflow-reader-frontend.XXXXXX")"

cleanup() {
  local status=$?
  set +e
  if [[ -n "$work_dir" ]]; then
    rm -rf -- "$work_dir"
  fi
  exit "$status"
}
trap cleanup EXIT

fetch_route() {
  local route="$1"
  local name="$2"
  local output="$work_dir/$name"
  local status

  if ! "$curl_bin" \
    --silent --show-error --fail \
    --max-time "$max_time" \
    --output "$output" \
    --write-out '%{http_code}' \
    "$base_url$route" > "$output.status"; then
    fail "GET $route failed"
  fi

  status="$(<"$output.status")"
  if [[ "$status" != "200" ]]; then
    fail "GET $route returned HTTP $status"
  fi
}

contains() {
  local file="$1"
  local value="$2"
  grep -Fq -- "$value" "$file" || fail "$file does not contain expected frontend contract: $value"
}

not_contains() {
  local file="$1"
  local value="$2"
  if grep -Fq -- "$value" "$file"; then
    fail "$file contains forbidden frontend data: $value"
  fi
}

fetch_route /reader reader.html
contains "$work_dir/reader.html" 'lang="zh-CN"'
contains "$work_dir/reader.html" 'id="main-content"'
contains "$work_dir/reader.html" 'role="search"'
contains "$work_dir/reader.html" 'class="search-bar"'
contains "$work_dir/reader.html" 'class="skip-link"'
contains "$work_dir/reader.html" ':focus-visible'
contains "$work_dir/reader.html" '@media (max-width: 640px)'
contains "$work_dir/reader.html" '@media (prefers-reduced-motion: reduce)'
contains "$work_dir/reader.html" '/reader/manifest.webmanifest'
not_contains "$work_dir/reader.html" 'X-InkFlow-Legado-Token'

fetch_route /reader/account account.html
contains "$work_dir/account.html" 'id="reader-login-form"'
contains "$work_dir/account.html" 'id="reader-register-form"'
contains "$work_dir/account.html" 'autocomplete="current-password"'
contains "$work_dir/account.html" 'autocomplete="new-password"'
contains "$work_dir/account.html" 'sessionStorage'
not_contains "$work_dir/account.html" 'localStorage'
not_contains "$work_dir/account.html" 'X-InkFlow-Legado-Token'

fetch_route /reader/shelf shelf.html
contains "$work_dir/shelf.html" 'data-reader-dashboard="shelf"'
contains "$work_dir/shelf.html" 'reader-dashboard-list'
contains "$work_dir/shelf.html" 'aria-live="polite"'

fetch_route /reader/history history.html
contains "$work_dir/history.html" 'data-reader-dashboard="history"'
contains "$work_dir/history.html" 'reader-dashboard-list'
contains "$work_dir/history.html" 'aria-live="polite"'

fetch_route /reader/offline offline.html
contains "$work_dir/offline.html" '当前处于离线状态'
contains "$work_dir/offline.html" '返回书库'

for page in account.html shelf.html history.html offline.html; do
  contains "$work_dir/$page" 'rel="manifest"'
  contains "$work_dir/$page" '/reader/sw.js'
  not_contains "$work_dir/$page" 'X-InkFlow-Legado-Token'
done

fetch_route /reader/manifest.webmanifest manifest.json
contains "$work_dir/manifest.json" '"start_url": "/reader"'
contains "$work_dir/manifest.json" '"scope": "/reader/"'
contains "$work_dir/manifest.json" '"display": "standalone"'
contains "$work_dir/manifest.json" '"src": "/reader/icon-192.svg"'
contains "$work_dir/manifest.json" '"src": "/reader/icon-512.svg"'
contains "$work_dir/manifest.json" '"sizes": "192x192"'
contains "$work_dir/manifest.json" '"sizes": "512x512"'

fetch_route /reader/sw.js service-worker.js
contains "$work_dir/service-worker.js" 'inkflow-reader-shell-v1'
contains "$work_dir/service-worker.js" '/reader/offline'
contains "$work_dir/service-worker.js" 'request.mode === "navigate"'
contains "$work_dir/service-worker.js" 'self.clients.claim()'
not_contains "$work_dir/service-worker.js" '/api/v1/me/reading'
not_contains "$work_dir/service-worker.js" 'auth/refresh'

for icon in 192 512; do
  fetch_route "/reader/icon-$icon.svg" "icon-$icon.svg"
  contains "$work_dir/icon-$icon.svg" '<svg'
  contains "$work_dir/icon-$icon.svg" 'viewBox="0 0 512 512"'
done

fetch_route /admin/operations operations.html
contains "$work_dir/operations.html" 'id="operations-content"'
contains "$work_dir/operations.html" '/api/v1/admin/operations/overview'
contains "$work_dir/operations.html" '/api/v1/admin/operations/alerts/history?'
contains "$work_dir/operations.html" 'operations-history-more'
contains "$work_dir/operations.html" 'operations-action-reason'
contains "$work_dir/operations.html" 'aria-live="polite"'
not_contains "$work_dir/operations.html" 'innerHTML'
not_contains "$work_dir/operations.html" 'CredentialReferenceId'
not_contains "$work_dir/operations.html" 'Variables'
not_contains "$work_dir/operations.html" 'X-InkFlow-Legado-Token'

printf 'reader-frontend-runtime-smoke: PASS (Reader/PWA/Operations frontend contracts)\n'
