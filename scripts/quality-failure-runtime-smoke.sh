#!/usr/bin/env bash
set -Eeuo pipefail

export LC_ALL=C

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck disable=SC1091
source "$script_dir/load-local-env.sh"

base_url="${1:-${INKFLOW_QUALITY_FAILURE_RUNTIME_SMOKE_BASE_URL:-http://localhost:8080}}"
book_id="${INKFLOW_QUALITY_FAILURE_RUNTIME_SMOKE_BOOK_ID:-}"
chapter_id="${INKFLOW_QUALITY_FAILURE_RUNTIME_SMOKE_CHAPTER_ID:-}"
source_id="${INKFLOW_QUALITY_FAILURE_RUNTIME_SMOKE_SOURCE_ID:-inkflow-quality-a}"
good_marker="${INKFLOW_QUALITY_FAILURE_RUNTIME_SMOKE_GOOD_MARKER:-InkFlow quality fixture good marker}"
low_marker="${INKFLOW_QUALITY_FAILURE_RUNTIME_SMOKE_LOW_MARKER:-InkFlow quality fixture truncated marker}"
max_time="${INKFLOW_QUALITY_FAILURE_RUNTIME_SMOKE_CURL_MAX_TIME:-10}"
curl_bin="${INKFLOW_QUALITY_FAILURE_RUNTIME_SMOKE_CURL_BIN:-curl}"
jq_bin="${INKFLOW_QUALITY_FAILURE_RUNTIME_SMOKE_JQ_BIN:-jq}"
work_dir=""

fail() {
  printf 'quality-failure-runtime-smoke: %s\n' "$1" >&2
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

if ! [[ "$source_id" =~ ^[A-Za-z0-9][A-Za-z0-9._-]*$ ]]; then
  fail 'source id must contain only letters, digits, dots, underscores, and hyphens'
fi

if [[ -z "$good_marker" || -z "$low_marker" || "$good_marker" == "$low_marker" ]]; then
  fail 'good and low quality markers must be non-empty and distinct'
fi

if ! [[ "$max_time" =~ ^[1-9][0-9]*$ ]] || (( max_time > 60 )); then
  fail 'INKFLOW_QUALITY_FAILURE_RUNTIME_SMOKE_CURL_MAX_TIME must be an integer from 1 to 60'
fi

for executable in "$curl_bin" "$jq_bin"; do
  if ! command -v "$executable" >/dev/null 2>&1; then
    fail "required executable not found: $executable"
  fi
done

work_dir="$(mktemp -d "${TMPDIR:-/tmp}/inkflow-quality-failure-runtime.XXXXXX")"

cleanup() {
  local status=$?
  set +e
  if [[ -n "$work_dir" ]]; then
    rm -rf -- "$work_dir"
  fi
  exit "$status"
}
trap cleanup EXIT

request_get() {
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

assert_json_args() {
  local expression="$1"
  local file="$2"
  local message="$3"
  shift 3

  "$jq_bin" -e "$@" "$expression" "$file" >/dev/null || fail "$message"
}

request_get "/api/v1/chapters/$chapter_id/content" "$work_dir/web-content.json"
assert_json_args \
  '.chapterId == $chapter and .bookId == $book and .sourceId == $source and (.paragraphs | type == "array") and ((.paragraphs | join("\n")) | contains($good)) and ((.paragraphs | join("\n")) | contains($low) | not)' \
  "$work_dir/web-content.json" \
  'Web content replaced the selected good version with the low-quality replay' \
  --arg book "$book_id" \
  --arg chapter "$chapter_id" \
  --arg source "$source_id" \
  --arg good "$good_marker" \
  --arg low "$low_marker"

request_get "/api/legado/v1/chapters/$chapter_id" "$work_dir/legado-content.json"
assert_json_args \
  '.chapterId == $chapter and ((.content | contains($good)) and (.content | contains($low) | not))' \
  "$work_dir/legado-content.json" \
  'Legado content replaced the selected good version with the low-quality replay' \
  --arg chapter "$chapter_id" \
  --arg good "$good_marker" \
  --arg low "$low_marker"

request_get "/reader/read/$chapter_id" "$work_dir/reader.html"
grep -Fq -- "$good_marker" "$work_dir/reader.html" \
  || fail 'Reader HTML did not expose the selected good version'
if grep -Fq -- "$low_marker" "$work_dir/reader.html"; then
  fail 'Reader HTML exposed the low-quality replay instead of the selected good version'
fi

printf 'quality-failure-runtime-smoke: PASS (good version remains selected across Web, Legado, and Reader)\n'
