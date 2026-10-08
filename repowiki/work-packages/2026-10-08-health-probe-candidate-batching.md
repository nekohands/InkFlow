# 5.70 Health-probe candidate batching

Status: Accepted

## Objective

Keep the scheduled health probe bounded as the number of unhealthy
`(SourceId, Capability)` rows grows. Each ten-minute scheduler tick should
read and inspect only one deterministic page, while an in-process cursor lets
later ticks eventually visit the remaining candidates.

## Evidence gap

`HealthProbeService.ProbeDueAsync` calls `ISourceHealthRepository.ListUnhealthyAsync`,
and the EF implementation materializes every unhealthy capability row before
the service filters cooldowns. Source and capability cardinality can therefore
turn one scheduler tick into an unbounded read and probe fan-out.

## Scope

- Add a stable `(SourceId, Capability)` keyset page contract for unhealthy
  health rows, with a bounded page and `HasMore`/cursor result.
- Use a maximum of 100 candidates per health-probe batch and keep the cursor
  in `HealthProbeBackgroundService`, resetting it after the last page.
- Preserve cooldown checks, adapter lookup, Toc sample selection, health
  recording, cancellation, result shape, safe failure reasons, and existing
  `ProbeDueAsync` callers.
- Add focused unit and PostgreSQL repository regressions and synchronize the
  source-runtime and delivery documentation.

## Non-goals

- No durable cursor, schema, migration, public API/Legado JSON, health policy
  or cooldown algorithm change.
- No due-candidate SQL expression, probe concurrency, rate-limit policy,
  source rotation, live-source/manual acceptance, or unrelated repository
  caller change.

## Acceptance

1. Production health-probe scheduling never materializes the complete unhealthy
   health table; each batch reads at most 100 candidates plus one look-ahead row.
2. The production query filters `Unhealthy`, orders by `(SourceId, Capability)`,
   applies the keyset cursor, and returns a deterministic next cursor.
3. The scheduler advances the in-process cursor only after a successful batch
   and resets it at the final page; all existing due/skip/probe/recording and
   cancellation semantics remain unchanged.

## Risks and boundaries

The cursor is intentionally process-local: a restart replays from the first
page, and a candidate whose key is before the current cursor may wait until the
next scan cycle. A page may contain not-yet-due rows, so the fixed ceiling is a
candidate-read/probe-fan-out bound rather than a database-side due filter.

## Verification plan

- Red/green HealthProbeService regression proving a batch is bounded and its
  cursor advances, plus existing behavior regression.
- PostgreSQL repository regression for source/capability order, keyset
  continuation, look-ahead, and cancellation.
- Full Unit, Architecture, Contract, Release Restore/Build, migration model
  check, diff/secret audit, applicable Integration/Runtime, and exact-SHA
  CI/Docker/Security verification.

## Implementation

- `ISourceHealthRepository` now exposes `SourceHealthPage` and the stable
  `(SourceId, Capability)` cursor; the EF repository filters `Unhealthy`,
  applies the keyset predicate, orders deterministically, and reads `limit + 1`.
- `HealthProbeService.ProbeDueBatchAsync` caps scheduled candidates at 100 and
  keeps the existing full-list `ProbeDueAsync` behavior for other callers.
- `HealthProbeBackgroundService` advances an in-process cursor only after a
  successful batch and resets it after the last page. No schema, migration, or
  public protocol changed.
- Source Runtime and architecture Wiki constraints now document the bounded
  candidate page, look-ahead, restart replay, and non-due-page boundary.

## Verification and delivery evidence

- TDD red: the new batch regression first failed to compile because the batch
  API did not exist; green focused HealthProbeService: `7/7`.
- `dotnet restore InkFlow.sln`: PASS; Release Build: PASS, `0 warnings / 0
  errors`; Unit `617/617`; Architecture `1/1`; Contract `12/12`.
- Windows-equivalent migration model check: `11/11` PASS; `wsl.exe bash -n
  scripts/verify-migrations.sh`: PASS; `git diff --check` and changed-file
  secret scan: PASS.
- Full Integration attempted: `130` total, `8 passed / 3 skipped / 119
  blocked` by unavailable Windows Docker Engine `npipe://./pipe/docker_engine`;
  the new PostgreSQL regression compiled but could not start Testcontainers.
- Initial candidate `16b34e94bb5fb0214c2765fa2bad79604c5bc603` correctly exposed
  a shared-container fixture assumption in CI; `6acb91910db8eb90fb3ea06cfc80031bd4cfbaaf`
  isolates the regression with a keyset start cursor and passed the full gate.
- Exact final SHA `6acb91910db8eb90fb3ea06cfc80031bd4cfbaaf` passed CI
  `37730186762` (`Unit 617/617`, `Architecture 1/1`, `Contract 12/12`,
  PostgreSQL Integration `127 passed / 3 skipped`, Compose/runtime/Redis/
  backup/diagnostics), Docker `37730186799`, and Security `37730186829`; all
  three runs report the same head SHA.

## Accepted boundary

The scheduled path is bounded and remote runtime evidence is green. The cursor
remains process-local, restarts replay the first page, pages may contain
not-yet-due rows, and no schema, migration, public protocol, due SQL policy,
probe concurrency/rate-limit policy, source rotation, or live-source acceptance
was added. `.workbuddy-ai/` remains untracked and untouched.
