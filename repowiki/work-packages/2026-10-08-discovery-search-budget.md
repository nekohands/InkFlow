# 5.66 Discovery search budget fencing

Status: Accepted

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

## Delivery record (2026-10-08)

Work package: Accepted.

Implementation: `BookDiscoveryService` now rejects trimmed queries over 256
characters before touching sources, limits each source to 100 hits and the
total canonical discovery result to 100, and reports stable non-sensitive
warnings for truncation. The public response shape, adapter contract, schema,
and migration surface are unchanged.

Acceptance: PASS — overlong queries touch no source; per-source and total
discovery work are bounded; truncation warnings are stable; empty, normal,
cancellation, and idempotency behavior remain covered.

Build: PASS — `dotnet restore InkFlow.sln`; Release build with 0 warnings and
0 errors.

Tests: PASS/PARTIAL — focused `BookDiscoveryServiceTests` 12/12, Unit 609/609,
Architecture 1/1, Contract 12/12, and migration model check 11/11 passed.
Integration ran with 8 passed / 3 skipped / 116 blocked because the local
Windows Docker named pipe is unavailable.

Runtime: PASS via remote CI/Docker PostgreSQL, Compose, and runtime smoke;
local Docker runtime is BLOCKED by the same unavailable named pipe.

Security: PASS — diff/secret audit and Security workflow passed.

CI: GREEN — exact SHA `193722cffb726c7338128be1f66111369b48a8a9` passed [CI
37715153756](https://github.com/nekohands/InkFlow/actions/runs/37715153756),
[Docker 37715153801](https://github.com/nekohands/InkFlow/actions/runs/37715153801),
and [Security 37715153778](https://github.com/nekohands/InkFlow/actions/runs/37715153778).

Findings fixed: the discovery orchestration layer had no query-length,
per-source-hit, or total-result fence despite lower-level adapter budgets.

Remaining risks: source registry cardinality and live-source/manual Release
Candidate acceptance remain separate boundaries; local Docker-backed
Integration/Runtime evidence remains unavailable.

Commit: `193722cffb726c7338128be1f66111369b48a8a9` pushed to `origin/dev`;
this documentation closeout follows it.

Documentation sync: RepoWiki, source-runtime, delivery profile, roadmap, and
handoff records updated; `.workbuddy-ai/` remains untracked and untouched.

Next step: no active work package; intake the next evidence-backed minimal
boundary when requested.
