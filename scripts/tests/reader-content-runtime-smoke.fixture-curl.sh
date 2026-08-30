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
  */reader/read/*)
    printf '%s' 'id="reading-progress" class="reader-content__body" 正文来自已发布的 Canonical Content reading/progress/ 本章结束' > "$output"
    ;;
  *)
    exit 1
    ;;
esac

printf '200'
