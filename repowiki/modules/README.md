# Modules

Audience: Agent-only.
Agent read instruction: Read this page when locating module, host, adapter, or ownership boundaries; then read the relevant architecture topic and source/test paths.
Scope: one-line responsibility per module/host/adapter, with source paths.
Detail lives in the linked architecture pages; tests live under `tests/`
(Unit / Architecture / Integration / Contract).

Parent/root index: [../README.md](../README.md)
Human/Wiki counterpart: [docs/architecture/architecture.md](../../docs/architecture/architecture.md),
[docs/architecture/domain-model.md](../../docs/architecture/domain-model.md)

## Hosts (`src/Apps/`)

| Host | Responsibility |
| --- | --- |
| `InkFlow.Api` | Public API, Legado API, Reader HTML/PWA shell, Operations Center; composition root for most modules |
| `InkFlow.Worker` | Crawler task polling/lease, book packages, Outbox relay, Inbox consumption, retention jobs (messaging/audit/identity) |
| `InkFlow.Scheduler` | Update scans that enqueue crawler tasks; periodic source capability health probes |
| `InkFlow.Migrations` | Applies all module DbContext migrations; fail-closed on model drift |

### Host composition and navigation

- API: [Program.cs](../../src/Apps/InkFlow.Api/Program.cs) owns startup/middleware order; [ApiServiceRegistration](../../src/Apps/InkFlow.Api/ApiServiceRegistration.cs) owns the existing DI, authentication/authorization, rate-limit and observation setup; [ApiEndpoints](../../src/Apps/InkFlow.Api/ApiEndpoints.cs) composes feature maps. Handlers stay in feature `*Endpoints.cs` files. Shared `/api/v1` rate limiting and each feature's authorization are unchanged.
- [AuditedResult](../../src/Apps/InkFlow.Api/AuditedResult.cs) is the shared wrapper for commands whose response waits for audit: await the audit sink with the supplied cancellation token before creating/executing the response. Audit errors/cancellation prevent response execution. Feature helpers retain their own event and payload mappings. The existing `DeveloperEndpointAudit` in [DeveloperEndpoints](../../src/Apps/InkFlow.Api/DeveloperEndpoints.cs) instead records already-completed commands with `CancellationToken.None` and catches non-cancellation audit failures; these semantics must not be silently unified.
- [Worker Program](../../src/Apps/InkFlow.Worker/Program.cs) registers services; `TaskPollingService`, `BookPackagePollingService`, `MessageRetentionBackgroundService`, `MappingContentPublisher` and `CompositeTaskExecutor` live in same-named files in that host. [Scheduler Program](../../src/Apps/InkFlow.Scheduler/Program.cs) registers `UpdateScanBackgroundService` and `HealthProbeBackgroundService` from their same-named files. Scope/lease/cancellation/timing behavior is unchanged.
- Regression evidence: [ApiCompositionTests](../../tests/InkFlow.UnitTests/ApiCompositionTests.cs), [AuditedResponseTests](../../tests/InkFlow.UnitTests/AuditedResponseTests.cs); delivery state: [simplification work package](../work-packages/2026-10-15-project-simplification.md).

## Modules (`src/Modules/`)

| Module | Responsibility |
| --- | --- |
| `Identity` | Users, sessions/tokens (replay detection, family revocation), avatars, permission grants, retention |
| `Library` | Canonical books/chapters, matching, alignment, private library |
| `Sources` | Source registry, Rule DSL, capability health, credentials (reference-only); bounded rule execution and source adapters |
| `Crawling` | Tasks/lease/retry/dead-letter, collection runs, discovery, fetch artifacts |
| `Content` | Content AST, versions (append-only), quality selection, policy/takedown |
| `Reading` | Reader preferences, progress, shelf/history-facing state |
| `Legado` | Protocol DTOs, rule generator, compatibility profile |
| `Developers` | Developer apps, API keys |
| `Billing` | Plans, entitlements, quotas, usage ledger |
| `Operations` | Alert snapshots/history, consistency read model, source status |
| `Search` | Reserved; search currently filters catalog via `Library`/`Crawling` discovery |

## Adapters (`src/Adapters/`)

| Adapter | Responsibility |
| --- | --- |
| `Kanunu8` | Trusted CodeAdapter (GB2312/GBK legacy site) |
| `SeventeenK` | Trusted CodeAdapter (JSON API allowlist hosts) |

## BuildingBlocks (`src/BuildingBlocks/`)

`Domain` / `Application` / `Persistence` / `Messaging` (Outbox/Inbox) /
`Security` (SSRF, password hashing, audit) / `Observability` (OTel setup, SLO).

Shared dependencies: [../architecture.md](../architecture.md) for dependency
direction and invariants every module must keep.

Sources runtime navigation: `RuleBasedSourceAdapter` owns capability-specific
mapping and source-context normalization; `RuleListProjector` owns shared
selector traversal, cancellation and result budgets for list capabilities.
`RuleAdapter` remains the request/session/pagination execution module. `RuleSelectorEvaluator`
validates and dispatches to the internal CSS, HTML/XML XPath and JSONPath
implementations behind `ISelectorLanguageEvaluator`; shared parser limits live
in `SelectorEvaluationLimits`. Historical batch evidence is recorded in [the module
refactor package](../work-packages/2026-10-15-module-refactor.md).

## Current source reconciliation (2026-10-22)

- [RuleAdapter](../../src/Modules/InkFlow.Modules.Sources/Application/RuleAdapter.cs) now composes `RuleExecutionPreflight`, `RuleCredentialResolver`, `RuleHttpExecutor`, `RulePreRequestExecutor`, `RulePaginationExecutor`, `RuleResponseEvaluator`/`RuleResponseValueExtractor`, and `RuleRequestBudget`; do not replan these extractions from the old Batch 2 record.
- [CrawlerTaskProcessor](../../src/Modules/InkFlow.Modules.Crawling/Application/CrawlerTaskProcessor.cs) composes run coordination, execution, failure and outcome handling; [ReadingStateService](../../src/Modules/InkFlow.Modules.Reading/Application/ReadingStateService.cs) composes shelf queries/commands, history, progress, preferences and visibility.
- [CatalogQueryService](../../src/Modules/InkFlow.Modules.Content/Application/CatalogQueryService.cs) uses separate book/content readers; [BookPackageService](../../src/Modules/InkFlow.Modules.Content/Application/BookPackageService.cs) has separate creation, queries and generation responsibilities.
- `ReaderHtml*` remains in Content.Application (RF-04 Planned). [RF-02](../work-packages/2026-10-22-rf02-consistency.md) moves consistency DTOs/service/grouped pure rules to Operations.Application; API retains `EfConsistencySnapshotReader` and projects domain enum values through `ConsistencyDomainValues`. No foreign module edge is added to Operations.
- [RF-01](../work-packages/2026-10-22-rf01-architecture-guards.md) adds compiled dependency guards and an explicit graph/inventory for all 11 modules. Human navigation is synchronized in [architecture](../../docs/architecture/architecture.md); current exact-SHA CI evidence and limits are in the RF-01 record, not implied by module navigation or acceptance of every historical batch.

RF-03 keeps the RF-02 ownership boundaries; only Operations evaluation indexes and bounded issue retention change. See [RF-03](../work-packages/2026-10-22-rf03-consistency-performance.md) for measured targets and acceptance evidence.
