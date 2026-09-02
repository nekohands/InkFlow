#!/usr/bin/env bash
set -Eeuo pipefail

export LC_ALL=C

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck disable=SC1091
source "$script_dir/load-local-env.sh"

base_url="${1:-${INKFLOW_READER_ACCOUNT_SMOKE_BASE_URL:-http://localhost:8080}}"
email="${INKFLOW_READER_ACCOUNT_SMOKE_EMAIL:-}"
password="${INKFLOW_READER_ACCOUNT_SMOKE_PASSWORD:-}"
book_id="${INKFLOW_READER_ACCOUNT_SMOKE_BOOK_ID:-}"
chapter_id="${INKFLOW_READER_ACCOUNT_SMOKE_CHAPTER_ID:-}"
max_time="${INKFLOW_READER_ACCOUNT_SMOKE_CURL_MAX_TIME:-10}"
curl_bin="${INKFLOW_READER_ACCOUNT_SMOKE_CURL_BIN:-curl}"
jq_bin="${INKFLOW_READER_ACCOUNT_SMOKE_JQ_BIN:-jq}"
work_dir=""
access_token=""

fail() {
  printf 'reader-account-runtime-smoke: %s\n' "$1" >&2
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

if [[ -z "$email" || -z "$password" || -z "$book_id" || -z "$chapter_id" ]]; then
  fail 'email, password, book id, and chapter id must be supplied through environment variables'
fi

if ! [[ "$max_time" =~ ^[1-9][0-9]*$ ]] || (( max_time > 60 )); then
  fail 'INKFLOW_READER_ACCOUNT_SMOKE_CURL_MAX_TIME must be an integer from 1 to 60'
fi

if ! command -v "$curl_bin" >/dev/null 2>&1; then
  fail "curl executable not found: $curl_bin"
fi

if ! command -v "$jq_bin" >/dev/null 2>&1; then
  fail "jq executable not found: $jq_bin"
fi

work_dir="$(mktemp -d "${TMPDIR:-/tmp}/inkflow-reader-account.XXXXXX")"

cleanup() {
  local status=$?
  set +e

  if [[ -n "$access_token" ]]; then
    "$curl_bin" --silent --show-error --max-time "$max_time" \
      --request POST \
      -H "Authorization: Bearer $access_token" \
      "$base_url/api/v1/auth/logout" \
      >/dev/null 2>&1 || true
  fi

  if [[ -n "$work_dir" ]]; then
    rm -rf -- "$work_dir"
  fi
  exit "$status"
}
trap cleanup EXIT

request() {
  local method="$1"
  local route="$2"
  local token="$3"
  local payload="$4"
  local output="$5"
  local expected="$6"
  local status
  local -a args=(
    --silent
    --show-error
    --max-time "$max_time"
    --request "$method"
  )

  if [[ -n "$token" ]]; then
    args+=(-H "Authorization: Bearer $token")
  fi

  if [[ "$payload" != '__NO_BODY__' ]]; then
    args+=(-H 'Content-Type: application/json' --data "$payload")
  fi

  if ! status="$("$curl_bin" "${args[@]}" \
    --output "$output" \
    --write-out '%{http_code}' \
    "$base_url$route")"; then
    fail "request $method $route could not be completed"
  fi

  if [[ "$status" != "$expected" ]]; then
    fail "request $method $route returned HTTP $status; expected $expected"
  fi
}

assert_json() {
  local file="$1"
  local expression="$2"
  shift 2

  if ! "$jq_bin" -e "$@" "$expression" "$file" >/dev/null; then
    fail "JSON assertion failed for $file"
  fi
}

read_token() {
  local file="$1"
  local field="$2"
  local value

  if ! value="$("$jq_bin" -er ".${field} | strings | select(length > 0)" "$file")"; then
    fail "response did not contain a usable $field"
  fi

  if (( ${#value} > 512 )); then
    fail "$field exceeded the maximum accepted token length"
  fi

  printf '%s' "$value"
}

registration_payload="$("$jq_bin" -nc \
  --arg email "$email" \
  --arg password "$password" \
  '{email: $email, password: $password}')"

request GET /api/v1/auth/me '' '__NO_BODY__' "$work_dir/unauthenticated-me.json" 401
request GET /api/v1/me/reading/shelf '' '__NO_BODY__' "$work_dir/unauthenticated-shelf.json" 401
request GET /api/v1/me/profile/avatar '' '__NO_BODY__' "$work_dir/unauthenticated-avatar.json" 401

request POST /api/v1/auth/register '' "$registration_payload" "$work_dir/register.json" 200
assert_json "$work_dir/register.json" '.user.email == $email and .user.role == "Reader" and (.user.id | strings | length > 0)' --arg email "$email"
registered_access_token="$(read_token "$work_dir/register.json" access_token)"

request GET /api/v1/auth/me "$registered_access_token" '__NO_BODY__' "$work_dir/registered-me.json" 200
assert_json "$work_dir/registered-me.json" '.email == $email and .role == "Reader"' --arg email "$email"
request POST /api/v1/auth/logout "$registered_access_token" '__NO_BODY__' "$work_dir/registered-logout.json" 204
request GET /api/v1/auth/me "$registered_access_token" '__NO_BODY__' "$work_dir/registered-me-after-logout.json" 401

request POST /api/v1/auth/login '' "$registration_payload" "$work_dir/login.json" 200
assert_json "$work_dir/login.json" '.user.email == $email and .user.role == "Reader"' --arg email "$email"
login_access_token="$(read_token "$work_dir/login.json" access_token)"
login_refresh_token="$(read_token "$work_dir/login.json" refresh_token)"
request GET /api/v1/auth/me "$login_access_token" '__NO_BODY__' "$work_dir/logged-in-me.json" 200
assert_json "$work_dir/logged-in-me.json" '.email == $email and .role == "Reader"' --arg email "$email"

request POST /api/v1/auth/refresh '' \
  "$("$jq_bin" -nc --arg refresh_token "$login_refresh_token" '{refresh_token: $refresh_token}')" \
  "$work_dir/refresh.json" 200
assert_json "$work_dir/refresh.json" '.user.email == $email and .user.role == "Reader"' --arg email "$email"
rotated_access_token="$(read_token "$work_dir/refresh.json" access_token)"
rotated_refresh_token="$(read_token "$work_dir/refresh.json" refresh_token)"
if [[ "$rotated_refresh_token" == "$login_refresh_token" ]]; then
  fail 'refresh token rotation did not issue a new refresh token'
fi

request POST /api/v1/auth/refresh '' \
  "$("$jq_bin" -nc --arg refresh_token "$login_refresh_token" '{refresh_token: $refresh_token}')" \
  "$work_dir/reused-refresh.json" 401

access_token="$rotated_access_token"
request GET /api/v1/auth/me "$access_token" '__NO_BODY__' "$work_dir/me.json" 200
assert_json "$work_dir/me.json" '.email == $email and .role == "Reader"' --arg email "$email"

printf '%s' 'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=' \
  | base64 --decode > "$work_dir/avatar.png"
request_file() {
  local method="$1"
  local route="$2"
  local token="$3"
  local file="$4"
  local output="$5"
  local expected="$6"
  local status
  local -a args=(
    --silent
    --show-error
    --max-time "$max_time"
    --request "$method"
  )

  if [[ -n "$token" ]]; then
    args+=(-H "Authorization: Bearer $token")
  fi
  args+=(--form "file=@$file")

  if ! status="$($curl_bin "${args[@]}" \
    --output "$output" \
    --write-out '%{http_code}' \
    "$base_url$route")"; then
    fail "request $method $route could not be completed"
  fi

  if [[ "$status" != "$expected" ]]; then
    fail "request $method $route returned HTTP $status; expected $expected"
  fi
}

request_file PUT /api/v1/me/profile/avatar "$access_token" "$work_dir/avatar.png" \
  "$work_dir/avatar-upload.json" 204
request GET /api/v1/me/profile/avatar "$access_token" '__NO_BODY__' \
  "$work_dir/avatar-response.bin" 200
[[ -s "$work_dir/avatar-response.bin" ]] || fail 'uploaded avatar response was empty'

request GET /api/v1/me/reading/preferences "$access_token" '__NO_BODY__' "$work_dir/default-preferences.json" 200
assert_json "$work_dir/default-preferences.json" \
  '.fontSizePercent == 100 and .lineHeightPercent == 180 and .theme == "System"'

request GET '/api/v1/me/reading/shelf?limit=100' "$access_token" '__NO_BODY__' "$work_dir/empty-shelf.json" 200
assert_json "$work_dir/empty-shelf.json" 'type == "array" and length == 0'
request GET '/api/v1/me/reading/history?limit=100' "$access_token" '__NO_BODY__' "$work_dir/empty-history.json" 200
assert_json "$work_dir/empty-history.json" 'type == "array" and length == 0'
request GET "/api/v1/me/reading/progress/$book_id" "$access_token" '__NO_BODY__' "$work_dir/missing-progress.json" 404

request PUT /api/v1/me/reading/preferences "$access_token" \
  '{"fontSizePercent":120,"lineHeightPercent":200,"theme":"Sepia"}' \
  "$work_dir/updated-preferences.json" 200
assert_json "$work_dir/updated-preferences.json" \
  '.fontSizePercent == 120 and .lineHeightPercent == 200 and .theme == "Sepia"'
request GET /api/v1/me/reading/preferences "$access_token" '__NO_BODY__' "$work_dir/persisted-preferences.json" 200
assert_json "$work_dir/persisted-preferences.json" \
  '.fontSizePercent == 120 and .lineHeightPercent == 200 and .theme == "Sepia"'
request PUT /api/v1/me/reading/preferences "$access_token" \
  '{"fontSizePercent":79}' "$work_dir/invalid-preferences.json" 400

request PUT "/api/v1/me/reading/shelf/$book_id" "$access_token" \
  '{"status":"NotARealShelfStatus"}' "$work_dir/invalid-shelf.json" 400
request PUT "/api/v1/me/reading/shelf/$book_id" "$access_token" \
  '{"status":"Reading"}' "$work_dir/shelf-put.json" 200
assert_json "$work_dir/shelf-put.json" \
  '.bookId == $book_id and .status == "Reading" and (.chapterCount | numbers | . >= 1)' \
  --arg book_id "$book_id"
request GET '/api/v1/me/reading/shelf?limit=100' "$access_token" '__NO_BODY__' "$work_dir/shelf-after-put.json" 200
assert_json "$work_dir/shelf-after-put.json" \
  'length == 1 and .[0].bookId == $book_id and .[0].status == "Reading"' \
  --arg book_id "$book_id"

progress_payload="$("$jq_bin" -nc \
  --arg chapter_id "$chapter_id" \
  '{chapterId: $chapter_id, paragraphIndex: 0, progressPercent: 37}')"
request PUT "/api/v1/me/reading/progress/$book_id" "$access_token" \
  "$progress_payload" "$work_dir/progress-put.json" 200
assert_json "$work_dir/progress-put.json" \
  '.bookId == $book_id and .chapterId == $chapter_id and .paragraphIndex == 0 and .progressPercent == 37' \
  --arg book_id "$book_id" --arg chapter_id "$chapter_id"
request GET "/api/v1/me/reading/progress/$book_id" "$access_token" '__NO_BODY__' "$work_dir/progress-get.json" 200
assert_json "$work_dir/progress-get.json" \
  '.bookId == $book_id and .chapterId == $chapter_id and .progressPercent == 37' \
  --arg book_id "$book_id" --arg chapter_id "$chapter_id"
request PUT "/api/v1/me/reading/progress/$book_id" "$access_token" \
  '{"chapterId":"00000000-0000-0000-0000-000000000001","paragraphIndex":0,"progressPercent":10}' \
  "$work_dir/invalid-progress.json" 404

request GET '/api/v1/me/reading/shelf?limit=100' "$access_token" '__NO_BODY__' "$work_dir/shelf-after-progress.json" 200
assert_json "$work_dir/shelf-after-progress.json" \
  'length == 1 and .[0].bookId == $book_id and .[0].currentChapterId == $chapter_id and .[0].progressPercent == 37' \
  --arg book_id "$book_id" --arg chapter_id "$chapter_id"
request GET '/api/v1/me/reading/history?limit=100' "$access_token" '__NO_BODY__' "$work_dir/history-after-progress.json" 200
assert_json "$work_dir/history-after-progress.json" \
  'length == 1 and .[0].bookId == $book_id and .[0].chapterId == $chapter_id' \
  --arg book_id "$book_id" --arg chapter_id "$chapter_id"

request POST /api/v1/me/legado/tokens "$access_token" \
  '{"name":"runtime smoke token"}' "$work_dir/legado-token.json" 201
assert_json "$work_dir/legado-token.json" \
  '.id != null and (.token | strings | length > 0) and (.bookSource | type == "object")'
legado_token_id="$($jq_bin -er '.id | strings | select(length > 0)' "$work_dir/legado-token.json")"
request GET /api/v1/me/legado/tokens "$access_token" '__NO_BODY__' \
  "$work_dir/legado-token-list.json" 200
assert_json "$work_dir/legado-token-list.json" \
  'length == 1 and .[0].id == $token_id and .[0].revokedAt == null' \
  --arg token_id "$legado_token_id"
request DELETE "/api/v1/me/legado/tokens/$legado_token_id" "$access_token" \
  '__NO_BODY__' "$work_dir/legado-token-revoke.json" 204
request GET /api/v1/me/legado/tokens "$access_token" '__NO_BODY__' \
  "$work_dir/legado-token-list-after-revoke.json" 200
assert_json "$work_dir/legado-token-list-after-revoke.json" \
  'type == "array" and length == 0' || fail '撤销后令牌记录未删除'

request DELETE "/api/v1/me/reading/shelf/$book_id" "$access_token" \
  '__NO_BODY__' "$work_dir/shelf-delete.json" 204
request GET '/api/v1/me/reading/shelf?limit=100' "$access_token" '__NO_BODY__' "$work_dir/empty-shelf-after-delete.json" 200
assert_json "$work_dir/empty-shelf-after-delete.json" 'type == "array" and length == 0'

request POST /api/v1/auth/logout "$access_token" '__NO_BODY__' "$work_dir/logout.json" 204
access_token=""
request GET /api/v1/auth/me '' '__NO_BODY__' "$work_dir/me-after-logout.json" 401
request GET /api/v1/me/reading/shelf '' '__NO_BODY__' "$work_dir/shelf-after-logout.json" 401

printf 'reader-account-runtime-smoke: PASS (register, login, refresh rotation, logout, preferences, shelf, progress, history, Legado token revoke-and-delete)\n'
