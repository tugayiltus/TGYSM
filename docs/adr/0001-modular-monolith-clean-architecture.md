# ADR-0001: Modular Monolith with Clean Architecture Layers

**Date**: 2026-09-26
**Status**: accepted
**Deciders**: Tugay İLTUŞ

## Context

TGYSM is an ITSM platform for one company: incident and request management, IT inventory and asset management, SLA, email notifications, knowledge base and reporting (`tgysm.md` §1). The team size and hosting target are unknown. Business rules (approval, single active asset assignment, SLA pause rules) must stay testable without HTTP or a database, and a background worker (Phase 4) must reuse the same rules.

## Decision

Build one deployable backend as a modular monolith on .NET 10 with four layers:

- `Tgysm.Domain` — entities, business rules and state transitions. No EF Core, ASP.NET Core or HTTP dependency.
- `Tgysm.Application` — use-case handlers (one class per use case, plain `HandleAsync` method) with FluentValidation, ports (`IAppDbContext`, `IPasswordService`, `IAuditLog`, `ICurrentActor`) and access-scope checks. It may use EF Core query APIs through `IAppDbContext`, but never Npgsql or ASP.NET Core.
- `Tgysm.Infrastructure` — EF Core + Npgsql, migrations, password hashing adapter.
- `Tgysm.Api` — Minimal API endpoints, authentication, authorization policies, error mapping, composition root and CLI commands.

Endpoints never touch `DbContext` for business data; they call handlers. `Tgysm.ArchitectureTests` enforces the dependency direction. The Worker project is added in Phase 4.

## Alternatives Considered

### Alternative 1: Microservices
- **Pros**: independent deployment and scaling per module.
- **Cons**: distributed transactions for assignment + audit + outbox, more infrastructure, more operations work.
- **Why not**: one company, unknown load, small team. `tgysm.md` §15 states microservices are not required.

### Alternative 2: MediatR request pipeline
- **Pros**: uniform pipeline behaviors (validation, logging).
- **Cons**: commercial license for newer versions, indirection when reading code.
- **Why not**: plain handler classes resolved from DI give the same testability with less indirection.

### Alternative 3: Repository per aggregate instead of `IAppDbContext`
- **Pros**: Application layer free of EF Core types.
- **Cons**: many thin wrappers, lost query composition, duplicated paging/filter code.
- **Why not**: EF Core already is a unit of work + repository; the architecture tests keep Npgsql and ASP.NET Core out of Application.

## Consequences

### Positive
- Business rules are unit-testable; integration tests cover handlers against real PostgreSQL.
- One deployment unit, one transaction boundary for record + audit (+ outbox later).

### Negative
- Application depends on `Microsoft.EntityFrameworkCore` abstractions.
- All modules share one database and release cycle.

### Risks
- Module boundaries can erode inside one assembly. Mitigation: feature folders per module and architecture tests.
