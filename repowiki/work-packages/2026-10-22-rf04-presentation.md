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

Production `9e9f9eb`; 22 canonical-LF complete output hashes, 28 focused tests PASS; four viewport behavior runs, 40 screenshots, 450 isolated desktop samples PASS. Raw data/environment/screenshots: [browser-before](../../docs/engineering/benchmarks/rf04/browser-before/browser.json); human record holds payload/node/p50/p95 table. Measurements use navigation start through script/font readiness and two animation frames, not LCP. 200% CSS zoom is a reflow proxy only. No uncaught JS errors; console/network errors are injected 404/503/401/offline only. The exploratory run overlapping build activity was discarded.

Operations confirmation pre-fills a suggested reason; blank validation explicitly clears it. SW scope `/reader/` excludes `/reader`; offline tests enter scope first. No production behavior changed to accommodate harness assumptions. Browser fixtures do not prove real response headers/auth/storage; existing API runtime and contract gates remain mandatory.
