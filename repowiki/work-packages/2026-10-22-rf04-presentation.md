# RF-04 presentation ownership

Audience: Agent-only.
Agent read instruction: Read before Reader baseline, presentation migration, verification or closeout.
Scope: Reader, Account/PWA and Operations browser rendering/resources move from Content.Application to API Presentation, after behavior/browser baselines.

Status: In Progress; baseline commit `9e9f9ebb5476f82ee3e06aaf942d8b423c9c5e67`. User explicitly authorized RF-04 with baseline before migration.

## Contract

Goal: keep content business code free of browser/session/PWA/UI responsibilities and make browser assets separately editable and packaged. Sequence: baseline/export/browser -> Reader -> Account/PWA -> Operations -> full gates. No redesign, new static resource URLs, stack/session change, schema/query changes, new API behavior or RF-05. Content AST and pure content rendering remain in Content; API depends on existing display DTOs. This enforces the accepted direction and needs no new ADR.

Preserve SSR HTML (canonical LF normalization only), text escaping, URLs/IDs/DOM, inline scripts/styles, media/content/status/cache/security headers, session/refresh behavior, PWA scope and cache exclusions, keyboard/focus and layouts. Resources must ship in the assembly, never read source-tree files. Remove migrated Content implementation; no compatibility wrapper.

Baseline source fact: current ReaderClientScript requires a session on non-account pages and redirects to login with safe returnTo; historical anonymous-reader notes are not the current session contract. Backend authorization remains independent. Do not weaken this behavior during migration.

## Verification plan

Freeze deterministic complete output hashes for catalog/search/empty/degraded/XSS, TOC 0/100/1k/10k, short/long/missing chapters, account/register/shelf/history/offline/Operations and PWA assets. Browser: isolated Chromium, fixed localhost fixture responses, Mobile 390x844, Tablet 768x1024, Desktop 1440x900, Wide 1920x1080; keyboard/dialog/focus, settings/navigation/refresh, 200% zoom, long/empty/error, session/401, offline/cache and console/network evidence. Record payload/DOM/render/navigation timings with 3 warmups, 30 samples, 3 rounds for size cases. Structural migration acceptance is exact behavior/output equivalence, not an unmeasured speedup claim.

Fixture browser tests exercise real renderer/browser scripts with deterministic API doubles; they do not prove real authentication/persistence. Existing source-build CI supplies real API/PG/Redis/Runtime/Contract/Security evidence. Local Docker unavailable at intake; DevTools MCP absent, bundled Playwright is the browser fallback. Manual real-device/account/PWA installation/long-reading acceptance remains open.

Inspect diff, restore/build/test, Architecture/Contract/Reading/Reader tests, publish resource independence, frontend/edge/account/content/navigation/Admin/Legado runtime, migration checks and exact-SHA CI/Docker/Security. Mirror affected Wiki and Chinese docs; pending gates prevent Accepted.

## Sources

- [Plan](2026-10-22-optimization-refactoring-plan.md), [architecture](../architecture.md), [verification](../operations/verification.md).
- [Human baseline and delivery record](../../docs/engineering/reader-presentation-migration.md), [frontend standard](../../docs/engineering/frontend-design.md).
- [Fixture exporter](../../tools/InkFlow.ReaderFixtures/ReaderFixture.cs), [Reader endpoints](../../src/Apps/InkFlow.Api/ReaderEndpoints.cs).

## Frozen pre-migration evidence

Production `9e9f9eb`; 22 canonical-LF complete output hashes, 28 focused tests PASS; four viewport behavior runs, 40 screenshots, 450 isolated desktop samples PASS. Raw data/environment/screenshots: [browser-before](../../docs/engineering/benchmarks/rf04/browser-before/browser.json); human record holds payload/node/p50/p95 table. Measurements use navigation start through authentication-shell/font readiness and two animation frames, not LCP. 200% CSS zoom is a reflow proxy only. No uncaught JS errors; console/network errors are injected 404/503/401/offline only. The exploratory run overlapping build activity was discarded.

Operations confirmation pre-fills a suggested reason; blank validation explicitly clears it. SW scope `/reader/` excludes `/reader`; offline tests enter scope first. No production behavior changed to accommodate harness assumptions. Browser fixtures do not prove real response headers/auth/storage; existing API runtime and contract gates remain mandatory.

Increment 1: baseline frozen at `1c258c3`; repeat browser run matched all 40 screenshots. The three partial files share shell members and move ownership atomically to API Presentation; Reader/shared shell assets are extracted first. 22 complete output hashes and all 28 focused tests PASS. No routes, protocols or business queries changed.

Increment 2: Account/session, shelf/history, detail/progress and PWA scripts/manifest/icon are embedded resources; Guid-only progress substitutions preserve escaped HTML and exact output. 28 focused tests including 22 frozen output hashes PASS.

Increment 3: Operations HTML composer stays in API Presentation; styles/script/header are embedded resources. All 28 focused tests and the original 22 complete output hashes PASS. New architecture guards bind Reader ownership and browser resources to API, forbid their return to Content. Candidate verification includes published fixture execution outside the checkout and same-runner before/after browser CI against frozen `1c258c3`.

## Candidate local verification

16 individually editable resources are embedded in API; renderer C# contains page composition/Guid substitution, and migrated Content files are removed. No production dependency/static URL/schema/query change. Restore/Release Build 0 warnings/errors; Unit 660, Architecture 53, Contract 12 PASS. Integration 8 passed / 128 DockerUnavailable / 3 skips; local Runtime BLOCKED pending exact-SHA CI.

Published fixture assembly executed from system temp directory exports 22 cases. Before/after output hashes, 40 PNG hashes, four viewport behavior summaries, payload and DOM are identical; no uncaught JS errors. [After raw 450 samples](../../docs/engineering/benchmarks/rf04/browser-after.json); same screenshots as the archived baseline. Timing differences include browser/system noise and are not a claimed speedup. All extracted JS syntax checks and npm dependency audit PASS. Full remote gates pending.

Migration drift check PASS for all 11 contexts. A focused ContentPolicy persistence test reproduced the local Docker named-pipe failure; the 128 integration failures are not treated as passes.

## Browser gate correction after `17ac880`

CI 39792862294 browser job failed at `long-768`: before screenshot preceded progress acknowledgement, after showed 已同步; all 22 published output manifests matched. Wait for acknowledgement before screenshots. Further local replay of identical output found 9 rounded-edge pixels differing by one RGB level and another 19 edge pixels by up to five levels; two stable frames and disabling GPU did not eliminate quantization.

Final screenshot protocol moves pointer away, waits fonts/progress, then two identical captures; pixel comparison accepts at most 32 changed pixels per image: RGB delta <=1 is quantization, larger deltas must be classified as antialiasing by pixelmatch at zero color threshold. Any other color change, alpha or dimension change fails; no masked regions. Boundary regressions cover 32/33 pixels, delta 1/2, alpha/dimensions. Output/DOM/behavior remain exact; report retains all PNG hashes and per-image changedPixels/maxRgbDelta/materialChanges. Original frozen artifacts are retained unchanged. Docker/Security and consistency budget gates passed for the first candidate; corrected full CI is still required.

Browser tools require Node 24, with Playwright 1.62.1, pngjs 7.0.0 and pixelmatch 7.1.0 pinned as dev-only dependencies. The PNG-byte gate correction is explicit; no production asset or frozen output expectation changed to make tests pass.

Corrected local replay PASS: all four viewports, 40 screenshots, exact output/DOM/behavior and 450 samples per side; [settled before](../../docs/engineering/benchmarks/rf04/browser-settled-before.json) / [settled after](../../docs/engineering/benchmarks/rf04/browser-settled-after.json). Comparator 3 boundary tests and npm audit PASS. Initial validate (real integration/runtime) and consistency jobs passed; only browser gate failed. Full corrected-SHA CI remains required.
