#!/usr/bin/env bash
set -Eeuo pipefail

export LC_ALL=C

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck disable=SC1091
source "$script_dir/load-local-env.sh"

base_url="${1:-${INKFLOW_LEGADO_RUNTIME_SMOKE_BASE_URL:-http://localhost:8080}}"
book_id="${INKFLOW_LEGADO_RUNTIME_SMOKE_BOOK_ID:-}"
chapter_id="${INKFLOW_LEGADO_RUNTIME_SMOKE_CHAPTER_ID:-}"
search_query="${INKFLOW_LEGADO_RUNTIME_SMOKE_QUERY:-}"
expected_marker="${INKFLOW_LEGADO_RUNTIME_SMOKE_MARKER:-正文来自已发布的 Canonical Content}"
max_time="${INKFLOW_LEGADO_RUNTIME_SMOKE_CURL_MAX_TIME:-10}"
curl_bin="${INKFLOW_LEGADO_RUNTIME_SMOKE_CURL_BIN:-curl}"
jq_bin="${INKFLOW_LEGADO_RUNTIME_SMOKE_JQ_BIN:-jq}"
work_dir=""

fail() {
  printf 'legado-runtime-smoke: %s\n' "$1" >&2
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

if [[ -z "$book_id" || -z "$chapter_id" ]]; then
  fail 'canonical book id and chapter id must be supplied through environment variables'
fi

for id in "$book_id" "$chapter_id"; do
  if ! [[ "$id" =~ ^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-8][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}$ ]]; then
    fail 'book and chapter ids must be GUIDs'
  fi
done

if [[ -z "$expected_marker" ]]; then
  fail 'expected content marker must not be empty'
fi

if ! [[ "$max_time" =~ ^[1-9][0-9]*$ ]] || (( max_time > 60 )); then
  fail 'INKFLOW_LEGADO_RUNTIME_SMOKE_CURL_MAX_TIME must be an integer from 1 to 60'
fi

for executable in "$curl_bin" "$jq_bin"; do
  if ! command -v "$executable" >/dev/null 2>&1; then
    fail "required executable not found: $executable"
  fi
done

work_dir="$(mktemp -d "${TMPDIR:-/tmp}/inkflow-legado-runtime.XXXXXX")"

cleanup() {
  local status=$?
  set +e
  if [[ -n "$work_dir" ]]; then
    rm -rf -- "$work_dir"
  fi
  exit "$status"
}
trap cleanup EXIT

request_json() {
  local route="$1"
  local output="$2"
  local status

  if ! status="$("$curl_bin" \
    --silent --show-error --max-time "$max_time" \
    --output "$output" \
    --write-out '%{http_code}' \
    "$base_url$route")"; then
    fail "GET $route could not be completed"
  fi

  if [[ "$status" != "200" ]]; then
    fail "GET $route returned HTTP $status; expected 200"
  fi
}

assert_json() {
  local expression="$1"
  local file="$2"
  local message="$3"
  "$jq_bin" -e "$expression" "$file" >/dev/null || fail "$message"
}

assert_json_arg() {
  local name="$1"
  local value="$2"
  local expression="$3"
  local file="$4"
  local message="$5"
  "$jq_bin" -e --arg "$name" "$value" "$expression" "$file" >/dev/null || fail "$message"
}

encoded_query="$("$jq_bin" -rn --arg query "$search_query" '$query | @uri')"
request_json \
  "/api/legado/v1/search?q=$encoded_query" \
  "$work_dir/search.json"
assert_json_arg book "$book_id" \
  '.data | type == "array" and any(.[]; .bookId == $book and (.title | length) > 0 and (.author | length) > 0 and .detailUrl == ("/api/legado/v1/books/" + $book))' \
  "$work_dir/search.json" \
  'Legado Search did not return the acceptance book with a stable detail URL'

detail_url="$("$jq_bin" -er --arg book "$book_id" \
  '.data[] | select(.bookId == $book) | .detailUrl' \
  "$work_dir/search.json")"
[[ "$detail_url" == "/api/legado/v1/books/$book_id" ]] || \
  fail 'Legado Search detail URL is not the expected relative v1 route'

request_json "/api/legado/v1/books/$book_id" "$work_dir/book.json"
assert_json_arg book "$book_id" \
  '.bookId == $book and (.title | length) > 0 and (.author | length) > 0 and .tocUrl == ("/api/legado/v1/books/" + $book + "/chapters")' \
  "$work_dir/book.json" \
  'Legado BookInfo payload is incomplete or points outside the v1 route'

request_json "/api/legado/v1/books/$book_id/chapters" "$work_dir/toc.json"
assert_json_arg chapter "$chapter_id" \
  '.data | type == "array" and any(.[]; .chapterId == $chapter and .index == 0 and .chapterUrl == ("/api/legado/v1/chapters/" + $chapter))' \
  "$work_dir/toc.json" \
  'Legado TOC did not return the expected stable chapter URL'

request_json "/api/legado/v1/chapters/$chapter_id" "$work_dir/content.json"
assert_json_arg chapter "$chapter_id" \
  '.chapterId == $chapter and (.title | length) > 0' \
  "$work_dir/content.json" \
  'Legado Content payload has an invalid chapter identity'
assert_json_arg marker "$expected_marker" \
  '(.content | type == "string" and contains($marker) and (contains("<p>") | not))' \
  "$work_dir/content.json" \
  'Legado Content did not return the published plain-text canonical content'

request_json /legado/book-source.json "$work_dir/manifest.json"
assert_json \
  '.searchUrl | contains("/api/legado/v1/search?q={{key}}")' \
  "$work_dir/manifest.json" \
  'generated public Legado manifest does not point to the v1 Search route'
assert_json \
  '.ruleSearch.bookList == "$.data[*]" and .ruleSearch.bookUrl == "$.detailUrl" and .ruleBookInfo.tocUrl == "$.tocUrl" and .ruleToc.chapterUrl == "$.chapterUrl" and .ruleContent.content == "$.content" and (has("header") | not)' \
  "$work_dir/manifest.json" \
  'generated public Legado manifest rules are incomplete or contain personal credentials'

printf 'legado-runtime-smoke: PASS (manifest, Search, BookInfo, TOC, Content)\n'
