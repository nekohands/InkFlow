#!/usr/bin/env bash
set -Eeuo pipefail

output=""
url=""

while (($# > 0)); do
  case "$1" in
    --output)
      output="$2"
      shift 2
      ;;
    --write-out|--max-time)
      shift 2
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
body=""

case "$url" in
  */api/legado/v1/search\?q=*)
    body="{\"data\":[{\"bookId\":\"$book_id\",\"title\":\"InkFlow Runtime Acceptance Fixture\",\"author\":\"InkFlow Automation\",\"detailUrl\":\"/api/legado/v1/books/$book_id\"}]}"
    ;;
  */api/legado/v1/books/$book_id)
    body="{\"bookId\":\"$book_id\",\"title\":\"InkFlow Runtime Acceptance Fixture\",\"author\":\"InkFlow Automation\",\"tocUrl\":\"/api/legado/v1/books/$book_id/chapters\"}"
    ;;
  */api/legado/v1/books/$book_id/chapters)
    body="{\"data\":[{\"chapterId\":\"$chapter_id\",\"index\":0,\"title\":\"Automated Acceptance Chapter\",\"chapterUrl\":\"/api/legado/v1/chapters/$chapter_id\"}]}"
    ;;
  */api/legado/v1/chapters/$chapter_id)
    body="{\"chapterId\":\"$chapter_id\",\"title\":\"Automated Acceptance Chapter\",\"content\":\"第一段\\n\\n正文来自已发布的 Canonical Content\"}"
    ;;
  */legado/book-source.json)
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
