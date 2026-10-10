#!/usr/bin/env bash
# Lints the GitHub Actions workflows (actionlint; install: brew install actionlint).
set -euo pipefail
cd "$(dirname "$0")/.."

actionlint .github/workflows/*.yml
