#!/usr/bin/env bash
# One TGYSM backup (ADR-0025): pg_dump (custom format) + file store + Data Protection keys + manifest.json in
# tgysm-YYYYMMDD-HHMMSS.tar.gz (UTC). The manifest carries the SHA-256 of db.dump and the number of files the archive
# holds; restore checks both before writing. Written under a temporary name and renamed when complete; expired archives
# and day-old temporary leftovers are deleted only after a successful run. Any failure exits non-zero and leaves
# earlier archives untouched.
set -euo pipefail
# Archives hold the database and the Data Protection keys: owner-only files (security review, ADR-0025).
umask 077

readonly BACKUP_DIR="${BACKUP_DIR:-/backups}"
readonly DATA_DIR="${DATA_DIR:-/data}"
readonly RETENTION_DAYS="${BACKUP_RETENTION_DAYS:-14}"
readonly MINUTES_PER_DAY=1440
readonly ARCHIVE_PREFIX="tgysm-"
readonly PARTIAL_PREFIX=".partial-"
# GNU tar exit code 1: a file changed while it was read (attachments are written while the backup runs).
readonly TAR_FILE_CHANGED=1

stamp="$(date -u +%Y%m%d-%H%M%S)"
archive="$BACKUP_DIR/$ARCHIVE_PREFIX$stamp.tar.gz"
partial_tar="$BACKUP_DIR/$PARTIAL_PREFIX$stamp.tar"
partial="$partial_tar.gz"
work="$(mktemp -d)"
cleanup() { rm -rf "$work"; rm -f "$partial_tar" "$partial"; }
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
dump_sha256="$(sha256sum "$work/db.dump" | cut -d ' ' -f 1)"

# File store first: a file that changes while it is read is tolerated there (the database snapshot is what counts),
# never in the keys. The manifest's fileCount is what the archive really holds.
tar_status=0
tar -cf "$partial_tar" --warning=no-file-changed -C "$DATA_DIR" files || tar_status=$?
if (( tar_status != 0 && tar_status != TAR_FILE_CHANGED )); then exit "$tar_status"; fi
tar -rf "$partial_tar" -C "$DATA_DIR" keys
file_count="$(tar -tf "$partial_tar" files | grep -c '[^/]$' || true)"

cat > "$work/manifest.json" <<JSON
{
  "appVersion": "${APP_VERSION:-unknown}",
  "latestMigration": "$latest_migration",
  "createdAt": "$(date -u +%Y-%m-%dT%H:%M:%SZ)",
  "dbSha256": "$dump_sha256",
  "fileCount": $file_count,
  "tables": $tables
}
JSON

tar -rf "$partial_tar" -C "$work" db.dump manifest.json
gzip "$partial_tar"
mv "$partial" "$archive"
echo "Backup written: $archive"

find "$BACKUP_DIR" -maxdepth 1 -name "$ARCHIVE_PREFIX*.tar.gz" -mmin "+$((RETENTION_DAYS * MINUTES_PER_DAY))" -print -delete
# Leftovers of runs killed before their cleanup (power loss, OOM); a day old, so never a run in progress.
find "$BACKUP_DIR" -maxdepth 1 -name "$PARTIAL_PREFIX*" -mmin "+$MINUTES_PER_DAY" -print -delete
