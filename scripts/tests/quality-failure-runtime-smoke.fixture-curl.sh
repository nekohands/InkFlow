#!/usr/bin/env bash
set -Eeuo pipefail

output=""
url=""

while (($# > 0)); do
  case "$1" in
    --output|-o)
      output="$2"
      shift 2
      ;;
    --write-out|-w|--max-time)
      shift 2
      ;;
    --request|-X|--header|-H|--data|--data-raw|--data-binary)
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
source_id="inkflow-quality-a"
good_marker="InkFlow quality fixture good marker"
low_marker="InkFlow quality fixture truncated marker"
body=""

case "$url" in
  *"/api/v1/chapters/$chapter_id/content")
    body="{\"chapterId\":\"$chapter_id\",\"bookId\":\"$book_id\",\"index\":0,\"title\":\"Quality Failure Acceptance Chapter\",\"sourceId\":\"$source_id\",\"paragraphs\":[\"$good_marker\",\"The selected canonical version remains readable after a truncated replay.\"]}"
    ;;
  *"/api/legado/v1/chapters/$chapter_id")
    body="{\"chapterId\":\"$chapter_id\",\"title\":\"Quality Failure Acceptance Chapter\",\"content\":\"$good_marker. The selected canonical version remains readable after a truncated replay.\"}"
    ;;
  *"/reader/read/$chapter_id")
    body="<!DOCTYPE html><html lang=\"zh-CN\"><head><title>Quality Failure Acceptance Chapter</title></head><body><main><h1>Quality Failure Acceptance Chapter</h1><p>$good_marker</p><p>The selected canonical version remains readable after a truncated replay.</p></main></body></html>"
    ;;
  *)
    exit 1
    ;;
esac

printf '%s' "$body" > "$output"
printf '200'
