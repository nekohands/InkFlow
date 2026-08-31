#!/usr/bin/env bash
set -Eeuo pipefail

fixture_runner="${INKFLOW_ACCEPTANCE_FIXTURE_RUNNER:-}"
if [[ "$#" -ne 1 || -z "${1:-}" ]]; then
  printf 'disable-acceptance-user: usage: %s <email>\n' "${0##*/}" >&2
  exit 2
fi

if [[ -n "$fixture_runner" ]]; then
  if [[ ! -f "$fixture_runner" ]]; then
    printf 'disable-acceptance-user: fixture runner not found: %s\n' "$fixture_runner" >&2
    exit 1
  fi

  exec bash "$fixture_runner" disable-user "$1"
fi

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
compose_file="${INKFLOW_ACCEPTANCE_COMPOSE_FILE:-$script_dir/../docker-compose.build.yml}"
docker_bin="${INKFLOW_ACCEPTANCE_DOCKER_BIN:-docker}"

if [[ ! -f "$compose_file" ]]; then
  printf 'disable-acceptance-user: compose file not found: %s\n' "$compose_file" >&2
  exit 1
fi

if ! command -v "$docker_bin" >/dev/null 2>&1; then
  printf 'disable-acceptance-user: docker executable not found: %s\n' "$docker_bin" >&2
  exit 1
fi

"$docker_bin" compose \
  --file "$compose_file" \
  --profile acceptance \
  run --rm --no-deps acceptance-fixtures \
  disable-user "$1"
