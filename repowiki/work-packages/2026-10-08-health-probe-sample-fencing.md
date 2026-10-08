# 5.69 Health-probe sample lookup fencing

Status: In Progress

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

## Verification plan

- Red/green HealthProbeService regression proving the full-list method is not
  touched, plus repository ordering/filter regression.
- Full Unit, Architecture, Contract, Release Restore/Build, migration model
  check, diff/secret audit, applicable Integration/Runtime, and exact-SHA
  CI/Docker/Security verification.
