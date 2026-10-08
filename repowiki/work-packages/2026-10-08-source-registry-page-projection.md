# Work Package 5.78 — Source registry page projection

## Intake

- Status: In Progress.
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

Implementation and verification evidence will be recorded here before the
package is marked Accepted.
