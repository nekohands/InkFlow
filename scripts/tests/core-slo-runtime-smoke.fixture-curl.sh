#!/usr/bin/env bash
set -Eeuo pipefail

url="${!#}"
case "$url" in
  */api/developer/v1/books)
    printf '401\t0.001\n'
    ;;
  http://*/*|https://*/*)
    printf '200\t0.001\n'
    ;;
  *)
    printf 'fixture curl: unexpected URL\n' >&2
    exit 1
    ;;
esac
