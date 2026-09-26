#!/usr/bin/env bash
# Apply EF Core migrations to the database named by TGYSM_DB_* (default: local tgysm_dev).
set -euo pipefail
cd "$(dirname "$0")/../backend"

readonly DB_HOST="${TGYSM_DB_HOST:-localhost}"
readonly DB_NAME="${TGYSM_DB_NAME:-tgysm_dev}"
readonly DB_USER="${TGYSM_DB_USER:-tgysm}"

export TGYSM_CONNECTION_STRING="Host=$DB_HOST;Database=$DB_NAME;Username=$DB_USER"
dotnet tool run dotnet-ef database update \
  --project src/Tgysm.Infrastructure --startup-project src/Tgysm.Infrastructure
