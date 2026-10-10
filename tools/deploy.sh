#!/usr/bin/env bash
# Deploys the TGYSM stack (ADR-0024): quality gates, image build, fresh third-party images, then compose up (migrate
# runs before api/worker), then waits until the API answers through nginx. The deployed tag (Git short hash) is
# recorded in deploy/.version, so later tools/compose.sh calls keep using it. Refuses a working tree with uncommitted
# changes. --skip-gates skips lint and tests (explicit only).
set -euo pipefail
cd "$(dirname "$0")/.."
source tools/lib/env-file.sh

readonly READY_TIMEOUT_SECONDS=180
readonly HEALTH_PATH="/api/v1/health"
readonly ENV_FILE="${TGYSM_ENV_FILE:-deploy/.env}"
readonly VERSION_FILE="deploy/.version"
readonly DEFAULT_HTTPS_PORT=443
# Images compose pulls from a registry; tgysm-api and tgysm-worker are built locally.
readonly THIRD_PARTY_SERVICES=(postgres nginx)
skip_gates=false
if [[ "${1:-}" == "--skip-gates" ]]; then skip_gates=true; fi

for file in deploy/certs/tgysm.crt deploy/certs/tgysm.key "$ENV_FILE"; do
  if [[ ! -f "$file" ]]; then
    echo "Missing $file (see docs/operations.md)." >&2
    exit 1
  fi
done

if [[ -n "$(git status --porcelain)" ]]; then
  echo "Refused: the working tree has uncommitted changes (git status). Deploy a committed state only." >&2
  exit 1
fi

if [[ "$skip_gates" == false ]]; then
  tools/lint.sh
  tools/test.sh
fi

tools/docker-build.sh
version="$(git rev-parse --short HEAD)"
previous="$(cat "$VERSION_FILE" 2>/dev/null || true)"
echo "$version" > "$VERSION_FILE"
echo "Deploying $version (previous: ${previous:-none}); recorded in $VERSION_FILE."
tools/compose.sh pull "${THIRD_PARTY_SERVICES[@]}"
tools/compose.sh up -d --remove-orphans

https_port="$(env_value "$ENV_FILE" HTTPS_PORT)"
https_port="${https_port:-$DEFAULT_HTTPS_PORT}"
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
