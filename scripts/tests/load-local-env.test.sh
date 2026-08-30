#!/usr/bin/env bash
set -Eeuo pipefail

root_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd)"
env_file="$(mktemp "${TMPDIR:-/tmp}/inkflow-load-env.XXXXXX")"

cleanup() {
  local status=$?
  set +e
  rm -f -- "$env_file"
  exit "$status"
}
trap cleanup EXIT

printf '%s\n' \
  '# comments and blank lines are ignored' \
  'INKFLOW_LOAD_ENV_FILE_VALUE=from-file' \
  'export INKFLOW_LOAD_ENV_EXPORT_VALUE=from-export' \
  'INKFLOW_LOAD_ENV_QUOTED_VALUE="from quoted file"' \
  'INKFLOW_LOAD_ENV_HASH_VALUE=keep#hash' \
  'INKFLOW_LOAD_ENV_EMPTY_VALUE=' \
  'INKFLOW_LOAD_ENV_LITERAL_VALUE=$(must-not-execute)' > "$env_file"

output="$(
  INKFLOW_ENV_FILE="$env_file" \
  INKFLOW_LOAD_ENV_PRECEDENCE_VALUE=from-shell \
    bash -c '
      set -Eeuo pipefail
      source "$1"
      printf "%s|%s|%s|%s|%s|%s|%s" \
        "$INKFLOW_LOAD_ENV_FILE_VALUE" \
        "$INKFLOW_LOAD_ENV_EXPORT_VALUE" \
        "$INKFLOW_LOAD_ENV_QUOTED_VALUE" \
        "$INKFLOW_LOAD_ENV_HASH_VALUE" \
        "$INKFLOW_LOAD_ENV_EMPTY_VALUE" \
        "$INKFLOW_LOAD_ENV_LITERAL_VALUE" \
        "$INKFLOW_LOAD_ENV_PRECEDENCE_VALUE"
    ' bash "$root_dir/scripts/load-local-env.sh"
)"

test "$output" = 'from-file|from-export|from quoted file|keep#hash||$(must-not-execute)|from-shell'
printf 'load-local-env.test: PASS\n'
