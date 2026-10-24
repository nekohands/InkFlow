# RF-07 integrated regression and evidence closeout

Audience: Agent-only.
Agent read instruction: Read before auditing, verifying or closing RF-07; use linked Git/source/CI evidence, preserve historical failures and open manual acceptance.
Scope: audit RF-01 through RF-05 acceptance, run cross-surface regression, synchronize documentation and explicitly disposition conditional RF-06.
Status: Accepted / Completed at regression candidate `096eb3ebef1d200bb017943fbbcf56dee618906c`. User explicitly authorized RF-07 on 2026-10-24. Historical checkpoints below retain their original pending/failure state; this acceptance section is current.
Owner: InkFlow maintainers; execution by Codex in D:/Git/InkFlow.
Entry: dev at `6a5b1c982dca489fb8f09f9bf3c6668b994de2ba`, clean worktree, no `.codegraph/`. No RF-07 command/mutation ran before the interrupted intake resumed.
Parent/root: [Work packages](README.md) / [RepoWiki](../README.md).
Human counterpart: [综合回归与收尾](../../docs/engineering/optimization-refactoring-closeout-2026-10-24.md).

## Contract and acceptance

1. Recheck RF-01..05 implementation SHAs and their CI/Docker/Security runs, all required jobs/attempts, workflow definitions and ancestry to entry HEAD. Preserve original failures/repairs and evidence limitations; historical acceptance does not prove the current candidate.
2. Reconcile frozen rule/PostgreSQL/Reader/Developer observations, budgets, source/test ownership and acceptance boundaries. Keep exact report/protocol equivalence and existing numeric/screenshot thresholds.
3. Execute fresh Restore, zero-warning Release Build, full solution tests, Architecture/Contract and 11-context migration checks. Recheck Docker; local environment failures remain failures/BLOCKED, with actual PostgreSQL/Redis/Compose evidence supplied by exact-candidate CI.
4. Require all current CI jobs (validate, reader-browser, consistency-measurement, developer-measurement), five image builds plus published Compose, and four security jobs. Include Developer/Admin/Reader/Legado/Source/private-library/collection-package, Core SLO/OTLP and backup/restore runtime. No weakened assertions, new skips or budget relaxation.
5. Synchronize plan, Wiki/catalog/metadata, verification navigation, Progress/Handoff/profile and Chinese delivery record. Accept only after candidate gates; check any documentation closeout SHA independently.

Allowed write set: this Wiki/human work package, existing plan and delivery state/indexes, affected Wiki/doc structural fixes, and bounded non-secret evidence under docs/engineering/benchmarks/rf07/. Build/test/CI temporary outputs belong under ignored artifacts/. No production source, schema, public contract, authentication, dependency upgrade, deployment or main merge is planned. Existing dev push workflows build/publish scanned candidate images as configured. ADR change is N/A (accepted architecture direction retained). RF-06 remains conditional and is deferred from this selected first-round closeout unless a concrete benefit and its own regressions are separately authorized. Real-device/account/upstream/production acceptance remains open.

## Preflight and continuity

Rebuilt scope from current AGENTS, Wiki index, verification topic, RF plan/packages and local workflow profile; prior conversation is a retrieval lead only. Entry health lead: entry SHA CI 39818441078, Docker 39818441049, Security 39818441050 report completed/success; all child jobs will be reconciled in the audit. SDK 10.0.402 observed; docker command absent. Date follows client Asia/Singapore 2026-10-24; machine-readable evidence records the UTC tool observation separately.

Documentation preflight found NORMALIZATION_REQUIRED: RF-05 used an inline Sources label instead of a Sources section, causing the generator check to reject it. Repair preserves every source link and all package evidence, adds the required section boundary, and regenerates catalog/metadata. Existing adoption baseline is reused for unchanged topics; no workflow upgrade or repository-wide rewrite. Conformance and RepoWiki sync remain pending the generator/link check. Read-only evidence discovery may proceed; task gates start only after that repair passes.

## Claim register

| ID | Kind | Status | Claim / binding / next check |
| --- | --- | --- | --- |
| RF07-C1 | CONTRACT | CURRENT | Explicit RF-07 request and accepted plan define this bounded audit/regression/closeout; no RF-06 implementation or production acceptance closure. |
| RF07-F1 | FACT | CURRENT | Entry branch/commit/clean worktree observed from git on 2026-10-24; recheck before mutation, candidate and final report. |
| RF07-F2 | FACT | CURRENT | Generator --check rejected RF-05 Sources structure; preserve failure and require repaired catalog/link result. |
| RF07-F3 | FACT | CURRENT | All 10 implementation/closeout commits and 30 workflows/124 jobs PASS after c3347ca attempt 2. Initial cancelled snapshot, final audit, frozen blobs and artifact provenance retained below. |
| RF07-F4 | FACT | CURRENT | Fresh local gates and all 14 exact-candidate remote jobs PASS; local Docker limitation, two live skips and manual/production acceptance remain explicit. Terminal snapshot/log receipts and raw artifacts bound below. |

## Sources

- [Plan](2026-10-22-optimization-refactoring-plan.md), [verification](../operations/verification.md), [workflow profile](../../docs/delivery/PROJECT_WORKFLOW.md), [development workflow](../../docs/engineering/development-workflow.md).
- [RF-01](2026-10-22-rf01-architecture-guards.md), [RF-02](2026-10-22-rf02-consistency.md), [RF-03](2026-10-22-rf03-consistency-performance.md), [RF-04](2026-10-22-rf04-presentation.md), [RF-05](2026-10-23-rf05-developer-api.md).
- [Workflow definitions](../../.github/workflows/), [tests](../../tests/), [runtime scripts](../../scripts/), [measurement/browser tools](../../tools/).

Preflight repair result: catalog --write and --check PASS (40 Wiki pages), local link closure and git diff --check PASS. Workflow conformance: COMPLIANT; RepoWiki sync: PASS for the affected entry/package/index/metadata scope. Original RF-05 structural failure is preserved above. No source, workflow, threshold, test selection or existing acceptance evidence changed.

## Historical audit checkpoint

Authenticated gh metadata plus workflow definitions at each immutable SHA reconciled 10 commits (five implementations and five documentation closeouts), Git ancestry and 30 workflow/job sets. All 15 implementation workflows PASS. Documentation closeouts: 14/15 PASS; RF-02 closeout `c3347caa1027025788f5c9dd66696a0d8d23ae3e` CI 39770903564 attempt 1 was cancelled at 2026-10-22T11:32:59Z. Both measurement and validate jobs were cancelled; measurement upload failed because artifacts/consistency did not exist. Build/tests/migration had passed, but runtime and measurement were incomplete. Cancellation initiator/reason is not established. Original snapshot is [historical-audit-initial.json](../../docs/engineering/benchmarks/rf07/historical-audit-initial.json); never report that attempt as green.

The original cancelled job log was read before requesting a full rerun of CI 39770903564 at its original c3347ca SHA (both jobs need execution; no source/assertion/workflow change). RF-02 implementation acceptance remains supported by its separate successful 3a38ad1 runs. Current source's baseline is separately supported by all exact-entry 6a5b1c9 jobs. Current read-only regression may run independently while repairing this historical documentation gate; final acceptance joins both branches and requires both complete. No dependent production package is implemented while a required predecessor is unresolved.

Frozen data inspection: 24 paired RF-02 full/report/count hashes match, 720 samples per consistency dataset, 13 SQL commands per PostgreSQL sample; accepted RF-04 Linux data has matching four-viewport behavior, 450 samples per side, 40 zero-pixel-difference captures and no uncaught page errors. Git blob identities for all 15 JSON evidence files are retained in the audit output. Numeric budget/Developer comparators and fresh candidate measurements still must run. These checks establish historical evidence integrity, not current runtime performance.

Claim update: RF07-F3 implementation subclaim is FACT/CURRENT (15/15 PASS); documentation subclaim is FACT/CURRENT with one cancelled gate, repair pending. RF07-F4 remains UNKNOWN until fresh regression. Allowed write set additionally includes RF-02 Wiki/human addenda preserving and resolving the discovered historical cancellation.

## Candidate regression checkpoint

Restore/Release solution and three diagnostic/fixture tools PASS, 0 warnings/errors. Full Unit 726, Architecture 53, Contract 12 PASS; full local Integration actually executed: 8 passed / 128 DockerUnavailable failures / 3 skips. Migration drift 11/11 PASS. Comparator regressions: consistency 6, Developer 2, screenshot boundaries 3 PASS. Frozen RF-03 rule/PostgreSQL budgets and RF-05 810-observation comparator replay PASS.

Six downloaded files (RF-02 PostgreSQL, RF-03 PostgreSQL, RF-04 before/after browser reports, RF-05 before/after observations) match immutable Git blob bytes exactly. [Provenance](../../docs/engineering/benchmarks/rf07/artifact-provenance.json) binds each to the GitHub run/artifact ID, head SHA, reported archive digest and raw-file SHA-256. No normalization, missing data substitution or summary-only acceptance.

First fresh local rule run was retained as FAIL: 1k healthy median-of-round p95 4.3031ms exceeded the 4.23496ms frozen limit. Its log interval overlaps this task's artifact-provenance/download operations, violating the isolation precondition; it is not an eligible isolated performance trial. Stopped task-local concurrent build/download work and repeated the identical Release binary/protocol without changing code, warmups, samples, thresholds or expected hashes. The isolated 720-sample run PASSes every frozen gate; 1k healthy p95 2.3926ms. [First raw](../../docs/engineering/benchmarks/rf07/initial-rules-overlap.json), [first failure](../../docs/engineering/benchmarks/rf07/initial-rules-comparison.txt), [isolated raw](../../docs/engineering/benchmarks/rf07/isolated-rules.json), [isolated comparison](../../docs/engineering/benchmarks/rf07/isolated-rules-comparison.txt). This is a measurement-control correction, not a new speedup or process-peak claim. Retain RF07-M01 as an operational control: no parallel task-local builds/downloads during future local performance gates; owner maintainers, recheck at each performance run. Uncontrolled external activity remains a diagnostic limitation.

[Local verification](../../docs/engineering/benchmarks/rf07/local-verification.json) binds toolchain, protocol and consumed source/test/tool/script/workflow/build-config Git object identities to entry 6a5b1c9. Only docs/evidence/indexes differ in the pending candidate, so local code gates retain those unchanged input bindings; final CI binds the complete candidate commit. Current candidate PostgreSQL/Redis/runtime/browser/performance/security/CI remains PENDING; historical c3347ca CI repair is an independent required join.

## Reconciled historical branch

c3347ca CI 39770903564 attempt 2 completed/success with 2/2 jobs. The historical audit was freshly repeated after repair: all 10 implementation/closeout commits have valid ancestry, and all 30 exact-target workflow/job sets (124 jobs) PASS. [Final snapshot](../../docs/engineering/benchmarks/rf07/historical-audit-final.json) includes definition blobs, expected names, observed jobs/steps, attempt, repository/branch and SHA checks; [initial snapshot](../../docs/engineering/benchmarks/rf07/historical-audit-initial.json) retains the cancellation. RF-02 Wiki/human addenda document both attempts. The historical branch is closed; current candidate regression remains pending.

Candidate 096eb3e measurement/browser artifacts have been archived with [run/artifact provenance](../../docs/engineering/benchmarks/rf07/candidate-artifact-provenance.json): PostgreSQL 720 samples satisfy all frozen RF-03 gates and preserve 13 SQL commands/full reports; Developer 810 protocol/header/body/ledger/SQL observations match; Reader 22 output manifests, four viewport behaviors, 450 samples per side and 40 screenshots (all zero changed pixels), no uncaught errors. These three jobs PASS; full candidate runtime is still pending at this checkpoint.

## Acceptance (2026-10-24)

Accepted / Completed at regression candidate `096eb3ebef1d200bb017943fbbcf56dee618906c`: [CI 40060934590](https://github.com/nekohands/InkFlow/actions/runs/40060934590) 4/4, [Docker 40060934577](https://github.com/nekohands/InkFlow/actions/runs/40060934577) 6/6, [Security 40060934575](https://github.com/nekohands/InkFlow/actions/runs/40060934575) 4/4, all completed/success at the exact SHA. [Terminal evidence](../../docs/engineering/benchmarks/rf07/candidate-remote-verification.json) binds every expected job, target and outcome. Candidate source/config/test/tool input identities match the locally verified entry identities; only docs/evidence/indexes changed.

Fresh CI log receipts confirm Unit 726, Architecture 53, Contract 12; PostgreSQL Integration 136 passed / 3 skipped and configured Redis separately 1/1; migration 11/11. [Selected log evidence](../../docs/engineering/benchmarks/rf07/candidate-validation-summary.txt) retains counts and Developer/Admin/Reader/Legado/failover/quality/private-library/collection-package, Core SLO/OTLP and backup PASS receipts. Source-build Compose API/Worker/Scheduler/Collector, published-image Compose and all security checks PASS. Two opt-in live-source tests remain NOT RUN; Redis's default skip is covered by its configured execution. Local Integration still has 128 DockerUnavailable failures, not a local pass.

All selected first-round packages RF-01..05 and RF-07 are accepted. Historical re-audit: 10 commits, 30 workflows, 124 expected jobs PASS, including repaired c3347ca attempt 2. Source failures, cancellations and earlier correction evidence remain intact. Current fresh 720-sample local rule and 720-sample PostgreSQL budgets/full reports, 810 Developer protocol/ledger/SQL observations, and 22 Reader outputs/four viewports/40 zero-pixel-difference screenshots/450 samples per side PASS. Numeric, visual and protocol expectations were not weakened.

Findings resolved: (1) RF-05 Sources/catalog structural defect normalized with all links/evidence preserved; (2) RF-02 cancelled documentation CI rerun at unchanged SHA and fully reconciled; (3) overlapping local measurement retained as failed/ineligible, isolation control corrected and identical isolated protocol passed. RF07-M01 is an ongoing execution control, not a claim of a code speedup or process-peak reduction.

RF-06 disposition: Deferred (conditional, not selected for the recommended first round). F-05/F-06 are retained as historical leads, not proof of a required refactor. Reopen only for a separately bounded, evidenced messaging/parser maintenance benefit or defect, with required PostgreSQL transaction/lease/idempotency/rollback or deterministic parser/security regressions. This does not assert that the entire messaging/parser codebase was audited or is free of future opportunities.

Remaining release acceptance: existing real-device/Reading 3.0/Personal token, real-account/PWA/long-reading, live upstream/second-source/failover and production credentials/OTLP/SLO/backup/security governance remain open. Owner inherits maintainers; next action is arrange the dedicated account/device/environment and collect the existing checklist evidence before release. Local Docker recheck condition: a running reachable engine (then rerun full local Integration/runtime); remote evidence already satisfies this package's container layer. Performance control recheck: each future local performance run. No outstanding RF-07 acceptance blocker; project stage remains 1.0 Release Candidate.

Final documentation set: plan/disposition, Wiki source/catalog/metadata, verification navigation, RF-02 addenda, Chinese delivery/benchmark index, Progress/Handoff/profile and archived evidence synchronized. RepoWiki sync/conformance and Markdown checks must pass again before commit. The documentation-only closeout SHA requires its own exact-SHA CI/Docker/Security check; it does not inherit 096eb3e's status. No production merge/deployment or RF-06 implementation is part of this closeout.

Closeout document verification: COMPLIANT / RepoWiki sync PASS, generator --write/--check 40 pages, affected Markdown/source/evidence links and git diff --check PASS. Allowed write inventory contains docs/RepoWiki/evidence only; known secret-pattern scan and consumed code/config identity check PASS. Existing adoption baseline reused for unchanged areas; no rule version/authority or architecture change.
