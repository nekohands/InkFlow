# RepoWiki

Audience: Agent-only. Authority: project contracts and evidence.
Agent read instruction: Agents MUST read this index before planning, implementation, verification, or closeout; then read only the relevant topic pages and linked source/config/test/runtime evidence.
Style: concise English facts and links; preserve conditions, exceptions, evidence,
and exact Chinese domain terms/UI text. This Wiki is the sole project knowledge
source for Agents; Chinese docs are descriptive capability/delivery output only.
Semantic drift or information loss blocks completion.

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

## Workflow gates

- Conformance/adoption: the 2026-10-11 recheck is `COMPLIANT` after the Wiki
  metadata/source/catalog and docs-index normalization recorded in
  [the adoption record](../docs/delivery/adoption-2026-10-02.md); the preserved
  accepted package at that checkpoint was 5.81. The 2026-10-15 normalization
  archives completed/superseded progress, handoff and profile blocks into the
  existing docs histories, preserves pending acceptance items, and updates host
  navigation; the current package below supersedes the old no-active-work state.
- Current work package: [Module refactor](work-packages/2026-10-15-module-refactor.md),
  authorized by the explicit 2026-10-15 user request; responsibility-centered
  Sources/Crawling/Reading batches are in progress. The preceding host organization
  package remains historical delivery evidence.
- Required project gates: Restore, Release Build, affected Unit/Architecture/
  Integration/Contract checks, applicable Runtime/Security, exact target-SHA
  CI/Docker/Security, then Progress/Handoff and RepoWiki sync. Local Docker is
  a known blocker when the Windows Docker named pipe is unavailable.
- Status vocabulary: Planned, In Progress, Implemented, Locally Validated,
  CI Green, Accepted, Completed, Blocked. A local pass or pending CI is not
  completion.
- CodeGraph: `.codegraph/` is present; `codegraph explore "project structure,
  entrypoints, and verification commands"` was executed on 2026-10-11 and
  returned 37 symbols across 5 files.

## Current work

Active work package: [Module refactor](work-packages/2026-10-15-module-refactor.md). The preceding [Project simplification](work-packages/2026-10-15-project-simplification.md) remains delivery evidence. 5.81 [Reading state bounded metadata projection](work-packages/2026-10-08-reading-state-bounded-projection.md) remains Accepted.
- Last completed: Reading state bounded metadata projection (5.81) — implementation SHA `e85dbc143fb6b7d20030403239aceedb34ec43d9` passed exact-head CI `37775920297`, Docker `37775919950`, and Security `37775919947`; local ReadingState `9/9`, Unit `627/627`, Architecture `1/1`, Contract `12/12`, Release Restore/Build, migration model `11/11`, diff and secret checks passed. Focused PostgreSQL Integration remained blocked by the Windows Docker named pipe; CI supplied migrations, PostgreSQL/Redis, runtime smoke, SLO, backup/restore, and diagnostics.
- Last completed: Legado book-info metadata projection (5.80) — implementation SHA `4eb63e5a1a0a95872ec6dc9814ab0fcba126a0f0` passed exact-head CI `37771778894`, Docker `37771778862`, and Security `37771778935`; local Unit/Architecture/Contract, Release Restore/Build, migration model `11/11`, script syntax, diff, and secret checks passed, while the focused PostgreSQL Integration regression was blocked by the Windows Docker named pipe. CI supplied migrations, PostgreSQL/Redis, runtime smoke, SLO, backup/restore, and diagnostics; Docker and Security passed their complete gates.
-Last completed: Legado TOC canonical chapter projection (5.79) — implementation SHA `c0aefc08682e9ed1bb22b201ec16b4c5a9b7ba10` passed exact-head CI `37768061749`, Docker `37768061718`, and Security `37768061741`; local Unit/Architecture/Contract, Release Build, migration model, diff, and secret checks passed, while the focused PostgreSQL Integration regression was blocked by the Windows Docker named pipe. CI supplied migrations, PostgreSQL/Redis, runtime smoke, SLO, backup/restore, and diagnostics; Docker and Security passed their complete gates.
-Previous completed: source registry page projection (5.78) — implementation SHA `24669af2f343f47cbd1e29924772b15d5956a67a` passed exact-head CI `37764067745`, Docker `37764067733`, and Security `37764067771`; local Unit/Architecture/Contract, Release Build, migration model, diff, and secret checks passed, while local PostgreSQL Integration was blocked by the Windows Docker named pipe. CI supplied migrations, PostgreSQL/Redis, runtime smoke, SLO, backup/restore, and diagnostics; Docker and Security passed their complete gates.
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
Agent work-package summary: [Workflow 0.93.0 documentation normalization](work-packages/2026-10-11-workflow-093-documentation-normalization.md).
Unresolved conflicts: none. Known documented boundary (not a conflict): Reader
visibility of platform-level collection runs/packages is an accepted product
decision (5.52, ADR 0028) pending ownership fields; the 2026-09-11 review's H3
item is superseded by that record.

<!-- repowiki:index:start -->
## Generated catalog

Navigation is grouped by RepoWiki folder; topic pages retain their own source citations.

- [Decisions](decisions/README.md) — accepted architecture decisions (ADR). One decision per file; new work
- [Modules](modules/README.md) — one-line responsibility per module/host/adapter, with source paths.
- [Operations](operations/README.md) — operational verification commands, evidence sources, and known environment boundaries.
  - [Verification](operations/verification.md) — real commands and evidence sources per surface, with observed local
- [Work packages](work-packages/README.md) — bounded implementation, verification, acceptance, and handoff records. Historical packages remain evidence and are not active authorization.
  - [Catalog query pagination and N+1 reduction](work-packages/2026-10-07-catalog-query-pagination.md)
  - [Entitlement actor validation](work-packages/2026-10-07-entitlement-actor-validation.md)
  - [Canonical match query bounding](work-packages/2026-10-08-canonical-match-query-bound.md)
  - [Code adapter response and regex bounds](work-packages/2026-10-08-code-adapter-bounds.md)
  - [Work Package 5.75 — Content-fetch chapter ID projection](work-packages/2026-10-08-content-fetch-chapter-id-projection.md)
  - [Crawler handler lease renewal](work-packages/2026-10-08-crawler-handler-lease-renewal.md)
  - [5.66 Discovery search budget fencing](work-packages/2026-10-08-discovery-search-budget.md)
  - [5.70 Health-probe candidate batching](work-packages/2026-10-08-health-probe-candidate-batching.md)
  - [5.69 Health-probe sample lookup fencing](work-packages/2026-10-08-health-probe-sample-fencing.md)
  - [Legado book-info metadata projection](work-packages/2026-10-08-legado-book-info-metadata-projection.md)
  - [Legado TOC canonical chapter projection](work-packages/2026-10-08-legado-toc-chapter-projection.md)
  - [Work Package 5.76 — Matching source-book metadata projection](work-packages/2026-10-08-matching-source-book-metadata-projection.md)
  - [Private book optimistic concurrency](work-packages/2026-10-08-private-book-optimistic-concurrency.md)
  - [5.72 Reader chapter metadata point lookup](work-packages/2026-10-08-reader-chapter-metadata-point-lookup.md)
  - [Work Package 5.74 — Reading history chapter metadata point lookup](work-packages/2026-10-08-reading-history-chapter-metadata-point-lookup.md)
  - [Work Package 5.81 — Reading state bounded metadata projection](work-packages/2026-10-08-reading-state-bounded-projection.md)
  - [5.65 Rule selector execution deadline fencing](work-packages/2026-10-08-rule-selector-execution-deadline.md)
  - [5.73 Source content chapter metadata point lookup](work-packages/2026-10-08-source-content-chapter-metadata-point-lookup.md)
  - [Work Package 5.77 — Source enabled-state projection](work-packages/2026-10-08-source-enabled-state-projection.md)
  - [5.67 Source list-result budget fencing](work-packages/2026-10-08-source-list-result-budget.md)
  - [5.71 Source registry page fencing](work-packages/2026-10-08-source-registry-page-fencing.md)
  - [Work Package 5.78 — Source registry page projection](work-packages/2026-10-08-source-registry-page-projection.md)
  - [5.68 Scheduled update-scan page/fan-out fencing](work-packages/2026-10-08-update-scan-page-fencing.md)
  - [Workflow 0.93.0 documentation normalization](work-packages/2026-10-11-workflow-093-documentation-normalization.md)
  - [Module refactor — responsibility seams and extension points](work-packages/2026-10-15-module-refactor.md)
  - [Project simplification — host composition and audited responses](work-packages/2026-10-15-project-simplification.md)
- [Architecture](architecture.md) — binding invariants, dependency direction, data consistency, and source

Catalog metadata: [meta/repowiki-metadata.json](meta/repowiki-metadata.json)
<!-- repowiki:index:end -->
