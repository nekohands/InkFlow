# 5.66 Discovery search budget fencing

Status: In Progress

## Objective

Bound user-triggered source discovery so a single search cannot send an
oversized keyword to adapters or import/match an unbounded number of source
hits. Preserve explainability through the existing stable `warnings` field.

## Evidence gap

`BookDiscoveryService` trims empty input but has no query-length or discovery
result budget. It forwards every adapter hit through BookInfo import and
canonical matching, while the public search response preserves every merged
result. Source adapters already have response/execution bounds, but the
orchestrator has no application-side work fence.

## Scope

- Add a fixed maximum trimmed query length and short-circuit overlong queries
  before listing sources or invoking adapters.
- Bound source hits processed per source and total canonical discovery results.
- Emit stable, non-sensitive warnings when a bound truncates discovery.
- Add deterministic offline regressions and synchronize source-runtime and
  delivery documentation.

## Non-goals

- No change to `ISourceAdapter`, public API/Legado JSON shape, catalog query
  pagination, source registry paging, parser/HTTP budgets, schema, migration,
  permissions, or retry policy.
- No full-text search, relevance ranking, cursor API, or live-source
  acceptance.

## Acceptance

1. Empty queries retain zero-source-touch behavior.
2. Overlong queries return a stable warning without listing sources or calling
   an adapter.
3. A source cannot cause more than the configured per-source hit count to be
   imported/matched; total returned discovery results are bounded as well.
4. Truncation is reported without exception details, caller cancellation still
   propagates, and normal discovery/merge/idempotency behavior remains green.

## Risks and boundaries

This is an in-process Crawling orchestration guard. Truncation can make a
search incomplete, so the result is explicitly warned rather than silently
represented as complete. Existing adapter response/DSL budgets remain the
lower-level memory and network boundary; source registry cardinality is a
separate administrative/storage concern.

## Verification plan

- Red/green `BookDiscoveryServiceTests` for query, per-source, total-result,
  warning, and cancellation behavior.
- Full Unit, Architecture, and Contract tests; Release Restore/Build;
  migration model check and diff/secret audit.
- Run applicable Integration/Runtime gates; report the known local Docker
  named-pipe limitation explicitly and verify the candidate SHA in CI/Docker/
  Security before acceptance.
