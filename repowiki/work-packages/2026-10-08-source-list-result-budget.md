# 5.67 Source list-result budget fencing

Status: Accepted

## Objective

Bound source adapter Search/TOC list results so a valid-but-hostile or unexpectedly
large response cannot fan out into an unbounded in-memory result set or an
unbounded source-book chapter sync. Preserve fail-closed behavior and existing
adapter/public contracts.

## Evidence gap

`SourceRuleExecutionLimits` already bounds requests, response bytes, execution
time, selector work, and result bytes, but RuleBasedSourceAdapter and trusted
CodeAdapters do not bound the number of projected Search/TOC items. The common
`SourceCatalogService` TOC path also accepts every adapter-returned entry before
domain synchronization.

## Scope

- Add a validated finite `MaxResultItems` budget to the existing source execution
  limits, defaulting to the established 10,000 chapter/package ceiling.
- Fail closed when RuleBased, Kanunu8, or 17K Search/TOC projection crosses the
  item budget; do not return partial lists.
- Add a defense-in-depth TOC count check in `SourceCatalogService` and deterministic
  regressions for the limit, stable failure, identity, cancellation, and normal
  fixture paths.
- Synchronize source-runtime and delivery documentation.

## Non-goals

- No change to `ISourceAdapter` method signatures, public/Legado JSON, pagination
  semantics, source registry paging, HTTP/body/time/regex budgets, chapter IDs,
  existing chapters, schema, migration, permissions, retry policy, or live-source
  acceptance.

## Acceptance

1. Normal Search/TOC fixtures retain their current results and stable IDs/order.
2. RuleBased, Kanunu8, and 17K list projections reject more than the validated
   `MaxResultItems` without returning a partial list.
3. `SourceCatalogService` rejects an over-limit TOC from any adapter before
   persistence and reports a stable non-sensitive error.
4. Caller cancellation still propagates; existing empty/error/health behavior and
   public contracts remain unchanged.

## Risks and boundaries

This is a fail-closed source-runtime guard: an over-limit upstream result is
reported as a failed capability operation rather than silently truncated. The
default 10,000 item ceiling matches the existing package chapter ceiling; callers
may use a lower validated limit in controlled fixtures/hosts. Response byte and
execution budgets remain separate lower-level defenses.

## Verification plan

- Red/green focused RuleBased, Kanunu8, 17K, and SourceCatalogService tests.
- Full Unit, Architecture, and Contract tests; Release Restore/Build; migration
  model check and diff/secret audit.
- Run applicable Integration/Runtime gates; record the known local Docker
  named-pipe limitation explicitly and verify the candidate SHA in CI/Docker/
  Security before acceptance.

## Implementation

- `SourceRuleExecutionLimits.MaxResultItems` is validated, defaults to 10,000,
  and fences RuleBased Search/TOC plus Kanunu8 and 17K Search/TOC projections.
- Over-limit projections return no partial list. `SourceCatalogService` adds a
  10,000-entry defense-in-depth TOC check before health success or persistence.
- No `ISourceAdapter`, public API/Legado JSON, schema, or migration change.

## Delivery evidence

- Focused adapter/catalog regressions: 25/25; full Unit: 615/615;
  Architecture: 1/1; Contract: 12/12.
- Restore and Release Build: PASS, 0 warnings / 0 errors; migration model
  check: 11/11; `git diff --check` and diff/secret audit: PASS.
- Local Integration was attempted: 8 passed / 3 skipped / 116 blocked by the
  unavailable Windows Docker endpoint `npipe://./pipe/docker_engine`.
- Candidate implementation SHA `2853c5e71cb321284113637042a7a91b224b069f`
  passed exact-SHA CI `37717734392`, Docker `37717734423`, and Security
  `37717734362`, including remote PostgreSQL/runtime/Compose smoke.

Local Docker/Testcontainers and live-source/manual Release Candidate checks
remain independent environment boundaries; `.workbuddy-ai/` remains untracked
and untouched.
