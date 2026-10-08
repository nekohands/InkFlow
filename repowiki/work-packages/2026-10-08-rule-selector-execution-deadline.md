# 5.65 Rule selector execution deadline fencing

Status: Accepted

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

## Delivery record (2026-10-08)

Work package: Accepted
Implementation: `RuleAdapter` fences field, pagination-selector, and response-variable extraction; `RuleBasedSourceAdapter` fences Search/TOC list binding and preserves caller cancellation propagation; deterministic field, response-variable, Search, and TOC regressions added.
Acceptance: PASS — internal selector/list-binding deadline expiry fails closed without values, response bodies, or partial entries; caller cancellation propagates; existing valid selector, pagination, credential, and regex behavior remains green.
Build: PASS — `dotnet restore InkFlow.sln` and `dotnet build InkFlow.sln -c Release --no-restore`, 0 warnings / 0 errors.
Tests: PASS/PARTIAL — focused `RuleAdapterTests` 56/56 and `RuleBasedSourceAdapterPaginationTests` 5/5; full Unit 605/605; Architecture 1/1; Contract 12/12. Solution Integration locally was 8 passed / 3 skipped / 116 blocked by unavailable `npipe://./pipe/docker_engine`; CI covered the PostgreSQL/runtime path.
Runtime: PASS — exact-SHA CI covered Compose, Runtime smoke, Redis, PostgreSQL backup/restore, and diagnostics; local Docker runtime remained blocked by the same named pipe.
Security: PASS — staged diff/secret audit and exact-SHA Security workflow passed; no public contract, credential storage, permission, schema, or migration change.
Project-defined gates/phase exit: PASS — PowerShell-equivalent migration model check 11/11; no schema/migration change; exact-SHA CI/Docker/Security all green.
CI: GREEN — candidate `8ec53bdbbf5092c70ff83c2b2ba3b22b22796eac` passed CI `37710808321`, Docker `37710808308`, and Security `37710808245`.
Findings: Selector and list-binding boundaries were not observing the existing internal execution deadline; local Testcontainers could not connect to the Windows Docker named pipe.
Fixed: Added deadline checks around selector/regex/transform extraction, pagination continuation, response variables, and Search/TOC list projection with fail-closed result handling and caller-cancellation preservation.
Remaining risks/blockers: Real sources, production credentials, real-device/manual Release Candidate acceptance, and local Docker/Testcontainers remain outside or unavailable for this package.
Commit/PR: `8ec53bdbbf5092c70ff83c2b2ba3b22b22796eac` pushed to `origin/dev`; no PR created.
Documentation/state sync: RepoWiki work package/index, source-runtime architecture, workflow profile, progress, and handoff synchronized.
RepoWiki sync: PASS — implementation, tests, boundaries, evidence, and blockers agree across source, tests, Wiki, and Chinese state records.
Evidence limitations: Local Integration/Testcontainers was blocked by Docker availability; remote CI supplied production-like PostgreSQL/runtime evidence. GitHub emitted only existing Node.js 20 and ubuntu-latest migration annotations; no job failed.
Next step: Re-intake the next evidence-backed package; keep real-source/manual acceptance and local Docker boundaries visible.
