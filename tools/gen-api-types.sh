#!/usr/bin/env bash
# Regenerate the OpenAPI document and the frontend API types after an API contract change.
set -euo pipefail
cd "$(dirname "$0")/.."

readonly OPENAPI_FILE="openapi/Tgysm.Api.json"
readonly SCHEMA_FILE="src/shared/api/schema.d.ts"

mkdir -p frontend/openapi
# The OpenAPI generator starts the host, which validates App:PublicBaseUrl (empty by default) on start.
export App__PublicBaseUrl="http://localhost:5173"
dotnet build backend/src/Tgysm.Api --nologo -p:OpenApiGenerateDocuments=true
cd frontend
npx openapi-typescript "$OPENAPI_FILE" -o "$SCHEMA_FILE"
echo "Generated $OPENAPI_FILE and $SCHEMA_FILE"
