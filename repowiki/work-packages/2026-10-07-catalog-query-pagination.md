# Catalog query pagination and N+1 reduction

RepoWiki index: [README.md](../README.md)
Workflow profile: [PROJECT_WORKFLOW.md](../../docs/delivery/PROJECT_WORKFLOW.md)
Related contract: [Developer API v1](../../docs/architecture/developer-api.md)

## Work Package

```text
Name: Catalog query pagination and N+1 reduction
Objective / user outcome: Bound catalog list work at the query boundary and remove per-book aggregate/policy reads.
In scope: CanonicalBook summary projection, bulk Content Policy read, CatalogQueryService bounded list/search, Developer catalog limit forwarding, focused unit/integration regression tests.
Non-goals: Cursor/continuation tokens, full-text search, changes to BookId/ChapterId, source discovery, content policy semantics, or UI redesign.
Assumptions: Existing limit=1..100 contract is the required page bound; public catalog/read paths use the service default bound of 100.
Affected modules/files: Library application/persistence; Content application/persistence; API Developer catalog mapping; Unit and PostgreSQL integration tests.
Affected contracts/data/permissions/architecture: Internal repository read ports and the existing Developer API limit behavior; no schema, migration, permission, or ownership change.
Affected invariants/decision records: Canonical identities remain stable; catalog reads use persisted Canonical data and Content Policy; PostgreSQL remains the fact source.
Affected project-defined gates or phase/release exit criteria: Catalog read performance item from the 2026-09-11 review; Release Build, Unit, Architecture, Contract, integration/runtime evidence as applicable.
Expected evidence sources: local build/unit/architecture/contract; PostgreSQL integration in CI; source/runtime evidence from existing gates.
Grillme decisions resolved: target caller = public/developer catalog readers; outcome = bounded read with unchanged response shape; scope = no cursor API or search redesign.
Acceptance criteria:
  - [x] happy path: Catalog list/search returns the same visible book fields and chapter counts while applying a bounded limit before materializing full aggregates.
  - [x] important error path: taken-down books remain hidden; invalid/empty ID collections do not cause unbounded or cross-book policy reads.
  - [x] regression/compatibility: existing repository test doubles remain source-compatible through safe default read-port fallbacks; Developer API limit remains 1..100.
Risks and invariants: Filtering after a bounded candidate query may return fewer than the requested limit when candidates are taken down; no public cursor is introduced in this package.
Verification plan:
  - [x] diff/secrets/scope audit
  - [x] build: `dotnet restore InkFlow.sln`; `dotnet build InkFlow.sln -c Release --no-restore` (environment may require the documented restore workaround)
  - [x] tests: focused CatalogQueryService `11/11`; full Unit `590/590`, Architecture `1/1`, Contract `12/12`
  - [ ] runtime/integration: real PostgreSQL repository projection/policy regressions; Docker/Compose only if available
  - [x] security/architecture: policy filtering, stable IDs, no secret/logging changes, Architecture tests
  - [x] project-defined gates/phase exit: record as applicable; no phase exit change
  - [x] manual/real-environment acceptance: N/A for this backend-only bounded query slice
  - [ ] CI: target commit's required CI/Docker/Security jobs
Documentation/state updates: update Developer API contract note, Progress, Handoff, and this record with evidence.
Affected Wiki pages and source/test evidence: repowiki/README.md; this work package; CatalogQueryService; repository contracts and tests.
Affected Chinese human pages: docs/architecture/developer-api.md; docs/roadmap/progress.md; docs/handoff/handoff.md.
```

## Verification Matrix

```text
Surface | Required check | Evidence source | Command/evidence | Result | Notes
Build/restore | Release restore/build | local | `dotnet restore InkFlow.sln`; `dotnet build InkFlow.sln -c Release --no-restore` | PASS | 0 warnings, 0 errors.
Logic/parser | Catalog bounded list/search regression | local | focused Catalog `11/11`; Unit `590/590` | PASS | Includes search-after-filter and bulk policy regressions.
Boundary | Module dependency rules | local | Architecture `1/1` | PASS | No dependency direction change.
Persistence/infra | Summary/policy projection | CI/local Docker | focused PostgreSQL tests | BLOCKED | Tests compile; Testcontainers cannot connect to `npipe://./pipe/docker_engine`.
External contract | Developer catalog limit | local/CI | Contract `12/12` | PASS | Existing response and limit contract preserved.
Runtime/business path | API/catalog smoke | CI or available runtime | `docker compose -f docker-compose.build.yml config --quiet` | BLOCKED | Docker CLI is unavailable on this host; no runtime claim.
Manual/real environment | Device/live source | N/A | backend-only slice | N/A | Not affected.
UI/browser | Reader visual/accessibility | N/A | backend-only slice | N/A | No UI change planned.
Security | Policy visibility and secret scan | local/CI | diff review and unchanged auth/permission surface | PASS | No new credential, token, cookie, or permission path.
Project-defined gate/state | Catalog review item | local/CI | work package and handoff evidence | PARTIAL | Local gates complete; Docker and remote CI remain.
Documentation/state | Progress/Handoff/contract/Wiki | local | updated pages and links | PASS | Final commit hash to be filled after docs commit.
CI | Required jobs for target commit | CI | push pending | NOT TRIGGERED | Do not report GREEN before required workflows finish.
RepoWiki sync | Wiki/source/human agreement and links | local | source/docs cross-check and `git diff --check` | PASS | Wiki remains the AI source of truth.
```

## Delivery Record

```text
Work package: In Progress
Implementation: `a700977`, `771f027`, `c0d79df`
Acceptance: PARTIAL — local logic and contract gates pass; PostgreSQL/runtime/CI evidence remains.
Build: PASS — Release build 0 warnings, 0 errors.
Tests: PARTIAL — Unit `590/590`, Architecture `1/1`, Contract `12/12`; PostgreSQL-focused tests BLOCKED by Docker named pipe.
Runtime: BLOCKED — Docker CLI/Engine unavailable on this host.
Security: PASS — no new credential, permission, or logging surface; diff reviewed.
Project-defined gates/phase exit: PARTIAL — review item implemented; no phase exit change.
CI: NOT TRIGGERED — candidate is ready to push.
Findings: CatalogQueryService had 3N+1 reads, Developer catalog applied limit after full loading, and search could apply limit before matching candidates.
Fixed: bounded Canonical summaries, bulk latest Content Policy reads, query-bound Developer limits, and provider-side simple search filtering with literal LIKE escaping.
Remaining risks/blockers: Windows Docker availability, PostgreSQL/Runtime evidence, and remote CI jobs.
Commit/PR: `a700977`, `771f027`, `c0d79df`; docs closeout commit pending.
Documentation/state sync: Developer API, Progress, Handoff, RepoWiki index, and this record updated.
RepoWiki sync: PASS (scope, source changes, evidence, and human-facing pages agree).
Evidence limitations: Testcontainers could not start because `npipe://./pipe/docker_engine` is unavailable; no CI has run before push.
Next step: commit the documentation closeout, push `dev`, then read the required CI/Docker/Security jobs and return to any failed root cause.
```
