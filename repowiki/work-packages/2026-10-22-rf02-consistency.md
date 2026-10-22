# RF-02 — consistency responsibilities and measurement

Audience: Agent-only.
Agent read instruction: Read before implementing or validating RF-02; preserve complete report semantics and distinguish rule-only measurement from database measurement.
Scope: pure consistency contracts/service/rules in Operations, EF and enum metadata adaptation in API, characterization and reproducible baseline.
Status: In Progress.
Authorization: user explicitly requested RF-02 on 2026-10-22.
Baseline: `aa15b512321f0cd7c3e991ec7ef9a3815a6b9743`, clean dev checkout.
Parent/root: [Work packages](README.md) / [RepoWiki](../README.md).
Human counterpart: [一致性检查拆分与测量](../../docs/engineering/consistency-check-refactoring.md).

## Contract and acceptance

- Move pure snapshot/report DTOs, report orchestration and Sources/Library/Content/Crawling rule responsibilities to Operations.Application. Keep cross-schema EF and domain enum adaptation in API; no new module dependency edges.
- Preserve all issue codes/messages, complete count, ordinal ordering, first 1,000 examples, CheckedAt and cancellation propagation. Characterize empty, healthy, orphan, duplicate, mismatch and over-limit cases before moving code.
- No RF-03 optimization, schema/migration, isolation, permission or public JSON changes. Sequential reads remain non-atomic; all 13 metadata collections and all issues are still materialized.
- Measure fixed Release fixtures with 3 warmups, 30 samples, 3 rounds; 1k/10k/100k canonical chapters, two source chapters and content versions each. Keep raw parameters/samples, p50/p95, allocations, process lifetime peak memory, SQL count/rows/plans and complete report equivalence. Separate pure-rule and PostgreSQL evidence. No speedup claim or invented RF-03 target.
- Risks: dropped rules, enum drift, ordering/sample changes; mitigate with characterization, explicit pure value definitions from host enums, architecture tests and PostgreSQL checks.

## Verification

NOT RUN for this package: Restore/Release Build, Unit/Architecture/Contract/Integration, migration drift 11 contexts, Admin/Operations runtime and exact-SHA CI/Docker/Security. Local Docker was unavailable in RF-01; recheck and obtain real remote evidence if still blocked. Existing live/manual/production gates stay open.

Update: local Restore/Release Build (solution and benchmark) PASS, zero warnings/errors; Unit 656/656, Architecture 51/51, Contract 12/12 PASS. Full Integration actually ran: 8 passed / 128 DockerUnavailable failures / 3 skipped. Local PostgreSQL benchmark also BLOCKED on the Docker named pipe. Migration/remote gates pending. All 46 issue emission sites retained and EF reader body unchanged by direct comparison.

Before/after rule measurements: 720 samples each, all 24 paired rounds match complete issue and report hashes, including 200,030 issues. [Before raw](../../docs/engineering/benchmarks/rf02/before-rules.json), [after raw](../../docs/engineering/benchmarks/rf02/after-rules.json); baseline production aa15b51, harness a043e24, after = this extraction. Shared local machine had concurrent build activity; an attempted benchmark rebuild hit Windows apphost locking and passed after the measurement exited. These are equivalence/cost-scale evidence, NOT speedup or RF-03 budget evidence. RF-03 must remeasure isolated conditions and freeze noise/targets before optimizing. Peak memory is process-lifetime Working Set; allocations are process-total deltas; hashes are outside timed work. PostgreSQL job uses an owned container, warm caches and no user-supplied database. UNIQUE-protected duplicates are tested in memory only; database corruption is a persistable cross-schema orphan. Existing live/manual/production gates remain open.

## Sources

- [Pure rules and contracts](../../src/Modules/InkFlow.Modules.Operations/Application/Consistency/)
- [EF adapter](../../src/Apps/InkFlow.Api/EfConsistencySnapshotReader.cs)
- [Host enum metadata](../../src/Apps/InkFlow.Api/ConsistencyDomainValues.cs)
- [Characterization](../../tests/InkFlow.UnitTests/ConsistencyCharacterizationTests.cs)
- [PostgreSQL regression](../../tests/InkFlow.IntegrationTests/ConsistencyCheckIntegrationTests.cs)
- [Measurement harness](../../tools/InkFlow.ConsistencyBenchmarks/)
- [CI](../../.github/workflows/ci.yml)
