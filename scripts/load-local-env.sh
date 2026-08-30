#!/usr/bin/env bash

# Load the repository-root .env without executing it. This file is intended to
# be sourced by local validation scripts; existing environment variables win.

if [[ "${BASH_SOURCE[0]}" == "$0" ]]; then
  exit 0
fi

if [[ "${INKFLOW_SKIP_LOCAL_ENV:-0}" == "1" ||
      "${INKFLOW_LOCAL_ENV_LOADED:-0}" == "1" ]]; then
  return 0
fi

env_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
env_file="${INKFLOW_ENV_FILE:-$env_root/.env}"

if [[ ! -f "$env_file" ]]; then
  export INKFLOW_LOCAL_ENV_LOADED=1
  return 0
fi

line_number=0
while IFS= read -r line || [[ -n "$line" ]]; do
  line_number=$((line_number + 1))
  line="${line%$'\r'}"

  if [[ "$line" =~ ^[[:space:]]*$ || "$line" =~ ^[[:space:]]*# ]]; then
    continue
  fi

  if [[ ! "$line" =~ ^[[:space:]]*(export[[:space:]]+)?([A-Za-z_][A-Za-z0-9_]*)[[:space:]]*=[[:space:]]*(.*)$ ]]; then
    printf 'load-local-env: invalid assignment at %s:%s\n' "$env_file" "$line_number" >&2
    return 1
  fi

  key="${BASH_REMATCH[2]}"
  value="${BASH_REMATCH[3]}"

  if [[ "${value:0:1}" == '"' ]]; then
    if [[ "${#value}" -lt 2 || "${value: -1}" != '"' ]]; then
      printf 'load-local-env: unterminated quoted value at %s:%s\n' "$env_file" "$line_number" >&2
      return 1
    fi
    value="${value:1:${#value}-2}"
  elif [[ "${value:0:1}" == "'" ]]; then
    if [[ "${#value}" -lt 2 || "${value: -1}" != "'" ]]; then
      printf 'load-local-env: unterminated quoted value at %s:%s\n' "$env_file" "$line_number" >&2
      return 1
    fi
    value="${value:1:${#value}-2}"
  else
    value="${value#"${value%%[![:space:]]*}"}"
    value="${value%"${value##*[![:space:]]}"}"
  fi

  # Match Compose's precedence: an explicitly exported value wins over .env.
  if [[ ! -v "$key" ]]; then
    export "$key=$value"
  fi
done < "$env_file"

export INKFLOW_LOCAL_ENV_LOADED=1
