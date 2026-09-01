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
    --write-out)
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

case "$url" in
  */reader/books/33333333-3333-4333-8333-333333333333)
    printf '%s' 'class="book-hero" InkFlow Edge &lt;Metadata&gt; InkFlow Edge &amp; Author overflow-wrap: anywhere; 开始阅读' > "$output"
    ;;
  *)
    exit 1
    ;;
esac

printf '200'
