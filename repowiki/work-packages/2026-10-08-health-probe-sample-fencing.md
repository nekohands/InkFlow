# 5.69 Health-probe sample lookup fencing

Status: Accepted

## Objective

Keep the scheduled Toc health probe bounded when it chooses a sample book for a
source. The probe must query only the first matching source book instead of
materializing the entire source-book table, while preserving the existing
silent-no-sample, empty-Toc failure, recovery, cancellation, and safe reason
semantics.

## Evidence gap

`HealthProbeService.ProbeTocAsync` calls `ISourceBookRepository.ListAllAsync`
and then filters in memory to the first book for the unhealthy source. The
10-minute scheduler therefore loads every imported source book for each due
Toc probe, even though one ordered sample is sufficient.

## Scope

- Add a source-filtered first-book repository query ordered by
  `(CreatedAt, Id)` and limited to one row.
- Use that query from the Toc health probe and keep the existing adapter,
  recorder, health-state, cancellation, and failure-classification flow.
- Add focused unit and PostgreSQL repository regressions and synchronize the
  source-runtime and delivery documentation.

## Non-goals

- No paging or durable cursor for the unhealthy-health candidate list.
- No schema, migration, public API/Legado JSON, source adapter, HTTP budget,
  retry policy, scheduler interval, or live-source/manual acceptance change.
- No change to other `ISourceBookRepository` callers or their aggregate loading
  semantics.

## Acceptance

1. A due Toc probe performs a source-filtered bounded lookup and never calls
   `ListAllAsync`.
2. The production query returns at most one chapter-free `SourceBook`, ordered
   deterministically by `(CreatedAt, Id)` and filtered by `SourceId`; no match
   returns null.
3. Existing no-sample silent skip, empty/non-empty Toc handling, health
   recording, caller cancellation, and non-sensitive failure reasons remain
   unchanged.

## Risks and boundaries

The probe intentionally continues using one imported book as its Toc sample;
this package makes selection deterministic and bounded but does not add sample
rotation or health-candidate paging. The existing `ListAllAsync` contract is
retained for unrelated test doubles and non-probe boundaries unless evidence
shows a safe removal is needed.

## Implementation

- Added `ISourceBookRepository.FindFirstForSourceAsync` and implemented the EF
  query with `SourceId` filtering, `(CreatedAt, Id)` ordering, `FirstOrDefaultAsync`,
  and a chapter-free rehydrated `SourceBook`.
- `HealthProbeService.ProbeTocAsync` now uses the bounded query; the unit fake
  throws if `ListAllAsync` is touched, and the repository regression covers
  source filtering, stable ordering, and an empty chapter collection.
- Updated the deterministic EndToEnd source-book fixture and synchronized the
  Source Runtime, delivery, Progress, Handoff, and RepoWiki records.

## Verification

- TDD red state reproduced the three affected health-probe failures; focused
  green HealthProbeService tests passed `6/6`, and the affected EndToEnd test
  passed `1/1`.
- Restore passed; Release Build passed with `0 warnings / 0 errors`; full Unit
  passed `616/616`; Architecture `1/1`; Contract `12/12`; migration model check
  `11/11`; migration shell syntax, diff checks, and secret audit passed.
- Local Integration was `8 passed / 3 skipped / 118 blocked`: the PostgreSQL
  repository test and the remaining Testcontainers cases could not start
  because Windows Docker Engine named pipe `npipe://./pipe/docker_engine` is
  unavailable. Remote CI supplied the PostgreSQL and runtime evidence.
- Exact implementation SHA `62331b47f1e1efd2d4080477b45e15661567e79f` passed
  CI `37724656744`, Docker `37724656752`, and Security `37724656807`; all
  reported the same head SHA.

## Delivery

Implementation candidate `62331b47f1e1efd2d4080477b45e15661567e79f` is pushed
to `origin/dev`. The implementation and this documentation closeout are
separate commits so the remote gates remain attributable to exact SHAs.

## Boundary

The probe intentionally continues using one imported book as its Toc sample;
this package makes selection deterministic and bounded but does not add sample
rotation or health-candidate paging. `ListAllAsync` remains for unrelated
boundaries, and `.workbuddy-ai/` remains untracked and untouched.
