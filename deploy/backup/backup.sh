#!/usr/bin/env bash
# One TGYSM backup (ADR-0025): pg_dump (custom format) + file store + Data Protection keys + manifest.json in
# tgysm-YYYYMMDD-HHMMSS.tar.gz (UTC). Written under a temporary name and renamed when complete; expired archives
# are deleted only after a successful run. Any failure exits non-zero and leaves earlier archives untouched.
set -euo pipefail

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

pg_dump --format=custom --file="$work/db.dump"

latest_migration="$(psql -XtAc 'SELECT migration_id FROM "__EFMigrationsHistory" ORDER BY migration_id DESC LIMIT 1')"
# Exact row counts of every table, as one JSON object (restore checks them).
tables="$(psql -XtAc "SELECT json_object_agg(table_name, (xpath('/row/c/text()', query_to_xml(format('SELECT count(*) AS c FROM %I.%I', table_schema, table_name), false, true, '')))[1]::text::bigint ORDER BY table_name) FROM information_schema.tables WHERE table_schema = 'public' AND table_type = 'BASE TABLE'")"
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
