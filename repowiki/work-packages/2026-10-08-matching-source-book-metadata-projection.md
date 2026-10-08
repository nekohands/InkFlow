# Work Package 5.76 — Matching source-book metadata projection

Status: In Progress

## Objective

Keep canonical-book matching bounded to the source-book data it actually needs:
title and author metadata. Do not materialize the full `SourceBook` aggregate
and all chapter rows just to acquire matching keys.

## Scope

- Add `SourceBookMetadata` and `ISourceBookRepository.GetMetadataAsync` with a
  compatibility fallback for existing test doubles.
- Implement the production EF query as a source-book-only scalar projection.
- Change only `CanonicalBookMatchingService.CreateOrMatchAsync` to consume the
  metadata projection.
- Add Unit coverage proving matching does not call full `GetAsync`, and
  PostgreSQL coverage for projected fields, missing books, cancellation, and
  the absence of chapter loading.
- Synchronize Source Runtime, architecture Wiki, Progress, Handoff, delivery
  profile, and this RepoWiki page.

## Non-goals

- No change to confirmed-candidate resolution, canonical match locking,
  title/author normalization, candidate creation, chapter mapping, catalog
  import/sync, content fetching, public API/Legado contracts, Schema/Migration,
  cache, retry/HTTP budgets, or `.workbuddy-ai/`.
- `ISourceBookRepository.GetAsync` remains the full aggregate read for writes,
  chapter mapping, synchronization, and other intentional callers.

## Acceptance

1. Matching results, missing-book errors, idempotency, same-title/author reuse,
   confirmed-candidate fast path, and cancellation semantics remain unchanged.
2. The matching path no longer calls full source-book `GetAsync`; production EF
   returns only title and author from `source_books`.
3. Existing repository test doubles remain source-compatible through the default
   fallback, while the production implementation forwards cancellation.
4. Applicable local gates and exact-head CI, Docker, Security, and documentation
   evidence are recorded before this package is marked Accepted.

## Risks and verification

The main risks are changing the missing-book branch or matching keys, and EF
accidentally loading chapter rows. Establish a focused red read-counter
regression first, then run focused/full Unit, Architecture, Contract,
Restore/Release Build, migration model and script checks, Integration,
diff/secret audit, and exact-head remote gates. Local PostgreSQL/Compose
evidence may remain blocked by the known Windows Docker Engine named pipe
limitation; remote CI must cover that gap.

## Evidence

Implementation, test, and delivery evidence will be appended here before
closeout.
