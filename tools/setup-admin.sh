#!/usr/bin/env bash
# Create the first admin (once). Usage: tools/setup-admin.sh --email <email> --first-name <name> --last-name <name>
set -euo pipefail
cd "$(dirname "$0")/.."

readonly DB_HOST="${TGYSM_DB_HOST:-localhost}"
readonly DB_NAME="${TGYSM_DB_NAME:-tgysm_dev}"
readonly DB_USER="${TGYSM_DB_USER:-tgysm}"

tools/migrate.sh
export ConnectionStrings__Default="Host=$DB_HOST;Database=$DB_NAME;Username=$DB_USER"
dotnet run --project backend/src/Tgysm.Api --no-launch-profile -- setup-admin "$@"
