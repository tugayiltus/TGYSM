#!/usr/bin/env bash
# Deploys the TGYSM stack (ADR-0024): quality gates, image build, then compose up (migrate runs before api/worker),
# then waits until the API answers through nginx. --skip-gates skips lint and tests (explicit only).
set -euo pipefail
cd "$(dirname "$0")/.."

readonly READY_TIMEOUT_SECONDS=180
readonly HEALTH_PATH="/api/v1/health"
readonly ENV_FILE="${TGYSM_ENV_FILE:-deploy/.env}"
skip_gates=false
if [[ "${1:-}" == "--skip-gates" ]]; then skip_gates=true; fi

for file in deploy/certs/tgysm.crt deploy/certs/tgysm.key "$ENV_FILE"; do
  if [[ ! -f "$file" ]]; then
    echo "Missing $file (see docs/operations.md)." >&2
    exit 1
  fi
done

if [[ "$skip_gates" == false ]]; then
  tools/lint.sh
  tools/test.sh
fi

tools/docker-build.sh
TGYSM_VERSION="$(git rev-parse --short HEAD)" tools/compose.sh up -d --remove-orphans

https_port="$(sed -n 's/^HTTPS_PORT=//p' "$ENV_FILE")"
https_port="${https_port:-443}"
for ((second = 0; second < READY_TIMEOUT_SECONDS; second++)); do
  if curl -fsSk -o /dev/null "https://localhost:$https_port$HEALTH_PATH"; then
    echo "TGYSM is up on https://localhost:$https_port"
    exit 0
  fi
  sleep 1
done

echo "Timed out waiting for $HEALTH_PATH through nginx; see tools/compose.sh logs" >&2
tools/compose.sh ps >&2
exit 1
