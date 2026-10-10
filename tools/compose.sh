#!/usr/bin/env bash
# docker compose for the TGYSM stack: always deploy/compose.yaml with deploy/.env (ADR-0024). The image tag is the one
# tools/deploy.sh recorded in deploy/.version; TGYSM_VERSION in the environment overrides it.
set -euo pipefail
cd "$(dirname "$0")/.."

readonly COMPOSE_FILE="deploy/compose.yaml"
readonly ENV_FILE="${TGYSM_ENV_FILE:-deploy/.env}"
readonly VERSION_FILE="deploy/.version"

if [[ ! -f "$ENV_FILE" ]]; then
  echo "Missing $ENV_FILE: copy deploy/.env.example and fill it in." >&2
  exit 1
fi

if [[ -z "${TGYSM_VERSION:-}" && -s "$VERSION_FILE" ]]; then
  TGYSM_VERSION="$(tr -d '[:space:]' < "$VERSION_FILE")"
  export TGYSM_VERSION
fi

exec docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" "$@"
