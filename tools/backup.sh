#!/usr/bin/env bash
# Runs one backup now in the backup service (ADR-0025) and prints the archive path inside the backups volume.
set -euo pipefail
cd "$(dirname "$0")/.."

tools/compose.sh exec -T backup bash /backup/backup.sh
