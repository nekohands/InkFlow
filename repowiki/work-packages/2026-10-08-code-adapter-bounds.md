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
  - [x] happy path: existing Kanunu8 GB18030 book/TOC/content parsing and 17K JSON book/TOC/free-content parsing remain valid through existing fixtures.
  - [x] important error path: Kanunu8 and 17K responses over MaxBytes are rejected before decode/parse; Kanunu8 static regexes use the configured finite timeout and timeout returns no partial result.
  - [x] regression/compatibility: SSRF/host/identifier/VIP boundaries, adapter IDs, response shapes, RuleAdapter budgets, and persistence remain unchanged.
Risks and invariants: A shared reader must preserve Content-Length and streaming overflow rejection; code adapters must use the same registered limits as RuleAdapter. Oversized/timeout failures must not expose partial data.
Verification plan:
  - [x] diff/secrets/scope audit
  - [x] build: `dotnet restore InkFlow.sln`; `dotnet build InkFlow.sln -c Release --no-restore` — 0 warnings / 0 errors
  - [x] tests: focused adapter/HTTP bounds `13/13`; full Unit `595/595`, Architecture `1/1`, Contract `12/12`
  - [x] runtime/integration: remote CI runtime smoke and affected source fixture paths passed; local integration is BLOCKED by unavailable Docker named pipe
  - [x] security/architecture: bounded stream, configured regex timeout, SSRF/host preservation, no secret/logging changes, Architecture tests
  - [x] project-defined gates/phase exit: Source runtime budget gate PASS; no phase exit change
  - [x] manual/real-environment acceptance: N/A for this bounded internal adapter slice
  - [x] CI: target commit's required CI/Docker/Security jobs GREEN
Documentation/state updates: update Source runtime contract, RepoWiki index/work package, Progress and Handoff at closeout.
Affected Wiki pages and source/test evidence: repowiki/README.md; this record; SourceRuleExecutionLimits; ProductionSafeSourceHttpClient; KanunuSourceAdapter; SeventeenKSourceAdapter; adapter/HTTP unit tests.
Affected Chinese human pages: docs/architecture/source-runtime.md; docs/roadmap/progress.md; docs/handoff/handoff.md.
```

## Verification Matrix

```text
Surface | Required check | Evidence source | Command/evidence | Result | Notes
Build/restore | production build | local/CI | Restore; InkFlow.sln Release | PASS | 0 warnings / 0 errors
Logic/parser | CodeAdapter happy/error regressions | local | focused adapter/HTTP tests `13/13`; full Unit `595/595` | PASS | oversized bodies and configured regex timeout covered
Boundary | module/architecture checks | local/CI | Architecture `1/1` | PASS | no dependency direction change
Persistence/infra | migration/integration | local/CI | migration model check; Integration `8 passed / 3 skipped / 113 blocked` locally | PASS / BLOCKED | all local Integration failures are Docker named-pipe environment failures; remote CI covered PostgreSQL and backup/restore
External contract | adapter fixture compatibility | fixture/CI | Contract `12/12`; existing Kanunu8/17K fixtures | PASS | no public contract change
Runtime/business path | source runtime smoke | CI | CI `37650394342` runtime and smoke steps | PASS | remote container runtime evidence
Manual/real environment | real external source | manual/live | N/A | N/A | no live source required for this slice
UI/browser | UI | N/A | N/A | N/A | backend adapter only
Security | response/regex/resource bounds | local/CI | staged secret/scope review; Security `37650394257` | PASS | no secret/logging change
Project-defined gate/state | Source runtime budget gate | local/CI | bounded reader, regex timeout, Architecture `1/1` | PASS | no phase exit change
Documentation/state | both audiences synchronized | local | Source runtime, RepoWiki, Progress, Handoff, workflow profile | PASS | final closeout commit
CI | target commit workflows | CI | CI `37650394342`; Docker `37650394297`; Security `37650394257` | GREEN | all exact head SHA `0d7d5ce426b0b42840505168bedf07aa2367df2a`
RepoWiki sync | Wiki/source/human agreement | local | affected pages and source evidence | PASS | claims match implementation and gates
```

## Delivery Record

```text
Work package: Accepted
Implementation: Complete in code candidate `0d7d5ce`.
Acceptance: PASS — successful fixture parsing preserved; oversized responses fail before decode/parse; Kanunu8 regexes use the configured finite timeout; compatibility boundaries remain unchanged.
Build: PASS — Restore; Release Build 0 warnings / 0 errors.
Tests: PASS — focused `13/13`; Unit `595/595`; Architecture `1/1`; Contract `12/12`; migration model check `11/11`. Local Integration attempted but BLOCKED by `npipe://./pipe/docker_engine`.
Runtime: PASS — remote CI runtime/Compose smoke; local Docker runtime BLOCKED by the same named pipe.
Security: PASS — staged scope/secret review; remote Security GREEN.
Project-defined gates/phase exit: PASS — source runtime budget gate; no phase exit change.
CI: GREEN — CI/Docker/Security all success on exact head `0d7d5ce426b0b42840505168bedf07aa2367df2a`.
Findings: Kanunu8 uses GetByteArrayAsync and static Regex without a timeout; 17K uses ReadAsStringAsync without the shared response byte budget. RuleAdapter already has bounded reads and regex timeouts.
Fixed: Added shared streaming `SourceResponseReader`; reused it in ProductionSafeSourceHttpClient, Kanunu8 and 17K; applied configured response/regex limits and timeout-safe empty/failure behavior; added focused regressions and synchronized source-runtime docs.
Remaining risks/blockers: Local Testcontainers/runtime remains unavailable; remote CI supplied PostgreSQL/runtime evidence. Real upstream/live-source and Release Candidate manual acceptance remain outside this slice.
Commit/PR: Code candidate `0d7d5ce`; closeout documentation is the final commit on `dev`.
Documentation/state sync: Source runtime contract, RepoWiki architecture/index/work package, workflow profile, Progress and Handoff synchronized.
RepoWiki sync: PASS — affected Wiki/source/runtime and human pages agree with implementation and evidence.
Evidence limitations: No live external source is required; fixture evidence does not replace the remote runtime evidence. Local container evidence remains unavailable.
Next step: Re-intake the optimistic-concurrency candidate; do not expand this slice into Rule DSL redesign or live-source acceptance.
```
