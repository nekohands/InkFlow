#!/usr/bin/env bash
set -Eeuo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck disable=SC1091
source "$script_dir/load-local-env.sh"

compose_file="${COMPOSE_FILE:-${INKFLOW_SOURCE_COMPOSE_FILE:-docker-compose.build.yml}}"
database_service="${DATABASE_SERVICE:-postgres}"
database_name="${DATABASE_NAME:-inkflow}"
database_user="${DATABASE_USER:-inkflow}"
restore_database="${RESTORE_DATABASE:-inkflow_backup_verify}"
backup_file="${BACKUP_FILE:-}"
created_backup_file=0

compose() {
  docker compose --ansi never -f "$compose_file" "$@"
}

fail() {
  printf 'backup-restore-drill: %s\n' "$1" >&2
  exit 1
}

validate_identifier() {
  local name="$1"
  if [[ ! "$name" =~ ^[a-z_][a-z0-9_]*$ ]]; then
    fail "database identifier is not safe: $name"
  fi
}

validate_identifier "$database_name"
validate_identifier "$database_user"
validate_identifier "$restore_database"

if [[ "$database_name" == "$restore_database" ]]; then
  fail 'restore database must be different from the source database'
fi

if [[ -z "$backup_file" ]]; then
  backup_file="$(mktemp "${TMPDIR:-/tmp}/inkflow-backup.XXXXXX.dump")"
  created_backup_file=1
fi

cleanup() {
  local status=$?
  set +e
  compose exec -T "$database_service" psql \
    -X -q -v ON_ERROR_STOP=1 -U "$database_user" -d "$database_name" \
    -c "DROP DATABASE IF EXISTS \"$restore_database\";" \
    >/dev/null 2>&1
  if [[ "$created_backup_file" == 1 ]]; then
    rm -f -- "$backup_file"
  fi
  exit "$status"
}
trap cleanup EXIT

database_signature() {
  local database="$1"
  local table_name row_count
  local tables

  tables="$(compose exec -T "$database_service" psql \
    -X -qAt -U "$database_user" -d "$database" \
    -c "SELECT quote_ident(table_schema) || '.' || quote_ident(table_name)
          FROM information_schema.tables
         WHERE table_type = 'BASE TABLE'
           AND table_schema NOT IN ('pg_catalog', 'information_schema')
         ORDER BY table_schema, table_name;")"

  while IFS= read -r table_name; do
    [[ -z "$table_name" ]] && continue
    row_count="$(compose exec -T "$database_service" psql \
      -X -qAt -U "$database_user" -d "$database" \
      -c "SELECT count(*) FROM $table_name;")"
    printf '%s=%s\n' "$table_name" "$row_count"
  done <<< "$tables"
}

printf 'Creating custom-format PostgreSQL backup...\n'
compose exec -T "$database_service" pg_dump \
  --format=custom --no-owner --no-acl \
  --username="$database_user" --dbname="$database_name" \
  > "$backup_file"

backup_size="$(wc -c < "$backup_file" | tr -d '[:space:]')"
if [[ ! "$backup_size" =~ ^[1-9][0-9]*$ ]]; then
  fail 'backup archive is empty'
fi

source_signature="$(database_signature "$database_name")"
source_audit_count="$(compose exec -T "$database_service" psql \
  -X -qAt -U "$database_user" -d "$database_name" \
  -c 'SELECT count(*) FROM "audit"."events";')"
if [[ ! "$source_audit_count" =~ ^[1-9][0-9]*$ ]]; then
  fail 'source database has no audit events; run the runtime smoke flow before this drill'
fi

printf 'Recreating isolated restore database...\n'
compose exec -T "$database_service" psql \
  -X -q -v ON_ERROR_STOP=1 -U "$database_user" -d "$database_name" \
  -c "DROP DATABASE IF EXISTS \"$restore_database\";"
compose exec -T "$database_service" psql \
  -X -q -v ON_ERROR_STOP=1 -U "$database_user" -d "$database_name" \
  -c "CREATE DATABASE \"$restore_database\";"

printf 'Restoring backup into isolated database...\n'
compose exec -T "$database_service" pg_restore \
  --exit-on-error --no-owner --no-acl \
  --username="$database_user" --dbname="$restore_database" \
  < "$backup_file"

restored_signature="$(database_signature "$restore_database")"
if [[ "$source_signature" != "$restored_signature" ]]; then
  printf 'backup-restore-drill: table row-count signature mismatch\n' >&2
  diff -u \
    <(printf '%s\n' "$source_signature") \
    <(printf '%s\n' "$restored_signature") || true
  exit 1
fi

restored_audit_count="$(compose exec -T "$database_service" psql \
  -X -qAt -U "$database_user" -d "$restore_database" \
  -c 'SELECT count(*) FROM "audit"."events";')"
if [[ "$source_audit_count" != "$restored_audit_count" ]]; then
  fail "audit event count mismatch: source=$source_audit_count restored=$restored_audit_count"
fi

printf 'backup-restore-drill: PASS (archive=%s bytes, audit_events=%s)\n' \
  "$backup_size" "$restored_audit_count"
