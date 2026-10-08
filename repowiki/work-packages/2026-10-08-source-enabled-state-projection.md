# Work Package 5.77 — Source enabled-state projection

## Intake

- Status: Accepted.
- Objective: keep source-health gating and registered code-adapter lookup from
  loading and deserializing a complete `Source` aggregate when they only need
  source existence and `IsEnabled`.
- Scope: add a nullable enabled-state read to `ISourceRepository`; implement a
  source-table scalar projection in `EfSourceRepository`; route
  `SourceHealthService` and the registered-code-adapter branch of
  `SourceAdapterFactory` through it; add focused Unit and PostgreSQL regressions.
- Non-goals: rule-based adapter loading, source lifecycle writes, Rule DSL
  validation or execution, source paging, health-state transitions, public
  API/Legado contracts, Schema/Migration, caching, credentials, or
  `.workbuddy-ai/`.
- Acceptance:
  - enabled, disabled, and missing sources preserve existing decisions;
  - health gating and registered code-adapter lookup do not call the full
    aggregate read in production paths;
  - the EF query projects only `sources.IsEnabled`, propagates cancellation,
    and keeps full `GetAsync` for callers that need `RuleDsl` or write state;
  - rule-based adapter lookup still loads the full source exactly as before.
- Risks: a missing source must remain distinguishable from a disabled source;
  changing the rule-adapter branch could add a second query or lose the DSL, so
  it remains outside this slice.
- Verification plan: TDD red/green focused tests; full Unit,
  Architecture, Contract, restore, Release Build, migration model check,
  applicable Integration/Runtime, diff/secret audit, and exact-head CI,
  Docker, and Security gates.

## Delivery

- Implementation: `ISourceRepository.GetEnabledAsync` defaults to a compatible
  full-read fallback; `EfSourceRepository` overrides it with an `AsNoTracking`
  `sources.IsEnabled` scalar projection. `SourceHealthService` and the
  registered CodeAdapter branch use the projection; RuleBased Adapter keeps
  the existing full `GetAsync` path.
- Regression: TDD red state proved both high-frequency paths still performed a
  full read; focused SourceAdapterFactory and SourceCapabilityHealth tests are
  `14/14` after the fix. Enabled, disabled, missing, cancellation, SQL
  projection, and RuleBased full-read behavior are covered.
- Local verification: Unit `624/624`, Architecture `1/1`, Contract `12/12`,
  restore, Release Build `0 warnings / 0 errors`, 11-context migration model
  check, `git diff --check`, and secret audit `0` hits passed. Integration was
  `8 passed / 3 skipped / 126 blocked`; the PostgreSQL regression compiled but
  local Testcontainers execution is blocked by Windows Docker Engine
  `npipe://./pipe/docker_engine`.
- Remote gates: implementation SHA
  `b753a8bc207fda442148f0ab70af9ffcb2826bba` passed exact-head [CI
  37759082085](https://github.com/nekohands/InkFlow/actions/runs/37759082085),
  [Docker 37759082091](https://github.com/nekohands/InkFlow/actions/runs/37759082091),
  and [Security 37759082083](https://github.com/nekohands/InkFlow/actions/runs/37759082083).
  CI supplied migration, PostgreSQL/Redis, runtime smoke, SLO, backup/restore,
  and diagnostics evidence; Docker and Security completed successfully.
- Boundary: no public Contract, Schema/Migration, cache, credential, or
  `.workbuddy-ai/` change.
