# ADR-0012: Attachment Storage and Scanning

**Date**: 2026-09-26
**Status**: accepted
**Deciders**: Tugay İLTUŞ

## Context

Tickets take attachments: 10 MB each, up to 5 per request, PDF/PNG/JPEG/TXT/CSV; stored outside the web root; downloadable only through the authorized API and only after scanning (`tgysm.md` §7.2). Hosting and antivirus are not chosen yet.

## Decision

- `IFileStore` port; `LocalFileStore` writes to a configurable folder (`Storage:RootPath`, outside `wwwroot`) using random keys, never the original name.
- `FileTypeSniffer` checks magic bytes (PDF, PNG, JPEG) and UTF-8 validity without NUL bytes (TXT, CSV); extension and declared content type must agree.
- `IFileScanner` port; `DevelopmentFileScanner` marks type-checked files `clean`. Attachments are downloadable only when `scan_status = clean`.
- Metadata lives in `ticket_attachments`; the attachment inherits the visibility of its comment.
- A production scanner (for example ClamAV) is chosen with hosting in Phase 6.

## Alternatives Considered

### Alternative 1: ClamAV now
- **Pros**: real scanning from day one.
- **Cons**: extra daemon in every developer and test setup.
- **Why not**: owner chose the port now, real scanner with hosting.

### Alternative 2: Store files in PostgreSQL (`bytea`)
- **Pros**: one backup.
- **Cons**: large rows, heavier backups and queries.
- **Why not**: files stay on a file store; metadata in the database.

## Consequences

### Positive
- Storage and scanner can change without touching handlers.

### Negative
- Until Phase 6 no malware scanning exists; type checks only.

### Risks
- Backups must include the file folder together with the database (`tgysm.md` §17).
