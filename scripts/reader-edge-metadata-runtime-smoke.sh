#!/usr/bin/env bash
set -Eeuo pipefail

export LC_ALL=C

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck disable=SC1091
source "$script_dir/load-local-env.sh"

base_url="${1:-${INKFLOW_READER_EDGE_SMOKE_BASE_URL:-http://localhost:8080}}"
book_id="${2:-${INKFLOW_READER_EDGE_SMOKE_BOOK_ID:-}}"
max_time="${INKFLOW_READER_EDGE_SMOKE_CURL_MAX_TIME:-10}"
curl_bin="${INKFLOW_READER_EDGE_SMOKE_CURL_BIN:-curl}"
work_dir=""

fail() {
  printf 'reader-edge-metadata-runtime-smoke: %s\n' "$1" >&2
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

if ! [[ "$book_id" =~ ^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-8][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}$ ]]; then
  fail 'book id must be a GUID'
fi

if ! [[ "$max_time" =~ ^[1-9][0-9]*$ ]] || (( max_time > 60 )); then
  fail 'INKFLOW_READER_EDGE_SMOKE_CURL_MAX_TIME must be an integer from 1 to 60'
fi

if ! command -v "$curl_bin" >/dev/null 2>&1; then
  fail "curl executable not found: $curl_bin"
fi

work_dir="$(mktemp -d "${TMPDIR:-/tmp}/inkflow-reader-edge.XXXXXX")"

cleanup() {
  local status=$?
  set +e
  if [[ -n "$work_dir" ]]; then
    rm -rf -- "$work_dir"
  fi
  exit "$status"
}
trap cleanup EXIT

page="$work_dir/book.html"
status_file="$work_dir/book.status"
if ! "$curl_bin" \
  --silent --show-error --fail \
  --max-time "$max_time" \
  --output "$page" \
  --write-out '%{http_code}' \
  "$base_url/reader/books/$book_id" > "$status_file"; then
  fail "GET /reader/books/$book_id failed"
fi

status="$(<"$status_file")"
if [[ "$status" != "200" ]]; then
  fail "GET /reader/books/$book_id returned HTTP $status"
fi

contains() {
  local value="$1"
  grep -Fq -- "$value" "$page" || fail "book detail is missing expected edge contract: $value"
}

not_contains() {
  local value="$1"
  if grep -Fq -- "$value" "$page"; then
    fail "book detail contains unescaped edge data: $value"
  fi
}

contains 'class="book-hero"'
contains 'InkFlow Edge &lt;Metadata&gt;'
contains 'InkFlow Edge &amp; Author'
contains 'overflow-wrap: anywhere;'
contains '开始阅读'
not_contains '<img'
not_contains 'InkFlow Edge <Metadata>'

printf 'reader-edge-metadata-runtime-smoke: PASS (max-length metadata, escaping, wrapping, and no-cover detail path)\n'
