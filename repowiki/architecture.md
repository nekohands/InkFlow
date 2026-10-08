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
- `BookDiscoveryService` adds a caller-side search fence: trimmed keywords are
  limited to 256 UTF-16 characters, each source contributes at most 100 hits to
  import/matching, and discovery returns at most 100 merged canonical books.
  Truncation is reported through the existing stable `DiscoveryOutcome.Warnings`
  without changing the public or Legado JSON shape.
- Source Search/TOC list projection has a separate `SourceRuleExecutionLimits.MaxResultItems`
  fence (default 10,000). RuleBased, Kanunu8, and SeventeenK fail closed rather
  than return partial lists when the item budget is crossed; `SourceCatalogService`
  repeats the TOC count check before persistence for any adapter. This does not
  change `ISourceAdapter`, public/Legado JSON, pagination, schema, or migrations.
- Scheduled update scans page `SourceBook` rows by the stable `(CreatedAt, Id)` keyset and process at most 100 books per scheduler tick. The scheduler keeps the cursor in memory only, advances it after a successful batch, and resets it after the last page; restart replay is bounded by the existing task dedupe gate. No cursor persistence, schema, or migration is part of this boundary.
- The active Toc health probe selects one chapter-free sample `SourceBook` with a source-filtered `(CreatedAt, Id)` query and `LIMIT 1`; it must not materialize the complete source-book table. No sample remains a silent skip, and this does not page unhealthy health candidates or add a durable cursor.
- Content-fetch chaining reads only the current source book's external chapter IDs in persisted TOC order through `ISourceBookRepository.ListChapterIdsAsync`; it must not materialize a full `SourceBook` aggregate merely to decide which chapters need fetching. Missing and empty books still produce zero enqueues, while `GetAsync` remains for directory sync, matching, chapter mapping, and other aggregate callers. This projection does not change task payloads, health/freshness, dedupe, collection-run semantics, schema, or migrations.
- Canonical book matching reads only source-book title and author through `ISourceBookRepository.GetMetadataAsync`; the production query projects `source_books` fields without loading chapter rows. Missing books retain the existing failure branch, while full `GetAsync` remains for catalog sync, writes, chapter mapping, and other aggregate callers. This projection does not change match locking, normalization, candidate semantics, public contracts, schema, or migrations.
- Source health gating and registered CodeAdapter lookup read only `ISourceRepository.GetEnabledAsync`: `null` means missing, `false` disabled, and `true` executable. The production EF query projects `sources.IsEnabled` without loading or deserializing Rule DSL; RuleBased Adapter lookup retains full `GetAsync` because it needs the rule document. This does not change source dispatch, health transitions, public contracts, schema, or migrations.
- Scheduled health probes page `Unhealthy` capability rows by the stable `(SourceId, Capability)` keyset, read at most 100 candidates plus one look-ahead per batch, and keep an in-process cursor in the Scheduler. The cursor advances only after a successful batch and resets after the final page; pages may contain not-yet-due rows, restart replay is expected, and no cursor persistence, schema, or migration is part of this boundary.
- Search discovery and direct book-URL resolution page the source registry by the stable `Source.Id` keyset, read at most 100 sources plus one look-ahead per request page, and never use `ListAsync` to materialize the complete registry. Page consumption preserves source order, disabled-source filtering, adapter resolution, warnings, cancellation, and existing result semantics; explicit Operations Center full snapshots may retain `ListAsync`. The cursor is request-local and adds no schema or migration.
- Trusted Kanunu8/17K CodeAdapters use the same `SourceRuleExecutionLimits`:
  `SourceResponseReader` rejects oversized bodies before decode/parse, and
  Kanunu8 static extraction regexes use the finite `MaxRegexTime` ceiling.
