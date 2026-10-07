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
  - [x] happy path: an active Administrator can append a plan assignment with the existing reason/audit behavior.
  - [x] important error path: an empty, missing, inactive, or non-Administrator actor is rejected before target/plan lookup or assignment persistence and maps to HTTP 403 at the API boundary.
  - [x] regression/compatibility: target-user validation, plan lookup, response shape, route, and existing Developer API authentication remain unchanged.
Risks and invariants: The composition adapter must derive actor eligibility from the Identity user record; no caller-provided role or target-user identity may weaken the check. No schema or migration is introduced.
Verification plan:
  - [x] diff/secrets/scope audit: `git diff --check` clean; scoped files only; `.workbuddy-ai/` preserved untracked.
  - [x] build: `dotnet restore InkFlow.sln` and `dotnet build InkFlow.sln -c Release --no-restore` PASS with 0 warnings / 0 errors.
  - [x] tests: focused Commercial/Identity tests `18/18`; full Unit `592/592`; Architecture `1/1`; Contract `12/12`.
  - [x] runtime/integration: local Billing Testcontainers `BLOCKED` by unavailable `npipe://./pipe/docker_engine`; remote CI `37638597477` completed PostgreSQL/runtime validation successfully.
  - [x] security/architecture: actor is resolved from the Identity record and requires active Administrator; stable 403 mapping; no secret/logging changes; Architecture PASS.
  - [x] migration/project gates: Windows equivalent migration check PASS for all 11 contexts; `bash -n scripts/verify-migrations.sh` PASS; no Schema/Migration change and no phase exit change.
  - [x] manual/real-environment acceptance: N/A for this backend authorization slice.
  - [x] CI: `37638597477` CI, `37638597479` Docker, and `37638597492` Security all GREEN for `d0413f2`.
Documentation/state updates: synchronized Developer API contract note, RepoWiki index/work package, Progress, Handoff, and this record with evidence.
Affected Wiki pages and source/test evidence: repowiki/README.md; this work package; EntitlementService; Billing contracts; Developer endpoint/composition adapter; CommercialFoundationTests.
Affected Chinese human pages: docs/architecture/developer-api.md; docs/roadmap/progress.md; docs/handoff/handoff.md.
```

## Delivery Record

```text
Work package: Accepted
Implementation: `IBillingUserStatusReader.IsActiveAdministratorAsync` and the EntitlementService actor guard; API Identity adapter and stable 403 mapping; regression tests.
Acceptance: PASS — active Administrator assignment remains valid; empty/unknown/inactive/non-Administrator actors are rejected before persistence; route, payload, target/plan behavior, reason, and audit remain compatible.
Build: PASS — restore and Release build, 0 warnings / 0 errors.
Tests: PASS — focused `18/18`, Unit `592/592`, Architecture `1/1`, Contract `12/12`; remote PostgreSQL integration covered by CI.
Runtime: PASS — remote CI/runtime smoke and Docker GREEN; local Testcontainers BLOCKED by unavailable Docker named pipe.
Security: PASS — actor eligibility is derived from Identity state; no secrets or caller-provided role claims are trusted.
Project-defined gates/phase exit: PASS — migration model check 11/11 and shell syntax PASS; no phase exit change.
CI: GREEN — CI `37638597477`, Docker `37638597479`, Security `37638597492`, all for `d0413f2`.
Findings: The API policy was Administrator-only, but direct service calls accepted any non-empty actor.
Fixed: Added the service-boundary active Administrator check and mapped `ActorNotAllowed` to `403 entitlement_management_forbidden`.
Remaining risks/blockers: Local Docker remains unavailable; real accounts and other Release Candidate manual gates remain outside this backend slice.
Commit/PR: Implementation `d0413f2`; documentation closeout is committed on `dev` with this record.
Documentation/state sync: Developer API contract, RepoWiki index/work package, Progress, Handoff, and workflow profile synchronized.
RepoWiki sync: PASS — intake scope, source boundary, and non-goals are aligned with the current profile.
Next step: New work package requires intake; remaining candidates are optimistic concurrency and adapter regex/read bounds.
```
