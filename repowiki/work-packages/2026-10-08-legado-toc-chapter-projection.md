# Legado TOC canonical chapter projection

Date: 2026-10-08
Status: Accepted
Package: 5.79

## Objective

Keep the Legado TOC path bounded to canonical book visibility and chapter metadata. A TOC request must not materialize the full `CanonicalBook` aggregate and all chapter rows merely to return directory entries.

## Scope

- Add `CanonicalChapterSummary(Guid Id, int Index, string Title)` and `ICanonicalBookRepository.ListChapterSummariesAsync`.
- Keep the interface fallback for old test doubles; production EF uses a scalar `AsNoTracking` projection of `Id`, `ChapterIndex`, and `Title` ordered by `(ChapterIndex, Id)`.
- Add `CatalogQueryService.GetChapterListAsync` with the existing takedown-first and missing-book-null semantics.
- Route `LegadoContractService.GetTocAsync` through the bounded chapter list while keeping the public response and URL shape unchanged.
- Keep `GetBookAsync` for full book detail and `GetChapterContentAsync` for point content reads.

## Non-goals

No public API/Legado JSON or URL change, no BookId/ChapterId change, no content-selection change, no schema or migration, no cache or permission change, and no `.workbuddy-ai/` changes.

## Acceptance and evidence

- TDD red/green: the focused regression first failed to compile because the chapter-list service seam was absent, then passed.
- Focused Catalog/Legado tests: `23/23`.
- Unit: `625/625`; Architecture: `1/1`; Contract: `12/12`.
- Restore and Release Build: PASS, `0 warnings / 0 errors`.
- Migration model verification: `11/11`; script syntax, `git diff --check`, and added-line secret audit: PASS.
- Focused PostgreSQL Integration SQL-shape regression: BLOCKED by local Windows Docker Engine endpoint `npipe://./pipe/docker_engine`; no product assertion failed.
- Candidate implementation SHA `c0aefc08682e9ed1bb22b201ec16b4c5a9b7ba10` passed exact-head [CI 37768061749](https://github.com/nekohands/InkFlow/actions/runs/37768061749), [Docker 37768061718](https://github.com/nekohands/InkFlow/actions/runs/37768061718), and [Security 37768061741](https://github.com/nekohands/InkFlow/actions/runs/37768061741).

## Boundary and handoff

Full aggregate reads remain intentional for book detail and other callers that require the complete model. Local Testcontainers and real-source/manual Legado client acceptance remain separate environment gates. `.workbuddy-ai/` remains untracked and untouched.
