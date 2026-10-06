# Modules

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
| `InkFlow.Scheduler` | Update scans that enqueue crawler tasks |
| `InkFlow.Migrations` | Applies all module DbContext migrations; fail-closed on model drift |

## Modules (`src/Modules/`)

| Module | Responsibility |
| --- | --- |
| `Identity` | Users, sessions/tokens (replay detection, family revocation), avatars, permission grants, retention |
| `Library` | Canonical books/chapters, matching, alignment, private library |
| `Sources` | Source registry, Rule DSL, capability health, credentials (reference-only) |
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
