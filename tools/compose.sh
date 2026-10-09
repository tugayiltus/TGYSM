#!/usr/bin/env bash
# docker compose for the TGYSM stack: always deploy/compose.yaml with deploy/.env (ADR-0024).
set -euo pipefail
cd "$(dirname "$0")/.."

readonly COMPOSE_FILE="deploy/compose.yaml"
readonly ENV_FILE="${TGYSM_ENV_FILE:-deploy/.env}"

if [[ ! -f "$ENV_FILE" ]]; then
  echo "Missing $ENV_FILE: copy deploy/.env.example and fill it in." >&2
  exit 1
fi

exec docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" "$@"
