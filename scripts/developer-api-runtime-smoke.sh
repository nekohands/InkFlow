#!/usr/bin/env bash
set -Eeuo pipefail

export LC_ALL=C

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck disable=SC1091
source "$script_dir/load-local-env.sh"

base_url="${1:-${INKFLOW_DEVELOPER_API_SMOKE_BASE_URL:-http://localhost:8080}}"
max_time="${INKFLOW_DEVELOPER_API_SMOKE_CURL_MAX_TIME:-10}"
curl_bin="${INKFLOW_DEVELOPER_API_SMOKE_CURL_BIN:-curl}"
jq_bin="${INKFLOW_DEVELOPER_API_SMOKE_JQ_BIN:-jq}"
work_dir=""
user_token=""
application_id=""
issued_key_id=""
issued_raw_key=""
rotated_key_id=""
rotated_raw_key=""

fail() {
  printf 'developer-api-runtime-smoke: %s\n' "$1" >&2
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
  fail 'INKFLOW_DEVELOPER_API_SMOKE_CURL_MAX_TIME must be an integer from 1 to 60'
fi

if ! command -v "$curl_bin" >/dev/null 2>&1; then
  fail "curl executable not found: $curl_bin"
fi

if ! command -v "$jq_bin" >/dev/null 2>&1; then
  fail "jq executable not found: $jq_bin"
fi

work_dir="$(mktemp -d "${TMPDIR:-/tmp}/inkflow-developer-api.XXXXXX")"

cleanup() {
  local status=$?
  set +e

  # There is intentionally no account-delete API. Revoke the application so
  # a failed smoke run cannot leave an active developer credential behind.
  if [[ -n "$user_token" && -n "$application_id" ]]; then
    "$curl_bin" --silent --show-error --max-time "$max_time" \
      --request DELETE \
      -H "Authorization: Bearer $user_token" \
      "$base_url/api/v1/me/developer-applications/$application_id" \
      >/dev/null 2>&1 || true
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
    --request POST \
    "${headers[@]}" \
    --data "$payload" \
    --output "$output" \
    "$base_url$route"; then
    fail "POST $route failed"
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

get_developer_json() {
  local route="$1"
  local raw_key="$2"
  local output="$3"
  local response_headers="$4"

  if ! "$curl_bin" \
    --silent --show-error --fail \
    --max-time "$max_time" \
    -H "X-InkFlow-Api-Key: $raw_key" \
    --dump-header "$response_headers" \
    --output "$output" \
    "$base_url$route"; then
    fail "GET $route with developer API key failed"
  fi
}

expect_status() {
  local route="$1"
  local expected="$2"
  local output="$3"
  shift 3

  local status
  if ! status="$("$curl_bin" \
    --silent --show-error \
    --max-time "$max_time" \
    "$@" \
    --output "$output" \
    --write-out '%{http_code}' \
    "$base_url$route")"; then
    fail "request $route could not be completed"
  fi

  if [[ "$status" != "$expected" ]]; then
    fail "request $route returned HTTP $status; expected $expected"
  fi
}

expect_developer_status() {
  local route="$1"
  local raw_key="$2"
  local expected="$3"
  local output="$4"
  shift 4

  expect_status "$route" "$expected" "$output" \
    -H "X-InkFlow-Api-Key: $raw_key" "$@"
}

run_id="$(date -u +%s%N)-$$-${RANDOM}"
test_password='correct horse battery staple'
email="ci-developer-${run_id}@example.com"

expect_status /api/developer/v1/books?limit=1 401 "$work_dir/unauthenticated.json"

registration_payload="$("$jq_bin" -nc \
  --arg email "$email" \
  --arg password "$test_password" \
  '{email: $email, password: $password}')"
post_json /api/v1/auth/register '' "$registration_payload" "$work_dir/register.json"
user_token="$("$jq_bin" -er '.access_token' "$work_dir/register.json")"

get_json /api/v1/me/entitlement "$user_token" "$work_dir/entitlement.json"
"$jq_bin" -e \
  '.plan.code == "free" and (.plan.entitlements | index("developer.catalog.read") != null) and .quota.limitUnits >= 1000' \
  "$work_dir/entitlement.json" >/dev/null || fail 'free developer catalog entitlement is not available'

post_json /api/v1/me/developer-applications/ "$user_token" \
  '{"name":"CI Developer API Smoke"}' "$work_dir/application-create.json"
application_id="$("$jq_bin" -er '.applicationId' "$work_dir/application-create.json")"
"$jq_bin" -e \
  --arg application_id "$application_id" \
  '.applicationId == $application_id and .environment == "production" and .revokedAt == null' \
  "$work_dir/application-create.json" >/dev/null || fail 'application creation response is invalid'

get_json /api/v1/me/developer-applications/ "$user_token" "$work_dir/application-list.json"
"$jq_bin" -e \
  --arg application_id "$application_id" \
  'any(.[]; .applicationId == $application_id and .revokedAt == null)' \
  "$work_dir/application-list.json" >/dev/null || fail 'created application is missing from the owner list'

post_json "/api/v1/me/developer-applications/$application_id/keys" "$user_token" \
  '{"name":"CI Developer API Key"}' "$work_dir/key-create.json"
issued_key_id="$("$jq_bin" -er '.key.keyId' "$work_dir/key-create.json")"
issued_raw_key="$("$jq_bin" -er '.apiKey' "$work_dir/key-create.json")"
"$jq_bin" -e \
  --arg key_id "$issued_key_id" \
  '.key.keyId == $key_id and (.apiKey | startswith("lf_dev_")) and .key.scope == "catalog.read" and .key.revokedAt == null' \
  "$work_dir/key-create.json" >/dev/null || fail 'API key issuance response is invalid'

get_json "/api/v1/me/developer-applications/$application_id/keys" "$user_token" "$work_dir/key-list-before.json"
"$jq_bin" -e \
  --arg key_id "$issued_key_id" \
  'all(.[]; has("apiKey") | not) and any(.[]; .keyId == $key_id and .scope == "catalog.read" and .revokedAt == null)' \
  "$work_dir/key-list-before.json" >/dev/null || fail 'key list does not redact the raw key or omit the issued key'

get_developer_json '/api/developer/v1/books?limit=10' "$issued_raw_key" \
  "$work_dir/catalog-before.json" "$work_dir/catalog-before.headers"
"$jq_bin" -e 'type == "array"' "$work_dir/catalog-before.json" >/dev/null || fail 'developer catalog list is not an array'
if ! grep -Eiq '^Cache-Control:[[:space:]]*private,[[:space:]]*no-store[[:space:]]*$' "$work_dir/catalog-before.headers"; then
  fail 'developer catalog response is missing private no-store cache control'
fi

expect_status '/api/developer/v1/books?limit=1&api_key=not-a-header-credential' 401 \
  "$work_dir/query-credential.json"
expect_status '/api/developer/v1/books?limit=1' 401 "$work_dir/bearer-credential.json" \
  -H "Authorization: Bearer $issued_raw_key"

post_json "/api/v1/me/developer-applications/$application_id/keys/$issued_key_id/rotate" "$user_token" \
  '{}' "$work_dir/key-rotate.json"
rotated_key_id="$("$jq_bin" -er '.key.keyId' "$work_dir/key-rotate.json")"
rotated_raw_key="$("$jq_bin" -er '.apiKey' "$work_dir/key-rotate.json")"
"$jq_bin" -e \
  --arg old_key_id "$issued_key_id" \
  --arg new_key_id "$rotated_key_id" \
  '.key.keyId == $new_key_id and $old_key_id != $new_key_id and (.apiKey | startswith("lf_dev_")) and .key.scope == "catalog.read"' \
  "$work_dir/key-rotate.json" >/dev/null || fail 'API key rotation response is invalid'
if [[ "$issued_raw_key" == "$rotated_raw_key" ]]; then
  fail 'API key rotation reused the old raw key'
fi

expect_developer_status '/api/developer/v1/books?limit=1' "$issued_raw_key" 401 \
  "$work_dir/old-key-after-rotation.json"
get_developer_json '/api/developer/v1/search?q=' "$rotated_raw_key" \
  "$work_dir/catalog-after-rotation.json" "$work_dir/catalog-after-rotation.headers"
"$jq_bin" -e 'type == "array"' "$work_dir/catalog-after-rotation.json" >/dev/null || fail 'rotated developer key cannot access catalog search'

get_json "/api/v1/me/developer-applications/$application_id/keys" "$user_token" "$work_dir/key-list-after-rotation.json"
"$jq_bin" -e \
  --arg old_key_id "$issued_key_id" \
  --arg new_key_id "$rotated_key_id" \
  'all(.[]; has("apiKey") | not) and any(.[]; .keyId == $old_key_id and .revokedAt != null) and any(.[]; .keyId == $new_key_id and .revokedAt == null)' \
  "$work_dir/key-list-after-rotation.json" >/dev/null || fail 'key rotation state is not reflected in the key list'

expect_developer_status '/api/developer/v1/books?limit=1' "$rotated_raw_key" 200 \
  "$work_dir/catalog-before-revoke.json"
if ! "$curl_bin" --silent --show-error --fail --max-time "$max_time" \
  --request DELETE \
  -H "Authorization: Bearer $user_token" \
  "$base_url/api/v1/me/developer-applications/$application_id/keys/$rotated_key_id" \
  >/dev/null; then
  fail 'DELETE developer API key failed'
fi
expect_status '/api/developer/v1/books?limit=1' 401 "$work_dir/new-key-after-revoke.json" \
  -H "X-InkFlow-Api-Key: $rotated_raw_key"
rotated_raw_key=''

if ! "$curl_bin" --silent --show-error --fail --max-time "$max_time" \
  --request DELETE \
  -H "Authorization: Bearer $user_token" \
  "$base_url/api/v1/me/developer-applications/$application_id" \
  >/dev/null; then
  fail 'DELETE developer application failed'
fi
application_id=''

printf 'developer-api-runtime-smoke: PASS (account, entitlement, app/key lifecycle, redaction, header-only auth, catalog quota path, rotation, revoke)\n'
