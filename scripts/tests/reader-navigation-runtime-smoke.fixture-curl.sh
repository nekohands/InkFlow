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
  */reader/read/11111111-1111-4111-8111-111111111111)
    printf '%s' 'id="reading-progress" href="/reader/read/22222222-2222-4222-8222-222222222222" rel="next" Automated Acceptance Chapter' > "$output"
    ;;
  */reader/read/22222222-2222-4222-8222-222222222222)
    printf '%s' 'id="reading-progress" href="/reader/read/11111111-1111-4111-8111-111111111111" rel="prev" Automated Acceptance Follow-up' > "$output"
    ;;
  *)
    exit 1
    ;;
esac

printf '200'
