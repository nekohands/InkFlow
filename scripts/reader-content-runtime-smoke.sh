#!/usr/bin/env bash
set -Eeuo pipefail

export LC_ALL=C

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck disable=SC1091
source "$script_dir/load-local-env.sh"

base_url="${1:-${INKFLOW_READER_CONTENT_SMOKE_BASE_URL:-http://localhost:8080}}"
chapter_id="${2:-${INKFLOW_READER_CONTENT_SMOKE_CHAPTER_ID:-}}"
expected_marker="${INKFLOW_READER_CONTENT_SMOKE_MARKER:-正文来自已发布的 Canonical Content}"
max_time="${INKFLOW_READER_CONTENT_SMOKE_CURL_MAX_TIME:-10}"
curl_bin="${INKFLOW_READER_CONTENT_SMOKE_CURL_BIN:-curl}"
work_dir=""

fail() {
  printf 'reader-content-runtime-smoke: %s\n' "$1" >&2
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

if [[ -z "$chapter_id" ]]; then
  fail 'chapter id must be supplied as the second argument or through the environment'
fi

if ! [[ "$chapter_id" =~ ^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-8][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}$ ]]; then
  fail 'chapter id must be a GUID'
fi

if [[ -z "$expected_marker" ]]; then
  fail 'expected marker must not be empty'
fi

if ! [[ "$max_time" =~ ^[1-9][0-9]*$ ]] || (( max_time > 60 )); then
  fail 'INKFLOW_READER_CONTENT_SMOKE_CURL_MAX_TIME must be an integer from 1 to 60'
fi

if ! command -v "$curl_bin" >/dev/null 2>&1; then
  fail "curl executable not found: $curl_bin"
fi

work_dir="$(mktemp -d "${TMPDIR:-/tmp}/inkflow-reader-content.XXXXXX")"

cleanup() {
  local status=$?
  set +e
  if [[ -n "$work_dir" ]]; then
    rm -rf -- "$work_dir"
  fi
  exit "$status"
}
trap cleanup EXIT

chapter_file="$work_dir/chapter.html"
status="$($curl_bin \
  --silent --show-error \
  --max-time "$max_time" \
  --output "$chapter_file" \
  --write-out '%{http_code}' \
  "$base_url/reader/read/$chapter_id")" || fail 'chapter request could not be completed'

if [[ "$status" != "200" ]]; then
  fail "GET /reader/read/$chapter_id returned HTTP $status"
fi

grep -Fq -- 'id="reading-progress"' "$chapter_file" \
  || fail 'chapter page is missing the reading progress element'
grep -Fq -- 'class="reader-content__body"' "$chapter_file" \
  || fail 'chapter page is missing the content body'
grep -Fq -- "$expected_marker" "$chapter_file" \
  || fail 'chapter page does not contain the published-content marker'
grep -Fq -- 'reading/progress/' "$chapter_file" \
  || fail 'chapter page is missing the progress synchronization contract'
grep -Fq -- '本章结束' "$chapter_file" \
  || fail 'chapter page is missing the chapter end marker'
if grep -Fq -- '该章节尚未发布内容' "$chapter_file"; then
  fail 'chapter page still reports unpublished content'
fi

printf 'reader-content-runtime-smoke: PASS (published content, reader progress contract)\n'
