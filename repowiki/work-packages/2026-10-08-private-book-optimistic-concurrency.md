# Private book optimistic concurrency

RepoWiki index: [README.md](../README.md)
Workflow profile: [PROJECT_WORKFLOW.md](../../docs/delivery/PROJECT_WORKFLOW.md)
Related feature/module docs: [Architecture](../architecture.md); [Private Library](../../docs/architecture/architecture.md#3-private-library)

## Work Package

```text
Name: Private book optimistic concurrency
Objective / user outcome: Concurrent private-book metadata edits must not silently overwrite a newer edit.
In scope: PrivateBook monotonic Version; library schema/model migration; atomic owner-scoped conditional update; Private Library service/API request and response contract; stable 409 mapping; unit, contract, and PostgreSQL integration regressions.
Non-goals: CanonicalBook/SourceBook/Identity/Reading concurrency; delete compare-and-swap; chapter/content editing; automatic merge or retry; ETag/If-Match; catalog/search/UI changes.
Assumptions: Version starts at 1, increments exactly once for each successful private-book metadata update, and is returned in PrivateBookView. PUT requires a positive version; create/import still start at version 1.
Affected modules/files: Library domain/application/persistence/API; Library migration; Unit/Contract/Integration tests; RepoWiki and Chinese delivery/progress/handoff pages.
Affected contracts/data/permissions/architecture: Private Library PUT gains a required version precondition and maps stale versions to 409; private_books gains a non-null bigint Version initialized to 1. UserId ownership and private/public separation remain unchanged.
Affected invariants/decision records: PostgreSQL remains the source of truth; BookId/ChapterId identity and private ownership are unchanged; no ADR is needed because this closes the existing private metadata lost-update gap within the documented Library boundary.
Expected evidence sources: red/green focused tests; Release Restore/Build; full Unit, Architecture, Contract, migration checks; PostgreSQL Testcontainers and remote runtime/CI/Security evidence.
Acceptance criteria:
  - [x] happy path: create/get/list/import expose Version=1; a PUT with the current version succeeds and returns the next version.
  - [x] important error path: missing/non-positive version is invalid; a stale version returns 409 and does not change the stored title/author.
  - [x] concurrency regression: two reads at the same version allow exactly one conditional save; the stale save reports conflict and cannot overwrite the winner.
  - [x] compatibility/security: owner filtering, private path isolation, response fields, import/export behavior, and module dependency direction remain valid; no secret/logging changes.
Risks and invariants: The write must use one database conditional update guarded by UserId, book ID, and Version; a pre-read followed by an unconditional SaveChanges is not sufficient. Existing rows must receive Version=1 through the migration.
Verification plan:
  - [x] diff/scope/secret audit
  - [x] build: `dotnet restore InkFlow.sln`; `dotnet build InkFlow.sln -c Release --no-restore`
  - [x] tests: focused Library/API tests; full Unit, Architecture, Contract
  - [x] persistence/runtime: migration model check and PostgreSQL integration; remote runtime smoke
  - [x] security/architecture: owner-scoped conditional write and no cross-module dependency change
  - [x] CI: target SHA CI/Docker/Security all GREEN
Documentation/state updates: synchronize RepoWiki index/work package, workflow profile, Progress, Handoff, and affected Library contract notes at closeout.
```

## Delivery Record

```text
Work package: Accepted
Implementation: Added monotonic `PrivateBook.Version` (initial value 1), required positive PUT version precondition, stable 409 conflict mapping, owner-scoped SQL conditional update, version migration, and focused regressions.
Acceptance: PASS; create/get/list/import expose version 1, successful updates increment once, stale/missing versions do not overwrite data, and concurrent writes permit exactly one winner.
Build: PASS; `dotnet restore` and Release build completed with 0 warnings / 0 errors.
Tests: PASS; focused Library/API 13/13, Unit 597/597, Architecture 1/1, Contract 12/12. Local Integration attempted (125 total: 8 passed, 3 skipped, 114 Docker-unavailable failures) and is environment-blocked; remote PostgreSQL/runtime passed.
Runtime: PASS remotely through CI runtime smoke and Docker Compose published-image verification; local Testcontainers remains BLOCKED by unavailable `npipe://./pipe/docker_engine`.
Security: PASS; remote dependency/SBOM/filesystem/CodeQL gates green and owner filter plus positive version validation reviewed.
CI: GREEN for exact SHA `9610b76fa14b572da4a3203c9047ec0a9ae2d8e0`; CI `37655196433`, Docker `37655726362`, Security `37655196520`.
Findings: PrivateLibraryService previously read a book and EfPrivateBookRepository wrote by key without a version predicate; concurrent stale metadata could overwrite newer data.
Fixed: Conditional owner/user/book/version update, explicit conflict result, migration defaulting existing rows to version 1, API contract and regression coverage.
Remaining risks/blockers: Windows Docker named pipe still blocks local Testcontainers; real-account/manual Release Candidate acceptance remains outside this work package.
Commit/PR: Implementation candidate `9610b76fa14b572da4a3203c9047ec0a9ae2d8e0` pushed to `origin/dev`; closeout documentation follows in the current delivery commit.
Documentation/state sync: RepoWiki architecture/work-package index, workflow profile, roadmap progress, handoff, Library architecture and invariants synchronized.
Next step: Re-intake the next bounded work package; keep local Docker and real-account/manual Release Candidate acceptance as separate follow-up gates.
```
