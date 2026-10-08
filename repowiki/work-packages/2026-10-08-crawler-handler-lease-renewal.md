# Crawler handler lease renewal

Status: In Progress
Name: Crawler handler lease renewal
Objective / user outcome: A single crawler handler that runs longer than its initial task lease keeps exclusive ownership until it finishes or is cancelled, so another worker does not reclaim the task mid-execution.
In scope: CrawlerTask lease renewal domain rule; PostgreSQL owner/status/expiry-guarded renewal; CrawlerTaskProcessor heartbeat and lease-loss cancellation; focused unit and PostgreSQL regression tests.
Non-goals: Inbox/Outbox batch lease renewal, public API/Legado changes, schema/migration changes, retry-policy redesign, parallel worker scheduling, or external-source/manual acceptance.
Assumptions: Worker crawler tasks use the existing two-minute lease; a handler that loses renewal ownership must stop and leave the durable task recoverable after the lease expires. Production renewal uses an independent DI scope so it never concurrently uses the executor scope's EF DbContext.
Affected modules/files: Crawling Domain/Application/Infrastructure; Worker DI composition; Crawling Unit/Integration tests; RepoWiki and Chinese delivery state.
Affected contracts/data/permissions/architecture: Internal ICrawlerTaskRepository contract only; PostgreSQL remains authoritative; no stable IDs, Source/Canonical separation, public contract, permission, or migration changes.
Affected invariants/decision records: Lease owner exclusivity and bounded retry attempts remain unchanged. No ADR is needed because this closes the already accepted lease-recovery direction without changing the architecture.
Affected project-defined gates or phase/release exit criteria: Runtime/worker reliability, Release Build, Unit, Architecture, PostgreSQL Integration, migration-model check, security/diff audit, and CI/Docker/Security workflows.
Expected evidence sources: Red/green focused tests; local Release Build and applicable test suites; local PostgreSQL attempt or explicit Docker blocker; remote CI PostgreSQL/runtime; diff/secrets/architecture checks; synchronized RepoWiki, Progress, Handoff, and workflow profile.

Acceptance criteria:

- [ ] An active, unexpired Leased/Running task can renew only through its current lease owner and receives a new expiry.
- [ ] Expired, terminal, or owner-mismatched tasks are not renewed; no schema or migration is introduced.
- [ ] A handler running past half of the configured lease renews before expiry and can complete normally.
- [ ] If renewal is lost or fails, the executor is cancelled and the processor does not persist a terminal result under a lease it no longer owns.
- [ ] Existing task success, retry, dead-letter, cancellation, and parent-run behavior remain compatible.
- [ ] Applicable unit, architecture, PostgreSQL integration, build, migration, security, and CI gates have observed evidence; unavailable local Docker remains explicitly blocked rather than inferred green.

Risks and invariants: The renewal UPDATE must be conditional on task ID, current owner, active status, and a still-valid lease. The heartbeat must not share an EF DbContext with the long-running executor. Lease loss must not be converted into a normal success/failure write. No secrets or external source response data are touched.

Verification plan:

- [ ] Diff/secrets/scope audit with `git diff --check` and reviewed staging; preserve `.workbuddy-ai/` untracked.
- [ ] Focused Unit tests for domain renewal, heartbeat success, and lease-loss cancellation; then full Unit and Architecture suites.
- [ ] Focused PostgreSQL Integration tests for owner/status/expiry guards; then the applicable Integration suite.
- [ ] `dotnet restore InkFlow.sln`, Release solution build, migration model check, and applicable runtime/worker evidence.
- [ ] Security/architecture review for owner fencing, cancellation, DbContext scope separation, and no contract/migration drift.
- [ ] Push candidate commit and confirm exact-SHA CI, Docker, and Security workflows finish successfully before acceptance.

Delivery boundary: no schema/migration, API, Legado, UI, Source Rule, or real-account changes; `.workbuddy-ai/` remains user-owned and untracked.
