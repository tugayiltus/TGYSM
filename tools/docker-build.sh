#!/usr/bin/env bash
# Builds the TGYSM images (ADR-0024): tgysm-api and tgysm-worker, tagged with the Git short hash and "latest", on fresh
# base images (--pull). Refuses a working tree with uncommitted changes, because the hash tag would then name code the
# image does not hold. --allow-dirty builds it anyway and tags it <hash>-dirty (development and drills only).
set -euo pipefail
cd "$(dirname "$0")/.."

readonly API_IMAGE="tgysm-api"
readonly WORKER_IMAGE="tgysm-worker"
readonly LATEST_TAG="latest"
readonly DIRTY_SUFFIX="-dirty"

allow_dirty=false
if [[ "${1:-}" == "--allow-dirty" ]]; then allow_dirty=true; fi

version="$(git rev-parse --short HEAD)"
if [[ -n "$(git status --porcelain)" ]]; then
  if [[ "$allow_dirty" == false ]]; then
    echo "Refused: the working tree has uncommitted changes (git status). Commit them, or pass --allow-dirty for a <hash>$DIRTY_SUFFIX test image." >&2
    exit 1
  fi
  version="$version$DIRTY_SUFFIX"
  echo "Warning: uncommitted changes; tagging $version." >&2
fi
readonly version

for target in api worker; do
  image="$API_IMAGE"
  if [[ "$target" == "worker" ]]; then image="$WORKER_IMAGE"; fi
  docker build --pull --target "$target" --build-arg "APP_VERSION=$version" -t "$image:$version" -t "$image:$LATEST_TAG" .
done

echo "Built $API_IMAGE:$version and $WORKER_IMAGE:$version"
