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
- [AuditedResult](../../src/Apps/InkFlow.Api/AuditedResult.cs) is the single API command-response wrapper: await the audit sink with the supplied cancellation token before creating/executing the response. Audit errors/cancellation prevent response execution. Feature helpers retain their own event and payload mappings.
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
in `SelectorEvaluationLimits`. Batch evidence is recorded in [the module
refactor package](../work-packages/2026-10-15-module-refactor.md).
