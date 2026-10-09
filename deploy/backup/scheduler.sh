#!/usr/bin/env bash
# Runs backup.sh every night at BACKUP_TIME (HH:MM) in TZ (ADR-0025). Sourcing this file only defines next_run_epoch.
set -euo pipefail

readonly DEFAULT_BACKUP_TIME="02:30"
readonly SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# next_run_epoch <now-epoch> <HH:MM>: the first moment strictly after now at HH:MM local time (TZ).
next_run_epoch() {
  local now="$1" at="$2" today candidate
  today="$(date -d "@$now" +%F)"
  candidate="$(date -d "$today $at" +%s)"
  if (( candidate <= now )); then
    candidate="$(date -d "$(date -d "$today + 1 day" +%F) $at" +%s)"
  fi
  echo "$candidate"
}

main() {
  local at="${BACKUP_TIME:-$DEFAULT_BACKUP_TIME}" now next
  while true; do
    now="$(date +%s)"
    next="$(next_run_epoch "$now" "$at")"
    echo "Next backup at $(date -d "@$next" '+%F %H:%M %Z')"
    sleep "$((next - now))"
    if ! bash "$SCRIPT_DIR/backup.sh"; then
      echo "Backup FAILED at $(date '+%F %H:%M %Z'); earlier archives are untouched." >&2
    fi
  done
}

if [[ "${BASH_SOURCE[0]}" == "$0" ]]; then
  main
fi
