#!/usr/bin/env bash
# Lint gate: C# formatting, ESLint (zero warnings), TypeScript, Astryx doctor (ADR-0027) and the tools/deploy paths named in the docs.
set -euo pipefail
cd "$(dirname "$0")/.."

readonly MIGRATIONS_DIR="backend/src/Tgysm.Infrastructure/Persistence/Migrations"

dotnet format backend/Tgysm.slnx --verify-no-changes --exclude "$MIGRATIONS_DIR"
(cd frontend && npm run lint && npm run typecheck)
# Astryx doctor is a hard gate: any [fail] check stops the lint, and so does a growing count of purity findings.
# The budget covers the known, reasoned exceptions: native download links (the router must not handle a file URL),
# the Chart.js canvas parent (position + height), and native <form>/<main> semantics. Lower it when one goes away.
export DOCTOR_FINDINGS_BUDGET=16
doctor_json="$(cd frontend && npx astryx doctor --built . --json)"
DOCTOR_JSON="$doctor_json" node -e '
  const report = JSON.parse(process.env.DOCTOR_JSON);
  const checks = [];
  const walk = (node) => {
    if (Array.isArray(node)) { node.forEach(walk); return; }
    if (node && typeof node === "object") {
      if (typeof node.id === "string" && typeof node.status === "string") { checks.push(node); return; }
      Object.values(node).forEach(walk);
    }
  };
  walk(report);
  const failed = checks.filter((check) => check.status === "fail");
  const purity = checks.find((check) => check.id === "built-astryx-purity");
  const findings = purity && purity.status !== "ok" ? Number((/^(\d+) findings/.exec(purity.message) ?? [])[1] ?? 0) : 0;
  const budget = Number(process.env.DOCTOR_FINDINGS_BUDGET);
  failed.forEach((check) => console.error(`astryx doctor [fail] ${check.id}: ${check.message}`));
  if (findings > budget) console.error(`astryx doctor: ${findings} purity findings, budget ${budget}. ${purity.message}`);
  if (checks.length === 0) console.error("astryx doctor: no checks in the JSON report");
  process.exit(failed.length > 0 || findings > budget || checks.length === 0 ? 1 : 0);
'
tools/check-doc-paths.sh
