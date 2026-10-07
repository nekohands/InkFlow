# Code adapter response and regex bounds

RepoWiki index: [README.md](../README.md)
Workflow profile: [PROJECT_WORKFLOW.md](../../docs/delivery/PROJECT_WORKFLOW.md)
Related feature/module docs: [Source runtime](../architecture.md); [Source runtime contract](../../docs/architecture/source-runtime.md); [Security model](../../docs/architecture/security-model.md)

## Work Package

```text
Name: Code adapter response and regex bounds
Objective / user outcome: Ensure trusted CodeAdapters cannot allocate unbounded response bodies or run static extraction regexes without a finite timeout.
In scope: Shared bounded HttpContent reader; ProductionSafeSourceHttpClient reuse; Kanunu8 and 17K response limits; Kanunu8 regex timeout; focused adapter regression tests and source-runtime documentation.
Non-goals: Rule DSL selector redesign, new adapters, source URL/host policy changes, retry policy, public ISourceAdapter contract changes, Schema/Migration, or live external-source acceptance.
Assumptions: Existing SourceRuleExecutionLimits.MaxBytes and MaxRegexTime are the single policy; the default 2 MiB response and 2 second regex ceilings remain unchanged.
Affected modules/files: Sources Application/Infrastructure; Kanunu8 and SeventeenK adapters; Unit tests; RepoWiki and Chinese source-runtime/delivery pages.
Affected contracts/data/permissions/architecture: Internal adapter construction gains an optional limits dependency; ISourceAdapter method signatures and source identity contracts remain unchanged. No database or module-boundary change.
Affected invariants/decision records: Source outbound requests retain SSRF and allowed-host checks; paid 17K content remains fail-closed; bounded reads happen before decoding/parsing; no ADR is needed because this enforces the existing execution-budget direction.
Affected project-defined gates or phase/release exit criteria: Source runtime security/budget gate; Release Build, Unit, Architecture, Contract, migration, remote runtime and Security evidence.
Expected evidence sources: focused and full local tests/build; remote CI/Docker/Security; source-runtime docs and diff/security review.
Grillme decisions resolved: standard intake; target = custom CodeAdapter resource bounds; outcome = preserve successful parsing while rejecting oversized responses and bounding regex execution; no public contract redesign.
Acceptance criteria:
  - [ ] happy path: existing Kanunu8 GB18030 book/TOC/content parsing and 17K JSON book/TOC/free-content parsing remain valid.
  - [ ] important error path: Kanunu8 and 17K responses over MaxBytes are rejected before decode/parse; Kanunu8 static regexes have a finite timeout and timeout cannot hang the adapter.
  - [ ] regression/compatibility: SSRF/host/identifier/VIP boundaries, adapter IDs, response shapes, RuleAdapter budgets, and persistence remain unchanged.
Risks and invariants: A shared reader must preserve Content-Length and streaming overflow rejection; code adapters must use the same registered limits as RuleAdapter. Oversized/timeout failures must not expose partial data.
Verification plan:
  - [ ] diff/secrets/scope audit
  - [ ] build: `dotnet restore InkFlow.sln`; `dotnet build InkFlow.sln -c Release --no-restore`
  - [ ] tests: focused Kanunu8/17K/HTTP bounds tests; full Unit, Architecture, Contract
  - [ ] runtime/integration: remote CI runtime smoke and affected source fixture paths; local integration if Docker is available
  - [ ] security/architecture: bounded stream, regex timeout, SSRF/host preservation, no secret/logging changes, Architecture tests
  - [ ] project-defined gates/phase exit: record source-runtime gate; no phase exit change
  - [ ] manual/real-environment acceptance: N/A for this bounded internal adapter slice
  - [ ] CI: target commit's required CI/Docker/Security jobs
Documentation/state updates: update Source runtime contract, RepoWiki index/work package, Progress and Handoff at closeout.
Affected Wiki pages and source/test evidence: repowiki/README.md; this record; SourceRuleExecutionLimits; ProductionSafeSourceHttpClient; KanunuSourceAdapter; SeventeenKSourceAdapter; adapter/HTTP unit tests.
Affected Chinese human pages: docs/architecture/source-runtime.md; docs/roadmap/progress.md; docs/handoff/handoff.md.
```

## Verification Matrix

```text
Surface | Required check | Evidence source | Command/evidence | Result | Notes
Build/restore | production build | local/CI | InkFlow.sln Release | NOT RUN | intake stage
Logic/parser | CodeAdapter happy/error regressions | local | focused adapter tests | NOT RUN | intake stage
Boundary | module/architecture checks | local/CI | Architecture tests | NOT RUN | no dependency direction change planned
Persistence/infra | migration/integration | local/CI | migration check and affected integration | NOT RUN | no schema change planned
External contract | adapter fixture compatibility | fixture/CI | existing Kanunu8/17K tests | NOT RUN | no public contract change
Runtime/business path | source runtime smoke | CI | required CI runtime smoke | NOT RUN | intake stage
Manual/real environment | real external source | manual/live | N/A | N/A | no live source required for this slice
UI/browser | UI | N/A | N/A | N/A | backend adapter only
Security | response/regex/resource bounds | local/CI | focused tests + Security workflow | NOT RUN | intake stage
Project-defined gate/state | Source runtime budget gate | local/CI | profile gate evidence | NOT RUN | intake stage
Documentation/state | both audiences synchronized | local | affected-page diff/link check | NOT RUN | closeout stage
CI | target commit workflows | CI | CI/Docker/Security | NOT TRIGGERED | intake stage
RepoWiki sync | Wiki/source/human agreement | local | affected pages and source evidence | NOT RUN | closeout stage
```

## Delivery Record

```text
Work package: In Progress
Implementation: NOT COMPLETE
Acceptance: NOT RUN
Build: NOT RUN
Tests: NOT RUN
Runtime: NOT RUN
Security: NOT RUN
Project-defined gates/phase exit: NOT RUN
CI: NOT TRIGGERED
Findings: Kanunu8 uses GetByteArrayAsync and static Regex without a timeout; 17K uses ReadAsStringAsync without the shared response byte budget. RuleAdapter already has bounded reads and regex timeouts.
Fixed: None yet.
Remaining risks/blockers: Local Docker may block Testcontainers/runtime; remote CI is required for PostgreSQL/runtime evidence. Preserve existing source host, SSRF, and VIP behavior.
Commit/PR: None yet.
Documentation/state sync: Intake created; closeout pages remain unchanged until evidence exists.
RepoWiki sync: PASS — intake scope, affected source/runtime pages, and non-goals are recorded; no source or human contract claim changed yet.
Evidence limitations: No live external source is required; fixture evidence cannot replace runtime evidence.
Next step: Add failing oversized-response and bounded-regex regression tests, then implement the shared reader and smallest adapter changes.
```
