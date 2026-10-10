#!/usr/bin/env bash
# Restores one TGYSM archive (ADR-0025) into the stack's database and file/key volumes. Runs in the "restore" service.
# Refuses, before writing anything: a file that is not a TGYSM archive (missing parts, a manifest without a well-formed
# latestMigration or dbSha256, a db.dump whose SHA-256 or a file count that differs from the manifest), an archive from
# a newer build than MAX_MIGRATION, and a target database that already has tables (unless --force). Prints what the
# archive holds before writing. The database is restored in one transaction; afterwards the table row counts are
# compared with the manifest and any difference fails.
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
# EF Core migration ids: a 14-digit UTC timestamp, an underscore, the name.
readonly MIGRATION_PATTERN='^[0-9]{14}_[A-Za-z0-9_]+$'
readonly SHA256_PATTERN='^[0-9a-f]{64}$'
readonly TABLE_COUNTS_SQL="SELECT json_object_agg(table_name, (xpath('/row/c/text()', query_to_xml(format('SELECT count(*) AS c FROM %I.%I', table_schema, table_name), false, true, '')))[1]::text::bigint ORDER BY table_name) FROM information_schema.tables WHERE table_schema = 'public' AND table_type = 'BASE TABLE'"

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

not_archive() { echo "Not a usable TGYSM archive ($1): $ARCHIVE" >&2; exit "$EXIT_NOT_ARCHIVE"; }
manifest_value() { sed -n "s/.*\"$1\": \"\{0,1\}\([^\",]*\)\"\{0,1\},\{0,1\}$/\1/p" "$work/manifest.json"; }

if [[ ! "$MAX_MIGRATION" =~ $MIGRATION_PATTERN ]]; then
  echo "MAX_MIGRATION is not a migration id: '$MAX_MIGRATION'" >&2
  exit 1
fi

listing="$(tar -tzf "$ARCHIVE" 2>/dev/null)" || not_archive "not a readable tar.gz"
for part in db.dump manifest.json files/ keys/; do
  if ! grep -q "^$part" <<<"$listing"; then not_archive "missing $part"; fi
done
tar -xzf "$ARCHIVE" -C "$work" manifest.json db.dump

latest="$(manifest_value latestMigration)"
created="$(manifest_value createdAt)"
app_version="$(manifest_value appVersion)"
dump_sha256="$(manifest_value dbSha256)"
file_count="$(manifest_value fileCount)"
echo "Archive: created ${created:-?}, app version ${app_version:-?}, latest migration ${latest:-?}, ${file_count:-?} file(s)."

if [[ ! "$latest" =~ $MIGRATION_PATTERN ]]; then not_archive "manifest latestMigration is empty or malformed: '$latest'"; fi
if [[ ! "$dump_sha256" =~ $SHA256_PATTERN ]]; then
  not_archive "manifest has no dbSha256; archives from before checksums cannot be verified, take a new backup"
fi
if [[ "$(sha256sum "$work/db.dump" | cut -d ' ' -f 1)" != "$dump_sha256" ]]; then not_archive "db.dump does not match the manifest's SHA-256"; fi
if [[ "$(grep -c '^files/.*[^/]$' <<<"$listing" || true)" != "$file_count" ]]; then
  not_archive "file count differs from the manifest's fileCount $file_count"
fi

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

pg_restore --no-owner --exit-on-error --single-transaction --dbname="$PGDATABASE" "$work/db.dump"

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
