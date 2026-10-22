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

## Current baseline clarification (2026-10-22)

The [optimization/refactoring plan](../work-packages/2026-10-22-optimization-refactoring-plan.md)
records rechecked, successful CI/Docker/Security runs at
`ca15cadbe1df97adf99150df0c9d3d7bc95e32af`. These are baseline results, not
verification of the uncommitted plan or later code changes. The current
Architecture test only checks module assembly loading; it does not enforce
dependency direction. RF-01 proposes that missing guard. Local Docker was not
rechecked during this documentation-only delivery; the dated environment and
test-count observations below remain historical. Human delivery status is
mirrored in the [Chinese plan](../../docs/engineering/optimization-refactoring-plan-2026-10-22.md).

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
