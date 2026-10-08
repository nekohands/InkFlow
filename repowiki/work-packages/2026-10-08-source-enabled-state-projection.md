# Work Package 5.77 — Source enabled-state projection

## Intake

- Status: In Progress.
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

Implementation and verification evidence will be recorded here before the
package is marked Accepted.
