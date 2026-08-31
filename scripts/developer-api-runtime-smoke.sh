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
disable_user_helper="${INKFLOW_DEVELOPER_API_SMOKE_DISABLE_USER_HELPER:-$script_dir/disable-acceptance-user.sh}"
work_dir=""
email=""
user_token=""
application_id=""
issued_key_id=""
issued_raw_key=""
rotated_key_id=""
rotated_raw_key=""
other_user_token=""
other_application_id=""
other_raw_key=""
other_email=""
quota_raw_keys=()

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

if [[ ! -f "$disable_user_helper" ]]; then
  fail "disabled-user helper not found: $disable_user_helper"
fi

work_dir="$(mktemp -d "${TMPDIR:-/tmp}/inkflow-developer-api.XXXXXX")"

cleanup() {
  local status=$?
  set +e

  # There is intentionally no account-delete API. Revoke the application so
  # a failed smoke run cannot leave an active developer credential behind.
  if [[ -n "$other_user_token" && -n "$other_application_id" ]]; then
    "$curl_bin" --silent --show-error --max-time "$max_time" \
      --request DELETE \
      -H "Authorization: Bearer $other_user_token" \
      "$base_url/api/v1/me/developer-applications/$other_application_id" \
      >/dev/null 2>&1 || true
  fi

  if [[ -n "$user_token" && -n "$application_id" ]]; then
    "$curl_bin" --silent --show-error --max-time "$max_time" \
      --request DELETE \
      -H "Authorization: Bearer $user_token" \
      "$base_url/api/v1/me/developer-applications/$application_id" \
      >/dev/null 2>&1 || true
  fi

  # Keep generated accounts from remaining active. The helper is deliberately
  # an explicit local/CI seam because disabling a user is an infrastructure
  # fixture operation, not a public product API.
  if [[ -n "$other_email" ]]; then
    bash "$disable_user_helper" "$other_email" >/dev/null 2>&1 || true
  fi
  if [[ -n "$email" ]]; then
    bash "$disable_user_helper" "$email" >/dev/null 2>&1 || true
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

quota_raw_keys=("$rotated_raw_key")
for quota_key_index in 1 2 3; do
  quota_key_payload="$("$jq_bin" -nc \
    --arg name "CI Developer Quota Key $quota_key_index" \
    '{name: $name}')"
  post_json "/api/v1/me/developer-applications/$application_id/keys" "$user_token" \
    "$quota_key_payload" "$work_dir/quota-key-$quota_key_index.json"
  quota_raw_keys+=("$("$jq_bin" -er '.apiKey' "$work_dir/quota-key-$quota_key_index.json")")
done

get_json /api/v1/me/entitlement "$user_token" "$work_dir/quota-entitlement-before.json"
quota_remaining="$("$jq_bin" -er '.quota.remainingUnits' "$work_dir/quota-entitlement-before.json")"
quota_cost=5
quota_success_count=$((quota_remaining / quota_cost))
if (( quota_success_count < 1 || quota_success_count > 1000 )); then
  fail "free quota remaining units produced an unsafe smoke count: $quota_remaining"
fi

quota_route='/api/developer/v1/chapters/00000000-0000-4000-8000-000000000001/content'
quota_response="$work_dir/quota-response.json"
for ((quota_request_index = 0; quota_request_index < quota_success_count; quota_request_index++)); do
  quota_key="${quota_raw_keys[$((quota_request_index % ${#quota_raw_keys[@]}))]}"
  expect_developer_status "$quota_route" "$quota_key" 404 "$quota_response"
done

quota_exceeded_key="${quota_raw_keys[$((quota_success_count % ${#quota_raw_keys[@]}))]}"
expect_developer_status "$quota_route" "$quota_exceeded_key" 429 \
  "$work_dir/quota-exceeded.json" --dump-header "$work_dir/quota-exceeded.headers"
"$jq_bin" -e \
  '.error == "quota_exceeded" and (.periodEnd | type == "string") and (.remainingUnits | type == "number")' \
  "$work_dir/quota-exceeded.json" >/dev/null || fail 'quota overage response is invalid'
if ! grep -Eiq '^Retry-After:[[:space:]]*[1-9][0-9]*[[:space:]]*$' \
  "$work_dir/quota-exceeded.headers"; then
  fail 'quota overage response is missing a positive Retry-After header'
fi

other_email="ci-developer-isolation-${run_id}@example.com"
other_registration_payload="$("$jq_bin" -nc \
  --arg email "$other_email" \
  --arg password "$test_password" \
  '{email: $email, password: $password}')"
post_json /api/v1/auth/register '' "$other_registration_payload" "$work_dir/other-register.json"
other_user_token="$("$jq_bin" -er '.access_token' "$work_dir/other-register.json")"

get_json /api/v1/me/entitlement "$other_user_token" "$work_dir/other-entitlement-before.json"
"$jq_bin" -e \
  '.plan.code == "free" and .quota.usedUnits == 0 and .quota.remainingUnits == .quota.limitUnits' \
  "$work_dir/other-entitlement-before.json" >/dev/null || fail 'a second user did not receive an isolated fresh quota'

post_json /api/v1/me/developer-applications/ "$other_user_token" \
  '{"name":"CI Developer Isolation Smoke"}' "$work_dir/other-application-create.json"
other_application_id="$("$jq_bin" -er '.applicationId' "$work_dir/other-application-create.json")"
post_json "/api/v1/me/developer-applications/$other_application_id/keys" "$other_user_token" \
  '{"name":"CI Developer Isolation Key"}' "$work_dir/other-key-create.json"
other_raw_key="$("$jq_bin" -er '.apiKey' "$work_dir/other-key-create.json")"
expect_developer_status '/api/developer/v1/books?limit=1' "$other_raw_key" 200 \
  "$work_dir/other-catalog.json"
"$jq_bin" -e 'type == "array"' "$work_dir/other-catalog.json" >/dev/null || \
  fail 'a second user could not access the developer catalog after the first user exhausted quota'

if ! bash "$disable_user_helper" "$other_email" >/dev/null 2>&1; then
  fail 'disabled-user fixture could not disable the second smoke account'
fi
expect_status /api/v1/me/entitlement 401 "$work_dir/other-entitlement-disabled.json" \
  -H "Authorization: Bearer $other_user_token"
expect_status '/api/developer/v1/books?limit=1' 401 "$work_dir/other-catalog-disabled.json" \
  -H "X-InkFlow-Api-Key: $other_raw_key"

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

printf 'developer-api-runtime-smoke: PASS (account, entitlement, app/key lifecycle, redaction, header-only auth, catalog quota path, quota 429/retry-after, cross-account isolation, disabled-user rejection, rotation, revoke)\n'
