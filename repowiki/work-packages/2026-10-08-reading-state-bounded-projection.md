Audience: Agent-only
Agent read instruction: Agents MUST read this record when planning, implementing, verifying, or closing work package 5.81; then read the linked Reading and Library source/contracts.

RepoWiki index: [README.md](../README.md)
Workflow gates: [PROJECT_WORKFLOW.md](../../docs/delivery/PROJECT_WORKFLOW.md)
Human workflow record: [progress.md](../../docs/roadmap/progress.md) and [handoff.md](../../docs/handoff/handoff.md)
Related contracts: [ReadingStateService.cs](../../src/Modules/InkFlow.Modules.Reading/Application/ReadingStateService.cs), [ReadingContracts.cs](../../src/Modules/InkFlow.Modules.Reading/Application/ReadingContracts.cs), [ICanonicalBookRepository.cs](../../src/Modules/InkFlow.Modules.Library/Application/ICanonicalBookRepository.cs)

## Work Package

```text
ID: 5.81
Status/phase: Implemented / locally validated; exact-SHA CI pending
Owner: InkFlow maintainers
Authorization/scope: user continuation request “循环推进”; source, tests, RepoWiki and current Chinese delivery records on dev; no .workbuddy-ai/ mutation.

Name: Reading state bounded metadata projection
Objective / user outcome: Reading shelf and progress paths must not materialize a full CanonicalBook aggregate when they only need book metadata, chapter count, or one chapter.
In scope: ReadingStateService shelf/progress reads; reuse ICanonicalBookRepository.GetSummaryAsync and GetChapterAsync; regression counters; affected architecture/progress/handoff/work-package records.
Non-goals: no public JSON/route change, no BookId/ChapterId change, no bulk/N+1 redesign, no content selection or policy change, no schema/migration/cache/permission/UI change, no full book-detail or content-package path change, no .workbuddy-ai/ change.
Scope/acceptance changes: N/A.
Workflow improvement candidate: N/A; this is the next evidence-backed bounded read-path gap after 5.80.
Assumptions: existing summary and point-chapter projections are authoritative and EF production overrides are already available; full aggregate remains for callers that mutate or need the chapter collection.
Affected modules/files (actual): `src/Modules/InkFlow.Modules.Reading/Application/ReadingStateService.cs`, `tests/InkFlow.UnitTests/ReadingStateTests.cs`, `repowiki/architecture.md`, `docs/architecture/architecture.md`, current delivery records, and this work package.
Affected contracts/data/permissions/architecture: application-internal result mapping only; public Reading response and persistence schema remain frozen.
Risk classification/basis: LOW/MEDIUM; reversible read-path change across shelf and progress use cases, no data/security/schema/live-source mutation, but user-visible metadata and takedown/error semantics must remain exact.
Recovery readiness: revert the implementation commit; no data compensation or migration required.
Capability safety metadata: read-only canonical summary/point queries plus existing Reading state writes; no new side effect, asynchronous operation, or external target.
Evidence confidentiality: only counts, test output, commit and run IDs are persisted; no credentials, tokens, cookies or user data.
Affected-surface reconciliation: PASS; actual diff matches the planned Reading application/test and documentation surfaces; no public contract, schema, migration, cache, permission, or `.workbuddy-ai/` drift.
Expected evidence sources: local source/tests/build, remote CI/Docker/Security, PostgreSQL integration when Docker is available.
Verification profile: full project-defined code package.
Authoritative validators by gate: dotnet CLI for local build/tests; migration verifier for model; GitHub Actions CI/Docker/Security for target SHA gates.

Acceptance criteria:
  - [x] AC-1 ListShelfAsync and PutShelfAsync preserve fields, ordering, missing/takedown behavior, and optional current-chapter metadata while production reads use summary plus at most one point chapter query, not full GetAsync.
  - [x] AC-2 GetProgressAsync and SaveProgressAsync preserve invalid/not-found/invalid-chapter behavior and returned chapter metadata while avoiding full CanonicalBook materialization.
  - [x] AC-3 cancellation, authorization/policy boundaries, stable identities, public responses, schema/migrations, and intentional full aggregate callers remain unchanged; regression tests prove zero full-book reads for the bounded paths.

Acceptance/evidence traceability: AC-1 -> ReadingStateService + ReadingStateTests shelf regression; AC-2 -> progress regression; AC-3 -> existing Unit/Architecture/Contract, diff, migration and exact-SHA gates.
Affected invariants/decision records: stable BookId/ChapterId; canonical content remains authoritative; content policy is checked before public state reads/writes; no ADR required because this implements the existing bounded read-model direction.
Affected project-defined gates or phase/release exit criteria: Reading state automation and Release Candidate runtime/CI regression; real account/device acceptance remains outside this package.
Blocker handoff: local PostgreSQL/Testcontainers and Compose runtime may remain BLOCKED by Windows Docker named pipe; owner InkFlow maintainers; recheck when Docker Engine is available; remote CI must supply the corresponding integration/runtime evidence.

Verification plan:
  - [x] TDD red/green focused ReadingStateTests with full-read counters; RED failed with 5 full reads, GREEN passed
  - [x] diff/scope/secret audit; bounded-read audit and added-line secret audit passed
  - [x] `dotnet restore InkFlow.sln`
  - [x] `dotnet build InkFlow.sln -c Release --no-restore --verbosity:minimal` (0 warnings / 0 errors)
  - [x] ReadingState focused `9/9`, Unit `627/627`, Architecture `1/1`, Contract `12/12`
  - [x] Windows `dotnet-ef` migration model verification `11/11`; WSL wrapper syntax passed but WSL lacks dotnet
  - [ ] focused PostgreSQL Integration; BLOCKED by `npipe://./pipe/docker_engine` unavailable
  - [ ] exact target-SHA CI, Docker and Security; pending candidate commit
  - [x] RepoWiki, architecture, progress, handoff and workflow records synchronized; `.workbuddy-ai/` remains untracked and untouched

Documentation/state updates: RepoWiki index/current state, this work package, docs/delivery/PROJECT_WORKFLOW.md, docs/roadmap/progress.md, docs/handoff/handoff.md; architecture.md/repowiki/architecture.md only if final verified read boundary needs a rule.
```

## Verification Matrix

```text
Surface | Required check | Authority | Result before candidate commit
Scope/read path | CodeGraph + targeted source/test review | repo source and tests | PASS
Logic/regression | focused ReadingStateTests red/green + full Unit | dotnet test | PASS (9/9; 627/627)
Build | Release restore/build | dotnet CLI | PASS (0 warnings / 0 errors)
Boundary | Architecture tests | dotnet test | PASS (1/1)
Persistence/infra | migration model + focused Integration | project verifier/Testcontainers | PARTIAL (11/11 model; Integration BLOCKED by Windows Docker)
External contract | Contract tests | dotnet test | PASS (12/12)
Runtime | Compose/reader/account smoke as applicable | CI runtime | PENDING candidate SHA
Security | secret/diff audit and Security workflow | git/CI | PARTIAL (local audits PASS; CI pending)
Documentation | RepoWiki/docs agreement | source + link audit | IN PROGRESS (implementation state synchronized; closeout pending)
CI | exact target SHA workflows | GitHub Actions | NOT TRIGGERED; candidate commit pending
```
