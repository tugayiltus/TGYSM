# ADR-0023: Admin-Managed Device Catalog for Asset Brands and Models

**Date**: 2026-10-09
**Status**: accepted
**Deciders**: Tugay İLTUŞ

## Context

Assets stored brand and model as free text typed into the asset form and the CSV import. The UI/UX review (`docs/superpowers/specs/2026-10-09-ui-ux-review.md` §7, UX-02) found spelling variants and duplicates ("Lenovo", " LENOVO ", "Lenova"), which split reports and searches. The owner decided that brands and models are admin-managed selection lists, and that a CSV row naming an unknown brand or model fails instead of creating one (`docs/superpowers/specs/2026-10-09-coreui-ui-ux-design.md` §6.1). Related: ADR-0010 (asset lifecycle), ADR-0011 (CSV import).

## Decision

- New tables `brands` and `asset_models`. A model belongs to one brand and one device type. Brand names are unique by `CatalogName.Normalize` (whitespace collapsed, then `TurkishText.ToSearchKey`); model names are unique per brand and type by the same key. Punctuation and digits are kept, so `ThinkPad T14` and `ThinkPad T14s` stay different.
- Admins manage the catalog under `/api/v1/admin/brands` and `/api/v1/admin/models` (create, rename, deactivate, reactivate; never delete). IT staff and admins read active entries for forms from `GET /api/v1/assets/catalog`. Changes are audited (`brands.*`, `asset_models.*`).
- An asset references its model through the required `assets.model_id` foreign key. The asset's type comes from the model, so type and model cannot disagree. Create and update take `modelId`; an inactive model is refused (`assets.model_inactive`) except for the asset's own current model, so assets whose model was retired stay editable.
- `assets.brand` and `assets.model` remain as server-maintained snapshots of the catalog names. They are set only from the chosen model and are rewritten for every affected asset when a brand or model is renamed, in the same transaction (`SerializationLocks.AssetCatalog`). This keeps the trigram search indexes, report and export rows and ticket asset links working without extra joins.
- CSV import resolves brand, type and model against the active catalog by the same key; a miss is the row error `assets.import_catalog_missing` and the all-or-nothing commit writes nothing.
- Migration `AssetCatalog` creates catalog entries from the distinct existing brand/type/model values (same normalization in SQL) and links every asset.

## Alternatives Considered

### Alternative 1: Keep free text and add suggestions
- **Pros**: no schema change; fast entry.
- **Cons**: suggestions do not stop new variants; CSV still writes anything.
- **Why not**: the owner asked for selection lists that prevent spelling errors and duplicates.

### Alternative 2: Drop the brand/model text columns and always join the catalog
- **Pros**: one source of truth; renames need no row updates.
- **Cons**: search indexes, report/export rows and ticket links would all need joins; larger change for no visible gain at single-company scale.
- **Why not**: snapshots kept in step by the server give the same consistency with far less churn.

### Alternative 3: Create unknown catalog entries from CSV after admin confirmation
- **Pros**: faster bulk import.
- **Cons**: a typo confirmed once becomes a permanent catalog entry.
- **Why not**: the owner chose a row error ("Tanım eksik").

## Consequences

### Positive
- No spelling variants or duplicate brands/models in new data; reports group correctly.
- Retired models stay readable and editable on existing assets.

### Negative
- Renaming a brand or model updates every asset row that shows it.
- CSV files must use catalog names; an admin adds missing entries first.

### Risks
- The SQL normalization in the migration only approximates `TurkishText` for unusual characters; existing dev/E2E data is plain ASCII, and the product was not live at migration time.

## Amendment 2026-10-09: Server-generated asset tags

- New assets get their tag from PostgreSQL sequence `asset_tag_seq`, formatted `DMR-` + six digits (`AssetTagFormat`; longer numbers are not truncated). Create requests carry no tag; edits never change it. Numbers are never reused; a rolled-back save leaves a gap.
- Existing tags stay as they are. A CSV row may leave the tag empty (generated at commit, the preview consumes no number) or give a legacy tag; a legacy tag using the `DMR-` prefix is refused (`assets.import_reserved_tag`) because it would collide with a later generated tag.
- A resent create of a device with a serial number is stopped by the serial uniqueness; a device without a serial number has no duplicate guard. No idempotency key was added: the form blocks double submission and the product serves one company, so the remaining risk is a rare duplicate an admin can retire.

