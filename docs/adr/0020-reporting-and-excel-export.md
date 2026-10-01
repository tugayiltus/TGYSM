# ADR-0020: Reporting and Excel Export

**Date**: 2026-10-02
**Status**: accepted
**Deciders**: Tugay İLTUŞ

## Context

Phase 5 adds reports (incidents, requests, operations, transfers, assets, warranty) with drill-down lists and Excel export (`docs/superpowers/specs/2026-09-27-phase-4-5-sla-email-knowledge-reports-design.md` §7, commit `3aa02ab`). A figure, the list behind it and the exported file must show the same numbers, results must respect who may see which tickets, and large exports must not block a request. Related: ADR-0013 (ticket access scope), ADR-0014 (worker and row leases), ADR-0012 (file store and scanning).

## Decision

- One `ReportFilter` and one query builder. `ReportScope` applies ADR-0013 rules (admin all tickets; IT staff tickets whose current `team_id` is in their teams; employees 403; asset and warranty reports for IT staff and admins). Each metric has its own predicate; the figure, the drill-down and the export all use the same builder with that predicate, so numbers, lists and files agree. Every ratio returns numerator and denominator; a zero denominator gives `null` (the UI shows "Veri yok").
- Excel files are written with ClosedXML: one data sheet plus a "Filtreler" sheet listing filters and scope; Turkish headers come from a server-side resource used only for export.
- Export up to 5,000 rows (`ReportExportLimits.SyncMaxRows`) is streamed synchronously. Above that, the sync endpoint returns 409 `reports.export_too_large` and the client creates an `export_jobs` row; the worker (row lease, ADR-0014) builds the file into the file store (ADR-0012) and marks it `ready`.
- A ready file lives 24 h (`ReportExportLimits.FileLifetime`); the `Cleanup` job deletes expired files and marks jobs `expired`; download after expiry returns 410 `reports.export_expired`.
- Download is creator-only and re-checks the creator's current role and scope at download time (inactive or demoted creator gets 404). The runner builds the file under the creator's scope, not the worker's. Exports are audited with report and filters.
- Charts use Recharts in a lazy route and a separate `vendor-charts` chunk.

## Alternatives Considered

### Alternative 1: Build the Excel file in the browser from the displayed data
- **Pros**: no server file storage, no jobs, no expiry handling.
- **Cons**: the browser holds only a page of rows, so scope and the row limit cannot be enforced on the full result; the server cannot audit what left, and the file could drift from the server-side numbers.
- **Why not**: scope, limit, audit and consistency must be decided on the server.

### Alternative 2: Always generate in the background
- **Pros**: one code path.
- **Cons**: every small export waits for the worker poll interval and a download step.
- **Why not**: most exports are small; the synchronous path up to 5,000 rows is simpler for users and the job path covers only the rest.

## Consequences

### Positive
- Numbers, drill-down lists and files cannot disagree, because they share one builder and predicate.
- Large exports do not hold a request open; leases keep two workers from building the same file.
- A stale or demoted account cannot fetch an old file.

### Negative
- Two export paths (sync and job) to test and keep in step.
- Files occupy storage for up to 24 h; the creator must wait for a job to finish.
- The shared builder couples every report to one filter model; a report needing a different filter shape needs a builder extension.

### Risks
- A new report that bypasses `ReportScope` would leak tickets across teams; scope must stay inside the builder.
- ClosedXML builds the workbook in memory; the 5,000 row sync limit bounds that, but job exports have no cap beyond practical size.
