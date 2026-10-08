# RepoWiki

Audience: AI. Authority: project contracts and evidence. Read only relevant topics.
Style: concise English facts and links; preserve conditions, exceptions, evidence,
and exact Chinese domain terms/UI text. AI maintains this Wiki and corresponding
Chinese guides in the same change. Semantic drift or information loss blocks completion.

Human documentation: [docs/](../docs/) — Chinese guides; reading order in [AGENTS.md](../AGENTS.md).
Workflow profile (Chinese, operational): [docs/delivery/PROJECT_WORKFLOW.md](../docs/delivery/PROJECT_WORKFLOW.md).

## Navigation

| Topic | Purpose | Page |
| --- | --- | --- |
| Workflow | Operational profile: commands, gates, state paths, preserved work package | [docs/delivery/PROJECT_WORKFLOW.md](../docs/delivery/PROJECT_WORKFLOW.md) |
| Architecture | Named invariants, module dependency direction, data consistency, source runtime constraints | [architecture.md](architecture.md) |
| Modules | Module/adapter/host map with scope and source paths | [modules/README.md](modules/README.md) |
| Verification | Check matrix: commands, evidence sources, known local blockers | [operations/verification.md](operations/verification.md) |
| Decisions | ADR index and rule for new decisions | [decisions/README.md](decisions/README.md) |
| Source Rule DSL contract | JSON schema is the fixed contract fixture | [../docs/contracts/source-rule-dsl-v1.schema.json](../docs/contracts/source-rule-dsl-v1.schema.json) |
| Legado contract | Public `/api/legado/v1/*` and Personal token behavior | [../docs/architecture/legado-contract.md](../docs/architecture/legado-contract.md) |
| Developer API contract | Entitlement/quota/API key behavior | [../docs/architecture/developer-api.md](../docs/architecture/developer-api.md) |

## Current work

Active work package: 5.70 [Health-probe candidate batching](work-packages/2026-10-08-health-probe-candidate-batching.md) — bound scheduled unhealthy-health candidate reads and fan-out with stable keyset paging and an in-process scheduler cursor. 5.69 [Health-probe sample lookup fencing](work-packages/2026-10-08-health-probe-sample-fencing.md), 5.68 [Scheduled update-scan page/fan-out fencing](work-packages/2026-10-08-update-scan-page-fencing.md), 5.67 [Source list-result budget fencing](work-packages/2026-10-08-source-list-result-budget.md), 5.66 [Discovery search budget fencing](work-packages/2026-10-08-discovery-search-budget.md), 5.65 [Rule selector execution deadline fencing](work-packages/2026-10-08-rule-selector-execution-deadline.md), and 5.64 [Crawler handler lease renewal](work-packages/2026-10-08-crawler-handler-lease-renewal.md) remain Accepted.
Last completed: health-probe sample lookup fencing (5.69) — implementation candidate `62331b47f1e1efd2d4080477b45e15661567e79f` passed CI `37724656744`, Docker `37724656752`, and Security `37724656807`; documentation closeout `9c8b9fe75fd6a9eb0aa2c49c0580583c31a30a4b` passed CI `37728036354`, Docker `37728036445`, and Security `37728036432`, all with exact matching head SHA.
Previous: code adapter response and regex bounds (5.61) — Accepted
Shared streaming response reads reject oversized bodies before decode/parse; Kanunu8 regex timeouts and adapter regression tests are covered by local gates and remote CI/Docker/Security GREEN at `0d7d5ce`.
Previous: entitlement actor validation (5.60) — Administrator enforcement at the Billing service boundary, stable forbidden mapping,
and no route/payload/schema/migration change. Previous: catalog query pagination and N+1 reduction (5.59) — bounded
Canonical summaries, bulk Content Policy reads, Developer catalog limit
forwarding, and no cursor API or schema change. Previous: batch lease renewal
(5.58) — `OutboxDispatcher` and
`InboxConsumerPump` renew the remaining claimed batch before each message via
`ExtendLeaseBatchAsync` (owner- and terminal-state-guarded UPDATE; default
no-op fallback is for test doubles only). Batch work slower than the lease no
longer hands messages to other instances mid-batch; a single handler outliving
its whole lease remains bounded by the idempotent consumer (CI/Docker/Security
GREEN at `1658b87`, runs 37478901744/37478902070/37478901915).

Current progress: [docs/roadmap/progress.md](../docs/roadmap/progress.md) ·
Current handoff: [docs/handoff/handoff.md](../docs/handoff/handoff.md) ·
History: [progress-history.md](../docs/roadmap/progress-history.md) /
[handoff-history.md](../docs/handoff/handoff-history.md). Read current first; consult
history only for earlier decisions or evidence.

## Migration and unresolved conflicts

Adoption record (inventory map, dispositions, sync verdict):
[docs/delivery/adoption-2026-10-02.md](../docs/delivery/adoption-2026-10-02.md).
Unresolved conflicts: none. Known documented boundary (not a conflict): Reader
visibility of platform-level collection runs/packages is an accepted product
decision (5.52, ADR 0028) pending ownership fields; the 2026-09-11 review's H3
item is superseded by that record.
