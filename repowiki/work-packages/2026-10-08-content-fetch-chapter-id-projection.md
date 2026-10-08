# Work Package 5.75 — Content-fetch chapter ID projection

Status: Accepted

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

TDD red/green: the focused Unit counter first observed one full aggregate read;
after the change `Chapters_Without_Artifacts_Are_Enqueued_In_Toc_Order` passed
with zero full-book reads and one chapter-ID projection. The focused
`ContentFetchChainServiceTests` class passed 10/10 and the full Unit suite
passed 623/623.

The production EF implementation uses one `AsNoTracking` join/projection,
orders by persisted `ChapterIndex`, returns zero rows for missing/empty books,
and forwards cancellation. The PostgreSQL regression also asserts scalar SQL
projection without the chapter `Title` column; its local execution was blocked
by the Windows Docker Engine named pipe, while remote CI exercised it.

Local gates: restore and tool restore PASS; Release build PASS with 0 warnings /
0 errors; Architecture 1/1; Contract 12/12; migration model 11/11; full
Integration 8 passed / 3 skipped / 124 blocked by unavailable
`npipe://./pipe/docker_engine`; `git diff --check` and added-line secret audit
PASS. The focused PostgreSQL test compiled but was locally BLOCKED by the same
Docker endpoint.

Implementation candidate `523a305f735fda252dbfcb27dc560f39c930b604` passed exact-
head CI `37748826161`, Docker `37748826364`, and Security `37748826225`; CI
passed migrations, full tests, Compose/runtime smoke, SLO, Redis, PostgreSQL
backup/restore, and diagnostics; Docker and Security passed their complete
build/scan gates.

Status: Accepted. No public contract, Schema/Migration, cache, or `.workbuddy-ai/`
change; `GetAsync` remains the intentional full aggregate path for other
callers.
