# RF-03 consistency performance

Audience: Agent-only.
Agent read instruction: Read before changing or accepting consistency optimization.
Scope: measured Operations consistency evaluation, bounded returned examples, unchanged EF metadata reads.

## Contract and sequence

Status: In Progress. User authorized RF-03 after RF-02; remeasure and freeze targets before production edits.
Baseline commit: `2aaefce7ad2e43c782563c8867292ff3146cd4aa`.
Goal: reduce evaluation allocations and large-fixture latency while counting every issue and retaining the exact ordinal first 1,000 examples.
Non-goals: RF-04/05, schema/index changes, SQL pagination, stronger snapshot isolation, public JSON/auth changes, production-load acceptance.
Risk: duplicate-first lookup behavior, complete counts and ordinal sampling must remain exact, including late-arriving smaller issues and tied keys. Full snapshots remain O(rows); bounded examples do not bound the scan.

## Measurement first

Local Release DLL, .NET 10.0.13, `DOTNET_TieredCompilation=0`, concurrency 1, no concurrent build; unchanged RF-02 fixture seed, 3 warmups, 30 samples per round, 3 rounds, empty/1k/10k/100k healthy and corrupt shapes. Keep raw measurements. Process lifetime peak includes fixtures and oracle serialization, so it is diagnostic, not a per-scan budget.
Fresh PostgreSQL baseline: [CI 39775842652](https://github.com/nekohands/InkFlow/actions/runs/39775842652), existing isolated benchmark runner, default JIT, PostgreSQL 18, warm cache. Compare PostgreSQL only with the same remote configuration, never against local rule times.
Baseline completed before production edits: local 100k healthy/corrupt median-of-round p50 566.56/915.66 ms, p95 663.50/1042.45 ms, allocation 290.48/379.34 MiB; p50 round spread 13.1%/7.1%. Fresh remote 100k healthy/orphan p50 1186.06/1440.73 ms, p95 1264.52/1521.09 ms, allocation 447.88/530.48 MiB; p50 spread 1.1% each. Remote 1k orphan spread 50.1%, so small-fixture timings are noisy. PostgreSQL 18.6, .NET 10.0.13, Ubuntu 26.04.1, 4 CPUs; benchmark job succeeded.

Frozen targets (2026-10-22, before production edits):
- Local 100k healthy AND corrupt allocation median <=85% baseline; corrupt median-of-round p50 AND p95 <=80% baseline (gain exceeds observed noise).
- PostgreSQL 100k healthy AND orphan allocation median <=90% baseline.
- Every shape: allocation <=105% baseline +64 KiB. Local p50/p95 <=120% baseline +1 ms; remote >=10k <=120% baseline +1 ms, <=1k <=150% baseline +5 ms (small-fixture noise, not a speedup claim).
- All 24 records per mode retain exact complete/report hashes and total counts; remote SQL text/count and row counts unchanged. At most 1,000 retained examples in the service; all rules still execute. No per-scan peak claim.
- Use median-of-three round quantiles and median of all 90 allocation samples. Retain/revert against these fixed targets; do not relax them after edits.
- [Local raw baseline](../../docs/engineering/benchmarks/rf03/before-rules.json), [PostgreSQL raw baseline](../../docs/engineering/benchmarks/rf03/before-postgres.json).

## Verification and acceptance

Freeze numerical targets from repeatability evidence before edits. Preserve complete issue/report hashes for all shapes; extend boundary, duplicate-first and late-relation regressions. Inspect diff; restore/build/test solution, Architecture/Contract and real PostgreSQL Integration; verify Admin runtime JSON and unchanged Core SLO, migration drift, Docker and Security on exact candidate SHA. Local Docker availability must be rechecked. Keep optimization only if targets pass without breaching tolerances. Report unexecuted gates as NOT RUN/BLOCKED, never Completed.

## Sources

- [Plan](2026-10-22-optimization-refactoring-plan.md), [RF-02](2026-10-22-rf02-consistency.md), [architecture](../architecture.md).
- [Human record](../../docs/engineering/consistency-check-performance.md).
- [Benchmark](../../tools/InkFlow.ConsistencyBenchmarks/Program.cs), [validator](../../src/Modules/InkFlow.Modules.Operations/Application/Consistency/ConsistencyCheckValidator.cs).

## Implementation and local evidence

Frozen target commit `95184eb` precedes production edits. Stage 1 (`0ba30b1`) replaces nine GroupBy/First lookup indexes with pre-sized dictionaries and TryAdd (first row retained; ordinal source keys). Stage 2 routes service evaluation through a max-priority queue, retains <=1,000 issue references, counts every emission, then sorts the retained sample using the original four ordinal keys. Equal ordering keys are identical error records. Public complete validation uses the same rules with an unbounded diagnostic result; no duplicated rule engine. No EF/rule predicate/schema/auth changes.

Local before -> lookup-only -> final (median-of-round p50/p95 ms; median allocated MiB):

| Shape | Baseline | Lookup-only | Final |
| --- | --- | --- | --- |
| 100k healthy | 566.56 / 663.50 / 290.48 | 364.79 / 401.25 / 159.87 | 354.79 / 411.79 / 159.87 |
| 100k corrupt | 915.66 / 1042.45 / 379.34 | 603.29 / 706.39 / 230.94 | 356.73 / 392.84 / 217.09 |

Both stages retained: indexes cut large allocations; bounded sampling additionally reduces corrupt p50 by 40.9% versus lookup-only and prevents retaining all 200,030 issue records. Every fixture/round complete/report hash and count matches. All frozen local budgets PASS. [Lookup raw](../../docs/engineering/benchmarks/rf03/lookups-rules.json), [final raw](../../docs/engineering/benchmarks/rf03/after-rules.json).

[Budget checker](../../scripts/compare-consistency-benchmarks.py) checks exact environment, complete/report equivalence, full sample/round coverage, SQL/rows and frozen numerical thresholds. Six positive/negative script tests pass; baseline-versus-itself fails the required improvement tests as expected. CI enforces the PostgreSQL budgets; environment/version drift requires explicit remeasurement, not silently refreshed limits.

Consistency focused tests 15/15 PASS, including 999/1000/1001 boundaries, reverse arrival, duplicate keys, tied-resource message ordering, complete baseline hashes and cancellation. Release restore/build PASS (0 warnings/errors). Full local run: Unit 659/659, Architecture 51/51, Contract 12/12 PASS; Integration 8 passed / 128 Docker-unavailable failures / 3 skipped. Focused PostgreSQL consistency test reproduces the Docker endpoint failure before test execution. Migration drift 11/11 PASS. Candidate CI pending. Local Docker command still absent; real PostgreSQL/runtime must be evidenced in CI. Scope/security diff review: no foreign module edge, input/payload/authorization, secret, schema or query change.

Limitations: full snapshots, lookup indexes and duplicate-rule grouping remain O(rows). Every issue still constructs its message/record before selection; only retained examples are bounded. Existing sequential cross-schema reads remain non-atomic. Process lifetime peak is diagnostic only. No production SLO/load claim or unrelated manual acceptance closure.
