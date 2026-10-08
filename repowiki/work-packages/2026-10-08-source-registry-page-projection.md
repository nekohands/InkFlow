# Work Package 5.78 — Source registry page projection

## Intake

- Status: Accepted.
- Objective: keep bounded source-registry paging from loading and deserializing
  Rule DSL when discovery and direct book-URL resolution only need source
  identity, base URL, and enabled state.
- Scope: narrow `ISourceRepository.ListPageAsync` to lightweight page entries;
  project `Source.Id`, `BaseUrl`, and `IsEnabled` directly in
  `EfSourceRepository`; keep `BookDiscoveryService` and
  `SourceBookUrlResolver` behavior unchanged; add Unit and PostgreSQL SQL-shape
  regressions.
- Non-goals: full source snapshots, `GetAsync`, RuleBased Adapter loading,
  source writes, health transitions, public API/Legado contracts, schema or
  migrations, caching, credentials, or `.workbuddy-ai/`.
- Acceptance:
  - source page ordering, cursor, look-ahead, limit, disabled filtering, URL
    matching, adapter selection, warnings, and cancellation remain stable;
  - production `ListPageAsync` selects only the page fields needed by its
    callers and does not read or deserialize `RuleDslJson`;
  - `ListAsync` remains the explicit full source snapshot and `GetAsync` remains
    available to rule execution, writes, and other aggregate callers;
  - compatibility fallbacks and existing test doubles keep their behavior.
- Risks: changing `SourcePage` item shape could accidentally hide fields needed
  by a page consumer; the consumer property set is limited to `Id`, `BaseUrl`,
  and `IsEnabled` before implementation.
- Verification plan: TDD red/green; focused discovery/URL resolver tests;
  full Unit, Architecture, Contract, restore, Release Build, migration model,
  applicable Integration/Runtime, diff/secret audit, and exact-head
  CI/Docker/Security gates.

## Delivery

- Implementation: `SourcePage` now carries `SourcePageEntry(Id, BaseUrl,
  IsEnabled)`; production `EfSourceRepository.ListPageAsync` projects those
  three columns before materialization, while `ListAsync`, `GetAsync`, and
  RuleBased Adapter full reads remain unchanged. Discovery, direct URL
  resolution, keyset paging, look-ahead, disabled-source handling, and the
  compatibility fallback keep their existing behavior.
- Regression: the first candidate `ef591f3cf9172693b89b1152f83944a185c00d4e`
  exposed an incorrect test assumption (`limit=2` did not guarantee the newly
  inserted source was on the first page). The test was corrected to use the
  allowed maximum page size; no product code changed in the fix.
- Local verification: focused discovery/URL resolver Unit `18/18`; full Unit
  `624/624`; Architecture `1/1`; Contract `12/12`; Restore and Release Build
  `0 warnings / 0 errors`; Windows PowerShell migration model check `11/11`;
  `git diff --check` and secret audit passed. Full local Integration was
  `8 passed / 3 skipped / 127 blocked`; all blocked cases failed at
  Testcontainers Docker endpoint `npipe://./pipe/docker_engine`, with no
  product assertion failure.
- Remote gates: final implementation SHA
  `24669af2f343f47cbd1e29924772b15d5956a67a` passed exact-head CI
  [37764067745](https://github.com/nekohands/InkFlow/actions/runs/37764067745),
  Docker [37764067733](https://github.com/nekohands/InkFlow/actions/runs/37764067733),
  and Security
  [37764067771](https://github.com/nekohands/InkFlow/actions/runs/37764067771).
  CI supplied PostgreSQL, Redis, migration, runtime smoke, SLO,
  backup/restore, and diagnostics evidence; Docker and Security completed
  successfully.
- Boundary: no public API/Legado, Schema/Migration, cache, credential, or
  `.workbuddy-ai/` change. Local Docker/Testcontainers and real external
  source/manual Release Candidate acceptance remain environment boundaries.
