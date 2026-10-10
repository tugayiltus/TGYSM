#!/usr/bin/env bash
# Pilot seed data (spec §5.1) through the API's "seed-pilot" command: users, organisation, catalog, assets, articles
# and tickets over the past 90 days, written by the application handlers. Prints the pilot password once.
#
#   tools/seed-pilot.sh [--scale <factor>]            local: seed TGYSM_DB_NAME (default tgysm_pilot), Development
#   tools/seed-pilot.sh --fresh [--scale <factor>]    local: drop and recreate the database, migrate, then seed
#   tools/seed-pilot.sh --drop                        local: drop the pilot database and stop
#   tools/seed-pilot.sh --compose [--scale <factor>]  deployed stack: one-off api container in the Pilot environment
#
# The command refuses outside Development/Pilot and once the database has a ticket. --fresh and --drop only touch
# databases whose name starts with tgysm_pilot.
set -euo pipefail
cd "$(dirname "$0")/.."

readonly DB_HOST="${TGYSM_DB_HOST:-localhost}"
readonly DB_NAME="${TGYSM_DB_NAME:-tgysm_pilot}"
readonly DB_USER="${TGYSM_DB_USER:-tgysm}"
readonly ADMIN_DB="postgres"
readonly PILOT_DB_PREFIX="tgysm_pilot"
readonly COMMAND="seed-pilot"
readonly PILOT_ENVIRONMENT="Pilot"

usage() {
  echo "Usage: tools/seed-pilot.sh [--compose | --fresh | --drop] [--scale <factor>]" >&2
  exit 1
}

mode="local"
seed_args=()
while [[ $# -gt 0 ]]; do
  case "$1" in
    --compose) mode="compose"; shift ;;
    --fresh) mode="fresh"; shift ;;
    --drop) mode="drop"; shift ;;
    --scale)
      [[ $# -ge 2 ]] || usage
      seed_args+=("--scale" "$2"); shift 2 ;;
    *) usage ;;
  esac
done

if [[ "$mode" == "compose" ]]; then
  exec tools/compose.sh run --rm -e ASPNETCORE_ENVIRONMENT="$PILOT_ENVIRONMENT" api "$COMMAND" "${seed_args[@]+"${seed_args[@]}"}"
fi

drop_database() {
  if [[ "$DB_NAME" != "$PILOT_DB_PREFIX"* ]]; then
    echo "Refusing to drop $DB_NAME: only databases named $PILOT_DB_PREFIX* are dropped." >&2
    exit 1
  fi
  psql -h "$DB_HOST" -d "$ADMIN_DB" -v ON_ERROR_STOP=1 -qc "DROP DATABASE IF EXISTS \"$DB_NAME\" WITH (FORCE)"
  echo "Database $DB_NAME dropped."
}

if [[ "$mode" == "drop" ]]; then
  drop_database
  exit 0
fi

if [[ "$mode" == "fresh" ]]; then
  drop_database
  psql -h "$DB_HOST" -d "$ADMIN_DB" -v ON_ERROR_STOP=1 -qc "CREATE DATABASE \"$DB_NAME\" OWNER \"$DB_USER\""
  echo "Database $DB_NAME created."
  TGYSM_DB_HOST="$DB_HOST" TGYSM_DB_NAME="$DB_NAME" TGYSM_DB_USER="$DB_USER" tools/migrate.sh
fi

export ConnectionStrings__Default="Host=$DB_HOST;Database=$DB_NAME;Username=$DB_USER"
export ASPNETCORE_ENVIRONMENT="Development"
dotnet run --project backend/src/Tgysm.Api --no-launch-profile -v quiet -- "$COMMAND" "${seed_args[@]+"${seed_args[@]}"}"
