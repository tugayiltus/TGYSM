#!/usr/bin/env bash
# One TGYSM backup (ADR-0025): pg_dump (custom format) + file store + Data Protection keys + manifest.json in
# tgysm-YYYYMMDD-HHMMSS.tar.gz (UTC). Written under a temporary name and renamed when complete; expired archives
# are deleted only after a successful run. Any failure exits non-zero and leaves earlier archives untouched.
set -euo pipefail
# Archives hold the database and the Data Protection keys: owner-only files (security review, ADR-0025).
umask 077

readonly BACKUP_DIR="${BACKUP_DIR:-/backups}"
readonly DATA_DIR="${DATA_DIR:-/data}"
readonly RETENTION_DAYS="${BACKUP_RETENTION_DAYS:-14}"
readonly MINUTES_PER_DAY=1440
readonly ARCHIVE_PREFIX="tgysm-"

stamp="$(date -u +%Y%m%d-%H%M%S)"
archive="$BACKUP_DIR/$ARCHIVE_PREFIX$stamp.tar.gz"
partial="$BACKUP_DIR/.partial-$stamp.tar.gz"
work="$(mktemp -d)"
cleanup() { rm -rf "$work"; rm -f "$partial"; }
trap cleanup EXIT

readonly LATEST_MIGRATION_SQL='SELECT migration_id FROM "__EFMigrationsHistory" ORDER BY migration_id DESC LIMIT 1'
# Exact row counts of every table, as one JSON object (restore checks them).
readonly TABLE_COUNTS_SQL="SELECT json_object_agg(table_name, (xpath('/row/c/text()', query_to_xml(format('SELECT count(*) AS c FROM %I.%I', table_schema, table_name), false, true, '')))[1]::text::bigint ORDER BY table_name) FROM information_schema.tables WHERE table_schema = 'public' AND table_type = 'BASE TABLE'"

# One snapshot for the dump, the counts and the migration id: the manifest describes exactly what the dump holds.
coproc SNAPSHOT { psql -XtAq; }
echo "BEGIN ISOLATION LEVEL REPEATABLE READ READ ONLY; SELECT pg_export_snapshot();" >&"${SNAPSHOT[1]}"
read -r snapshot <&"${SNAPSHOT[0]}"
pg_dump --format=custom --snapshot="$snapshot" --file="$work/db.dump"
echo "$LATEST_MIGRATION_SQL;" >&"${SNAPSHOT[1]}"
read -r latest_migration <&"${SNAPSHOT[0]}"
echo "$TABLE_COUNTS_SQL;" >&"${SNAPSHOT[1]}"
read -r tables <&"${SNAPSHOT[0]}"
echo "COMMIT;" >&"${SNAPSHOT[1]}"
exec {SNAPSHOT[1]}>&-
wait "$SNAPSHOT_PID"
file_count="$(find "$DATA_DIR/files" -type f | wc -l | tr -d ' ')"

cat > "$work/manifest.json" <<JSON
{
  "appVersion": "${APP_VERSION:-unknown}",
  "latestMigration": "$latest_migration",
  "createdAt": "$(date -u +%Y-%m-%dT%H:%M:%SZ)",
  "fileCount": $file_count,
  "tables": $tables
}
JSON

tar -czf "$partial" -C "$work" db.dump manifest.json -C "$DATA_DIR" files keys
mv "$partial" "$archive"
echo "Backup written: $archive"

find "$BACKUP_DIR" -maxdepth 1 -name "$ARCHIVE_PREFIX*.tar.gz" -mmin "+$((RETENTION_DAYS * MINUTES_PER_DAY))" -print -delete
