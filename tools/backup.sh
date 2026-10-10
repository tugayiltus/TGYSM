#!/usr/bin/env bash
# Runs one backup now in the backup service (ADR-0025) and prints the archive path inside the backups volume.
# --safety: the safety backup tools/restore.sh takes before --force. It runs in a one-off backup container (the backup
# service is stopped during a restore) and skips retention, so an expired archive that is about to be restored is not
# deleted first.
#
#   tools/backup.sh [--safety]
set -euo pipefail
cd "$(dirname "$0")/.."

readonly USAGE="usage: tools/backup.sh [--safety]"
readonly SKIP_RETENTION_VARIABLE="BACKUP_SKIP_RETENTION"

case "${1:-}" in
  "") tools/compose.sh exec -T backup bash /backup/backup.sh ;;
  --safety) tools/compose.sh run --rm --no-deps -T -e "$SKIP_RETENTION_VARIABLE=true" --entrypoint bash backup /backup/backup.sh ;;
  *) echo "$USAGE" >&2; exit 1 ;;
esac
