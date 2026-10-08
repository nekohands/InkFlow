# 5.72 Reader chapter metadata point lookup

Status: Locally Validated; candidate commit pending.

## Objective

Keep the public canonical chapter read path bounded when it only needs one chapter's metadata. `CatalogQueryService.GetChapterContentAsync` must not materialize every chapter in a `CanonicalBook` merely to obtain the requested chapter title and index.

## In scope

- Add a `bookId + chapterId` point lookup to `ICanonicalBookRepository` with a compatibility fallback for test doubles.
- Implement the point query in `EfCanonicalBookRepository` and map one `CanonicalChapter` row.
- Make `CatalogQueryService.GetChapterContentAsync` use the point lookup after the existing policy and content-selection gates.
- Add unit and PostgreSQL integration regressions for metadata, book ownership, bounded SQL, and cancellation where applicable.
- Preserve the existing reader output and documentation evidence.

## Non-goals

- No public API or Legado JSON change.
- No TOC/detail pagination; `GetAsync` remains the aggregate read for those contracts.
- No ContentVersion selection change, schema/migration change, cache, or new durable cursor.
- No change to policy ordering: takedown checks remain before body loading.
- Do not touch `.workbuddy-ai/`.

## Acceptance

- A chapter content read no longer calls the full-book repository method solely for chapter metadata.
- The point query requires both canonical book ID and chapter ID, returns null for a cross-book or missing chapter, and preserves current null/default reader output semantics.
- EF reads only the matching chapter row and propagates cancellation.
- Existing unit, architecture, contract, restore/build, migration-model, integration/runtime, security, and exact-SHA CI gates are evaluated and recorded; unavailable Docker evidence is explicitly marked.

## Risks and verification plan

The new application contract must not break existing repository test doubles, and the point lookup must retain the current book ownership check. Start with a unit regression that proves the reader path uses the point lookup without a full-book read, then add the repository integration regression. Run focused tests, full Unit/Architecture/Contract, Restore and Release Build, migration model validation, applicable Integration/Runtime checks, diff/secret audit, and exact-SHA CI/Docker/Security before closeout.

## Implementation and local evidence

- `ICanonicalBookRepository.GetChapterAsync` keeps a default compatibility fallback; `EfCanonicalBookRepository` overrides it with an `AsNoTracking` `FirstOrDefaultAsync` filtered by both `BookId` and `ChapterId`, mapping one `CanonicalChapter` row.
- `CatalogQueryService.GetChapterContentAsync` now uses the point lookup after the existing version, selection, and policy gates; full `GetAsync` remains for aggregate/detail/TOC reads.
- TDD red: the new reader regression failed as expected because the point-read counter stayed at `0`; green focused `CatalogQueryServiceTests` is `12/12`.
- Local gates: Restore/tool restore PASS; full Release Build `0 warnings / 0 errors`; Unit `620/620`; Architecture `1/1`; Contract `12/12`; PowerShell migration model check `11/11`; `bash -n scripts/verify-migrations.sh`; `git diff --check`; added-line secret audit PASS.
- Full solution test ran Unit/Architecture/Contract successfully; Integration was `8 passed / 3 skipped / 121 blocked` at class initialization because Windows Docker Engine endpoint `npipe://./pipe/docker_engine` is unavailable. The new PostgreSQL point-query regression compiled but did not obtain local container evidence.
- Runtime/Docker and exact-SHA CI/Docker/Security are pending the candidate push. `.workbuddy-ai/` remains untracked and untouched.
