#!/usr/bin/env bash
set -Eeuo pipefail

url="${!#}"
mode="${INKFLOW_SLO_FIXTURE_MODE:-normal}"

if [[ "$mode" == "slow-public" && "$url" == */api/v1/books ]]; then
  printf '200\t0.751\n'
  exit 0
fi

if [[ "$mode" == "boundary-public" && "$url" == */api/v1/books ]]; then
  printf '200\t0.750\n'
  exit 0
fi

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
