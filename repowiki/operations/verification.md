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

[RF-01](../work-packages/2026-10-22-rf01-architecture-guards.md) implements compiled
dependency, project graph and inventory guards. Local Restore/Release Build,
Architecture 51/51, Unit 648/648, Contract 12/12 and model drift 11/11 passed.
Full Integration ran: 8 passed, 128 DockerUnavailable failures, 3 skips (two
opt-in live cases and Redis configuration). `docker` is absent from PATH and
Testcontainers cannot connect to `npipe://./pipe/docker_engine`. Candidate
`34cfa8f` passed the remote Test step and Security; CI runtime is still running,
while Docker failed on Collector Go stdlib CVE-2026-111943. A minimal Go 1.26.10
build-toolchain patch requires new candidate gates. Baseline `ca15cad` runs are not RF-01 evidence.
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
