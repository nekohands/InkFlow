# Module refactor — responsibility seams and extension points

Audience: Agent-only.
Agent read instruction: Read when continuing the 2026-10-15 module refactor, changing Sources/Rule Runtime or later module batches, or verifying its commits.
ID: module-refactor-2026-10-15
Status: Batch 2 pushed; batch 3 planned
Owner: InkFlow maintainers
Authorization: explicit user request on 2026-10-15 to comprehensively refactor by module, clarify responsibilities, reduce duplication, improve extensibility, and test/commit/push every batch.
Record mode: FULL (cross-module refactoring with public-contract and security regression gates).
Parent/root: [Work packages](README.md) / [RepoWiki](../README.md).

## Objective and boundaries

Improve maintainability through responsibility-centered seams and shared implementations, not file-only moves. Preserve the modular-monolith direction, public routes/DTOs/policies, domain invariants, Source DSL safety limits, persistence schema/migrations, task timing/lease semantics, dependency versions, UI behavior, and user-owned `.workbuddy-ai/`.

Each batch must leave the repository buildable, run affected tests plus the relevant project gates, receive a descriptive commit, and be pushed to `origin/dev` with local and remote SHA verification. A batch may add an internal seam only when it removes real duplicated behavior or supports an existing variation; no speculative plugin framework.

## Batch plan

1. **Sources list projection** — centralize bounded list iteration, selector mapping, cancellation and result-byte/item budgets shared by rule Search and TOC capabilities. Preserve output and fail-closed limits.
2. **Sources selector languages** — separate CSS, HTML-XPath and JSONPath implementations behind the existing selector interface; retain bounded parsers and add only real language-level seams.
3. **Sources rule execution** — isolate request construction/continuation and response/session handling from the orchestration path after batch 1/2 evidence is green.
4. **Crawling/Content application** — inspect and consolidate repeated task outcome/projection orchestration only where call graphs and tests prove a shared responsibility.
5. **Reading/Library/Reader** — split policy/read-model concerns where current large modules mix ownership, visibility and presentation mapping; no UI or contract redesign in this package.

Do not start a later batch while the affected earlier batch is uncommitted or has a failing required gate. Replan the package if a batch needs a public contract, schema, security policy, dependency or architecture-direction change.

## Baseline and evidence

Baseline target: `9e10b1bbacc81e00fa0631cb2023d0ac84a65b09` on `dev`, pushed to `origin/dev`; prior host-composition refactor is in `9abffab`, with delivery evidence in its preceding docs commit. Baseline local non-Docker gates: Release Build 0 warnings/errors; Unit 630/630; Architecture 1/1; Contract 12/12; migration models 11/11. Local Integration/Compose remain blocked by unavailable Windows Docker Engine. The exact baseline CI run for the preceding refactor had CI still running when this package resumed; its Docker gate failed at the pre-existing OTLP image scan and Security completed successfully. Rebind all final evidence to each batch SHA.

## Acceptance criteria

- AC-1: Each changed module has a small, explicit interface/seam and one responsibility per implementation; duplication removed is named and covered by existing or new behavior tests.
- AC-2: Public behavior, authorization, validation, resource limits, error mapping, cancellation, persistence and scheduling semantics remain equivalent unless a later batch is explicitly replanned.
- AC-3: Every batch has local Build/affected Tests/Regression evidence, an inspected diff/secret/dependency boundary, an atomic commit and an `origin/dev` SHA match.
- AC-4: Applicable Architecture/Contract/Integration/Runtime/Security/CI results remain truthful; Docker and host blockers are not converted to passes.
- AC-5: RepoWiki module navigation, work-package evidence and current delivery pointers stay synchronized after each batch.

## Current batch: Sources selector languages

Design: `RuleSelectorEvaluator` retains common input validation and language dispatch, while internal CSS, HTML/XML XPath and JSONPath evaluators each own one bounded language implementation. The existing public `ISelectorEvaluator`, `RuleSelectorEvaluator` and `CssSelectorEvaluator` contracts remain unchanged. Shared document/match/depth limits live in one internal policy type; no new public interface or dependency.

Affected source/test paths: `src/Modules/InkFlow.Modules.Sources/Infrastructure/RuleSelectorEvaluator.cs`, `CssSelectorEvaluator.cs`, new `HtmlXPathSelectorEvaluator.cs`, `JsonPathSelectorEvaluator.cs`, `ISelectorLanguageEvaluator.cs`, `SelectorEvaluationLimits.cs`, plus existing selector, adapter, and source credential tests.

Next action: inspect the staged selector write set, commit and push this batch, then rebind the evidence to the pushed SHA before starting RuleAdapter execution refactoring.

## Batch 1 result — delivered

- `RuleBasedSourceAdapter` is 367 lines (baseline 406); the new `RuleListProjector` is 55 lines. Search and TOC no longer duplicate response-body traversal, selector invocation, cancellation checks, or result byte/item budget enforcement. Their distinct ID/title/default-author mapping remains local and explicit.
- Focused Sources regressions: 37/37 passed (`RuleBasedSourceAdapterPaginationTests`, `LinovelibSourceDefinitionTests`, `RuleSelectorEvaluatorTests`, `SourceCredentialTests`). Full Unit: 630/630 passed. Sources and solution Release Builds passed with 0 warnings/errors.
- The public constructor and `ISourceAdapter` contract are unchanged. No schema, migration, dependency, policy, route, external request, or scheduling behavior changed. Existing deadline, result-size/item fail-closed tests cover the shared seam.
- Delivery: commit `34a0315007551d9ed8333113c7f6729c0fb9cf9a` is pushed to `origin/dev`; local and remote SHA matched. GitHub Actions lookup for this SHA is temporarily blocked by the GitHub API rate limit and remains `NOT CHECKED`, not green by inference.

## Batch 2 result — delivered; CI pending

- `RuleSelectorEvaluator` is reduced from 1,458 lines to a 52-line dispatcher. HTML/XML XPath (981 lines) and JSONPath (458 lines) now have separate internal evaluators; CSS keeps its public compatibility class. The internal language seam is 19 lines and shared limits are defined once in `SelectorEvaluationLimits` (11 lines), removing the former mixed-language responsibility without adding a plugin framework.
- Focused selector/source regressions: 93/93 passed. Full Unit: 630/630; Architecture: 1/1; Contract: 12/12. Restore completed with all projects current; final solution Release Build passed with 0 warnings/errors.
- The bounded parser implementations and fail-closed document/match/depth limits were preserved. No route, DTO, public interface, authorization, schema/migration, dependency, network, or scheduling behavior changed. Local persistence Integration and Compose Runtime remain not rerun because this batch is internal Sources parsing only; the known Windows Docker Engine blocker remains documented.
- Diff check passed; no secrets or dependency changes were introduced. Delivery commit `49e3515996431c869558fd56d1be650a4e51ed76` is pushed to `origin/dev`; local and remote SHA matched. Exact-head GitHub Actions runs are pending: CI `38692401199`, Docker `38692401166`, Security `38692401205`. Pending is not treated as green.

Next batch: isolate the existing `RuleAdapter` request construction/continuation and response/session responsibilities only where its call graph and regression tests show a real seam, after the pending CI results are checked.

## Sources

- [Sources module](../../src/Modules/InkFlow.Modules.Sources/)
- [RuleBasedSourceAdapter](../../src/Modules/InkFlow.Modules.Sources/Application/RuleBasedSourceAdapter.cs)
- [RuleListProjector](../../src/Modules/InkFlow.Modules.Sources/Application/RuleListProjector.cs)
- [RuleSelectorEvaluator](../../src/Modules/InkFlow.Modules.Sources/Infrastructure/RuleSelectorEvaluator.cs)
- [CSS selector evaluator](../../src/Modules/InkFlow.Modules.Sources/Infrastructure/CssSelectorEvaluator.cs)
- [HTML/XML XPath evaluator](../../src/Modules/InkFlow.Modules.Sources/Infrastructure/HtmlXPathSelectorEvaluator.cs)
- [JSONPath evaluator](../../src/Modules/InkFlow.Modules.Sources/Infrastructure/JsonPathSelectorEvaluator.cs)
- [Selector language seam](../../src/Modules/InkFlow.Modules.Sources/Infrastructure/ISelectorLanguageEvaluator.cs)
- [Selector evaluation limits](../../src/Modules/InkFlow.Modules.Sources/Infrastructure/SelectorEvaluationLimits.cs)
- [Rule selector regressions](../../tests/InkFlow.UnitTests/RuleSelectorEvaluatorTests.cs)
- [Rule adapter regressions](../../tests/InkFlow.UnitTests/RuleAdapterTests.cs)
- [Rule-based adapter pagination regressions](../../tests/InkFlow.UnitTests/RuleBasedSourceAdapterPaginationTests.cs)
- [Linovelib source definition regressions](../../tests/InkFlow.UnitTests/LinovelibSourceDefinitionTests.cs)
- [Source credential regressions](../../tests/InkFlow.UnitTests/SourceCredentialTests.cs)
