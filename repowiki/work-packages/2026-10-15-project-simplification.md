# Project simplification — host composition and audited responses

Audience: Agent-only.
Agent read instruction: Read when continuing the project-wide simplification, changing host composition, or verifying route/audit compatibility.
ID: simplify-2026-10-15
Status: Implemented; local non-Docker gates validated; remote gates pending
Owner: InkFlow maintainers
Authorization: user request to organize the whole project with simpler design; prior request to push completed changes; grillme routine choices adopted.
Record mode: FULL (multiple hosts and externally observable routes).
Parent/root: [Work packages](README.md) / [RepoWiki](../README.md).

## Objective, scope and acceptance

Survey the maintained solution and remove concrete structural complexity without changing its modular-monolith architecture or business behavior.
The bounded implementation covers API service composition, feature route mapping, Worker/Scheduler class placement, and duplicated audited response wrappers.
No public route/DTO/policy, persistence schema, task timing, dependency version, HTML, or Source DSL behavior changes are intended. User-owned `.workbuddy-ai/` is excluded from reads and writes.

- AC-1: API startup describes composition and middleware order; route handlers live with their features. Existing routes, binding, policies, response formats and middleware order remain equivalent.
- AC-2: Worker/Scheduler startup contains registration and startup only; extracted class bodies retain their cancellation, lease, scope and timing behavior.
- AC-3: audited response execution has one implementation; audit completes before response creation/execution and failures prevent response execution. Existing payload/error mappings remain unchanged.
- AC-4: Release restore/build, Unit/Architecture/Contract tests, applicable integration/runtime and the candidate commit's CI/Docker/Security gates are recorded truthfully.
- AC-5: source navigation, current progress/handoff and Wiki are synchronized; reviewed commits are pushed to origin/dev.

## Evidence and baseline

Observed 2026-10-15, baseline `42fb096c710182bd0223f3fcd6bd6484db066035`, branch `dev`; tracked worktree clean; `.workbuddy-ai/` untracked.

| Claim | Kind/status | Evidence and validity |
| --- | --- | --- |
| SCOPE | CONTRACT/CURRENT | Current user request authorizes behavior-preserving organization across the repository. Replan if behavior/contracts must change. |
| STRUCTURE | FACT/CURRENT | Baseline source inventory: API Program 1773 lines; Worker Program 377 lines; Scheduler Program 151 lines; 8 nested AuditedResult implementations. |
| BASELINE | FACT/CURRENT | .NET SDK 10.0.401; restore PASS; Unit 627/627 PASS before edits, `artifacts/refactor-20261015/baseline/baseline.trx`. |
| CI-BASELINE | FACT/CURRENT | Baseline SHA CI 38114885885 and Security 38114885906 success; Docker 38114885938 failed before this work. Rechecked through gh. |

The initial sandboxed .NET invocation failed before build; `dotnet --info` reported access denied to Windows Service Control Manager. The reviewed external invocation restored successfully and produced the baseline above. This is an environment restriction, not a source regression.

## Survey and design decisions

- Existing modules, adapters and BuildingBlocks already provide the dependency boundaries; preserve them and reuse existing feature endpoint files.
- Host Program files mix registration, routes and concrete runtime classes. Move by responsibility using checked mechanical extraction, then test compiled endpoint metadata and existing behavior.
- Reuse the existing lazy result-factory form in LegadoTokenEndpoints for audit-first responses across all eight consumers.
- Large Reader HTML, Source DSL/selector, private import and persistence files encode distinct parsing/security/transaction semantics. Size alone does not justify rewriting their algorithms in this package.
- No ADR is required: dependencies, ownership and accepted architecture do not change.

## Execution and verification boundaries

Allowed writes: affected files under `src/Apps/InkFlow.Api`, `src/Apps/InkFlow.Worker`, `src/Apps/InkFlow.Scheduler`; focused Unit tests; this Wiki record/index/module map/catalog; affected architecture/progress/handoff/profile docs. Ignored `artifacts/refactor-20261015` contains temporary transformation/check scripts and local evidence only.
Documentation normalization at resume: current progress/handoff and profile retain completed package records and a stale skeleton-only state. Archive those blocks verbatim into the existing progress/handoff histories; preserve pending manual/environment items in current files. Both history paths are in the allowed write set. No historical evidence is discarded or promoted to current verification.
Read set: versioned repository source/config/tests/docs/Wiki, current .NET/Node toolchains and NuGet cache, configured package feeds, GitHub Actions metadata via gh. No live production data or external source accounts.
Risk: medium, reversible host-only refactor with frozen contracts. Review controls: source extraction equivalence, focused routing/audit regression checks, existing Unit/Contract/Architecture tests, remote runtime/security gates.
Stages: baseline -> API extraction -> focused tests -> worker extraction/audit consolidation -> full applicable gates -> candidate commit/push -> CI verification -> evidence closeout. A failed gate is retained and corrected before acceptance.
No delegated agents, production apply, destructive operations, reusable-resource changes or workflow source replacement: those controls are N/A. Tests and CI are identified by this package, baseline/candidate SHA, command selection and results/run IDs.
Known blocker: Docker image security scan was already RED. Its remediation is separate from behavior-preserving source organization; do not claim Accepted while the required gate is RED.

## Sources

- [Architecture and invariants](../architecture.md)
- [Module map](../modules/README.md)
- [Verification](../operations/verification.md)
- [Hosts](../../src/Apps/)
- [Unit tests](../../tests/InkFlow.UnitTests/)
- [CI definitions](../../.github/workflows/)

## Result

### Implementation and acceptance mapping

- AC-1: API Program is 43 lines; feature maps are composed by `ApiEndpoints`, DI/auth/rate-limit registration by `ApiServiceRegistration`. Independent comparison against `42fb096` confirms all 12 moved handler blocks, DI registration and middleware body unchanged (indent/import changes only). `ApiCompositionTests` builds actual endpoint delegates, checks unique method/path pairs and representative feature auth/rate-limit metadata.
- AC-2: Worker Program 377 -> 177 lines; Scheduler 151 -> 71. All 7 extracted class bodies and non-using host startup statements match the baseline. No project reference, package, migration, config or job timing edits.
- AC-3: 8 audit wrappers -> one `AuditedResult`; feature payload/status switches remain local. Existing endpoint tests and new delayed/failing/cancelled audit tests pass. Temporarily executing the response before the audit caused both new tests to fail; the mutation was reverted before final verification.
- AC-4: see gate table; local Docker and remote gates are not inferred from passing Unit tests.
- AC-5: module source navigation and architecture guide updated; completed/superseded records moved verbatim into existing docs histories. Current progress 537 -> 211 lines, handoff 540 -> 216, profile 282 -> 85; pending manual/real-environment items preserved. Commit/push and final remote evidence pending.

### Verification evidence (2026-10-15)

| Gate | Result | Evidence / validity |
| --- | --- | --- |
| Restore / Release build | PASS | `dotnet restore InkFlow.sln`; `dotnet build InkFlow.sln -c Release --no-restore`, 0 warnings/errors; repeated after import cleanup. |
| Unit / Architecture / Contract | PASS | 630/630, 1/1, 12/12; `dotnet test InkFlow.sln -c Release --no-build --no-restore`, TRX under `artifacts/refactor-20261015/final/`. Unit rerun after formatting also 630/630. |
| Integration | BLOCKED locally | 139 selected: 8 passed, 3 not executed, 128 failed in initialization with `DockerUnavailableException`; no available Docker CLI/Engine. Await same-commit CI PostgreSQL/Redis evidence. |
| Migration model | PASS | All 11 contexts from `scripts/verify-migrations.sh`, pinned dotnet-ef 10.0.4, Release `has-pending-model-changes --no-build`. PowerShell executes the exact script selection/options; no database mutation. |
| Local runtime | BLOCKED | Compose requires unavailable Docker; remote runtime smoke remains required. No production or external source interaction. |
| Review / regression attribution | PASS for reviewed source | Mechanical equivalence checks plus diff review of 8 payload adapters; no auth, validation, input bounds, schema or dependencies changed. Audit ordering mutation fails as intended. No independent reviewer claimed. |
| Documentation normalization | PASS | Five original blocks archived verbatim; unresolved checklist retained. 343 local Markdown target links checked; Wiki catalog generator write/check passes, 32 pages. Current source navigation, profile and state agree. |
| Exact-SHA CI / Docker / Security | NOT TRIGGERED yet | Push after final review; baseline Docker RED is retained as pre-existing, not attributed to refactoring. |

Review scope: correctness, readability, architecture, security and performance. No new business branches, queries or background loops; the shared lazy factory retains audit-before-response semantics. The development fallback connection string was moved unchanged, not newly introduced credentials. Actual writes are limited to the declared hosts/tests/Wiki/docs and ignored local verification artifacts; `.workbuddy-ai/` is untouched.

Normalization map: current progress completed 5.50–5.81 blocks -> progress history; current handoff completed blocks and superseded skeleton snapshot -> handoff history; profile accepted-package snapshot -> progress history. All source blocks were compared before/after; existing histories were retained. Conformance: COMPLIANT for the reconciled workflow/source navigation and current/history surfaces. RepoWiki sync: PASS.

Continuation scope rebuilt from current Wiki index, module map, verification page, work package, changed source/diff and TRX identities after context reduction; no target or scope drift found. No independent child/delegation, live rollout, reusable workflow-source change, destructive purge or production lease/approval is involved (N/A). Local evidence artifacts are ignored and contain no production credentials.

Remaining blocker owner: InkFlow maintainers. Next action: push reviewed candidate and inspect exact-SHA Actions; if the existing OTLP image scan remains RED, retain `Implemented` rather than `Accepted`, and address that pinned image in a separate verified dependency change. Recheck local integration/runtime when Docker is available; real device/source/account acceptance remains the existing separate release checklist.
