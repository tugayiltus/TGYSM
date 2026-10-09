#!/usr/bin/env bash
# Apply EF Core migrations and the post-migration data steps to the database named by TGYSM_DB_* (default: local
# tgysm_dev), through the API's "migrate" command — the same path the deployment uses (ADR-0024).
set -euo pipefail
cd "$(dirname "$0")/../backend"

readonly DB_HOST="${TGYSM_DB_HOST:-localhost}"
readonly DB_NAME="${TGYSM_DB_NAME:-tgysm_dev}"
readonly DB_USER="${TGYSM_DB_USER:-tgysm}"

export ConnectionStrings__Default="Host=$DB_HOST;Database=$DB_NAME;Username=$DB_USER"
export ASPNETCORE_ENVIRONMENT="Development"
dotnet run --project src/Tgysm.Api --no-launch-profile -v quiet -- migrate
