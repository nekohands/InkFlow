# Verification

Audience: Agent-only.
Agent read instruction: Read this page when selecting or reporting repository verification gates; then read the linked workflow profile and work-package evidence.
Scope: real commands and evidence sources per surface, with observed local
blockers. Evidence rules and status vocabulary live in the
[workflow profile](../../docs/delivery/PROJECT_WORKFLOW.md) and
[docs/engineering/development-workflow.md](../../docs/engineering/development-workflow.md)
(mandatory, Chinese).

Parent/root index: [../README.md](../README.md)

Operations index: [README.md](README.md)

## Current verification (2026-10-22)

[RF-02](../work-packages/2026-10-22-rf02-consistency.md) is Accepted at `3a38ad1`: local Restore/Release Build, Unit 656/656, Architecture 51/51, Contract 12/12, migration drift 11/11 passed. Local Integration = 8 passed / 128 DockerUnavailable / 3 skips; local Runtime blocked. Rule measurements before/after match; PostgreSQL benchmark job passed 720 samples with SQL/row/plan evidence. Exact-SHA CI 39766807840 (attempt 2), Docker 39766808118 and Security 39766807728 passed all jobs. CI Integration 136 passed / 3 skips, configured Redis 1/1, runtime/SLO/OTLP and backup passed. Initial Ubuntu package-index hash mismatch was resolved by rerunning the failed job with integrity checks intact. RF-01 below is previous-package evidence.

[RF-01](../work-packages/2026-10-22-rf01-architecture-guards.md) implements compiled
dependency, project graph and inventory guards. Local Restore/Release Build,
Architecture 51/51, Unit 648/648, Contract 12/12 and model drift 11/11 passed.
Full Integration ran: 8 passed, 128 DockerUnavailable failures, 3 skips (two
opt-in live cases and Redis configuration). `docker` is absent from PATH and
Testcontainers cannot connect to `npipe://./pipe/docker_engine`. The original
`34cfa8f` Docker failure (Collector Go stdlib CVE-2026-111943) is preserved.
The minimal Go 1.26.10 repair at `f90e6bd` passed all jobs in CI `39758029891`,
Docker `39758029869` and Security `39758029837`. Remote Integration: 136 passed /
3 skipped, configured Redis separately 1/1. Runtime/SLO/OTLP and backup/restore
passed; two opt-in live-source cases remain unrun. Baseline `ca15cad` runs are not RF-01 evidence.
The dated table below remains historical. Human counterpart:
[architecture guards](../../docs/engineering/architecture-guards.md).

## Sources

- [Project workflow profile](../../docs/delivery/PROJECT_WORKFLOW.md)
- [Development workflow](../../docs/engineering/development-workflow.md)
- [CI workflows](../../.github/workflows/)

| Surface | Command | Evidence source | Observed state (2026-10-02) |
| --- | --- | --- | --- |
| Restore | `dotnet restore InkFlow.sln` | local + CI | PASS |
| Build | `dotnet build InkFlow.sln -c Release` | local + CI | PASS; gate = 0 warnings / 0 errors (warnings-as-errors) |
| Unit | `dotnet test tests/InkFlow.UnitTests -c Release --no-build` | local + CI | PASS (583/583 at 5.54) |
| Architecture | `dotnet test tests/InkFlow.ArchitectureTests -c Release --no-build` | local + CI | PASS (1/1) |
| Contract | `dotnet test tests/InkFlow.ContractTests -c Release --no-build` | local + CI | PASS (12/12) |
| Integration | `dotnet test tests/InkFlow.IntegrationTests` (Testcontainers PostgreSQL 18) | CI only | Local BLOCKED: no Docker Engine on the Windows dev machine; CI PostgreSQL job is the evidence source |
| Migration drift | `bash scripts/verify-migrations.sh` | local + CI | PASS (11 contexts) |
| Compose/runtime smoke | `docker compose -f docker-compose.build.yml config --quiet` / `up -d --build --wait api worker scheduler otel-collector`; `scripts/*-runtime-smoke.sh` | CI + VM | Local BLOCKED (no Docker); CI Runtime smoke + Frontend smoke are the routine evidence; `docker-compose.yml` is the GHCR published-image stack, not daily verification |
| Security scan | `.github/workflows/security.yml` (deps, secrets, CodeQL, SBOM); Docker image Trivy | CI | GREEN at `f136ede` |
| CI gate | `.github/workflows/ci.yml`, `docker.yml`, `security.yml` (push/PR: `main` + `dev`) | GitHub Actions | Required for `Completed`; confirm run head SHA == commit |

Rules: a local pass never substitutes for CI; fixture/live/manual evidence stay
distinct; `NOT RUN`/`BLOCKED` stay explicit. Live source verification is opt-in
and separate from PR CI.

RF-03 adds frozen performance/report-equivalence checks to the independent PostgreSQL measurement job. Version/environment mismatch fails and requires an explicit rebaseline; no automatic budget relaxation. See [RF-03](../work-packages/2026-10-22-rf03-consistency-performance.md).

## Reader presentation equivalence (RF-04)

22 canonical-LF output fixtures guard HTML/resources. `tools/InkFlow.ReaderFixtures` exports the real renderer; `tools/InkFlow.ReaderBrowser` runs isolated Playwright/Chromium against loopback API doubles (never real credentials). Four responsive viewports cover keyboard/focus/session/navigation/empty/error/offline/Operations; desktop short/long and 100/1k/10k TOC use 3 warmups, 30 samples, 3 rounds. CI `reader-browser` publishes frozen `1c258c3` and the candidate, executes outside the source checkout and compares exact output manifests, 40 screenshots and payload/DOM in the same environment. Test doubles do not replace existing real API/PG/Redis/runtime/header/auth gates. CSS 200% zoom is a reflow proxy; manual device/account/PWA installation/long-reading acceptance remains open. Evidence and commands: [RF-04](../work-packages/2026-10-22-rf04-presentation.md).
