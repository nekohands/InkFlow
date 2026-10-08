# Work Package 5.74 — Reading history chapter metadata point lookup

Status: Accepted

## Objective

Keep bounded reading-history listing from materializing a complete
`CanonicalBook` chapter aggregate for each history row. The user-visible
history result must keep its existing book/chapter metadata and ordering.

## Scope

- Add a bounded canonical-book summary point read to the existing
  `ICanonicalBookRepository` contract, with an EF projection implementation.
- Change only `ReadingStateService.ListHistoryAsync` to use the summary and the
  existing chapter point read.
- Add unit coverage proving the history path does not call full `GetAsync`, and
  PostgreSQL coverage for bounded point queries and book/chapter identity.
- Update the progress, handoff, delivery profile, and this RepoWiki page with
  local and exact-SHA remote evidence.

## Non-goals

- No change to the public Reading/Legado response contract, history limits,
  takedown policy, ordering, persistence schema, migration, cache, or content
  selection.
- No change to shelf/progress/write paths; those remain separate follow-up
  boundaries.
- No change to `CanonicalBook.GetAsync` callers that intentionally need the full
  aggregate, and no changes to `.workbuddy-ai/`.

## Acceptance

1. `ListHistoryAsync` still clamps the requested history page and preserves
   title, author, chapter title/index, timestamps, ordering, missing-item skip,
   and takedown behavior.
2. The production EF path uses bounded summary/chapter projections and does
   not materialize the full canonical chapter collection for one history row.
3. Cancellation is propagated; wrong-book chapter identities do not leak into
   history results.
4. Applicable local gates and exact-head CI, Docker, and Security runs are
   recorded before this package is marked Accepted.

## Risk and verification plan

The main risk is changing visibility or lookup ordering while replacing the
aggregate read. Establish a focused red regression first, then run the focused
Reading tests, full Unit/Architecture/Contract suites, restore, Release build,
migration model and script checks, diff/secret audit, applicable Integration
tests, and exact-head remote gates. Local PostgreSQL/Compose evidence may remain
blocked by the known Windows Docker Engine named pipe limitation; remote CI must
cover that gap.

## Evidence

Implementation: `ICanonicalBookRepository.GetSummaryAsync` adds a bounded
`CanonicalBookSummary` projection; the EF implementation filters the book
before projecting and counts chapters with a correlated query. The history
path now uses the summary and existing `GetChapterAsync`, preserving missing,
takedown, order, metadata, and cancellation behavior. Full aggregate reads
remain for intentional callers.

Local verification: TDD red first failed on the full-book read counter; the
focused regression then passed `1/1`, and all `ReadingStateTests` passed `8/8`.
Restore and tool restore passed; Unit `623/623`, Architecture `1/1`, and
Contract `12/12` passed; Release Build passed with `0 warnings / 0 errors`;
the migration model check passed `11/11`; `wsl.exe bash -n
scripts/verify-migrations.sh` passed; `git diff --check` and the added-line
secret audit passed. The full local Integration run was `8 passed / 3 skipped
/ 123 blocked` because Windows Docker Engine does not expose
`npipe://./pipe/docker_engine`; the focused PostgreSQL regression was likewise
compiled but locally blocked by that named pipe.

Remote verification: implementation candidate
`4e918c1df43367a8ed52d9e52d2170c6782266b3` passed exact-head CI
`37744554098`, Docker `37744554120`, and Security `37744554105`. CI covered
migrations, full tests with PostgreSQL, Compose/runtime smoke, SLO, Redis,
backup/restore, and diagnostics; Docker built/scanned/published the four
business images and verified Compose images; Security passed SBOM, filesystem,
NuGet, and CodeQL checks. The first CI attempt hit a transient external
registry authorization error; its failed job was rerun on the same SHA and
finished GREEN.

Status: Accepted. No public contract, schema/migration, cache, or
`.workbuddy-ai/` change; the local Docker limitation remains an environment
boundary and remote CI supplies the PostgreSQL/runtime evidence.
