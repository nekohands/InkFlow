# Optimization and refactoring plan — documentation first

Audience: Agent-only.
Agent read instruction: Read before assessing or implementing the 2026-10-22 optimization/refactoring plan; distinguish verified baseline facts from proposed changes and preserve the historical module-refactor record.
Scope: evidence-based assessment, responsibility ownership, ordered work packages, measurement, regression and rollback criteria; documentation only in this delivery.
ID: optimization-refactoring-plan-2026-10-22
Status: RF-00 preserved in `df6c701`; RF-01 Accepted / Completed at `f90e6bd`; RF-02 Accepted at `3a38ad1`; RF-03 Accepted at `0a6e641`; RF-04 is Accepted at `4ea9dd0`; RF-05 and later remain Planned.
Owner: InkFlow maintainers.
Authorization: explicit user request on 2026-10-22, “优化重构项目, 先出文档”; the follow-up “继续” continues this documentation-first scope. Previous package instructions to implement/commit/push do not expand this request.
Subsequent authorization: “继续，按方案开始实施，先做 RF-01 架构保护网。” explicitly starts [RF-01](2026-10-22-rf01-architecture-guards.md), including its candidate-commit/CI gates. The documentation-only authorization and baseline below describe RF-00's original delivery, not a restriction on RF-01.
Parent/root: [Work packages](README.md) / [RepoWiki](../README.md).
Human counterpart: [中文优化与重构方案](../../docs/engineering/optimization-refactoring-plan-2026-10-22.md).

## Baseline and evidence

- Inspected on 2026-10-22: branch `dev`, HEAD `ca15cadbe1df97adf99150df0c9d3d7bc95e32af`; initial worktree clean. Local tracking ref `origin/dev` matches; this is not a live remote-branch assertion.
- Tracked `src/` contains 23 projects (4 hosts, 11 modules, 6 BuildingBlocks, 2 adapters) and 399 C# files excluding Migration directories. File sizes below are navigation evidence, not complexity scores or acceptance targets.
- `.codegraph/` is absent in this checkout. The 2026-10-11 observation remains historical; directed source/text inspection was used. Local Docker availability was not rechecked, and no production/load/browser performance baseline was measured.
- Requeried GitHub with `gh run list --commit <SHA>` and `gh run view <id> --json headSha,status,conclusion,jobs`. All jobs of [CI 39240297876](https://github.com/nekohands/InkFlow/actions/runs/39240297876), [Docker 39240297898](https://github.com/nekohands/InkFlow/actions/runs/39240297898), and [Security 39240298102](https://github.com/nekohands/InkFlow/actions/runs/39240298102) completed successfully at exactly that SHA. Runs were created on 2026-10-19; checked on 2026-10-22. Docker includes five image jobs (including Collector) and published-image verification; Security includes CodeQL, filesystem scan, SBOM and NuGet audit.
- This is baseline CI evidence, not a new run for these uncommitted documents, acceptance of all earlier batches, performance proof, or real-device/production acceptance.

## Reconciliation with previous work

The [2026-10-15 module refactor](2026-10-15-module-refactor.md) stopped recording at Batch 2, but later commits and current source contain further work:

- `RuleAdapter` is a 189-line orchestrator with preflight, credential, HTTP, pre-request, pagination, response-value and request-budget responsibilities already extracted.
- `CrawlerTaskProcessor` composes run coordination, execution, failure and outcome handling; discovery, content enqueue and collection-run responsibilities also have separate implementations.
- `ReadingStateService` composes shelf queries/commands, history, progress, preferences and visibility. Catalog and book-package implementations have already been decomposed.
- The old Collector Docker failure is historical and is not a current-HEAD blocker. Preserve old failed run evidence; do not label old runs successful or assume all intervening batches were individually accepted.

Current pointers are refreshed by this document package. Historical per-batch acceptance still requires reconciliation if resumed; do not mechanically restart already implemented batches.

## Findings

| ID | Verified source fact / limit of inference | Priority and action |
| --- | --- | --- |
| F-01 | `ModuleBoundarySmokeTests` only loads 11 assemblies. No dependency rule is enforced there. | P0: RF-01 adds real architecture guards; a green load test is not dependency proof. |
| F-02 | `ReaderHtml.cs` / `.UserPages.cs` / `.Operations.cs` contain 1,135 / 1,197 / 1,936 lines (4,268 total) of HTML/CSS/JS, browser session and PWA responsibilities inside Content.Application. | P1: RF-04 relocates presentation ownership; browser slowness is not measured. |
| F-03 | `ConsistencyCheck.cs` (881 lines) combines DTOs, report service, rules and EF reader. It loads 13 complete metadata collections, generates all issues, then returns at most 1,000. | P1: RF-02 separates responsibilities and measures; RF-03 conditionally bounds cost. Production impact remains unmeasured. |
| F-04 | `DeveloperEndpoints.cs` (904 lines) combines self-service management, administrator entitlement, external catalog, DTOs, quota results and audit. A quota helper already exists. | P1: RF-05 separates ownership without inventing a new quota algorithm. |
| F-05 | `EfMessagingMessageStore` in `MessagingStore.cs` (826 lines) implements five Outbox/Inbox/transport/dead-letter/retention ports. | P2 conditional: RF-06 requires demonstrated maintenance benefit and PostgreSQL regression evidence. |
| F-06 | `SourceRuleDslValidator` (883 lines) already delegates pagination checks; `HtmlXPathSelectorEvaluator` (870 lines) is already language-specific. | P2 conditional: size alone does not justify another parser abstraction or changed safety limits. |
| F-07 | Current Wiki/profile/Progress/Handoff still pointed at Batch 2 and its old RED gate. | Documentation addressed here, keeping historical results. |

This is a targeted assessment, not exhaustive type-dependency analysis, call-graph auditing, production SQL sampling, or browser execution verification.

## Invariants and non-goals

Retain the modular monolith, hosts, dependency direction, stable BookId/ChapterId, separate Source/Canonical identities, ingested-only reading, append-only content versions, and explainable/reversible matching/selection/failover. Domain must not gain EF/ASP.NET/Redis dependencies; modules must not depend on hosts or another module's persistence.

Keep routes/DTOs/status/error codes, distinct public/Developer/Personal Legado authentication/scope, ownership, quota and cache behavior. Redis stays rebuildable. Keep DSL schema/published rule/parser/normalizer version semantics, SSRF/same-origin and request/byte/time/regex/list limits, credential scope, cancellation and fail-closed behavior. Keep Outbox atomicity, Inbox identity/idempotency, lease ownership, retry/dead-letter/retention and dedicated migrations.

Presentation keeps contextual escaping, the bounded sessionStorage exception, same-origin authorization headers, private `no-store` responses and public-shell-only Service Worker caching. Preserve audit differences: `AuditedResult` awaits audit before response; `DeveloperEndpointAudit` uses `CancellationToken.None` and catches non-cancellation audit exceptions after completed commands. Do not unify these behaviors accidentally.

No code/test/dependency/schema/CI/deployment changes, commit/push or release in RF-00. No microservices, speculative plugin/mediator/generic repository framework, frontend-stack migration, UI redesign or broad package upgrade in the proposed refactor. Preserve `.workbuddy-ai/`.

The proposed ownership changes enforce existing direction, so no ADR is added now. Dependency-direction changes, public report/query consistency semantics, sessions, stack, contracts or schema changes require a Proposed ADR and contract-impact review before implementation.

## Target ownership (proposed, not implemented)

- API `Presentation/`: Reader, Account/PWA and Operations rendering/browser resources; receive appropriate display data. Content must not reference presentation.
- Existing Content/Library/Sources: canonical AST/content, visibility, selection, publication and packages; retain working internal seams.
- Operations.Application: consistency metadata contracts, pure rules and report orchestration. Cross-schema EF snapshot/query adapter remains in the API composition layer; never move foreign DbContexts into Operations.
- API Developer mappings: self-service, administrator entitlement, external read-only catalog; reuse explicit protocol result/quota/audit helpers.
- Existing Persistence BuildingBlock: messaging adapter decisions deferred to RF-06. Do not collapse distinct SQL transaction/lease semantics into a generic abstraction.

Add classes/interfaces only to hide real complexity, remove shared behavior, or represent an existing variation. File moves/line reduction/new forwarding layers alone are not acceptance benefits.

## Work packages and dependencies

| Package | Scope and entry condition | Acceptance / risk |
| --- | --- | --- |
| RF-00 | Documentation, baseline reconciliation and navigation preserved in `df6c701`. | Proposal delivered; not code acceptance. |
| RF-01 | Accepted / Completed at `f90e6bd`; see the dedicated package and gate repair evidence. | Type/namespace-aware rules for Domain infrastructure, module-to-host, foreign persistence and allowed module graph; positive and negative fixtures prove detection. |
| RF-02 | Consistency responsibility separation and benchmark fixtures after RF-01 gates. | Keep full counts, fields/codes, cancellation, empty/healthy/orphan/duplicate/mismatch and over-1,000-issue behavior; pure rules in Operations, EF adapter in host. Medium risk. |
| RF-03 | Measured consistency optimization after RF-02; fix quantitative target/tolerance before editing. | Accurate complete count, bounded retained issue examples, preserved relation and defined ordering semantics; SQL/streaming only as evidence warrants. High risk, may be explicitly deferred. |
| RF-04 | Reader, Account/PWA, then Operations presentation slices after RF-01 and browser baselines. | Keep SSR/URLs/DOM/headers/session/XSS/cache semantics, package resources in release output, update callers/tests and remove migrated implementations. High risk. |
| RF-05 | Developer mapping/result ownership after RF-01 and behavior characterization. | Preserve authentication/validation/quota/read ordering per route, operation units, TraceId, UTC period, 401/403/429/503, Retry-After, no-store, one-time secrets and audit/cancellation differences. High risk. |
| RF-06 | One evidenced messaging or parser issue after core packages and real regressions are available. | Transaction/lease/retention or fixture safety equivalence. Conditional; document deferral if no benefit. High risk. |
| RF-07 | Cross-surface regression and evidence/documentation closeout after selected packages. | Exact candidate-SHA gates and all applicable acceptance. Outstanding real-device/production checks stay open. |

Recommended sequence: RF-01 → RF-02 → RF-03 measurement decision → RF-04 → RF-05 → RF-07. RF-06 is not automatically part of the first round. RF-04/05 require RF-01, not an RF-03 speedup; do not proceed past a failing gate of the batch being changed.

### Implementation constraints

- Each module currently puts Domain/Application/Infrastructure in one assembly. Assembly-reference-only tests are insufficient; `using` text checks are also insufficient. Cover actual type dependencies including method bodies. Inventory legal edges; existing violations need explicit treatment, not blanket allowlists. Any necessary exception must be named with rationale and an exit condition. Choose a suitable dependency-inspection technique before adding a test dependency; do not split projects just for guards.
- Consistency RF-02 keeps `CheckedAt`, `Status`, `TotalIssueCount`, `ReturnedIssueCount`, `Truncated`, issue codes/evidence. Group rules by actual responsibility without a plugin rule engine. RF-03 may first retain ≤1,000 issue objects while counting all; this alone does not bound full input snapshots. Do not cap every table or stop after 1,000 issues: cross-page relations and full counts must remain correct. Freeze fixture behavior and specified ordering; review any new sampling/order contract. Current sequential queries are not an atomic database snapshot; stronger transaction consistency is a separately reviewed change.
- RF-04 slices are Reader pages; account/shelf/history/preferences/session/PWA; Operations. Keep content AST/pure content rendering in its business module. Embedded resources can preserve inline output while improving editability; new static URLs/cache/compression/versioning need a separate measured decision. No runtime reliance on source-tree files. Before implementation follow frontend-design: check at least three current peer products and applicable benchmark notes without broadening into redesign. Verify Mobile/Tablet/Desktop/Wide Desktop, keyboard/focus, long/empty/error states, 200% zoom, chapter navigation and refresh. String tests do not replace browser or manual long-reading/device acceptance.
- RF-05 reuses `DeveloperEndpointResults.ReserveAsync`; no generic middleware changing sequence. Characterize issuance/rotation/revocation, ownership, disabled users, insufficient scope/quota, quota-store failure, cancellation and audit failure. Preserve distinct audit behavior.
- RF-06 requires concrete duplication/change-isolation benefit. Messaging keeps shared transaction/context lifetime, locks and identity checks. Parser changes keep errors/budgets, DTD/external entity rejection, regex, same-origin and credentials. PostgreSQL concurrency/rollback and deterministic parser fixtures are required; ordinary PR CI never depends on live third-party sites.

## Measurement and verification plan

At the RF-00 planning checkpoint measurements were NOT RUN. [RF-02](2026-10-22-rf02-consistency.md) now records completed consistency rule/PostgreSQL measurements; Reader and Developer measurements remain NOT RUN. Use identical Release environment, database version, seed, cache state, data and concurrency for before/after runs. Warm up 3 times, measure ≥30 samples per shape, repeat 3 rounds, retain raw data/parameters. Establish baseline noise and lock improvement targets/regression tolerances before performance code. Require a repeatable improvement in at least one target without exceeding other agreed tolerances; never invent speedup numbers.

- Consistency: 1k/10k/100k canonical chapters, 2 source chapters and 2 content versions each; empty, seeded orphan/duplicate, >1,000 issue cases. Record SQL count/rows/plans, elapsed p50/p95, allocations, peak memory and complete report equivalence.
- Reader: short/long content and 100/1,000/10,000 chapter TOCs; same device/viewports. Record payload, render/interaction/navigation latency and DOM size. Structural migration first requires behavioral equivalence.
- Developer: fixed ingested catalog, valid/invalid credential and quota fixtures, fixed concurrency. Record status/headers/body, ledger units/count, SQL count and latency; no upstream calls.
- Existing Core SLO thresholds remain mandatory; smoke windows are not production-load proof. Do not load unnecessary body columns while optimizing metadata.

Before builds, review actual diff for scope, secrets, dependencies, contracts, migrations and documentation. Code packages run `dotnet restore InkFlow.sln`, `dotnet build InkFlow.sln -c Release`, `dotnet test InkFlow.sln -c Release`; maintain zero warnings/errors. Add affected Architecture, Unit, Contract and real PostgreSQL/Redis Integration checks, migration drift for all 11 contexts where applicable, and runtime/security regressions.

RF-02/03 use Consistency Unit/Integration + Admin/Operations smoke; RF-04 uses ReaderHtml/Reading plus frontend/edge/account/content/navigation/Admin runtime, browser/PWA/security and relevant Legado regression; RF-05 uses Developer Unit/Contract/Billing persistence and Developer/Admin runtime; RF-06 uses Messaging transaction/idempotency/lease/retention or Sources/DSL/Selector/credential/SSRF fixtures and affected Worker/Source runtime.

Use source-build Compose: `docker compose -f docker-compose.build.yml config --quiet`, then `up -d --build --wait api worker scheduler otel-collector`, followed by affected `scripts/*-runtime-smoke.sh`. Recheck environment availability; record real blockers and obtain CI evidence when local Docker is unavailable. Candidate code commits require all mandatory CI/Docker/Security jobs complete at their exact SHA. Preserve independent live-source, real-device/account and production OTLP/SLO/backup acceptance boundaries.

## Rollback and stop conditions

One responsibility per reviewable batch, with atomic code/resource/DI rollback. No schema migration planned. If data or irreversible side effects become necessary, replan migration/compensation first. Failures/Pending stop dependent batches; inspect logs and fix root causes. Do not accept based on reduced LOC, increased test counts or implementation alone. Code changed after validation must be revalidated. Update Wiki and human docs in each delivery.

## RF-00 documentation delivery evidence (historical checkpoint)

The following records the original documentation-only turn. Subsequent RF-01
implementation and gates are recorded in [its package](2026-10-22-rf01-architecture-guards.md).

- New plan plus current pointers/module navigation refreshed; historical failed runs and pending real-world acceptance preserved.
- Local Restore/Build/Tests/Runtime/benchmarks: NOT RUN (documentation-only request).
- Current document CI: NOT TRIGGERED; no commit/push. Baseline CI/Docker/Security GREEN is separately scoped above.
- Documentation verification: PASS. Workflow plugin 0.102.1 `generate-repowiki-index.js --root repowiki --write` regenerated the catalog; `--check` passed for all 34 Wiki pages. Modified Markdown local link targets/format and metadata JSON were checked. `git diff --check` passed; the write set contains only docs/RepoWiki Markdown and generated Wiki metadata. Source spot-checks confirmed the Reader line counts, 13 consistency materializations, one Architecture test method and unchanged HEAD. Manual diff review plus known secret-pattern checks found no introduced secret material; this is not a new Security workflow run.
- Code findings F-01–F-06 remain unimplemented/conditional. Next implementation entry: RF-01 after this proposal is taken up; no project-wide Accepted/Completed claim.

## Sources

- [Chinese plan](../../docs/engineering/optimization-refactoring-plan-2026-10-22.md), [architecture invariants](../architecture.md), [verification gates](../operations/verification.md), [previous package](2026-10-15-module-refactor.md).
- [Architecture smoke](../../tests/InkFlow.ArchitectureTests/ModuleBoundarySmokeTests.cs).
- [RuleAdapter](../../src/Modules/InkFlow.Modules.Sources/Application/RuleAdapter.cs), [CrawlerTaskProcessor](../../src/Modules/InkFlow.Modules.Crawling/Application/CrawlerTaskProcessor.cs), [ReadingStateService](../../src/Modules/InkFlow.Modules.Reading/Application/ReadingStateService.cs), [CatalogQueryService](../../src/Modules/InkFlow.Modules.Content/Application/CatalogQueryService.cs), [BookPackageService](../../src/Modules/InkFlow.Modules.Content/Application/BookPackageService.cs).
- [ReaderHtml](../../src/Apps/InkFlow.Api/Presentation/ReaderHtml.cs), [UserPages](../../src/Apps/InkFlow.Api/Presentation/ReaderHtml.UserPages.cs), [Operations page](../../src/Apps/InkFlow.Api/Presentation/ReaderHtml.Operations.cs), [ReaderHtml tests](../../tests/InkFlow.UnitTests/ReaderHtmlTests.cs), [frontend design](../../docs/engineering/frontend-design.md).
- [Consistency implementation](../../src/Apps/InkFlow.Api/EfConsistencySnapshotReader.cs), [Unit](../../tests/InkFlow.UnitTests/ConsistencyCheckTests.cs), [Integration](../../tests/InkFlow.IntegrationTests/ConsistencyCheckIntegrationTests.cs).
- [Developer endpoints](../../src/Apps/InkFlow.Api/DeveloperEndpoints.cs), [AuditedResult](../../src/Apps/InkFlow.Api/AuditedResult.cs), [Developer Contract](../../tests/InkFlow.ContractTests/DeveloperApiContractReleaseGateTests.cs).
- [MessagingStore](../../src/BuildingBlocks/InkFlow.BuildingBlocks.Persistence/MessagingStore.cs), [Messaging integration](../../tests/InkFlow.IntegrationTests/MessagingPersistenceTests.cs), [DSL validator](../../src/Modules/InkFlow.Modules.Sources/Domain/SourceRuleDslValidator.cs), [XPath evaluator](../../src/Modules/InkFlow.Modules.Sources/Infrastructure/HtmlXPathSelectorEvaluator.cs).
- [CI configurations](../../.github/workflows/), [security model](../../docs/architecture/security-model.md), [Legado contract](../../docs/architecture/legado-contract.md), [current progress](../../docs/roadmap/progress.md), [handoff](../../docs/handoff/handoff.md).

RF-02 is now Accepted at `3a38ad1` following explicit user authorization and all required gates; see [RF-02](2026-10-22-rf02-consistency.md). The original findings above describe the pre-refactor baseline.

RF-03 is Accepted at `0a6e641` after explicit authorization: fresh baselines and frozen targets precede production optimization. See [RF-03](2026-10-22-rf03-consistency-performance.md). RF-04 is Accepted at `4ea9dd0`; RF-05 remains Planned.

RF-04 implementation and acceptance are tracked in [presentation ownership](2026-10-22-rf04-presentation.md). F-02 is the original intake observation; links now navigate to the migrated API renderer.
