# Performance measurement

Run by `tools/perf-measure.sh` on 2026-11-19T11:03:40Z at commit `812a5e5`. Spec 5.2; acceptance criterion 18 of `tgysm.md` section 20.

- Target: local Release API on http://localhost:5313, database tgysm_pilot_p9.
- Machine: Apple M1, 8 logical cores, 16 GiB RAM, macOS 26.7.
- Data volume before the run (tickets and assets as the IT staff account sees them, changes and tasks as the admin sees them): tickets=2000 assets=500 changes=60 tasks=229.
- Runs per operation: 5; times are wall-clock milliseconds from request to the last byte of the body, over HTTP on the same machine (no network, no browser rendering).
- The CSV import commit writes 5,000 assets, so each run grows the database by that many; it is timed once per run (one sample), the preview 5 times.
- No numeric targets are set (criterion 18); outliers are reported to the owner, not tuned.

| Operation | Runs | Median (ms) | p95 (ms) |
|---|---:|---:|---:|
| Ticket list (page of 50) | 5 | 9 | 10 |
| Ticket search | 5 | 10 | 12 |
| Workspace queue (unassigned) | 5 | 10 | 11 |
| Asset list (page of 50) | 5 | 10 | 11 |
| Asset search (brand name) | 5 | 12 | 13 |
| Change calendar (62 days) | 5 | 6 | 8 |
| Report: incidents | 5 | 50 | 55 |
| Report: requests | 5 | 22 | 24 |
| Report: operations | 5 | 22 | 31 |
| Report: transfers | 5 | 8 | 8 |
| Report: assets | 5 | 6 | 6 |
| Report: warranty | 5 | 4 | 5 |
| Report: changes | 5 | 4 | 5 |
| Report: tasks | 5 | 5 | 5 |
| Drill-down page (incidents, mttr) | 5 | 9 | 10 |
| Synchronous Excel export (incidents, mttr) | 5 | 76 | 112 |
| CSV import preview (5000 rows) | 5 | 130 | 135 |
| CSV import commit (5000 rows, once per run) | 1 | 3677 | 3677 |
