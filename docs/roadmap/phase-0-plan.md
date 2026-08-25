# Phase 0 Implementation Plan

## Goal

Phase 0 does not deliver the final reader product. It establishes a foundation that can be built, tested, migrated, observed and deployed without relying on manual database manipulation.

## Workstream 1 — Repository Structure

Refactor into:

- `src/Apps/InkFlow.Api`
- `src/Apps/InkFlow.Worker`
- `src/Apps/InkFlow.Scheduler`
- `src/Apps/InkFlow.Migrations`
- `src/BuildingBlocks/*`
- `src/Modules/{Identity,Library,Sources,Crawling,Content,Reading,Search,Legado}`
- `tests/{UnitTests,IntegrationTests,ArchitectureTests,ContractTests}`

Keep Git history; do not delete/recreate the repository.

## Workstream 2 — Building Blocks

Implement minimal, reusable primitives:

- UUIDv7-based strongly typed IDs
- `TimeProvider`
- `Result<T>` / stable Error Code model
- Domain/Application base abstractions only where immediately used
- UTC/ISO-8601 conventions

Avoid speculative generic frameworks.

## Workstream 3 — Persistence

- Add PostgreSQL provider and EF Core
- Module-owned DbContexts / schemas
- Add `InkFlow.Migrations`
- Initial authoritative tables only for Phase 1 needs
- No production `Database.Migrate()` on API startup
- Test migrations against real PostgreSQL via Testcontainers

## Workstream 4 — Messaging

- Transactional Outbox
- Inbox/idempotent consumer foundation
- PostgreSQL is source of truth
- Redis dispatch adapter may be added only after persistence behavior is tested

## Workstream 5 — Runtime Applications

API/Worker/Scheduler expose health/readiness endpoints or equivalent diagnostics.

Worker and Scheduler must remain stateless with important state persisted externally.

## Workstream 6 — Observability

Add OpenTelemetry instrumentation hooks for:

- ASP.NET requests
- Worker/Scheduler jobs
- database operations
- future outbound source requests

TraceId must propagate through task envelopes.

## Workstream 7 — Security Baseline

Phase 0 implements foundations, not the complete identity product:

- System/Admin/Anonymous principals
- authorization policy primitives
- audit event foundation
- secrets are not committed
- rate-limit hooks

## Workstream 8 — Testing

CI must run:

1. Restore
2. Formatting/static checks
3. Build with warnings as errors
4. Unit tests
5. Architecture tests
6. PostgreSQL/Redis integration tests where needed
7. Contract test skeleton
8. Container build/security baseline

Real source websites are not called from ordinary PR CI.

## Acceptance Criteria

Phase 0 is done only when all are true:

- clean checkout builds successfully
- all automated tests pass
- Docker Compose brings required dependencies to healthy state
- migration up/down/safety path is validated for current schema
- API starts and reports healthy
- Worker starts and reports healthy
- Scheduler starts and reports healthy
- Outbox/Inbox happy path and duplicate-consumption behavior are tested
- architecture dependency rules fail CI when intentionally violated
- CI on the target branch is green

## Explicit Deferred Items

- Full user registration/OAuth/2FA
- Billing
- OpenSearch
- Community Source marketplace
- Playwright runtime unless required by the first selected Official Source
- Kubernetes / Multi-Region
