# Crawler handler lease renewal

Status: Accepted
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

- [x] An active, unexpired Leased/Running task can renew only through its current lease owner and receives a new expiry.
- [x] Expired, terminal, or owner-mismatched tasks are not renewed; no schema or migration is introduced.
- [x] A handler running past half of the configured lease renews before expiry and can complete normally.
- [x] If renewal is lost or fails, the executor is cancelled and the processor does not persist a terminal result under a lease it no longer owns.
- [x] Existing task success, retry, dead-letter, cancellation, and parent-run behavior remain compatible.
- [x] Applicable unit, architecture, PostgreSQL integration, build, migration, security, and CI gates have observed evidence; unavailable local Docker remains explicitly blocked rather than inferred green.

Risks and invariants: The renewal UPDATE must be conditional on task ID, current owner, active status, and a still-valid lease. The heartbeat must not share an EF DbContext with the long-running executor. Lease loss must not be converted into a normal success/failure write. No secrets or external source response data are touched.

Verification plan:

- [x] Diff/secrets/scope audit with `git diff --check` and reviewed staging; preserve `.workbuddy-ai/` untracked.
- [x] Focused Unit tests for domain renewal, heartbeat success, and lease-loss cancellation; then full Unit and Architecture suites.
- [x] Focused PostgreSQL Integration test attempted; local Docker named pipe blocked, with remote CI PostgreSQL/runtime evidence.
- [x] `dotnet restore InkFlow.sln`, Release solution build, PowerShell-equivalent 11-context migration model check, and remote worker/runtime evidence.
- [x] Security/architecture review for owner fencing, cancellation, DbContext scope separation, and no contract/migration drift.
- [x] Candidate commit pushed; exact-SHA CI, Docker, and Security workflows finished successfully.

Delivery boundary: no schema/migration, API, Legado, UI, Source Rule, or real-account changes; `.workbuddy-ai/` remains user-owned and untracked.

## Delivery Record

```text
Work package: Accepted
Implementation: CrawlerTask.RenewLease; ICrawlerTaskRepository renewal contract with test-double fallback; PostgreSQL conditional UPDATE; CrawlerTaskProcessor independent-scope heartbeat and lease-loss cancellation; domain, processor, and PostgreSQL regression coverage.
Acceptance: PASS — active current owners renew before expiry; owner-mismatched, durable-expired, and terminal leases do not renew; long execution renews and completes; lease loss cancels execution without stale terminal persistence; existing task flows remain green.
Build: PASS — dotnet restore and Release solution build, 0 warnings / 0 errors.
Tests: PASS/PARTIAL — focused red/green CrawlerTask tests 21/21; full Unit 600/600; Architecture 1/1; Contract 12/12. Focused PostgreSQL Integration was attempted but locally BLOCKED by unavailable npipe://./pipe/docker_engine; remote CI covered PostgreSQL.
Runtime: PASS — remote CI runtime smoke, Redis, backup/restore, diagnostics, and Docker/Compose evidence passed; local Docker runtime NOT RUN because the same Docker named pipe was unavailable.
Security: PASS — parameterized owner/status/expiry fencing; no secret, credential, public contract, or permission changes; stale lease loss cannot write a terminal result.
Project-defined gates/phase exit: PASS — PowerShell-equivalent migration model check 11/11; no Schema/Migration or phase-exit change.
CI: GREEN — exact SHA 11a493a2da72dba38072cad1cbc142711654fc0d passed CI 37707275909, Docker 37707275881, and Security 37707275873.
Findings: The existing two-minute crawler lease had no renewal during a single long handler; local Testcontainers and WSL dotnet were unavailable, so the shell migration wrapper itself was not runnable locally.
Fixed: Added domain renewal, owner/status/expiry-guarded PostgreSQL renewal, independent DbContext scope heartbeat, and cancellation without stale terminal writes.
Remaining risks/blockers: Windows local Docker remains unavailable; real sources, accounts, and Release Candidate manual acceptance remain outside this backend reliability package.
Commit/PR: 11a493a2da72dba38072cad1cbc142711654fc0d pushed to origin/dev; no PR created.
Documentation/state sync: RepoWiki work package/index, docs/delivery/PROJECT_WORKFLOW.md, docs/roadmap/progress.md, and docs/handoff/handoff.md synchronized.
RepoWiki sync: PASS — implementation, boundaries, evidence, and blockers agree with source/tests and the Chinese state records.
Next step: Re-intake the next evidence-backed work package; keep the documented real-account/source/Docker/OTLP acceptance items open.
```
