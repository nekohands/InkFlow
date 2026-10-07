# Entitlement actor validation

RepoWiki index: [README.md](../README.md)
Workflow profile: [PROJECT_WORKFLOW.md](../../docs/delivery/PROJECT_WORKFLOW.md)
Related contract: [Developer API v1](../../docs/architecture/developer-api.md)

## Work Package

```text
Name: Entitlement actor validation
Objective / user outcome: Prevent entitlement assignment from accepting an actor that is not an active Administrator when the Billing application service is called directly.
In scope: Billing actor-status port; API composition-root implementation; EntitlementService assignment guard; stable 403 result mapping; focused unit regression tests and contract documentation.
Non-goals: Role-policy redesign, token/session redesign, optimistic concurrency, adapter regex/read bounds, entitlement plan/quota changes, schema/migration changes, or audit format changes.
Assumptions: The existing Administrator-only API policy remains the first authorization gate; the application service adds a defense-in-depth actor check without changing the endpoint route or request shape.
Affected modules/files: Billing application; API Developer endpoint/composition adapter; Unit/Contract tests; Developer API and delivery-state documentation.
Affected contracts/data/permissions/architecture: Internal IBillingUserStatusReader/IEntitlementService behavior and the existing admin endpoint's forbidden error path; no database schema, stable ID, Legado, or module dependency change.
Affected invariants/decision records: Commercial assignment remains Administrator-only, requires a reason and command audit; PostgreSQL remains authoritative for assignments; no ADR is needed because the accepted policy direction is being enforced at the application boundary.
Affected project-defined gates or phase/release exit criteria: Security/authorization review item for EntitlementService actor validation; Release Build, Unit, Architecture, Contract, integration/runtime evidence as applicable.
Expected evidence sources: focused and full local tests/build; remote PostgreSQL/runtime CI; diff/secrets and architecture checks; synchronized RepoWiki and Chinese contract/state pages.
Grillme decisions resolved: default standard intake; target = entitlement assignment service boundary; outcome = reject inactive/non-administrator actors with a stable forbidden result; no public route or payload redesign.
Acceptance criteria:
  - [ ] happy path: an active Administrator can append a plan assignment with the existing reason/audit behavior.
  - [ ] important error path: an empty, missing, inactive, or non-Administrator actor is rejected before target/plan lookup or assignment persistence and maps to HTTP 403 at the API boundary.
  - [ ] regression/compatibility: target-user validation, plan lookup, response shape, route, and existing Developer API authentication remain unchanged.
Risks and invariants: The composition adapter must derive actor eligibility from the Identity user record; no caller-provided role or target-user identity may weaken the check. No schema or migration is introduced.
Verification plan:
  - [ ] diff/secrets/scope audit
  - [ ] build: `dotnet restore InkFlow.sln`; `dotnet build InkFlow.sln -c Release --no-restore`
  - [ ] tests: focused Commercial Foundation tests; full Unit, Architecture, Contract
  - [ ] runtime/integration: affected Billing persistence tests where Docker is available; otherwise record the local Testcontainers blocker and use remote CI evidence
  - [ ] security/architecture: service actor boundary, 403 mapping, no secret/logging changes, Architecture tests
  - [ ] project-defined gates/phase exit: record as applicable; no phase exit change
  - [ ] manual/real-environment acceptance: N/A for this backend authorization slice
  - [ ] CI: target commit's required CI/Docker/Security jobs
Documentation/state updates: update Developer API contract note, RepoWiki index, Progress/Handoff at closeout, and this record with evidence.
Affected Wiki pages and source/test evidence: repowiki/README.md; this work package; EntitlementService; Billing contracts; Developer endpoint/composition adapter; CommercialFoundationTests.
Affected Chinese human pages: docs/architecture/developer-api.md; docs/roadmap/progress.md; docs/handoff/handoff.md.
```

## Delivery Record

```text
Work package: In Progress
Implementation: NOT COMPLETE
Acceptance: NOT RUN
Build: NOT RUN
Tests: NOT RUN
Runtime: NOT RUN
Security: NOT RUN
Project-defined gates/phase exit: NOT RUN
CI: NOT TRIGGERED
Findings: EntitlementService currently rejects only an empty actorId; the API policy is Administrator-only but the application service has no equivalent actor eligibility check.
Fixed: None yet.
Remaining risks/blockers: Local Windows Docker Engine may block Testcontainers integration; remote CI is required for the final PostgreSQL/runtime gate.
Commit/PR: None yet.
Documentation/state sync: Intake record created; closeout pages remain unchanged until evidence exists.
RepoWiki sync: PASS — intake scope, source boundary, and non-goals are aligned with the current profile.
Next step: Add a failing actor-validation regression, then implement the smallest compatible guard.
```
