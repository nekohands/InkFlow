#!/usr/bin/env bash
set -Eeuo pipefail

export LC_ALL=C

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck disable=SC1091
source "$script_dir/load-local-env.sh"

base_url="${1:-${INKFLOW_READER_NAVIGATION_SMOKE_BASE_URL:-http://localhost:8080}}"
first_chapter_id="${2:-${INKFLOW_READER_NAVIGATION_SMOKE_FIRST_CHAPTER_ID:-}}"
second_chapter_id="${3:-${INKFLOW_READER_NAVIGATION_SMOKE_SECOND_CHAPTER_ID:-}}"
first_title="${INKFLOW_READER_NAVIGATION_SMOKE_FIRST_TITLE:-Automated Acceptance Chapter}"
second_title="${INKFLOW_READER_NAVIGATION_SMOKE_SECOND_TITLE:-Automated Acceptance Follow-up}"
max_time="${INKFLOW_READER_NAVIGATION_SMOKE_CURL_MAX_TIME:-10}"
curl_bin="${INKFLOW_READER_NAVIGATION_SMOKE_CURL_BIN:-curl}"
work_dir=""

fail() {
  printf 'reader-navigation-runtime-smoke: %s\n' "$1" >&2
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

for chapter_id in "$first_chapter_id" "$second_chapter_id"; do
  if ! [[ "$chapter_id" =~ ^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-8][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}$ ]]; then
    fail 'chapter ids must be GUIDs'
  fi
done

if [[ "$first_chapter_id" == "$second_chapter_id" ]]; then
  fail 'chapter ids must be different'
fi

if [[ -z "$first_title" || -z "$second_title" ]]; then
  fail 'chapter titles must not be empty'
fi

if ! [[ "$max_time" =~ ^[1-9][0-9]*$ ]] || (( max_time > 60 )); then
  fail 'INKFLOW_READER_NAVIGATION_SMOKE_CURL_MAX_TIME must be an integer from 1 to 60'
fi

if ! command -v "$curl_bin" >/dev/null 2>&1; then
  fail "curl executable not found: $curl_bin"
fi

work_dir="$(mktemp -d "${TMPDIR:-/tmp}/inkflow-reader-navigation.XXXXXX")"

cleanup() {
  local status=$?
  set +e
  if [[ -n "$work_dir" ]]; then
    rm -rf -- "$work_dir"
  fi
  exit "$status"
}
trap cleanup EXIT

fetch_page() {
  local chapter_id="$1"
  local output="$2"
  local status

  if ! "$curl_bin" \
    --silent --show-error \
    --max-time "$max_time" \
    --output "$output" \
    --write-out '%{http_code}' \
    "$base_url/reader/read/$chapter_id" > "$output.status"; then
    fail "GET /reader/read/$chapter_id failed"
  fi

  status="$(<"$output.status")"
  if [[ "$status" != "200" ]]; then
    fail "GET /reader/read/$chapter_id returned HTTP $status"
  fi
}

first_page="$work_dir/first.html"
second_page="$work_dir/second.html"
fetch_page "$first_chapter_id" "$first_page"
fetch_page "$second_chapter_id" "$second_page"

grep -Fq -- "$first_title" "$first_page" \
  || fail 'first chapter title contract is missing'
grep -Fq -- 'id="reading-progress"' "$first_page" \
  || fail 'first chapter is missing the reader progress element'
grep -Fq -- "href=\"/reader/read/$second_chapter_id\" rel=\"next\"" "$first_page" \
  || fail 'first chapter is missing the next-chapter link'
if grep -Fq -- 'rel="prev"' "$first_page"; then
  fail 'first chapter must not expose a previous-chapter link'
fi

grep -Fq -- "$second_title" "$second_page" \
  || fail 'second chapter title contract is missing'
grep -Fq -- 'id="reading-progress"' "$second_page" \
  || fail 'second chapter is missing the reader progress element'
grep -Fq -- "href=\"/reader/read/$first_chapter_id\" rel=\"prev\"" "$second_page" \
  || fail 'second chapter is missing the previous-chapter link'
if grep -Fq -- 'rel="next"' "$second_page"; then
  fail 'last chapter must not expose a next-chapter link'
fi

printf 'reader-navigation-runtime-smoke: PASS (previous/next chapter links and boundaries)\n'
