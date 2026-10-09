#!/usr/bin/env bash
# Restores one TGYSM archive (ADR-0025) into the stack's database and file/key volumes. Runs in the "restore" service.
# Refuses, before writing anything: a file that is not a TGYSM archive, an archive from a newer build than MAX_MIGRATION,
# and a target database that already has tables (unless --force). After restoring it compares the table row counts
# with the manifest and fails on any difference.
set -euo pipefail
umask 077

readonly ARCHIVE="${1:?usage: restore.sh <archive> [--force]}"
readonly FORCE="${2:-}"
readonly DATA_DIR="${DATA_DIR:-/data}"
readonly MAX_MIGRATION="${MAX_MIGRATION:?MAX_MIGRATION (newest migration of the app image) is required}"
# The non-root user of the TGYSM images (mcr.microsoft.com/dotnet APP_UID).
readonly APP_UID=1654
readonly EXIT_NOT_ARCHIVE=2
readonly EXIT_NEWER_ARCHIVE=3
readonly EXIT_NOT_EMPTY=4
readonly EXIT_COUNT_MISMATCH=5
readonly TABLE_COUNTS_SQL="SELECT json_object_agg(table_name, (xpath('/row/c/text()', query_to_xml(format('SELECT count(*) AS c FROM %I.%I', table_schema, table_name), false, true, '')))[1]::text::bigint ORDER BY table_name) FROM information_schema.tables WHERE table_schema = 'public' AND table_type = 'BASE TABLE'"

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

listing="$(tar -tzf "$ARCHIVE" 2>/dev/null)" || { echo "Not a readable archive: $ARCHIVE" >&2; exit "$EXIT_NOT_ARCHIVE"; }
for part in db.dump manifest.json files/ keys/; do
  if ! grep -q "^$part" <<<"$listing"; then
    echo "Not a TGYSM archive (missing $part): $ARCHIVE" >&2
    exit "$EXIT_NOT_ARCHIVE"
  fi
done
tar -xzf "$ARCHIVE" -C "$work" manifest.json

latest="$(sed -n 's/.*"latestMigration": "\([^"]*\)".*/\1/p' "$work/manifest.json")"
# Migration ids start with a UTC timestamp, so they sort as text.
if [[ "$latest" > "$MAX_MIGRATION" ]]; then
  echo "Refused: the archive is from a newer build (migration $latest, this build knows up to $MAX_MIGRATION)." >&2
  exit "$EXIT_NEWER_ARCHIVE"
fi

existing="$(psql -XtAc "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'public'")"
if (( existing > 0 )); then
  if [[ "$FORCE" != "--force" ]]; then
    echo "Refused: the target database already has $existing table(s). Use --force to replace it." >&2
    exit "$EXIT_NOT_EMPTY"
  fi
  psql -Xqc "DROP SCHEMA public CASCADE; CREATE SCHEMA public;"
fi

tar -xzf "$ARCHIVE" -C "$work" db.dump
pg_restore --no-owner --exit-on-error --dbname="$PGDATABASE" "$work/db.dump"

find "$DATA_DIR/files" "$DATA_DIR/keys" -mindepth 1 -delete
tar -xzf "$ARCHIVE" -C "$DATA_DIR" files keys
chown -R "$APP_UID:$APP_UID" "$DATA_DIR/files" "$DATA_DIR/keys"
chmod 0700 "$DATA_DIR/keys"

expected="$(sed -n 's/.*"tables": \(.*\)$/\1/p' "$work/manifest.json")"
actual="$(psql -XtAc "$TABLE_COUNTS_SQL")"
if [[ "$expected" != "$actual" ]]; then
  echo "Row counts differ from the manifest after restore." >&2
  echo "expected: $expected" >&2
  echo "actual:   $actual" >&2
  exit "$EXIT_COUNT_MISMATCH"
fi
echo "Restored $ARCHIVE (migration $latest); row counts match the manifest."
