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

Active work package: None. 5.78 [Source registry page projection](work-packages/2026-10-08-source-registry-page-projection.md) is Accepted; 5.77 [Source enabled-state projection](work-packages/2026-10-08-source-enabled-state-projection.md) and earlier packages remain Accepted. Next work package must be re-intaken.
-Last completed: source registry page projection (5.78) — implementation SHA `24669af2f343f47cbd1e29924772b15d5956a67a` passed exact-head CI `37764067745`, Docker `37764067733`, and Security `37764067771`; local Unit/Architecture/Contract, Release Build, migration model, diff, and secret checks passed, while local PostgreSQL Integration was blocked by the Windows Docker named pipe. CI supplied migrations, PostgreSQL/Redis, runtime smoke, SLO, backup/restore, and diagnostics; Docker and Security passed their complete gates.
-Previous completed: source enabled-state projection (5.77) — implementation SHA `b753a8bc207fda442148f0ab70af9ffcb2826bba` passed exact-head CI `37759082085`, Docker `37759082091`, and Security `37759082083`; local Unit/Architecture/Contract, Release Build, migration model, diff, and secret checks passed, while local PostgreSQL Integration was blocked by the Windows Docker named pipe. CI supplied migrations, PostgreSQL/Redis, runtime smoke, SLO, backup/restore, and diagnostics; Docker and Security passed their complete gates.
-Previous completed: matching source-book metadata projection (5.76) — implementation SHA `835e0914b84469581f1d2a21a33fa9a65fac09f6` passed exact-head CI `37753946308`, Docker `37753946459`, and Security `37753946140`; local Unit/Architecture/Contract, Release Build, migration model, diff, and secret checks passed, while local PostgreSQL Integration was blocked by the Windows Docker named pipe. CI supplied migrations, PostgreSQL/Redis, runtime smoke, SLO, backup/restore, and diagnostics; Docker and Security passed their complete gates.
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
