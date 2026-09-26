# ADR-0007: First Admin Created by a CLI Command

**Date**: 2026-09-26
**Status**: accepted
**Deciders**: Tugay İLTUŞ

## Context

`tgysm.md` §3.1: the first admin is created once, by a secure one-time setup; no default password and no permanently open setup endpoint.

## Decision

- `tools/setup-admin.sh --email <email> --first-name <name> --last-name <name>` applies migrations and runs `Tgysm.Api setup-admin ...`.
- The command refuses (exit code 1, `setup.already_completed`) when any user exists.
- It generates a 16-character temporary password, prints it once, and sets `must_change_password`.
- No HTTP setup path exists.

## Alternatives Considered

### Alternative 1: Web setup page protected by a one-time token printed at startup
- **Pros**: no shell access needed after deploy.
- **Cons**: an HTTP surface that must be proven closed after use; token handling in logs.
- **Why not**: server shell access is available at install time; no HTTP surface is the safest option.

## Consequences

### Positive
- Nothing on the network can create the first account.

### Negative
- Installation needs shell access to the host.

### Risks
- Two parallel runs on an empty database could both pass the check. Setup is an operator action run once; add an advisory lock if this ever runs automatically.
