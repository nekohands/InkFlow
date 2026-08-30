#!/usr/bin/env bash
set -Eeuo pipefail

export LC_ALL=C

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck disable=SC1091
source "$script_dir/load-local-env.sh"

base_url="${1:-${INKFLOW_ADMIN_RUNTIME_SMOKE_BASE_URL:-http://localhost:8080}}"
admin_token="${INKFLOW_ADMIN_RUNTIME_SMOKE_ADMIN_TOKEN:-}"
operator_token="${INKFLOW_ADMIN_RUNTIME_SMOKE_OPERATOR_TOKEN:-}"
operator_id="${INKFLOW_ADMIN_RUNTIME_SMOKE_OPERATOR_ID:-}"
book_id="${INKFLOW_ADMIN_RUNTIME_SMOKE_BOOK_ID:-}"
source_id="${INKFLOW_ADMIN_RUNTIME_SMOKE_SOURCE_ID:-inkflow-acceptance}"
max_time="${INKFLOW_ADMIN_RUNTIME_SMOKE_CURL_MAX_TIME:-10}"
curl_bin="${INKFLOW_ADMIN_RUNTIME_SMOKE_CURL_BIN:-curl}"
jq_bin="${INKFLOW_ADMIN_RUNTIME_SMOKE_JQ_BIN:-jq}"
work_dir=""
grant_id=""

fail() {
  printf 'admin-runtime-smoke: %s\n' "$1" >&2
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

if [[ -z "$admin_token" || -z "$operator_token" || -z "$operator_id" || -z "$book_id" ]]; then
  fail 'admin/operator tokens, operator id, and book id must be supplied through environment variables'
fi

if ! [[ "$max_time" =~ ^[1-9][0-9]*$ ]] || (( max_time > 60 )); then
  fail 'INKFLOW_ADMIN_RUNTIME_SMOKE_CURL_MAX_TIME must be an integer from 1 to 60'
fi

if ! command -v "$curl_bin" >/dev/null 2>&1; then
  fail "curl executable not found: $curl_bin"
fi

if ! command -v "$jq_bin" >/dev/null 2>&1; then
  fail "jq executable not found: $jq_bin"
fi

work_dir="$(mktemp -d "${TMPDIR:-/tmp}/inkflow-admin-runtime.XXXXXX")"

cleanup() {
  local status=$?
  set +e

  if [[ -n "$grant_id" ]]; then
    "$curl_bin" --silent --show-error --max-time "$max_time" \
      --request DELETE \
      -H "Authorization: Bearer $admin_token" \
      "$base_url/api/v1/admin/sources/$source_id/permissions/$grant_id?reason=runtime-smoke-cleanup" \
      >/dev/null 2>&1 || true
  fi

  if [[ -n "$admin_token" ]]; then
    "$curl_bin" --silent --show-error --max-time "$max_time" --request POST -H "Authorization: Bearer $admin_token" -H 'Content-Type: application/json' --data '{"reason":"runtime smoke cleanup"}' "$base_url/api/v1/admin/sources/$source_id/health/search/enable" >/dev/null 2>&1 || true
    "$curl_bin" --silent --show-error --max-time "$max_time" --request PUT -H "Authorization: Bearer $admin_token" -H 'Content-Type: application/json' --data '{"credentialReferenceId":null,"reason":"runtime smoke cleanup"}' "$base_url/api/v1/admin/sources/$source_id/credential-binding" >/dev/null 2>&1 || true
    "$curl_bin" --silent --show-error --max-time "$max_time" --request POST -H "Authorization: Bearer $admin_token" -H 'Content-Type: application/json' --data '{"reason":"runtime smoke cleanup"}' "$base_url/api/v1/admin/content/takedowns/$book_id/restore" >/dev/null 2>&1 || true
  fi

  if [[ -n "$work_dir" ]]; then
    rm -rf -- "$work_dir"
  fi
  exit "$status"
}
trap cleanup EXIT

expect_status() {
  local route="$1"
  local expected="$2"
  local output="$3"
  shift 3

  local status
  if ! status="$("$curl_bin" --silent --show-error --max-time "$max_time" \
    "$@" --output "$output" --write-out '%{http_code}' "$base_url$route")"; then
    fail "request $route could not be completed"
  fi

  if [[ "$status" != "$expected" ]]; then
    fail "request $route returned HTTP $status; expected $expected"
  fi
}

get_json() {
  local route="$1"
  local token="$2"
  local output="$3"
  expect_status "$route" 200 "$output" -H "Authorization: Bearer $token"
}

post_json() {
  local route="$1"
  local token="$2"
  local payload="$3"
  local output="$4"
  expect_status "$route" 200 "$output" \
    -H "Authorization: Bearer $token" \
    -H 'Content-Type: application/json' \
    --request POST --data "$payload"
}

put_json() {
  local route="$1"
  local token="$2"
  local payload="$3"
  local output="$4"
  expect_status "$route" 200 "$output" \
    -H "Authorization: Bearer $token" \
    -H 'Content-Type: application/json' \
    --request PUT --data "$payload"
}

expect_status /api/v1/admin/plans 200 "$work_dir/plans.json" \
  -H "Authorization: Bearer $admin_token"
"$jq_bin" -e 'any(.[]; .code == "free") and any(.[]; .code == "pro")' \
  "$work_dir/plans.json" >/dev/null || fail 'admin plan list is incomplete'

get_json "/api/v1/admin/operations/overview?limit=10" "$admin_token" \
  "$work_dir/operations.json"
"$jq_bin" -e \
  '.status != null and .sources.status != null and .crawler.status != null and .consistency.status != null' \
  "$work_dir/operations.json" >/dev/null || fail 'operations overview shape is invalid'

get_json "/api/v1/admin/operations/alerts?limit=10" "$admin_token" \
  "$work_dir/alerts.json"
"$jq_bin" -e '.status != null and (.alerts | type == "array")' \
  "$work_dir/alerts.json" >/dev/null || fail 'operations alert shape is invalid'

get_json "/api/v1/admin/operations/alerts/history?limit=10" "$admin_token" \
  "$work_dir/alert-history.json"
"$jq_bin" -e '(.entries | type == "array") and (has("nextCursor"))' \
  "$work_dir/alert-history.json" >/dev/null || fail 'operations alert history shape is invalid'

get_json "/api/v1/admin/audit/events?limit=10" "$admin_token" \
  "$work_dir/audit-before.json"
"$jq_bin" -e '(.events | type == "array") and (has("nextCursor"))' \
  "$work_dir/audit-before.json" >/dev/null || fail 'audit page shape is invalid'

expect_status "/api/v1/admin/sources/$source_id/health" 403 \
  "$work_dir/operator-health-before.json" \
  -H "Authorization: Bearer $operator_token"

post_json "/api/v1/admin/sources/$source_id/permissions" "$admin_token" \
  "$("$jq_bin" -nc --arg user_id "$operator_id" \
    '{userId: $user_id, permission: "source.manage", reason: "runtime smoke grant"}')" \
  "$work_dir/grant.json"
grant_id="$("$jq_bin" -er '.grant.id' "$work_dir/grant.json")"
"$jq_bin" -e \
  --arg user_id "$operator_id" \
  '.status == "success" and .grant.userId == $user_id and .grant.permission == "source.manage" and .grant.isActive == true' \
  "$work_dir/grant.json" >/dev/null || fail 'source permission grant response is invalid'

post_json "/api/v1/admin/sources/$source_id/permissions" "$admin_token" \
  "$("$jq_bin" -nc --arg user_id "$operator_id" \
    '{userId: $user_id, permission: "source.manage", reason: "runtime smoke idempotency"}')" \
  "$work_dir/grant-repeat.json"
"$jq_bin" -e '.status == "alreadygranted" and .grant.id != null' \
  "$work_dir/grant-repeat.json" >/dev/null || fail 'source permission grant was not idempotent'

get_json "/api/v1/admin/sources/$source_id/permissions?limit=10" "$admin_token" \
  "$work_dir/permissions.json"
"$jq_bin" -e \
  --arg grant_id "$grant_id" \
  'any(.[]; .id == $grant_id and .permission == "source.manage" and .isActive == true)' \
  "$work_dir/permissions.json" >/dev/null || fail 'active source grant is missing from list'

get_json "/api/v1/admin/sources/$source_id/health" "$operator_token" \
  "$work_dir/operator-health-after.json"
"$jq_bin" -e 'type == "array"' "$work_dir/operator-health-after.json" >/dev/null || \
  fail 'authorized operator health response is not an array'

post_json "/api/v1/admin/sources/$source_id/health/search/disable" "$operator_token" \
  '{"reason":"runtime smoke disable"}' "$work_dir/health-disabled.json"
"$jq_bin" -e '.status == "applied" and .health.status == "Disabled" and .health.isAvailable == false' \
  "$work_dir/health-disabled.json" >/dev/null || fail 'source disable response is invalid'

post_json "/api/v1/admin/sources/$source_id/health/search/enable" "$operator_token" \
  '{"reason":"runtime smoke restore"}' "$work_dir/health-enabled.json"
"$jq_bin" -e '.status == "applied" and .health.status == "Unknown" and .health.isAvailable == true' \
  "$work_dir/health-enabled.json" >/dev/null || fail 'source enable response is invalid'

put_json "/api/v1/admin/sources/$source_id/credential-binding" "$admin_token" \
  '{"credentialReferenceId":"runtime-smoke-reference","reason":"runtime smoke binding"}' \
  "$work_dir/credential-set.json"
"$jq_bin" -e \
  '.status == "updated" and .credentialReferenceId == "runtime-smoke-reference"' \
  "$work_dir/credential-set.json" >/dev/null || fail 'source credential binding set response is invalid'

put_json "/api/v1/admin/sources/$source_id/credential-binding" "$admin_token" \
  '{"credentialReferenceId":null,"reason":"runtime smoke clear"}' \
  "$work_dir/credential-clear.json"
"$jq_bin" -e '.status == "cleared" and .credentialReferenceId == null' \
  "$work_dir/credential-clear.json" >/dev/null || fail 'source credential binding clear response is invalid'

post_json /api/v1/admin/content/takedowns "$admin_token" \
  "$("$jq_bin" -nc --arg book_id "$book_id" \
    '{bookId: $book_id, reason: "runtime smoke takedown"}')" \
  "$work_dir/takedown.json"
"$jq_bin" -e \
  --arg book_id "$book_id" \
  '.status == "applied" and .action == "takedown" and .isTakedown == true and .bookId == $book_id' \
  "$work_dir/takedown.json" >/dev/null || fail 'content takedown response is invalid'

expect_status "/api/v1/books/$book_id" 404 "$work_dir/book-after-takedown.json"

post_json "/api/v1/admin/content/takedowns/$book_id/restore" "$admin_token" \
  '{"reason":"runtime smoke restore"}' "$work_dir/restore.json"
"$jq_bin" -e \
  --arg book_id "$book_id" \
  '.status == "applied" and .action == "restore" and .isTakedown == false and .bookId == $book_id' \
  "$work_dir/restore.json" >/dev/null || fail 'content restore response is invalid'

get_json "/api/v1/books/$book_id" "$admin_token" "$work_dir/book-after-restore.json"
"$jq_bin" -e --arg book_id "$book_id" '.id == $book_id' \
  "$work_dir/book-after-restore.json" >/dev/null || fail 'restored book is not visible'

put_json "/api/v1/admin/users/$operator_id/entitlement" "$admin_token" \
  '{"planCode":"pro","reason":"runtime smoke plan assignment"}' \
  "$work_dir/entitlement.json"
"$jq_bin" -e --arg user_id "$operator_id" \
  '.userId == $user_id and .plan.code == "pro" and .plan.monthlyQuotaUnits > 1000' \
  "$work_dir/entitlement.json" >/dev/null || fail 'admin entitlement assignment response is invalid'

get_json /api/v1/me/entitlement "$operator_token" "$work_dir/operator-entitlement.json"
"$jq_bin" -e '.plan.code == "pro" and .quota.limitUnits > 1000' \
  "$work_dir/operator-entitlement.json" >/dev/null || fail 'assigned entitlement is not visible to operator'

get_json "/api/v1/admin/audit/events?action=content.policy.takedown&limit=10" \
  "$admin_token" "$work_dir/audit-policy.json"
"$jq_bin" -e \
  'any(.events[]; .action == "content.policy.takedown" and .outcome == "success")' \
  "$work_dir/audit-policy.json" >/dev/null || fail 'content policy audit event is missing'

if ! "$curl_bin" --silent --show-error --fail --max-time "$max_time" \
  --request DELETE \
  -H "Authorization: Bearer $admin_token" \
  "$base_url/api/v1/admin/sources/$source_id/permissions/$grant_id?reason=runtime-smoke-revoke" \
  --output "$work_dir/revoke.json"; then
  fail 'source permission revoke failed'
fi
grant_id=''
"$jq_bin" -e '.status == "success" and .grant.revokedAt != null and .grant.isActive == false' \
  "$work_dir/revoke.json" >/dev/null || fail 'source permission revoke response is invalid'

expect_status "/api/v1/admin/sources/$source_id/health" 403 \
  "$work_dir/operator-health-after-revoke.json" \
  -H "Authorization: Bearer $operator_token"

printf 'admin-runtime-smoke: PASS (admin/operations, audit, source permissions and health, credential binding, content policy, entitlement)\n'
