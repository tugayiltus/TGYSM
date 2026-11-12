# ADR-0029: Yearly Asset Tags (ASSET-YYYY-NNNN)

**Date**: 2026-11-12
**Status**: accepted
**Deciders**: Tugay İLTUŞ

## Context

ADR-0023 made the server generate every asset tag: `DMR-` and a six-digit number from the PostgreSQL sequence `asset_tag_seq`. The owner wants a tag that reads like the ticket numbers (ADR-0011) and shows the year: `ASSET-2026-0001`.

Commit: `72df221`.

## Decision

- **Format:** `ASSET-`, the company calendar year (`App:TimeZone`) of creation, and a number counted from 1 each year. The number has at least four digits and grows past 9999 (`ASSET-2026-10000`); it is never truncated (`AssetTagFormat`).
- **Counter:** table `asset_tag_counters` (one row per year), incremented by one `INSERT … ON CONFLICT DO UPDATE … RETURNING`, as for ticket numbers. Concurrent creates get distinct numbers; a rolled-back save leaves a gap, never a reuse. A number whose tag an asset already holds is skipped. The sequence `asset_tag_seq` is dropped.
- **Every tag is generated.** No asset gets a tag from a person or a file.
- **Existing tags:** the post-migration step `RenumberAssetTagsStep` (`2026-11-renumber-asset-tags`) turns every tag not yet in the new format into it once, both old `DMR-` tags and hand-typed legacy tags from CSV import (such as `dmrbs555555`): per company year of creation, in creation order. It sets each year's counter to the last number used and audits each change (`assets.tag_renumbered`, old and new tag). The step runs in `migrate` because it needs the company time zone, which SQL migrations cannot read.
- **CSV import:** the template has no "Demirbaş No" column. A file made for the old template may still have it; the column is not read, and every row gets a generated tag at commit. The error code `assets.import_reserved_tag` is removed.
- **QR labels (ADR-0028)** keep working after the renumbering: they carry the asset id, not the tag. Printed labels show the old text until they are printed again.

## Alternatives Considered

### Alternative 1: Keep the sequence and only change the text
- **Pros**: No new table; one global number.
- **Cons**: The number would not restart each year, so `ASSET-2027-0153` would not mean "the 153rd device of 2027".
- **Why not**: The owner chose a yearly count, matching ticket numbers.

### Alternative 2: Keep existing tags and use the new format only for new assets
- **Pros**: Printed labels stay correct; no data step.
- **Cons**: Two or more tag formats in one inventory for years.
- **Why not**: The owner chose to convert every existing tag.

### Alternative 4: Keep accepting legacy tags in CSV import
- **Pros**: A company moving from another inventory keeps its old labels.
- **Cons**: Mixed formats return with every import.
- **Why not**: The owner chose that the system gives every tag.

### Alternative 3: Convert existing tags in a SQL migration
- **Pros**: No application code in the data change.
- **Cons**: SQL migrations cannot read `App:TimeZone`, so the year of an asset created on New Year's Eve would follow UTC instead of the company calendar.
- **Why not**: A post-migration step has the configured zone.

## Consequences

### Positive
- Tags show the year and read like ticket numbers.
- One counter mechanism for tickets and assets.

### Negative
- Labels printed with an old tag show a number the system no longer uses; reprint them from the inventory list.
- An old CSV file with filled tags imports without error, but its tags are replaced; the preview no longer shows a tag column.
- Audit and history entries written before the step keep the old tag text.

### Risks
- A tag typed by a person into another system (spreadsheets, purchase records) no longer matches; the audit entry maps old to new.
