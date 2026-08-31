#!/usr/bin/env bash
set -Eeuo pipefail

output=""
method="GET"
url=""
state_file="${INKFLOW_FAILOVER_FIXTURE_STATE_FILE:-${TMPDIR:-/tmp}/inkflow-failover-fixture.state}"

while (($# > 0)); do
  case "$1" in
    --output|-o)
      output="$2"
      shift 2
      ;;
    --write-out|-w|--max-time)
      shift 2
      ;;
    --request|-X)
      method="${2^^}"
      shift 2
      ;;
    --header|-H|--data|--data-raw|--data-binary)
      shift 2
      ;;
    --silent|--show-error|--fail)
      shift
      ;;
    *)
      url="$1"
      shift
      ;;
  esac
done

if [[ -z "$output" || -z "$url" ]]; then
  exit 2
fi

book_id="11111111-1111-4111-8111-111111111111"
chapter_id="22222222-2222-4222-8222-222222222222"
source_a_id="inkflow-failover-a"
source_b_id="inkflow-failover-b"
source_id="$source_a_id"
source_marker="InkFlow failover source A marker"
body=""

if [[ -f "$state_file" ]]; then
  source_id="$source_b_id"
  source_marker="InkFlow failover source B marker"
fi

case "$method:$url" in
  POST:*"/api/v1/admin/sources/$source_a_id/health/content/disable")
    mkdir -p -- "$(dirname -- "$state_file")"
    : > "$state_file"
    body="{\"status\":\"applied\",\"action\":\"disable\",\"health\":{\"sourceId\":\"$source_a_id\",\"capability\":\"Content\",\"status\":\"Disabled\",\"isAvailable\":false}}"
    ;;
  POST:*"/api/v1/admin/sources/$source_a_id/health/content/enable")
    rm -f -- "$state_file"
    body="{\"status\":\"applied\",\"action\":\"enable\",\"health\":{\"sourceId\":\"$source_a_id\",\"capability\":\"Content\",\"status\":\"Unknown\",\"isAvailable\":true}}"
    ;;
  POST:*"/api/v1/admin/sources/$source_b_id/health/content/enable")
    body="{\"status\":\"applied\",\"action\":\"enable\",\"health\":{\"sourceId\":\"$source_b_id\",\"capability\":\"Content\",\"status\":\"Unknown\",\"isAvailable\":true}}"
    ;;
  GET:*"/api/v1/books/$book_id")
    body="{\"id\":\"$book_id\",\"title\":\"InkFlow Source Failover Fixture\",\"author\":\"InkFlow Automation\",\"chapters\":[{\"chapterId\":\"$chapter_id\",\"index\":0,\"title\":\"Failover Acceptance Chapter\"}]}"
    ;;
  GET:*"/api/v1/chapters/$chapter_id/content")
    body="{\"chapterId\":\"$chapter_id\",\"bookId\":\"$book_id\",\"index\":0,\"title\":\"Failover Acceptance Chapter\",\"sourceId\":\"$source_id\",\"paragraphs\":[\"$source_marker\",\"Canonical content remains readable during the runtime drill.\"]}"
    ;;
  GET:*"/api/legado/v1/search?q="*)
    body="{\"data\":[{\"bookId\":\"$book_id\",\"title\":\"InkFlow Source Failover Fixture\",\"author\":\"InkFlow Automation\",\"detailUrl\":\"/api/legado/v1/books/$book_id\"}]}"
    ;;
  GET:*"/api/legado/v1/books/$book_id/chapters")
    body="{\"data\":[{\"chapterId\":\"$chapter_id\",\"index\":0,\"title\":\"Failover Acceptance Chapter\",\"chapterUrl\":\"/api/legado/v1/chapters/$chapter_id\"}]}"
    ;;
  GET:*"/api/legado/v1/books/$book_id")
    body="{\"bookId\":\"$book_id\",\"title\":\"InkFlow Source Failover Fixture\",\"author\":\"InkFlow Automation\",\"tocUrl\":\"/api/legado/v1/books/$book_id/chapters\"}"
    ;;
  GET:*"/api/legado/v1/chapters/$chapter_id")
    body="{\"chapterId\":\"$chapter_id\",\"title\":\"Failover Acceptance Chapter\",\"content\":\"$source_marker. Canonical content remains readable during the runtime drill.\"}"
    ;;
  GET:*"/legado/book-source.json")
    body='{
      "searchUrl": "http://fixture.invalid/api/legado/v1/search?q={{key}}",
      "ruleSearch": {"bookList": "$.data[*]", "name": "$.title", "author": "$.author", "bookUrl": "$.detailUrl"},
      "ruleBookInfo": {"name": "$.title", "author": "$.author", "tocUrl": "$.tocUrl"},
      "ruleToc": {"chapterList": "$.data[*]", "chapterName": "$.title", "chapterUrl": "$.chapterUrl"},
      "ruleContent": {"content": "$.content"}
    }'
    ;;
  *)
    exit 1
    ;;
esac

printf '%s' "$body" > "$output"
printf '200'
