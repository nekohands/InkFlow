# Architecture

Scope: binding invariants, dependency direction, data consistency, and source
runtime constraints that every change must keep. Human counterparts:
[docs/architecture/invariants.md](../docs/architecture/invariants.md),
[docs/architecture/architecture.md](../docs/architecture/architecture.md),
[docs/architecture/source-runtime.md](../docs/architecture/source-runtime.md).

Parent/root index: [README.md](README.md)

## Named invariants (violating any requires a new ADR first)

1. External `BookId` / `ChapterId` are long-term stable.
2. `SourceBook != CanonicalBook`; `SourceChapter != CanonicalChapter`.
3. The reading path reads ingested Canonical Content, never live third-party fetching.
4. New content creates a new `ContentVersion`; never overwrite history.
5. Match / alignment / selection / failover must be explainable, traceable, reversible.
6. Legado is a first-class protocol with its own contract and tests.
7. Public vs private content authorization is strictly isolated.
8. Redis is never the only store for critical facts; derived stores must be rebuildable.
9. Community Sources run only in the restricted DSL/sandbox; no arbitrary code execution.
10. Modular monolith first; no premature microservices.
11. Every work package passes the real build/test/runtime/CI/fix/regression/documentation gate.

## Dependency direction

- Domain does not depend on EF Core, ASP.NET, or Redis.
- Modules do not depend on hosts or other modules' persistence; hosts (Api/Worker/
  Scheduler/Migrations) are composition roots referencing modules.
- Legado/Web are presentation; they must not leak into the domain.

## Data and consistency

- PostgreSQL is the source of truth for business data; Redis carries only
  rebuildable state (rate-limit snapshots, cache).
- Writes that span aggregates go through Transactional Outbox → at-least-once
  relay → Inbox with idempotent consumers.
- Production migrations run via the dedicated `InkFlow.Migrations` app; the API
  never auto-migrates. Schema changes follow Expand → Migrate → Contract;
  `scripts/verify-migrations.sh` fail-closes on model drift for all 11 DbContexts.
- Crawler tasks live in PostgreSQL; the Crawler executes and reports but never
  owns Canonical matching or final content selection.
- Private Library records are scoped by authenticated \`UserId\` and use an
  independent \`PrivateBookId\`; \`PrivateBookView.Version\` starts at 1 and
  increments on metadata updates. PUT writes are guarded atomically by
  \`UserId + PrivateBookId + expected Version\`; stale versions return the stable
  private-book conflict instead of overwriting newer metadata.

## Source runtime constraints

- Hierarchy: `ISourceAdapter` → `RuleAdapter` (DSL, most sites) / trusted
  `CodeAdapter` (Kanunu8, SeventeenK). Community sources: restricted DSL only.
- Outbound HTTP passes `SsrfGuard` (literal + DNS) and connection-level
  `SsrfSafeHttpMessageHandler` (same resolved IPs, no proxy, ports 80/443,
  ≤5 redirects); private/loopback/link-local/metadata ranges blocked.
- Budgets bound requests, bytes, time, regex, and response extraction/list
  binding; selector work that crosses the execution deadline fails closed and
  never returns partial values or pages. Credentials travel by reference only
  (`CredentialReference`), never in task payloads.
- Trusted Kanunu8/17K CodeAdapters use the same `SourceRuleExecutionLimits`:
  `SourceResponseReader` rejects oversized bodies before decode/parse, and
  Kanunu8 static extraction regexes use the finite `MaxRegexTime` ceiling.
