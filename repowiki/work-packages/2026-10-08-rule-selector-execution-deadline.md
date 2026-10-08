# 5.65 Rule selector execution deadline fencing

Status: In Progress

## Objective

When a RuleAdapter execution deadline expires during response field, pagination,
or response-variable extraction, the execution must fail closed instead of
returning values produced after the deadline.

## Evidence gap

`RuleAdapter` already creates a linked `MaxExecutionTime` cancellation token and
uses it for HTTP and credential resolution, but selector and extraction calls do
not check that token at their boundaries. A slow selector can therefore finish
after the execution budget and still produce a successful result.

## Scope

- `InkFlow.Modules.Sources.Application.RuleAdapter` extraction and selector
  cancellation boundaries, plus `RuleBasedSourceAdapter` Search/TOC list
  binding after paginated response collection.
- Existing source selector/adapter unit regressions covering ordinary extraction,
  pagination, and derived variables.
- Source runtime documentation and current delivery records.

## Non-goals

- No new selector syntax, dynamic multi-request/branch/recursive execution, or
  `MaxDepth` implementation.
- No public API/Legado contract, Source schema, migration, credential storage,
  retry policy, or live-source behavior changes.
- No change to caller cancellation semantics: caller cancellation still
  propagates as cancellation; only the internal execution deadline becomes the
  stable failed result.

## Acceptance

1. A selector or extraction step that returns after `MaxExecutionTime` cannot
   produce a successful or partial `RuleExecutionResult`.
2. Internal deadline expiry returns the existing stable
   `execution: time budget exceeded.` error without response bodies or values.
3. The same fencing applies to normal fields, pagination continuation selectors,
   response-derived variables, and Search/TOC list binding.
4. External caller cancellation remains propagated and existing valid selector,
   pagination, credential, and regex behavior remains unchanged.

## Risks and boundaries

The change is an in-process Sources runtime safety fix. It must preserve the
existing bounded selector implementations and their fail-closed behavior; no
database or public contract is affected. The test must be deterministic and not
depend on a real source or Docker.

## Verification plan

- Red/green focused `RuleAdapterTests` for post-deadline extraction and caller
  cancellation.
- Sources/selector focused unit tests, then full Unit and Architecture tests.
- Release Restore/Build, migration model check (no model change), Contract tests,
  diff/security review, and exact-SHA CI/Docker/Security gates.
- Local Docker/Testcontainers and live-source checks remain environment/manual
  boundaries and must be reported explicitly.

## Current evidence (2026-10-08)

- Implementation: `RuleAdapter` fences field, pagination-selector, and
  response-variable extraction; `RuleBasedSourceAdapter` fences Search/TOC
  list binding and preserves caller cancellation propagation.
- Local gates: Release restore/build passed with 0 warnings and 0 errors;
  Unit `605/605`, Architecture `1/1`, Contract `12/12`, and 11-context
  migration model check passed; diff/secret audit passed.
- Integration: `8 passed / 3 skipped / 116 blocked` because Testcontainers
  could not connect to `npipe://./pipe/docker_engine` on this Windows host.
- Remote exact-SHA CI/Docker/Security gates: pending candidate commit and push.
