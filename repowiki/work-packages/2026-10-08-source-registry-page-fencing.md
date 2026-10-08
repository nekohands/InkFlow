# 5.71 Source registry page fencing

Status: Implemented / Locally Validated; remote gates pending

## Objective

Bound the source-registry reads used by search discovery and direct book-URL resolution. A request must not materialize every registered `Source` and its rule document in one application call.

## Scope

- Add a stable `SourceScanCursor`/`SourcePage` keyset contract ordered by `Source.Id`.
- Keep the existing `ListAsync` contract for Operations Center and other explicit full-snapshot callers.
- Add the production EF page query with a fixed maximum page size and one look-ahead row.
- Make `BookDiscoveryService` and `SourceBookUrlResolver` consume pages sequentially while preserving source order, enabled-source filtering, adapter resolution, warnings, cancellation, and result semantics.
- Add unit regressions proving page continuation and integration SQL regressions proving ordering, look-ahead, limit validation, and cancellation.

## Non-goals

- No public API or Legado response change.
- No source registry schema, migration, source rotation, cache, or durable cursor.
- No change to search hit/result budgets, adapter HTTP budgets, health policy, or URL matching rules.
- No change to Operations Center's explicit full source snapshot.
- No live-source, production-credential, or manual Release Candidate acceptance.
- `.workbuddy-ai/` remains untracked and untouched.

## Acceptance

- Production discovery and direct-URL resolution never call `ISourceRepository.ListAsync` and hold at most one bounded source page at a time.
- Page SQL orders by `Source.Id`, applies keyset continuation, reads at most `limit + 1`, and returns deterministic `HasMore`/cursor state.
- All registered sources remain eligible across page boundaries; disabled sources, adapter failures, warnings, cancellation, and existing result limits retain their current behavior.
- Existing full-snapshot callers retain `ListAsync` behavior.

## Risk and verification plan

The main risk is silently skipping a source at a page boundary or changing search/URL precedence. Start with red unit tests for multi-page continuation and a PostgreSQL repository regression, then run the focused services, full Unit/Architecture/Contract suites, Release Restore/Build, migration model checks, diff/secret checks, applicable Integration/Runtime gates, and exact-SHA CI/Docker/Security.

## Implementation and local evidence

- `ISourceRepository` now exposes a `Source.Id` keyset `SourcePage`; the EF implementation validates the page size, applies stable ordering and continuation, and reads `limit + 1` rows. The default interface fallback preserves older in-memory test doubles.
- `BookDiscoveryService` and `SourceBookUrlResolver` consume 100-source pages and preserve existing source order, filtering, adapter, warning, cancellation, and result behavior. `OperationsCenter` remains on explicit `ListAsync` full snapshots.
- TDD red: the new multi-page tests first failed to compile because the page contract was absent. Green: focused discovery/URL Unit tests `18/18`; full Unit `619/619`; Architecture `1/1`; Contract `12/12`; Restore and tool restore PASS; Release Build `0 warnings / 0 errors`; migration model `11/11`; migration script syntax, `git diff --check`, and changed-file secret audit PASS.
- Full local Integration was attempted: `8 passed / 3 skipped / 120 blocked`; the new PostgreSQL page regression compiled but could not start Testcontainers because Windows Docker Engine `npipe://./pipe/docker_engine` is unavailable.

## Evidence

Intake is based on CodeGraph and source inspection: `EfSourceRepository.ListAsync` materializes all source rows including rule DSL JSON; `BookDiscoveryService` and `SourceBookUrlResolver` are the production callers that enumerate the registry for user-facing work. `OperationsCenter` is intentionally outside this package.
