#!/usr/bin/env bash
set -Eeuo pipefail

export LC_ALL=C

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck disable=SC1091
source "$script_dir/load-local-env.sh"

base_url="${1:-${INKFLOW_COLLECTION_PACKAGE_SMOKE_BASE_URL:-http://localhost:8080}}"
operator_token="${INKFLOW_COLLECTION_PACKAGE_SMOKE_OPERATOR_TOKEN:-}"
admin_token="${INKFLOW_COLLECTION_PACKAGE_SMOKE_ADMIN_TOKEN:-}"
reader_token="${INKFLOW_COLLECTION_PACKAGE_SMOKE_READER_TOKEN:-}"
book_id="${INKFLOW_COLLECTION_PACKAGE_SMOKE_BOOK_ID:-}"
pause_run_id="${INKFLOW_COLLECTION_PACKAGE_SMOKE_PAUSE_RUN_ID:-}"
stop_run_id="${INKFLOW_COLLECTION_PACKAGE_SMOKE_STOP_RUN_ID:-}"
cancel_run_id="${INKFLOW_COLLECTION_PACKAGE_SMOKE_CANCEL_RUN_ID:-}"
resume_run_id="${INKFLOW_COLLECTION_PACKAGE_SMOKE_RESUME_RUN_ID:-}"
collection_url="${INKFLOW_COLLECTION_PACKAGE_SMOKE_COLLECTION_URL:-}"
max_time="${INKFLOW_COLLECTION_PACKAGE_SMOKE_CURL_MAX_TIME:-10}"
poll_timeout="${INKFLOW_COLLECTION_PACKAGE_SMOKE_POLL_TIMEOUT:-120}"
poll_interval="${INKFLOW_COLLECTION_PACKAGE_SMOKE_POLL_INTERVAL:-2}"
curl_bin="${INKFLOW_COLLECTION_PACKAGE_SMOKE_CURL_BIN:-curl}"
jq_bin="${INKFLOW_COLLECTION_PACKAGE_SMOKE_JQ_BIN:-jq}"
work_dir=""
direct_run_id=""
collection_token=""
package_token=""

fail() {
  printf 'collection-package-runtime-smoke: %s\n' "$1" >&2
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

if [[ -z "$operator_token" || -z "$book_id" ]]; then
  fail 'operator token and canonical book id must be supplied through environment variables'
fi

collection_token="$operator_token"
package_token="$operator_token"
if [[ -n "$reader_token" ]]; then
  collection_token="$reader_token"
  package_token="$reader_token"
fi

for control_id in "$pause_run_id" "$stop_run_id" "$cancel_run_id" "$resume_run_id"; do
  if [[ -z "$control_id" ]]; then
    fail 'all four control run ids must be supplied through environment variables'
  fi
done

if ! [[ "$max_time" =~ ^[1-9][0-9]*$ ]] || (( max_time > 60 )); then
  fail 'INKFLOW_COLLECTION_PACKAGE_SMOKE_CURL_MAX_TIME must be an integer from 1 to 60'
fi

if ! [[ "$poll_timeout" =~ ^[1-9][0-9]*$ ]] || (( poll_timeout > 900 )); then
  fail 'INKFLOW_COLLECTION_PACKAGE_SMOKE_POLL_TIMEOUT must be an integer from 1 to 900'
fi

if ! [[ "$poll_interval" =~ ^[1-9][0-9]*$ ]] || (( poll_interval > 30 )); then
  fail 'INKFLOW_COLLECTION_PACKAGE_SMOKE_POLL_INTERVAL must be an integer from 1 to 30'
fi

for executable in "$curl_bin" "$jq_bin" unzip sha256sum; do
  if ! command -v "$executable" >/dev/null 2>&1; then
    fail "required executable not found: $executable"
  fi
done

work_dir="$(mktemp -d "${TMPDIR:-/tmp}/inkflow-collection-package.XXXXXX")"

cleanup() {
  local status=$?
  set +e

  if [[ -n "$direct_run_id" && -n "$operator_token" ]]; then
    "$curl_bin" --silent --show-error --max-time "$max_time" \
      --request POST \
      -H "Authorization: Bearer $operator_token" \
      -H 'Content-Type: application/json' \
      --data '{"action":"cancel","reason":"runtime smoke cleanup"}' \
      "$base_url/api/v1/admin/collection-runs/$direct_run_id/control" \
      >/dev/null 2>&1 || true
  fi

  if [[ -n "$work_dir" ]]; then
    rm -rf -- "$work_dir"
  fi
  exit "$status"
}
trap cleanup EXIT

request_status() {
  local method="$1"
  local route="$2"
  local token="$3"
  local payload="$4"
  local output="$5"
  local -a args=(
    --silent
    --show-error
    --max-time "$max_time"
    --request "$method"
    --output "$output"
    --write-out '%{http_code}'
  )

  if [[ -n "$token" ]]; then
    args+=(-H "Authorization: Bearer $token")
  fi

  if [[ -n "$payload" ]]; then
    args+=(-H 'Content-Type: application/json' --data "$payload")
  fi

  local status
  if ! status="$("$curl_bin" "${args[@]}" "$base_url$route")"; then
    fail "$method $route could not be completed"
  fi
  printf '%s' "$status"
}

expect_status() {
  local method="$1"
  local route="$2"
  local token="$3"
  local payload="$4"
  local expected="$5"
  local output="$6"
  local status
  status="$(request_status "$method" "$route" "$token" "$payload" "$output")"
  if [[ "$status" != "$expected" ]]; then
    fail "$method $route returned HTTP $status; expected $expected"
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

control_run() {
  local run_id="$1"
  local action="$2"
  local expected_status="$3"
  local output="$4"
  local payload
  payload="$($jq_bin -nc --arg action "$action" \
    '{action: $action, reason: "automated collection control smoke"}')"
  expect_status POST "/api/v1/admin/collection-runs/$run_id/control" \
    "$operator_token" "$payload" 200 "$output"
  assert_json_arg expected "$expected_status" \
    '.status == "applied" and .run.id != null and .run.status == $expected' \
    "$output" \
    "collection control $action did not result in $expected_status"
}

expect_status GET /api/v1/admin/collection-runs '' '' 401 \
  "$work_dir/collection-unauthenticated.json"
expect_status GET '/api/v1/admin/collection-runs?limit=10' "$collection_token" '' 200 \
  "$work_dir/collection-list.json"
assert_json '.data | type == "array"' \
  "$work_dir/collection-list.json" \
  'collection run list response is invalid'

expect_status POST /api/v1/admin/collection-runs '' \
  '{"url":"javascript:alert(1)"}' 401 \
  "$work_dir/collection-create-unauthenticated.json"
expect_status POST /api/v1/admin/collection-runs "$operator_token" \
  '{"url":"javascript:alert(1)"}' 422 \
  "$work_dir/collection-invalid-url.json"
assert_json '.error == "source-url.scheme"' \
  "$work_dir/collection-invalid-url.json" \
  'invalid collection URL did not return the stable scheme error'

expect_status POST /api/v1/admin/collection-runs "$operator_token" \
  '{"url":""}' 400 \
  "$work_dir/collection-empty-url.json"
assert_json '.error == "source-url.empty"' \
  "$work_dir/collection-empty-url.json" \
  'empty collection URL did not return the stable input error'

if [[ -z "$collection_url" ]]; then
  collection_url="https://inkflow-acceptance.invalid/book/runtime-$(date -u +%s%N)-$$-${RANDOM}"
fi
start_payload="$($jq_bin -nc --arg url "$collection_url" '{url: $url}')"
expect_status POST /api/v1/admin/collection-runs "$collection_token" \
  "$start_payload" 202 "$work_dir/collection-start.json"
assert_json \
  '.status == "accepted" and .run.id != null and (.run.status == "pending" or .run.status == "running")' \
  "$work_dir/collection-start.json" \
  'direct URL collection was not accepted'
direct_run_id="$($jq_bin -er '.run.id' "$work_dir/collection-start.json")"
expect_status GET "/api/v1/admin/collection-runs/$direct_run_id" "$collection_token" '' 200 \
  "$work_dir/collection-start-view.json"
assert_json_arg expected "$direct_run_id" \
  '.id == $expected and (.inputUrl | startswith("https://"))' \
  "$work_dir/collection-start-view.json" \
  'direct collection run view is invalid'
control_run "$direct_run_id" cancel cancelled "$work_dir/collection-direct-cancel.json"

control_run "$pause_run_id" pause paused "$work_dir/control-pause.json"
control_run "$pause_run_id" pause paused "$work_dir/control-pause-repeat.json"
control_run "$pause_run_id" resume pending "$work_dir/control-resume.json"
control_run "$resume_run_id" pause paused "$work_dir/control-resume-pause.json"
control_run "$resume_run_id" resume pending "$work_dir/control-resume-action.json"
control_run "$resume_run_id" resume pending "$work_dir/control-resume-repeat.json"
control_run "$stop_run_id" stop stopped "$work_dir/control-stop.json"
control_run "$stop_run_id" stop stopped "$work_dir/control-stop-repeat.json"
control_run "$stop_run_id" cancel cancelled "$work_dir/control-stop-cancel.json"
control_run "$cancel_run_id" cancel cancelled "$work_dir/control-cancel.json"
control_run "$cancel_run_id" cancel cancelled "$work_dir/control-cancel-repeat.json"

cleanup_payload="$($jq_bin -nc '{reason: "automated cancelled collection cleanup smoke"}')"
expect_status POST /api/v1/admin/collection-runs/cancelled/cleanup "$operator_token" \
  "$cleanup_payload" 200 "$work_dir/collection-cancelled-cleanup.json"
assert_json \
  '.status == "cleaned" and .deletedCount >= 3' \
  "$work_dir/collection-cancelled-cleanup.json" \
  'cancelled collection cleanup did not remove all cancelled fixture runs'
expect_status GET "/api/v1/admin/collection-runs/$cancel_run_id" "$operator_token" '' 404 \
  "$work_dir/collection-cancelled-cleanup-view.json"

assert_json \
  '.run.stage == "bookInfo" and .run.progressPercent == null and .run.totalTaskCount == 0' \
  "$work_dir/control-pause.json" \
  'pre-content collection progress should be indeterminate'

create_package() {
  local format="$1"
  local output="$work_dir/package-$format-create.json"
  local payload
  payload="$($jq_bin -nc --arg format "$format" '{format: $format}')"
  expect_status POST "/api/v1/admin/books/$book_id/packages" "$package_token" \
    "$payload" 202 "$output"
  assert_json_arg format "$format" \
    '.status == "accepted" and .package.id != null and .package.canonicalBookId != null and .package.format == $format and .package.status == "queued"' \
    "$output" \
    "$format package was not queued"
}

poll_package() {
  local format="$1"
  local package_id="$2"
  local output="$work_dir/package-$format-status.json"
  local deadline=$((SECONDS + poll_timeout))
  local status

  while (( SECONDS < deadline )); do
    expect_status GET "/api/v1/admin/packages/$package_id" "$package_token" '' 200 "$output"
    status="$($jq_bin -er '.status' "$output")"
    case "$status" in
      completed)
        assert_json \
          '.progressPercent == 100 and .artifactFileName != null and .artifactSha256 != null and .artifactLength > 0' \
          "$output" \
          "$format package completion metadata is invalid"
        assert_json_arg book "$book_id" \
          '.canonicalBookId == $book' "$output" \
          "$format package book identity is invalid"
        assert_json_arg format "$format" \
          '.format == $format' "$output" \
          "$format package format identity is invalid"
        return 0
        ;;
      failed|expired)
        fail "$format package ended in $status"
        ;;
    esac
    sleep "$poll_interval"
  done

  fail "$format package did not complete within ${poll_timeout}s"
}

download_package() {
  local format="$1"
  local package_id="$2"
  local status_file="$work_dir/package-$format-status.json"
  local artifact="$work_dir/package.$format"
  local headers="$work_dir/package-$format.headers"
  local expected_digest
  local actual_digest
  local expected_length
  local actual_length

  expected_digest="$($jq_bin -er '.artifactSha256' "$status_file")"
  expected_length="$($jq_bin -er '.artifactLength' "$status_file")"
  if ! "$curl_bin" --silent --show-error --fail --max-time "$max_time" \
    --request GET \
    -H "Authorization: Bearer $package_token" \
    --dump-header "$headers" \
    --output "$artifact" \
    "$base_url/api/v1/admin/packages/$package_id/download"; then
    fail "$format package download failed"
  fi

  [[ -s "$artifact" ]] || fail "$format package download is empty"
  actual_length="$(wc -c < "$artifact")"
  [[ "$actual_length" == "$expected_length" ]] || fail "$format package length does not match its metadata"
  actual_digest="$(sha256sum "$artifact" | awk '{print $1}')"
  [[ "$actual_digest" == "$expected_digest" ]] || fail "$format package hash does not match its metadata"

  case "$format" in
    zip)
      unzip -tqq "$artifact" || fail 'ZIP package is not a valid archive'
      unzip -p "$artifact" manifest.json | \
        "$jq_bin" -e --arg book "$book_id" \
        '.format == "zip" and .formatVersion == "1" and .bookId == $book and (.files | length > 0)' \
        >/dev/null || fail 'ZIP manifest is invalid'
      unzip -p "$artifact" chapters/000001.txt | \
        grep -Fq -- '正文来自已发布的 Canonical Content' || fail 'ZIP chapter content is missing'
      ;;
    epub)
      unzip -tqq "$artifact" || fail 'EPUB package is not a valid archive'
      [[ "$(unzip -p "$artifact" mimetype)" == 'application/epub+zip' ]] || fail 'EPUB mimetype is invalid'
      unzip -p "$artifact" OEBPS/content.opf | \
        grep -Fq -- 'application/xhtml+xml' || fail 'EPUB content manifest is missing'
      unzip -p "$artifact" OEBPS/chapters/000001.xhtml | \
        grep -Fq -- '正文来自已发布的 Canonical Content' || fail 'EPUB chapter content is missing'
      ;;
    txt)
      grep -Fq -- 'InkFlow Runtime Acceptance Fixture' "$artifact" || fail 'TXT title is missing'
      grep -Fq -- '作者：InkFlow Automation' "$artifact" || fail 'TXT author is missing'
      grep -Fq -- '正文来自已发布的 Canonical Content' "$artifact" || fail 'TXT chapter content is missing'
      ;;
    *)
      fail "unsupported smoke format: $format"
      ;;
  esac
}

expect_status GET "/api/v1/admin/packages/00000000-0000-0000-0000-000000000000" '' '' 401 \
  "$work_dir/package-unauthenticated.json"

declare -A package_ids=()
for format in zip epub txt; do
  create_package "$format"
  package_ids["$format"]="$($jq_bin -er '.package.id' "$work_dir/package-$format-create.json")"
done

expect_status GET '/api/v1/admin/packages?limit=100' "$package_token" '' 200 \
  "$work_dir/package-list.json"
for format in zip epub txt; do
  assert_json_arg package "${package_ids[$format]}" \
    'any(.data[]; .id == $package)' "$work_dir/package-list.json" \
    "$format package is missing from the durable package list"
done

for format in zip epub txt; do
  poll_package "$format" "${package_ids[$format]}"
  download_package "$format" "${package_ids[$format]}"
done

if [[ -n "$admin_token" ]]; then
  expect_status GET '/api/v1/admin/audit/events?action=book.package.create&limit=100' \
    "$admin_token" '' 200 "$work_dir/package-create-audit.json"
  assert_json \
    'any(.events[]; .action == "book.package.create" and .outcome == "success")' \
    "$work_dir/package-create-audit.json" \
    'book package creation audit event is missing'
  expect_status GET '/api/v1/admin/audit/events?action=book.package.download&limit=100' \
    "$admin_token" '' 200 "$work_dir/package-download-audit.json"
  assert_json \
    'any(.events[]; .action == "book.package.download" and .outcome == "success")' \
    "$work_dir/package-download-audit.json" \
    'book package download audit event is missing'
  expect_status GET '/api/v1/admin/audit/events?action=collection.run.cancelled.cleanup&limit=100' \
    "$admin_token" '' 200 "$work_dir/collection-cancelled-cleanup-audit.json"
  assert_json \
    'any(.events[]; .action == "collection.run.cancelled.cleanup" and .outcome == "success")' \
    "$work_dir/collection-cancelled-cleanup-audit.json" \
    'cancelled collection cleanup audit event is missing'
fi

printf 'collection-package-runtime-smoke: PASS (direct URL, durable controls, ZIP/EPUB/TXT packages, integrity, audit)\n'
