# ADR-0019: Knowledge Base

**Date**: 2026-10-02
**Status**: accepted
**Deciders**: Tugay İLTUŞ

## Context

Phase 5 adds a knowledge base: IT staff write how-to articles, employees read the ones meant for them, and articles are linked to tickets (`docs/superpowers/specs/2026-09-27-phase-4-5-sla-email-knowledge-reports-design.md` §6, commit `3aa02ab`). Employees must never see drafts, archived or internal articles, edits need history, search must cope with Turkish text, and an internal article must not leak through a public ticket reply. Related: ADR-0006 (Turkish text, search keys), ADR-0013 (ticket access scope).

## Decision

- Tables `knowledge_articles` (title ≤ 200, summary ≤ 500, Markdown body ≤ 50,000, `category_id`, `visibility`, `status`, `author_id`, `published_at`, `search_text`, `xmin` concurrency), `article_revisions` (append-only, one row per save, with title, summary, body, visibility, category, editor and time) and `ticket_articles` (unique ticket/article pair). Limits are named constants in `ArticleLimits`.
- Two visibility values only: `employees` and `internal`. Status is `draft`, `published` or `archived` (archived restores to draft). Employees see only `published` + `employees`; anything else is 404, including direct URLs, search and ticket links, so existence is not revealed. IT staff and admins create, edit, publish, archive and restore; publish, archive and visibility changes are audited.
- Search runs over a stored `search_text` (title + summary + body + category) built with `TurkishText.ToSearchKey` (ADR-0006) and matched through a trigram GIN index (`TrigramIndex` constants), the same mechanism already used elsewhere in the project.
- Markdown is rendered in the browser with `react-markdown` and no raw HTML (`rehype-raw` is not used); links get `rel="noopener noreferrer"`.
- Ticket links follow ADR-0013: IT links an article to a ticket in its scope. The employee ticket view lists only employee-visible links. The backend refuses a public reply that references an internal article id (`knowledge.internal_article_in_public_reply`); the UI picker offering employee-visible articles only is a convenience, not the guard.

## Alternatives Considered

### Alternative 1: PostgreSQL full-text search (`tsvector`)
- **Pros**: ranking, phrase queries and stemming are built in.
- **Cons**: PostgreSQL ships no Turkish stemmer in the default setup, so agglutinative forms would not match; a second search mechanism would sit next to the trigram indexes already used for tickets and assets.
- **Why not**: trigram search over a Turkish-normalised `search_text` finds partial and inflected words well enough for a single-company knowledge base and reuses existing code and conventions.

### Alternative 2: No revision table, only the latest article row
- **Pros**: simpler schema and writes.
- **Cons**: no way to see or recover earlier wording; an unwanted edit is lost.
- **Why not**: an append-only `article_revisions` row per save is cheap and gives an audit-friendly history.

## Consequences

### Positive
- Visibility is one rule applied in one query path; a hidden article is indistinguishable from a missing one.
- Full edit history, and a server-side guard that does not depend on the UI.
- No new database extension or search mechanism.

### Negative
- Trigram search has no relevance ranking beyond similarity and no stemming; very short queries match loosely.
- `search_text` duplicates article content and must be rebuilt on every save.
- Revisions grow the table by one full body per save.

### Risks
- Raw HTML or script in Markdown is only safe while `rehype-raw` stays out; adding it later needs a sanitiser and a new ADR.
- Any new endpoint that exposes articles must use the same visibility predicate, or internal content leaks.

## Amendment 2026-10-08

- Article and ticket search match through `EF.Functions.Like(search_text, '%key%', '\')` built by `LikePattern.Containing`, which escapes `%`, `_` and `\` so they match literally. Npgsql translates `string.Contains` to `strpos`, which the `gin_trgm_ops` indexes cannot serve; the trigram index decision above is unchanged (commit 7e3cf77).
- An article `summary` may be empty (still at most 500 characters); title and body stay required (commit 7e3cf77).
- Markdown headings render one level below the page title (`#` becomes `<h2>`, `######` stays `<h6>`), so the article title stays the page's only `<h1>`. Raw HTML stays off (commit 7e3cf77).
