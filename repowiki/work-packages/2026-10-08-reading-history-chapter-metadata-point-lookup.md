# Work Package 5.74 — Reading history chapter metadata point lookup

Status: In Progress

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

Implementation, test, and delivery evidence will be appended here before
closeout.
