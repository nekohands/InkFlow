# Work Package 5.76 — Matching source-book metadata projection

Status: Accepted

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

- Implementation candidate `835e0914b84469581f1d2a21a33fa9a65fac09f6` adds
  `SourceBookMetadata`, a compatibility fallback plus production EF
  `source_books` scalar projection, and routes
  `CanonicalBookMatchingService.CreateOrMatchAsync` through the metadata read;
  full aggregate reads remain for intentional callers.
- TDD red first failed the full-read counter (`expected 0, actual 1`); focused
  `CanonicalBookMatchingServiceTests` then passed `3/3`, and full Unit passed
  `623/623`.
- Architecture passed `1/1`; Contract passed `12/12`; Restore/tool restore,
  Release Build (`0 warnings / 0 errors`), migration model check (`11/11`),
  `git diff --check`, and the secret audit (`0` pattern hits) passed.
- The focused PostgreSQL projection regression compiled but could not execute;
  full local Integration was `8 passed / 3 skipped / 125 blocked` because the
  Windows Docker Engine endpoint `npipe://./pipe/docker_engine` was unavailable.
- Exact-head remote gates for the implementation candidate were all GREEN:
  [CI 37753946308](https://github.com/nekohands/InkFlow/actions/runs/37753946308),
  [Docker 37753946459](https://github.com/nekohands/InkFlow/actions/runs/37753946459),
  and [Security 37753946140](https://github.com/nekohands/InkFlow/actions/runs/37753946140).
  CI migration, full tests, Compose/runtime smoke, SLO, Redis, PostgreSQL
  backup/restore, and diagnostics all passed; Docker and Security completed
  their full gates.
- No public Contract, Schema/Migration, cache, or `.workbuddy-ai/` change was
  made. Documentation closeout was pushed separately and re-gated at its final
  head; the known local Docker limitation remains the only local blocker.
