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

## Amendment 2026-10-08

- Background exports are capped at 100,000 rows (`ReportExportLimits.JobMaxRows`): a larger request is refused when it is queued, and a job that has grown past the cap by the time it runs fails with `reports.export_too_large`. This closes the "no cap beyond practical size" risk above (commit 5ed4cfc).
- A job may start at most 3 runs (`ReportExportLimits.MaxAttempts`, column `export_jobs.attempts`). A run that takes the Worker down never records a failure, so its expired lease makes the job claimable again; once 3 runs have started, the next claim fails the job with `reports.export_attempts_exhausted` instead of running it (commit 7e3cf77).
- One user may have at most 3 pending or running background exports (`ReportExportLimits.MaxQueuedPerUser`); one more is refused with 409 `reports.export_queue_full`. The count and the insert run under `SerializationLocks.ExportQueue` so parallel requests cannot both pass (commit 7e3cf77).
- The API serves files the Worker writes, so both hosts must use one absolute file-store folder (ADR-0012): `Files:RootPath` is required outside Development (startup fails without it) and both hosts log the resolved root at startup (`FileStoreRootLog`). In Development an empty value means the user's local application data folder (`tgysm/files`), which both hosts resolve alike (commit 7e3cf77).
- Cleanup expires ready exports in batches of 100 (`CleanupHandler.ExportExpiryBatchSize`); it deletes the file first, and a file that cannot be deleted keeps its job `ready` for a later run (commit 7e3cf77).
- The chart is loaded lazily by the figure card (`DistributionChart`), not by a lazy route, so `vendor-charts` loads only with the first distribution card (commit 65d92e9).

## Amendment 2026-10-09

The chart library part of this decision (Recharts) is superseded by ADR-0022: report charts use Chart.js through `@coreui/react-chartjs`.
