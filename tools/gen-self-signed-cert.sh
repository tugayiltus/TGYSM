#!/usr/bin/env bash
# Writes a self-signed certificate pair for a test host into deploy/certs/ (never committed).
# Production uses the company certificate: copy it to deploy/certs/tgysm.crt and deploy/certs/tgysm.key.
set -euo pipefail
cd "$(dirname "$0")/.."

readonly CERT_DIR="deploy/certs"
readonly HOST_NAME="${1:-localhost}"
readonly VALID_DAYS=365
readonly KEY_BITS=2048

openssl req -x509 -newkey "rsa:$KEY_BITS" -nodes -days "$VALID_DAYS" \
  -keyout "$CERT_DIR/tgysm.key" -out "$CERT_DIR/tgysm.crt" \
  -subj "/CN=$HOST_NAME" -addext "subjectAltName=DNS:$HOST_NAME"
chmod 600 "$CERT_DIR/tgysm.key"
echo "Wrote $CERT_DIR/tgysm.crt and $CERT_DIR/tgysm.key for $HOST_NAME"
