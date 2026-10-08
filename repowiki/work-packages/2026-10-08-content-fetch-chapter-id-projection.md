# Work Package 5.75 — Content-fetch chapter ID projection

Status: In Progress

## Objective

Keep content-fetch chaining bounded to the data it actually needs: the
external chapter IDs in TOC order. Do not materialize a complete `SourceBook`
aggregate and chapter title rows just to decide which chapters need a fetch.

## Scope

- Add `ISourceBookRepository.ListChapterIdsAsync` with a compatibility fallback
  for existing test doubles.
- Implement the production EF query as a source-book identity join that
  projects ordered external chapter IDs without constructing `SourceBook`.
- Change only `ContentFetchChainService.EnqueuePendingContentFetchesAsync` to
  consume that projection.
- Add Unit coverage proving the chain does not call full `GetAsync`, and
  PostgreSQL coverage for order, missing/empty books, cancellation plumbing,
  and the scalar projection query.
- Synchronize Source Runtime, Progress, Handoff, delivery profile, and this
  RepoWiki page with actual evidence.

## Non-goals

- No change to `SourceCatalogService` directory sync, chapter mapping, source
  content fetching, task payloads, health gates, fetch-artifact freshness,
  dedupe/run gating, public API/Legado contracts, Schema/Migration, cache,
  retry/HTTP budgets, or `.workbuddy-ai/`.
- `ISourceBookRepository.GetAsync` remains the full aggregate read for writes,
  matching, mapping, and other intentional callers.

## Acceptance

1. Content-fetch enqueue order, new/stale/force-refresh decisions, health
   gating, dedupe, collection-run gating, missing-book, and empty-book
   behavior remain unchanged.
2. The content-fetch path no longer calls full `GetAsync`; production EF
   returns only external chapter IDs ordered by persisted chapter index.
3. Cancellation propagates through the new repository operation, and existing
   repository test doubles remain source-compatible through the fallback.
4. Applicable local gates and exact-head CI, Docker, Security, and documentation
   evidence are recorded before this package is marked Accepted.

## Risks and verification

The main risks are changing TOC order or collapsing missing and empty books
into different behavior, and EF translating the projection into an unintended
aggregate load. Establish a focused red counter regression first, then run
focused/full Unit, Architecture, Contract, Restore/Release Build, migration
model and script checks, Integration, diff/secret audit, and exact-head remote
gates. Local PostgreSQL/Compose evidence may remain blocked by the known
Windows Docker Engine named pipe limitation; remote CI must cover that gap.

## Evidence

Implementation, test, and delivery evidence will be appended here before
closeout.
