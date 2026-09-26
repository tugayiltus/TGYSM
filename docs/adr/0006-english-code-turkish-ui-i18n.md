# ADR-0006: English Code and URLs, Turkish UI through i18n

**Date**: 2026-09-26
**Status**: accepted
**Deciders**: Tugay İLTUŞ

## Context

The owner requires English for code, identifiers, database names, API endpoints and URL paths, and Turkish only for the user interface. `tgysm.md` §14 requires i18n keys, `tr-TR` formatting, correct I/İ/ı/i handling and Turkish suffix helpers.

## Decision

- All identifiers, tables, columns, routes and endpoints are English.
- UI text lives in `frontend/src/shared/i18n/locales/tr/*.json` (namespaces `common`, `auth`, `admin`, `errors`). Keys are typed through i18next `CustomTypeOptions`, so a missing key fails the TypeScript build.
- The API returns stable error codes (`ErrorCodes` in `Tgysm.Domain`). `TranslationCoverageTests` fails when any code or audit action lacks a Turkish text, or when `errors.json` holds a stale key.
- Dates and numbers use `Intl` with `tr-TR`; display time zone `Europe/Istanbul`.
- Case-insensitive matching uses the `tr-TR` culture on the backend (`TurkishText.ToSearchKey`) and `toLocaleLowerCase('tr-TR')` on the frontend. Emails use invariant lower case.
- `withSuffix` in `shared/lib/turkish.ts` supports two/four-way vowel harmony, buffer letters and `d/t` assimilation, with a pronunciation dictionary. It returns `null` when pronunciation is unknown (e.g. an unknown abbreviation); callers then use a suffix-free label such as `Atanan kişi: …`.

## Alternatives Considered

### Alternative 1: Turkish URLs (`/kayitlar`)
- **Pros**: readable for Turkish users.
- **Cons**: mixes languages between API and SPA; harder for the future mobile app and integrations.
- **Why not**: the owner chose English URLs.

## Consequences

### Positive
- Adding a second language later only needs new resource files.

### Negative
- Every visible string needs a key.

### Risks
- Suffix rules have exceptions; the dictionary must grow as real names appear.

## Amendment 2026-09-26

- Search and uniqueness keys fold dotless `ı` to `i` after `tr-TR` lower-casing (commit 8b3357d). Turkish lower-casing turns ASCII "I" into "ı", so "IT Destek" and "it destek" were different keys. Migration `FoldDotlessIInKeys` recomputed stored keys.
