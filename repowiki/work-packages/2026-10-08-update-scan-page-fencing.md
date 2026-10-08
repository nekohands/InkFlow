# 5.68 Scheduled update-scan page/fan-out fencing

Status: Accepted

## Objective

Bound the scheduled update scan so one scheduler tick does not materialize the
entire source-book table or enqueue an unbounded TOC fan-out. Preserve stable
book ordering, atomic task dedupe, health filtering, cancellation, and existing
source/task contracts.

## Evidence gap

`UpdateScanService.EnqueueTocScansAsync` currently calls `ListAllAsync`, loads
every imported source book into memory, and attempts one TOC task per book. The
30-minute scheduler has no per-tick work ceiling or cursor, so a growing source
book table can turn one tick into an unbounded database read and enqueue burst.

## Scope

- Add a bounded keyset page for source books ordered by `(CreatedAt, Id)`.
- Process at most 100 books per scheduled tick and retain an in-process cursor
  so successive ticks cover the next page; reset at the end of a full pass.
- Keep the existing atomic task dedupe and capability-health skip behavior.
- Add focused unit and PostgreSQL repository regressions and synchronize source
  runtime/delivery documentation.

## Non-goals

- No persistent scheduler cursor, schema, migration, public API/Legado JSON,
  source registry policy, worker queue redesign, task dedupe semantics, retry
  policy, source adapter behavior, or live-source/manual acceptance.
- No change to `ListAllAsync` callers outside the scheduled update-scan path.

## Acceptance

1. Each scheduled tick reads and processes at most 100 source books and cannot
   enqueue more than 100 TOC tasks.
2. Within a scheduler process, the cursor advances in deterministic
   `(CreatedAt, Id)` order, does not skip or duplicate a page, and resets after
   the last page; cancellation does not falsely advance it.
3. Existing health skips, atomic dedupe, normal results, and caller cancellation
   remain unchanged.
4. The production repository performs the page in SQL; it does not call
   `ListAllAsync` for the scheduled path.

## Risks and boundaries

The cursor is deliberately in-memory: a scheduler restart begins at the first
page and may revisit already scanned books, while task dedupe keeps that safe.
Durable cursor ownership and multi-scheduler coordination remain a separate
work package if restart fairness becomes a measured requirement.

## Verification plan

- Red/green UpdateScanService cursor/batch tests and PostgreSQL keyset-page
  regression.
- Full Unit, Architecture, Contract, Release Restore/Build, migration model
  check, diff/secret audit, applicable Integration/Runtime, and exact-SHA
  CI/Docker/Security verification.

## Implementation

- Added `ISourceBookRepository.ListPageAsync` with a stable `(CreatedAt, Id)`
  keyset cursor and a bounded EF/PostgreSQL query; the scheduled path no longer
  calls `ListAllAsync`.
- `UpdateScanService` and Scheduler process at most 100 books per tick, advance
  an in-process cursor only after a successful batch, and reset it after the
  last page. Existing health filtering, atomic task dedupe, and cancellation
  remain in the same loop.
- No public adapter/API/Legado contract, Schema, or Migration changed.

## Verification

- Focused `UpdateScanServiceTests` 4/4 and affected EndToEnd scheduler test
  1/1; Unit 616/616; Architecture 1/1; Contract 12/12.
- Release restore/build passed with 0 warnings / 0 errors; PowerShell
  migration model check 11/11; `git diff --check` and secret audit passed.
- Local Integration was executed: 8 passed / 3 skipped / 117 blocked by the
  unavailable Windows Docker named pipe `npipe://./pipe/docker_engine`.
  The PostgreSQL keyset-page regression passed in the remote full test gate.

## Delivery

- Initial implementation SHA `a9686fd114c2704360588b81eb64d9c6f35f9df3`
  exposed an existing integration-test repository double that still only
  implemented `ListAllAsync`; follow-up SHA
  `83f12e8cdc30837ed6a8309e0288829966065eaf` added the bounded test fixture
  implementation and is the final candidate.
- Final candidate CI `37721216436`, Docker `37721216460`, and Security
  `37721216368` all passed with the exact head SHA, including PostgreSQL,
  runtime, Compose, CodeQL, dependency, image, and filesystem gates.
- Remaining boundary: the cursor is intentionally in-memory; restart replay
  and multi-Scheduler coordination would require a separately owned durable
  cursor design. Real sources and Release Candidate manual acceptance remain
  independent.
