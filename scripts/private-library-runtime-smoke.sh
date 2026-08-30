#!/usr/bin/env bash
set -Eeuo pipefail

export LC_ALL=C

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck disable=SC1091
source "$script_dir/load-local-env.sh"

base_url="${1:-${INKFLOW_PRIVATE_LIBRARY_SMOKE_BASE_URL:-http://localhost:8080}}"
max_time="${INKFLOW_PRIVATE_LIBRARY_SMOKE_CURL_MAX_TIME:-10}"
curl_bin="${INKFLOW_PRIVATE_LIBRARY_SMOKE_CURL_BIN:-curl}"
jq_bin="${INKFLOW_PRIVATE_LIBRARY_SMOKE_JQ_BIN:-jq}"
work_dir=""
token_a=""
token_b=""
book_id=""
imported_book_id=""
epub_book_id=""
duplicate_book_id=""
deleted_book_id=""

fail() {
  printf 'private-library-runtime-smoke: %s\n' "$1" >&2
  exit 1
}

not_contains() {
  local file="$1"
  local value="$2"

  if grep -Fq -- "$value" "$file"; then
    fail "$file contains private data that should not be publicly visible: $value"
  fi
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
  fail 'INKFLOW_PRIVATE_LIBRARY_SMOKE_CURL_MAX_TIME must be an integer from 1 to 60'
fi

if ! command -v "$curl_bin" >/dev/null 2>&1; then
  fail "curl executable not found: $curl_bin"
fi

if ! command -v "$jq_bin" >/dev/null 2>&1; then
  fail "jq executable not found: $jq_bin"
fi

work_dir="$(mktemp -d "${TMPDIR:-/tmp}/inkflow-private-library.XXXXXX")"

cleanup() {
  local status=$?
  set +e

  # There is intentionally no account-delete API. The two uniquely named
  # smoke users are harmless test principals; books are removed on every exit.
  if [[ -n "$token_a" ]]; then
    for id in "$book_id" "$imported_book_id" "$epub_book_id" "$duplicate_book_id"; do
      if [[ -n "$id" ]]; then
        "$curl_bin" --silent --show-error --max-time "$max_time" \
          --request DELETE \
          -H "Authorization: Bearer $token_a" \
          "$base_url/api/v1/me/private-library/books/$id" \
          >/dev/null 2>&1 || true
      fi
    done
  fi

  if [[ -n "$work_dir" ]]; then
    rm -rf -- "$work_dir"
  fi
  exit "$status"
}
trap cleanup EXIT

post_json() {
  local route="$1"
  local token="$2"
  local payload="$3"
  local output="$4"
  local -a headers=(-H 'Content-Type: application/json')

  if [[ -n "$token" ]]; then
    headers+=(-H "Authorization: Bearer $token")
  fi

  if ! "$curl_bin" \
    --silent --show-error --fail \
    --max-time "$max_time" \
    "${headers[@]}" \
    --data "$payload" \
    --output "$output" \
    "$base_url$route"; then
    fail "POST $route failed"
  fi
}

put_json() {
  local route="$1"
  local token="$2"
  local payload="$3"
  local output="$4"
  local -a headers=(-H 'Content-Type: application/json')

  if [[ -n "$token" ]]; then
    headers+=(-H "Authorization: Bearer $token")
  fi

  if ! "$curl_bin" \
    --silent --show-error --fail \
    --max-time "$max_time" \
    --request PUT \
    "${headers[@]}" \
    --data "$payload" \
    --output "$output" \
    "$base_url$route"; then
    fail "PUT $route failed"
  fi
}

get_json() {
  local route="$1"
  local token="$2"
  local output="$3"
  local -a headers=()

  if [[ -n "$token" ]]; then
    headers+=(-H "Authorization: Bearer $token")
  fi

  if ! "$curl_bin" \
    --silent --show-error --fail \
    --max-time "$max_time" \
    "${headers[@]}" \
    --output "$output" \
    "$base_url$route"; then
    fail "GET $route failed"
  fi
}

get_json_with_headers() {
  local route="$1"
  local token="$2"
  local output="$3"
  local response_headers="$4"

  if ! "$curl_bin" \
    --silent --show-error --fail \
    --max-time "$max_time" \
    -H "Authorization: Bearer $token" \
    --dump-header "$response_headers" \
    --output "$output" \
    "$base_url$route"; then
    fail "GET $route failed"
  fi
}

get_file_with_headers() {
  local route="$1"
  local token="$2"
  local output="$3"
  local response_headers="$4"

  if ! "$curl_bin" \
    --silent --show-error --fail \
    --max-time "$max_time" \
    -H "Authorization: Bearer $token" \
    --dump-header "$response_headers" \
    --output "$output" \
    "$base_url$route"; then
    fail "GET $route could not download the file"
  fi
}

expect_status() {
  local route="$1"
  local token="$2"
  local expected="$3"
  local output="$4"
  local status
  local -a headers=()

  if [[ -n "$token" ]]; then
    headers+=(-H "Authorization: Bearer $token")
  fi

  if ! status="$("$curl_bin" \
    --silent --show-error \
    --max-time "$max_time" \
    "${headers[@]}" \
    --output "$output" \
    --write-out '%{http_code}' \
    "$base_url$route")"; then
    fail "GET $route could not be completed"
  fi

  if [[ "$status" != "$expected" ]]; then
    fail "GET $route returned HTTP $status; expected $expected"
  fi
}

delete_book() {
  local token="$1"
  local id="$2"

  if ! "$curl_bin" \
    --silent --show-error --fail \
    --max-time "$max_time" \
    --request DELETE \
    -H "Authorization: Bearer $token" \
    "$base_url/api/v1/me/private-library/books/$id" \
    >/dev/null; then
    fail "DELETE private book $id failed"
  fi
}

run_id="$(date -u +%s%N)-$$-${RANDOM}"
test_password='correct horse battery staple'
email_a="ci-private-${run_id}-a@example.com"
email_b="ci-private-${run_id}-b@example.com"

expect_status /api/v1/me/private-library/books '' 401 "$work_dir/unauthenticated.json"

registration_payload="$("$jq_bin" -nc \
  --arg email "$email_a" \
  --arg password "$test_password" \
  '{email: $email, password: $password}')"
post_json /api/v1/auth/register '' "$registration_payload" "$work_dir/register-a.json"
token_a="$("$jq_bin" -er '.access_token' "$work_dir/register-a.json")"

registration_payload="$("$jq_bin" -nc \
  --arg email "$email_b" \
  --arg password "$test_password" \
  '{email: $email, password: $password}')"
post_json /api/v1/auth/register '' "$registration_payload" "$work_dir/register-b.json"
token_b="$("$jq_bin" -er '.access_token' "$work_dir/register-b.json")"

book_payload="$("$jq_bin" -nc \
  --arg title 'CI Private Book' \
  --arg author 'CI Runtime' \
  '{title: $title, author: $author}')"
post_json /api/v1/me/private-library/books "$token_a" "$book_payload" "$work_dir/create.json"
book_id="$("$jq_bin" -er '.privateBookId' "$work_dir/create.json")"

get_json /api/v1/me/private-library/books "$token_a" "$work_dir/list.json"
if ! "$jq_bin" -e --arg id "$book_id" \
  'any(.[]; .privateBookId == $id and .title == "CI Private Book")' \
  "$work_dir/list.json" >/dev/null; then
  fail 'created private book is missing from the owner list'
fi

get_json "/api/v1/me/private-library/books/$book_id" "$token_a" "$work_dir/detail.json"
if ! "$jq_bin" -e \
  '.privateBookId != null and .title == "CI Private Book" and .author == "CI Runtime"' \
  "$work_dir/detail.json" >/dev/null; then
  fail 'created private book detail does not match the owner contract'
fi

updated_payload="$("$jq_bin" -nc \
  --arg title 'CI Private Book Updated' \
  --arg author 'CI Runtime Updated' \
  '{title: $title, author: $author}')"
put_json "/api/v1/me/private-library/books/$book_id" "$token_a" "$updated_payload" "$work_dir/update.json"
if ! "$jq_bin" -e \
  '.privateBookId != null and .title == "CI Private Book Updated" and .author == "CI Runtime Updated"' \
  "$work_dir/update.json" >/dev/null; then
  fail 'private book update did not persist the editable metadata'
fi

get_json_with_headers \
  "/api/v1/me/private-library/books/$book_id/chapters" \
  "$token_a" \
  "$work_dir/empty-chapters.json" \
  "$work_dir/empty-chapters.headers"
if ! "$jq_bin" -e 'length == 0' "$work_dir/empty-chapters.json" >/dev/null; then
  fail 'new private book should have no chapters'
fi
if ! grep -Eiq '^Cache-Control:[[:space:]]*private,[[:space:]]*no-store' \
  "$work_dir/empty-chapters.headers"; then
  fail 'private chapter list is missing private no-store cache policy'
fi

import_file="$work_dir/ci-private-import.txt"
printf '%s\n' \
  'InkFlow Private Book v1' \
  'Title: CI Imported Private Book' \
  'Author: CI Importer' \
  '' \
  '## Chapter 1: First Chapter' \
  '' \
  'private paragraph one' \
  '' \
  '## Chapter 2: Second Chapter' \
  '' \
  'private paragraph two' \
  > "$import_file"

if ! "$curl_bin" \
  --silent --show-error --fail \
  --max-time "$max_time" \
  -H "Authorization: Bearer $token_a" \
  --form "file=@$import_file;filename=ci-private-import.txt;type=text/plain" \
  --output "$work_dir/import.json" \
  "$base_url/api/v1/me/private-library/import"; then
  fail 'TXT private book import failed'
fi
imported_book_id="$("$jq_bin" -er '.book.privateBookId' "$work_dir/import.json")"
if ! "$jq_bin" -e '.chapterCount == 2 and .book.title == "CI Imported Private Book"' \
  "$work_dir/import.json" >/dev/null; then
  fail 'TXT import did not return the expected book metadata and chapter count'
fi

get_json \
  "/api/v1/me/private-library/books/$imported_book_id/chapters" \
  "$token_a" \
  "$work_dir/imported-chapters.json"
if ! "$jq_bin" -e \
  'length == 2 and .[0].title == "First Chapter" and .[1].title == "Second Chapter"' \
  "$work_dir/imported-chapters.json" >/dev/null; then
  fail 'imported chapter list does not preserve order and headings'
fi
chapter_id="$("$jq_bin" -er '.[0].privateChapterId' "$work_dir/imported-chapters.json")"

get_json_with_headers \
  "/api/v1/me/private-library/books/$imported_book_id/chapters/$chapter_id" \
  "$token_a" \
  "$work_dir/chapter.json" \
  "$work_dir/chapter.headers"
if ! "$jq_bin" -e \
  --arg chapter_id "$chapter_id" \
  --arg book_id "$imported_book_id" \
  '.privateChapterId == $chapter_id and .privateBookId == $book_id and (.paragraphs | index("private paragraph one") != null)' \
  "$work_dir/chapter.json" >/dev/null; then
  fail 'private chapter content did not preserve owner identity and paragraphs'
fi
if ! grep -Eiq '^Cache-Control:[[:space:]]*private,[[:space:]]*no-store' \
  "$work_dir/chapter.headers"; then
  fail 'private chapter content is missing private no-store cache policy'
fi

if ! "$curl_bin" \
  --silent --show-error --fail \
  --max-time "$max_time" \
  -H "Authorization: Bearer $token_a" \
  --output "$work_dir/export.txt" \
  "$base_url/api/v1/me/private-library/books/$imported_book_id/export?format=txt"; then
  fail 'private TXT export failed'
fi
if ! grep -Fq -- 'private paragraph one' "$work_dir/export.txt" ||
   ! grep -Fq -- 'private paragraph two' "$work_dir/export.txt"; then
  fail 'private TXT export did not include imported paragraphs'
fi

get_file_with_headers \
  "/api/v1/me/private-library/books/$imported_book_id/export?format=epub" \
  "$token_a" \
  "$work_dir/export.epub" \
  "$work_dir/export.epub.headers"
if ! grep -Eiq '^Content-Type:[[:space:]]*application/epub\+zip' \
  "$work_dir/export.epub.headers"; then
  fail 'private EPUB export did not return the EPUB content type'
fi
if [[ ! -s "$work_dir/export.epub" ]]; then
  fail 'private EPUB export was empty'
fi

if ! "$curl_bin" \
  --silent --show-error --fail \
  --max-time "$max_time" \
  -H "Authorization: Bearer $token_a" \
  --form "file=@$work_dir/export.epub;filename=ci-private-roundtrip.epub;type=application/epub+zip" \
  --output "$work_dir/epub-import.json" \
  "$base_url/api/v1/me/private-library/import"; then
  fail 'private EPUB import failed'
fi
epub_book_id="$("$jq_bin" -er '.book.privateBookId' "$work_dir/epub-import.json")"
if ! "$jq_bin" -e \
  '.chapterCount == 2 and .book.title == "CI Imported Private Book"' \
  "$work_dir/epub-import.json" >/dev/null; then
  fail 'EPUB round trip did not preserve book metadata and chapter count'
fi
get_json \
  "/api/v1/me/private-library/books/$epub_book_id/chapters" \
  "$token_a" \
  "$work_dir/epub-chapters.json"
if ! "$jq_bin" -e \
  'length == 2 and .[0].title == "First Chapter" and .[1].title == "Second Chapter"' \
  "$work_dir/epub-chapters.json" >/dev/null; then
  fail 'EPUB round trip did not preserve chapter order and headings'
fi
epub_chapter_id="$("$jq_bin" -er '.[0].privateChapterId' "$work_dir/epub-chapters.json")"
get_json \
  "/api/v1/me/private-library/books/$epub_book_id/chapters/$epub_chapter_id" \
  "$token_a" \
  "$work_dir/epub-chapter.json"
if ! "$jq_bin" -e \
  '.paragraphs | index("private paragraph one") != null' \
  "$work_dir/epub-chapter.json" >/dev/null; then
  fail 'EPUB round trip did not preserve chapter content'
fi

if ! "$curl_bin" \
  --silent --show-error --fail \
  --max-time "$max_time" \
  -H "Authorization: Bearer $token_a" \
  --form "file=@$import_file;filename=ci-private-duplicate.txt;type=text/plain" \
  --output "$work_dir/duplicate-import.json" \
  "$base_url/api/v1/me/private-library/import"; then
  fail 'duplicate private book import failed'
fi
duplicate_book_id="$("$jq_bin" -er '.book.privateBookId' "$work_dir/duplicate-import.json")"
if [[ "$duplicate_book_id" == "$imported_book_id" ]]; then
  fail 'duplicate import reused the original book identity'
fi
get_json \
  "/api/v1/me/private-library/books/$imported_book_id" \
  "$token_a" \
  "$work_dir/original-after-duplicate.json"
if ! "$jq_bin" -e \
  '.privateBookId != null and .title == "CI Imported Private Book" and .author == "CI Importer"' \
  "$work_dir/original-after-duplicate.json" >/dev/null; then
  fail 'duplicate import overwrote the original private book'
fi
get_json /api/v1/me/private-library/books "$token_a" "$work_dir/duplicate-list.json"
if ! "$jq_bin" -e \
  --arg first "$imported_book_id" \
  --arg second "$duplicate_book_id" \
  'any(.[]; .privateBookId == $first) and any(.[]; .privateBookId == $second)' \
  "$work_dir/duplicate-list.json" >/dev/null; then
  fail 'duplicate import did not retain both private book identities'
fi

get_json /api/v1/me/private-library/books "$token_a" "$work_dir/before-failed-import.json"
before_failed_import_count="$("$jq_bin" -er 'length' "$work_dir/before-failed-import.json")"
invalid_import_file="$work_dir/invalid-import.epub"
printf '%s\n' 'not an EPUB archive' > "$invalid_import_file"
invalid_import_status="$("$curl_bin" \
  --silent --show-error \
  --max-time "$max_time" \
  -H "Authorization: Bearer $token_a" \
  --form "file=@$invalid_import_file;filename=ci-private-invalid.epub;type=application/epub+zip" \
  --output "$work_dir/failed-import.json" \
  --write-out '%{http_code}' \
  "$base_url/api/v1/me/private-library/import")"
if [[ "$invalid_import_status" != "400" ]] ||
   ! "$jq_bin" -e '.error == "invalid_file"' "$work_dir/failed-import.json" >/dev/null; then
  fail "invalid EPUB import returned HTTP $invalid_import_status instead of the stable invalid_file response"
fi
get_json /api/v1/me/private-library/books "$token_a" "$work_dir/after-failed-import.json"
after_failed_import_count="$("$jq_bin" -er 'length' "$work_dir/after-failed-import.json")"
if [[ "$after_failed_import_count" != "$before_failed_import_count" ]]; then
  fail 'failed import left a partial private book behind'
fi

expect_status \
  "/api/v1/books/$imported_book_id" \
  '' \
  404 \
  "$work_dir/public-book.json"
expect_status \
  "/api/v1/chapters/$chapter_id/content" \
  '' \
  404 \
  "$work_dir/public-chapter.json"
expect_status \
  "/api/legado/v1/books/$imported_book_id" \
  '' \
  404 \
  "$work_dir/public-legado-book.json"
get_json /api/v1/books '' "$work_dir/public-catalog.json"
not_contains "$work_dir/public-catalog.json" 'CI Imported Private Book'
get_json /api/v1/me/reading/shelf "$token_a" "$work_dir/reading-shelf.json"
not_contains "$work_dir/reading-shelf.json" 'CI Imported Private Book'

expect_status \
  "/api/v1/me/private-library/books/$book_id" \
  "$token_b" \
  404 \
  "$work_dir/cross-user.json"

delete_book "$token_a" "$book_id"
delete_book "$token_a" "$imported_book_id"
delete_book "$token_a" "$epub_book_id"
delete_book "$token_a" "$duplicate_book_id"
deleted_book_id="$book_id"
book_id=''
imported_book_id=''
epub_book_id=''
duplicate_book_id=''

expect_status \
  "/api/v1/me/private-library/books/$deleted_book_id" \
  "$token_a" \
  404 \
  "$work_dir/deleted-book.json"

printf 'private-library-runtime-smoke: PASS (auth, ownership, CRUD, TXT/EPUB import/read/export, duplicate isolation, failed-import rollback)\n'
