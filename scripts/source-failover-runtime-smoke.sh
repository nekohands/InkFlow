#!/usr/bin/env bash
set -Eeuo pipefail

export LC_ALL=C

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck disable=SC1091
source "$script_dir/load-local-env.sh"

base_url="${1:-${INKFLOW_FAILOVER_RUNTIME_SMOKE_BASE_URL:-http://localhost:8080}}"
admin_token="${INKFLOW_FAILOVER_RUNTIME_SMOKE_ADMIN_TOKEN:-}"
book_id="${INKFLOW_FAILOVER_RUNTIME_SMOKE_BOOK_ID:-}"
chapter_id="${INKFLOW_FAILOVER_RUNTIME_SMOKE_CHAPTER_ID:-}"
source_a_id="${INKFLOW_FAILOVER_RUNTIME_SMOKE_SOURCE_A_ID:-inkflow-failover-a}"
source_b_id="${INKFLOW_FAILOVER_RUNTIME_SMOKE_SOURCE_B_ID:-inkflow-failover-b}"
source_a_marker="${INKFLOW_FAILOVER_RUNTIME_SMOKE_SOURCE_A_MARKER:-InkFlow failover source A marker}"
source_b_marker="${INKFLOW_FAILOVER_RUNTIME_SMOKE_SOURCE_B_MARKER:-InkFlow failover source B marker}"
max_time="${INKFLOW_FAILOVER_RUNTIME_SMOKE_CURL_MAX_TIME:-10}"
curl_bin="${INKFLOW_FAILOVER_RUNTIME_SMOKE_CURL_BIN:-curl}"
jq_bin="${INKFLOW_FAILOVER_RUNTIME_SMOKE_JQ_BIN:-jq}"
work_dir=""

fail() {
  printf 'source-failover-runtime-smoke: %s\n' "$1" >&2
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

if [[ -z "$admin_token" || -z "$book_id" || -z "$chapter_id" ]]; then
  fail 'admin token, canonical book id, and canonical chapter id must be supplied through environment variables'
fi

for id in "$book_id" "$chapter_id"; do
  if ! [[ "$id" =~ ^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-8][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}$ ]]; then
    fail 'book and chapter ids must be GUIDs'
  fi
done

for source_id in "$source_a_id" "$source_b_id"; do
  if ! [[ "$source_id" =~ ^[A-Za-z0-9][A-Za-z0-9._-]*$ ]]; then
    fail 'source ids must contain only letters, digits, dots, underscores, and hyphens'
  fi
done

if [[ -z "$source_a_marker" || -z "$source_b_marker" ]]; then
  fail 'source markers must not be empty'
fi

if ! [[ "$max_time" =~ ^[1-9][0-9]*$ ]] || (( max_time > 60 )); then
  fail 'INKFLOW_FAILOVER_RUNTIME_SMOKE_CURL_MAX_TIME must be an integer from 1 to 60'
fi

for executable in "$curl_bin" "$jq_bin"; do
  if ! command -v "$executable" >/dev/null 2>&1; then
    fail "required executable not found: $executable"
  fi
done

work_dir="$(mktemp -d "${TMPDIR:-/tmp}/inkflow-failover-runtime.XXXXXX")"

cleanup() {
  local status=$?
  set +e

  if [[ -n "$admin_token" ]]; then
    for source_id in "$source_a_id" "$source_b_id"; do
      "$curl_bin" --silent --show-error --max-time "$max_time" \
        --request POST \
        -H "Authorization: Bearer $admin_token" \
        -H 'Content-Type: application/json' \
        --data '{"reason":"runtime failover smoke cleanup"}' \
        "$base_url/api/v1/admin/sources/$source_id/health/content/enable" \
        >/dev/null 2>&1 || true
    done
  fi

  if [[ -n "$work_dir" ]]; then
    rm -rf -- "$work_dir"
  fi
  exit "$status"
}
trap cleanup EXIT

request_get() {
  local route="$1"
  local output="$2"
  shift 2

  local status
  if ! status="$("$curl_bin" \
    --silent --show-error --max-time "$max_time" \
    "$@" \
    --output "$output" \
    --write-out '%{http_code}' \
    "$base_url$route")"; then
    fail "GET $route could not be completed"
  fi

  if [[ "$status" != "200" ]]; then
    fail "GET $route returned HTTP $status; expected 200"
  fi
}

request_post() {
  local route="$1"
  local payload="$2"
  local output="$3"

  local status
  if ! status="$("$curl_bin" \
    --silent --show-error --max-time "$max_time" \
    --request POST \
    -H "Authorization: Bearer $admin_token" \
    -H 'Content-Type: application/json' \
    --data "$payload" \
    --output "$output" \
    --write-out '%{http_code}' \
    "$base_url$route")"; then
    fail "POST $route could not be completed"
  fi

  if [[ "$status" != "200" ]]; then
    fail "POST $route returned HTTP $status; expected 200"
  fi
}

assert_json() {
  local expression="$1"
  local file="$2"
  local message="$3"
  "$jq_bin" -e "$expression" "$file" >/dev/null || fail "$message"
}

assert_json_args() {
  local expression="$1"
  local file="$2"
  local message="$3"
  shift 3

  "$jq_bin" -e "$@" "$expression" "$file" >/dev/null || fail "$message"
}

assert_web_state() {
  local expected_source="$1"
  local expected_marker="$2"
  local state_label="$3"

  request_get "/api/v1/books/$book_id" "$work_dir/web-book-$state_label.json"
  assert_json_args \
    '.id == $book and (.chapters | type == "array") and any(.chapters[]; .chapterId == $chapter and .index == 0)' \
    "$work_dir/web-book-$state_label.json" \
    "Web catalog changed canonical identity during $state_label state" \
    --arg book "$book_id" \
    --arg chapter "$chapter_id"

  request_get "/api/v1/chapters/$chapter_id/content" "$work_dir/web-content-$state_label.json"
  assert_json_args \
    '.chapterId == $chapter and .bookId == $book and .sourceId == $source and (.paragraphs | type == "array" and (join("\n") | contains($marker)))' \
    "$work_dir/web-content-$state_label.json" \
    "Web content did not select the expected source during $state_label state" \
    --arg book "$book_id" \
    --arg chapter "$chapter_id" \
    --arg source "$expected_source" \
    --arg marker "$expected_marker"
}

assert_legado_state() {
  local expected_marker="$1"
  local state_label="$2"
  local output="$work_dir/legado-$state_label.log"

  if ! env \
    "INKFLOW_LEGADO_RUNTIME_SMOKE_CURL_BIN=$curl_bin" \
    "INKFLOW_LEGADO_RUNTIME_SMOKE_JQ_BIN=$jq_bin" \
    "INKFLOW_LEGADO_RUNTIME_SMOKE_CURL_MAX_TIME=$max_time" \
    "INKFLOW_LEGADO_RUNTIME_SMOKE_BOOK_ID=$book_id" \
    "INKFLOW_LEGADO_RUNTIME_SMOKE_CHAPTER_ID=$chapter_id" \
    "INKFLOW_LEGADO_RUNTIME_SMOKE_MARKER=$expected_marker" \
    bash "$script_dir/legado-runtime-smoke.sh" "$base_url" >"$output" 2>&1; then
    cat "$output" >&2
    fail "Legado chain failed during $state_label state"
  fi

  grep -Fq 'legado-runtime-smoke: PASS' "$output" \
    || fail "Legado smoke did not report success during $state_label state"
}

assert_web_state "$source_a_id" "$source_a_marker" initial
assert_legado_state "$source_a_marker" initial

request_post \
  "/api/v1/admin/sources/$source_a_id/health/content/disable" \
  '{"reason":"runtime failover disable source A"}' \
  "$work_dir/disable.json"
  assert_json_args \
    '.status == "applied" and .health.sourceId == $source and .health.capability == "Content" and .health.status == "Disabled" and .health.isAvailable == false' \
    "$work_dir/disable.json" \
    'source A was not disabled for Content capability' \
    --arg source "$source_a_id"

assert_web_state "$source_b_id" "$source_b_marker" failover
assert_legado_state "$source_b_marker" failover

request_post \
  "/api/v1/admin/sources/$source_a_id/health/content/enable" \
  '{"reason":"runtime failover restore source A"}' \
  "$work_dir/enable.json"
  assert_json_args \
    '.status == "applied" and .health.sourceId == $source and .health.capability == "Content" and .health.status == "Unknown" and .health.isAvailable == true' \
    "$work_dir/enable.json" \
    'source A was not restored for Content capability' \
    --arg source "$source_a_id"

assert_web_state "$source_a_id" "$source_a_marker" restored
assert_legado_state "$source_a_marker" restored

printf 'source-failover-runtime-smoke: PASS (stable Web/Legado identities, A→B failover, A recovery)\n'
