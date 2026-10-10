#!/usr/bin/env bash
# Tests the backup scripts (ADR-0025) against the running compose stack (tools/deploy.sh first).
# Checks: scheduler next-run times, archive contents and manifest (checksum, file count), retention after success
# (expired archives and day-old .partial-* leftovers), and that a failed backup writes nothing and deletes nothing.
set -euo pipefail
cd "$(dirname "$0")/../.."

readonly BACKUP_DIR="/backups"
readonly SECONDS_PER_DAY=86400
readonly OLD_ARCHIVE_DAYS=30
readonly OLD_PARTIAL_DAYS=2
failures=0

pass() { echo "PASS $1"; }
fail() { echo "FAIL $1"; failures=$((failures + 1)); }
in_backup() { tools/compose.sh exec -T backup bash -c "$1"; }

# 1. Scheduler: next 02:30 in Europe/Istanbul (UTC+3, no DST since 2016).
check_next() {
  local label="$1" now_utc="$2" expected_utc="$3" actual expected
  actual="$(in_backup "source /backup/scheduler.sh; TZ=Europe/Istanbul next_run_epoch \$(date -u -d '$now_utc' +%s) 02:30")"
  expected="$(in_backup "date -u -d '$expected_utc' +%s")"
  if [[ "$actual" == "$expected" ]]; then pass "$label"; else fail "$label (got $actual, expected $expected)"; fi
}
check_next "scheduler: before 02:30 runs the same night" "2026-10-09 20:00" "2026-10-09 23:30"
check_next "scheduler: after 02:30 runs the next night" "2026-10-10 00:00" "2026-10-10 23:30"
check_next "scheduler: exactly 02:30 runs the next night" "2026-10-09 23:30" "2026-10-10 23:30"

# 2. A run writes one archive with dump, files, keys and manifest; an expired archive goes, a fresh one stays.
in_backup "touch -d @\$((\$(date +%s) - $OLD_ARCHIVE_DAYS * $SECONDS_PER_DAY)) $BACKUP_DIR/tgysm-20000101-000000.tar.gz; touch $BACKUP_DIR/tgysm-29990101-000000.tar.gz"
in_backup "touch -d @\$((\$(date +%s) - $OLD_PARTIAL_DAYS * $SECONDS_PER_DAY)) $BACKUP_DIR/.partial-20000101-000000.tar"
if tools/backup.sh >/dev/null; then pass "backup: exit 0"; else fail "backup: exit 0"; fi
latest="$(in_backup "ls -t $BACKUP_DIR/tgysm-2[0-8]*.tar.gz | head -1")"
mode="$(in_backup "stat -c '%a' $latest")"
if [[ "$mode" == "600" ]]; then pass "archive readable by its owner only (600)"; else fail "archive mode is $mode, expected 600"; fi
contents="$(in_backup "tar -tzf $latest")"
for part in db.dump manifest.json files/ keys/; do
  if grep -q "^$part" <<<"$contents"; then pass "archive has $part"; else fail "archive has $part"; fi
done
manifest="$(in_backup "tar -xzOf $latest manifest.json")"
for key in appVersion latestMigration createdAt dbSha256 tables fileCount; do
  if grep -q "\"$key\"" <<<"$manifest"; then pass "manifest has $key"; else fail "manifest has $key"; fi
done
if grep -Eq '"users" *: *[0-9]' <<<"$manifest"; then pass "manifest counts users"; else fail "manifest counts users"; fi
recorded_sha="$(sed -n 's/.*"dbSha256": "\([0-9a-f]*\)".*/\1/p' <<<"$manifest")"
actual_sha="$(in_backup "tar -xzOf $latest db.dump | sha256sum | cut -d ' ' -f 1")"
if [[ -n "$recorded_sha" && "$recorded_sha" == "$actual_sha" ]]; then pass "manifest dbSha256 matches db.dump"; else fail "manifest dbSha256 '$recorded_sha' vs db.dump $actual_sha"; fi
recorded_files="$(sed -n 's/.*"fileCount": \([0-9]*\).*/\1/p' <<<"$manifest")"
archived_files="$(grep -c '^files/.*[^/]$' <<<"$contents" || true)"
if [[ "$recorded_files" == "$archived_files" ]]; then pass "manifest fileCount matches the archive ($archived_files)"; else fail "manifest fileCount $recorded_files vs $archived_files archived"; fi
if in_backup "test ! -e $BACKUP_DIR/.partial-20000101-000000.tar"; then pass "retention removed a day-old .partial leftover"; else fail "retention removed a day-old .partial leftover"; fi
if in_backup "test ! -e $BACKUP_DIR/tgysm-20000101-000000.tar.gz"; then pass "retention removed the expired archive"; else fail "retention removed the expired archive"; fi
if in_backup "test -e $BACKUP_DIR/tgysm-29990101-000000.tar.gz"; then pass "retention kept a recent archive"; else fail "retention kept a recent archive"; fi

# 3. Database down: non-zero exit, no new archive, nothing deleted.
in_backup "touch -d @\$((\$(date +%s) - $OLD_ARCHIVE_DAYS * $SECONDS_PER_DAY)) $BACKUP_DIR/tgysm-20000102-000000.tar.gz"
count_before="$(in_backup "ls $BACKUP_DIR/tgysm-*.tar.gz | wc -l")"
tools/compose.sh stop postgres >/dev/null 2>&1
if tools/backup.sh >/dev/null 2>&1; then fail "failed backup: non-zero exit"; else pass "failed backup: non-zero exit"; fi
count_after="$(in_backup "ls $BACKUP_DIR/tgysm-*.tar.gz | wc -l")"
if [[ "$count_before" == "$count_after" ]]; then pass "failed backup: no archive added or removed"; else fail "failed backup: archives $count_before -> $count_after"; fi
if in_backup "test -z \"\$(ls -A $BACKUP_DIR | grep -v '^tgysm-')\""; then pass "failed backup: no partial files left"; else fail "failed backup: no partial files left"; fi
tools/compose.sh start postgres >/dev/null 2>&1
in_backup "rm -f $BACKUP_DIR/tgysm-20000102-000000.tar.gz $BACKUP_DIR/tgysm-29990101-000000.tar.gz"

if [[ "$failures" -gt 0 ]]; then echo "$failures check(s) failed"; exit 1; fi
echo "All backup checks passed"
