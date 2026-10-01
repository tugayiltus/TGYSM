#!/usr/bin/env bash
# Plan progress: task status from the plan's SDD ledger, plus branch, recent commits and uncommitted changes.
# Usage: tools/progress.sh [plan-file]   (default: newest plan in docs/superpowers/plans/)
set -euo pipefail
cd "$(dirname "$0")/.."

readonly PLANS_DIR="docs/superpowers/plans"
readonly SDD_DIR=".superpowers/sdd"
readonly LEDGER_TAIL_LINES=5
readonly RECENT_COMMITS=5
readonly MAX_CHANGED_FILES=30

plan="${1:-$(ls -1 "$PLANS_DIR"/*.md | sort | tail -1)}"
ledger="$SDD_DIR/$(basename "$plan" .md)/progress.md"

echo "== Plan: $plan =="
if [ ! -f "$ledger" ]; then
  echo "No ledger at $ledger (plan not started, or finished and workspace removed)."
else
  grep -E '^### Task [0-9]+:' "$plan" | sed -E 's/^### Task ([0-9]+): (.*)/\1|\2/' |
    while IFS='|' read -r n title; do
      last="$(grep -E "^Task $n: " "$ledger" | tail -1 || true)"
      if grep -qE "^Task $n: complete" "$ledger"; then status="DONE"
      elif [ -n "$last" ]; then status="ACTIVE -> ${last#Task $n: }"
      else status="todo"; fi
      printf '%3s  %-45s %s\n' "$n" "$title" "$status"
    done

  echo; echo "== Last $LEDGER_TAIL_LINES ledger lines =="
  tail -"$LEDGER_TAIL_LINES" "$ledger"
fi

echo; echo "== Branch / last commits =="
git branch --show-current
git log --oneline -"$RECENT_COMMITS"

echo; echo "== Uncommitted changes =="
git status --short | head -"$MAX_CHANGED_FILES"
