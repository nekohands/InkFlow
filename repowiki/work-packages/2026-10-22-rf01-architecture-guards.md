# RF-01 — executable architecture guards

Audience: Agent-only.
Agent read instruction: Read before implementing, verifying or continuing RF-01; use the policy and compiled fixtures as executable dependency evidence.
Scope: test-only architecture guards, explicit module dependency inventory, negative/positive fixtures and synchronized delivery documentation.
ID: rf01-architecture-guards-2026-10-22
Status: Implemented; local verification recorded below, candidate CI pending.
Authorization: explicit follow-up, “继续，按方案开始实施，先做 RF-01 架构保护网。” This supersedes the plan's documentation-only restriction for RF-01, including its candidate-commit/CI delivery gates; later packages remain Planned.
Parent/root: [Work packages](README.md) / [RepoWiki](../README.md).
Human counterpart: [Architecture guards](../../docs/engineering/architecture-guards.md); parent [optimization/refactoring plan](../../docs/engineering/optimization-refactoring-plan-2026-10-22.md).

## Objective and boundaries

Enforce Domain infrastructure restrictions, module-to-host prohibition, foreign
module infrastructure isolation and the explicit module dependency graph using
compiled type/method-body references. Preserve the assembly-load smoke. Verify
that forbidden compiled fixtures fail and legitimate dependencies pass; new
module projects must not silently escape the inventory. No production behavior,
public contract, schema, migration or runtime configuration change is planned.

Baseline code: `ca15cadbe1df97adf99150df0c9d3d7bc95e32af`; the reviewed RF-00
documents were preserved separately in `df6c701` before RF-01 implementation.
SDK observed: 10.0.402. `.codegraph/` absent. `docker` is not available on PATH;
local Docker-dependent gates require CI evidence, not an assumed local pass.

## Design and acceptance

- Use NetArchTest.Rules 1.3.2 only in ArchitectureTests (`PrivateAssets=all`),
  centrally pinned. Its compiled dependency scanner avoids source-text `using`
  checks and avoids a project-specific IL parser. Validate actual coverage with
  fixtures, including method bodies and compiler-generated execution paths.
- Inventory each module and permitted module edges explicitly. A new project,
  unexpected reference or unknown module must fail closed. No blanket exceptions.
- Domain guards must distinguish namespaces/types from assemblies: every module
  combines Domain, Application and Infrastructure in one assembly today.
- AC-1: Each of the four guard categories detects an illegal fixture and permits
  legal controls; scanner/selection gaps found by fixtures are fixed before delivery.
- AC-2: All 11 production modules are included, with Search's currently empty
  assembly documented rather than silently excluding it. Preserve load smoke.
- AC-3: Local Restore/Release Build and applicable tests run; full solution tests
  report actual Docker blockers. Candidate-SHA CI/Docker/Security must all pass.
- AC-4: Record legitimate edges, findings, limitations, regression evidence and
  human/Wiki synchronization. No later refactor starts during a failing gate.

## Verification plan and risks

Start with failing fixture tests, then add guards and run production checks.
Add regressions for missed dependency forms, namespace prefix confusion and
inventory/reference drift. Inspect diff/secrets before builds. Run solution
restore, Release build, full tests and migration-model checks; use actual CI
container jobs for PostgreSQL/Redis/runtime/security evidence. Test-library
compatibility and false negatives are primary risks. Reflection strings/runtime
dynamic loading are outside static dependency proof and remain a review concern.

Rollback is an atomic revert of test/dependency/doc changes; no persisted data
changes are involved. Any discovered production violation is investigated and
handled explicitly, never hidden by a wildcard exemption.

## Evidence

- Initial RED: four forbidden compiled fixtures failed against an empty policy;
  three legal controls passed. Implementing the four rules made all seven pass.
- Expanded RED: six unused forbidden project references failed against an empty
  project policy; an internal nested type exposed a real namespace-selector gap.
  The upstream regex selector resolves only public/private nested namespaces.
  Selecting by full type-name namespace prefix plus `.` fixes internal nesting
  without matching lookalike namespaces. The regression remains enabled.
- Architecture GREEN: 51/51, including all 11 production modules, project graph,
  inventory, compiled positive/negative fixtures and preserved load smoke. No
  production violation was found within these four rules; no business exemption.
- Inventory mutation proof: an unreviewed temporary module project made the
  production inventory test fail; removing the probe restored the same test to
  GREEN. Probe files were removed; evidence in ignored `artifacts/rf01-inventory-red.log`.
- Restore and Release Build PASS (SDK 10.0.402, 0 warnings/errors). An initial
  build was started before restore finished and failed with missing MSTest
  references; the build after restore completion passed without code changes.
- Migration model PASS: `scripts/verify-migrations.sh`, 11/11 contexts, no drift.
- Full solution tests executed; local container failures are `BLOCKED` by
  `DockerUnavailableException` at `npipe://./pipe/docker_engine`, not a pass.
  Local TRX and log: ignored `artifacts/rf01-tests/` and `artifacts/rf01-tests.log`.
- Counts: Unit 648/648, Architecture 51/51, Contract 12/12. Integration 8 passed,
  128 failed (all DockerUnavailable at class initialization), 3 skipped (two
  opt-in live-source tests and Redis without `INKFLOW_REDIS_CONNECTION`). CI must
  provide PostgreSQL and its separate configured Redis check; opt-in live tests
  remain outside this package's acceptance.
- Remote CI/Docker/Security not yet triggered for RF-01. No Accepted/Completed claim.
- Documentation: Wiki catalog/metadata regenerated and checked (35 pages);
  local links checked in 14 changed Markdown files (230 targets, no missing
  target). Diff/known-secret-pattern review passed; production source, workflows,
  contracts and migrations are unchanged. Remote security scan remains pending.

## Legal graph and coverage

| Module | Permitted other modules |
| --- | --- |
| Identity | none |
| Library | Sources |
| Sources | none |
| Crawling | Library, Sources |
| Content | Library, Sources |
| Reading | Library, Content |
| Search | Library |
| Legado | Library, Content |
| Developers | none |
| Billing | none |
| Operations | none |

The graph allows module edges, never access to foreign Infrastructure. Domain
rejects EF Core, ASP.NET Core, StackExchange.Redis, Npgsql, BuildingBlocks.Persistence
and all module Infrastructure. Host targets are Api, Worker, Scheduler and
Migrations. Project-reference checks reject unapproved/unknown modules and hosts
even when no compiled type consumes that reference. Repository project inventory
must equal the policy and ArchitectureTests module references.

All handwritten production types must live in their module namespace. Ownership
checks inspect top-level types (nested types inherit ownership), excluding only
global CompilerGenerated helpers and regex-generator types in
`System.Text.RegularExpressions.Generated` carrying the matching GeneratedCode
tool marker. These generated ownership exceptions do not permit business dependency
exceptions. Async generated code is covered by the dependency scanner/fixture.

Fixture coverage: method-body references, async state machines, generic property
and constraint, attribute, own Domain persistence, foreign persistence, internal
nested host dependency and legal namespace lookalikes. Search remains included
despite having no business types. Tests require the repository source tree for
project inventory. Reflection strings/dynamic loading are outside static proof.
This package does not add a general Application-to-own-Infrastructure prohibition;
Billing's existing own persistence usage is outside the four agreed guards.

## Sources

- [Approved plan](2026-10-22-optimization-refactoring-plan.md), [architecture](../architecture.md), [verification](../operations/verification.md).
- [Architecture tests](../../tests/InkFlow.ArchitectureTests/), [central packages](../../Directory.Packages.props), [module projects](../../src/Modules/).
- [NetArchTest package](https://www.nuget.org/packages/NetArchTest.Rules/1.3.2), [upstream source tag](https://github.com/BenMorris/NetArchTest/tree/v1.3.2) (inspected with `gh`).
