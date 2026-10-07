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
  - [ ] happy path: create/get/list/import expose Version=1; a PUT with the current version succeeds and returns the next version.
  - [ ] important error path: missing/non-positive version is invalid; a stale version returns 409 and does not change the stored title/author.
  - [ ] concurrency regression: two reads at the same version allow exactly one conditional save; the stale save reports conflict and cannot overwrite the winner.
  - [ ] compatibility/security: owner filtering, private path isolation, response fields, import/export behavior, and module dependency direction remain valid; no secret/logging changes.
Risks and invariants: The write must use one database conditional update guarded by UserId, book ID, and Version; a pre-read followed by an unconditional SaveChanges is not sufficient. Existing rows must receive Version=1 through the migration.
Verification plan:
  - [ ] diff/scope/secret audit
  - [ ] build: `dotnet restore InkFlow.sln`; `dotnet build InkFlow.sln -c Release --no-restore`
  - [ ] tests: focused Library/API tests; full Unit, Architecture, Contract
  - [ ] persistence/runtime: migration model check and PostgreSQL integration; remote runtime smoke
  - [ ] security/architecture: owner-scoped conditional write and no cross-module dependency change
  - [ ] CI: target SHA CI/Docker/Security all GREEN
Documentation/state updates: synchronize RepoWiki index/work package, workflow profile, Progress, Handoff, and affected Library contract notes at closeout.
```

## Delivery Record

```text
Work package: In Progress
Implementation: Intake only; code not started.
Acceptance: NOT RUN
Build: NOT RUN
Tests: NOT RUN
Runtime: NOT RUN
Security: NOT RUN
CI: NOT TRIGGERED
Findings: PrivateLibraryService reads a book and EfPrivateBookRepository writes by key without a version predicate; concurrent stale metadata can overwrite newer data.
Fixed: None yet.
Remaining risks/blockers: Windows Docker named pipe may block local Testcontainers; remote CI is required for PostgreSQL/runtime evidence.
Commit/PR: Intake commit pending.
Documentation/state sync: Intake record and current-state indexes will be committed before implementation.
Next step: Add the red regression, then implement the smallest conditional versioned write.
```
