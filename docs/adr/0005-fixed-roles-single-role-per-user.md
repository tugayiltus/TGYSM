# ADR-0005: Fixed Roles, One Role per User

**Date**: 2026-09-26
**Status**: accepted
**Deciders**: Tugay İLTUŞ

## Context

`tgysm.md` §4 defines three fixed role packages (employee, IT staff, admin) and excludes direct user permissions and custom roles from the first release.

## Decision

- `users.role` is a text column with a CHECK constraint: `employee`, `it_staff`, `admin`. Domain enum `UserRole`; canonical names in `UserRoleNames`.
- The API uses the same snake_case names in JSON and in the role claim.
- Authorization policies map roles to endpoints: `admin` policy for `/api/v1/admin/*`. Team scope for IT staff arrives with tickets (Phase 3).
- The last active admin can be neither demoted nor deactivated (`users.last_admin`).
- A department approver must be an active admin (`departments.approver_invalid`).
- Only IT staff and admins can be team members (`teams.member_role_invalid`).

## Alternatives Considered

### Alternative 1: Identity role tables (many-to-many)
- **Pros**: flexible, standard.
- **Cons**: allows multiple roles per user, which the product does not want; more joins.
- **Why not**: one column with a CHECK constraint expresses the rule exactly.

## Consequences

### Positive
- Simple queries and claims; role rules are explicit.

### Negative
- Adding a role needs a migration and code change.

### Risks
- If the business later needs combined roles, write a superseding ADR.
