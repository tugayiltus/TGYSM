#!/usr/bin/env bash
# Builds the TGYSM images (ADR-0024): tgysm-api and tgysm-worker, tagged with the Git short hash and "latest".
set -euo pipefail
cd "$(dirname "$0")/.."

readonly API_IMAGE="tgysm-api"
readonly WORKER_IMAGE="tgysm-worker"
readonly LATEST_TAG="latest"
version="$(git rev-parse --short HEAD)"
readonly version

for target in api worker; do
  image="$API_IMAGE"
  if [[ "$target" == "worker" ]]; then image="$WORKER_IMAGE"; fi
  docker build --target "$target" --build-arg "APP_VERSION=$version" -t "$image:$version" -t "$image:$LATEST_TAG" .
done

echo "Built $API_IMAGE:$version and $WORKER_IMAGE:$version"
