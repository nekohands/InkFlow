# Phase 1 Acceptance Criteria

## Phase 1A — Single Source Vertical Slice

Use one simple, complete Official Source that can exercise Search, BookInfo, TOC, Content and Update primarily through HTTP + HTML/JSON.

The first Official Source should use RuleAdapter to validate DSL v1.

### Required flow

1. Search a book from the Source.
2. Import a `SourceBook`.
3. Create/link a stable `CanonicalBook`.
4. Fetch TOC and create `SourceChapter` records.
5. Create stable `CanonicalChapter` records/mappings.
6. Fetch one or more chapter bodies.
7. Persist FetchArtifact metadata and RawHash.
8. Extract/Sanitize/Normalize into Content AST.
9. Calculate CanonicalHash and Quality v1 evidence.
10. Persist ContentBlob/ContentVersion.
11. Select a current Content Version.
12. Read the book and chapter from Minimal Web Reader.
13. Generate `/legado/book-source.json`.
14. Import the InkFlow source into Legado.
15. Search the same book through Legado.
16. Open BookInfo, TOC and Content through Legado.
17. Scheduler detects a new chapter/update without manual DB editing.
18. CI and Docker baseline stay green.

### Minimal Web Reader UX acceptance

The Phase 1 reader is intentionally small, but it is still a user-facing product surface and must follow `../engineering/frontend-design.md`.

Before implementation:

- review at least 3 active comparable reading products
- record the page goal, useful patterns, rejected patterns, mobile behavior and desktop behavior
- do not copy a competitor page verbatim

Required pages/flows:

```text
Search
→ Book Detail
→ TOC
→ Reader
→ Next / Previous Chapter
```

Acceptance evidence:

### Automated evidence (2026-08-31)

The following automated evidence is complete and is separate from the human/visual
checklist below:

- [x] Browser automation covered the Web Reader public routes, search interaction,
  empty/error states, chapter shell width, keyboard focus and horizontal-overflow
  boundaries at mobile, tablet, desktop and wide-desktop viewports.
- [x] Source-built Compose runtime smoke covered the public Reader/PWA shell,
  manifest, icons, Service Worker registration/activation, offline-shell fallback,
  account/shelf/history state paths and sensitive-data exclusions.
- [x] Source-built Compose runtime smoke covered the protected Operations page,
  anonymous authorization rejection, status/empty/error rendering and the
  authenticated collection/package contract; the collection workbench smoke also
  covered direct URL input, persisted controls, EPUB/TXT/ZIP integrity and download
  availability.
- [x] Unit, Contract and CI runtime gates covered semantic HTML markers,
  `aria-live`/focus/reduced-motion hooks, session-token URL/HTML exclusions and
  stable API response contracts.

Evidence is recorded in `docs/roadmap/progress.md` sections 4.75, 4.82–4.86,
4.97–4.99 and 5.8, with repository entrypoints
`scripts/reader-frontend-runtime-smoke.sh`,
`scripts/reader-account-runtime-smoke.sh`,
`scripts/collection-package-runtime-smoke.sh` and their regressions under
`scripts/tests/`. These checks do not claim real PWA installation, long-duration
reading, real-account behavior or Reading 3.0/MuMu acceptance.

### Human / visual acceptance evidence

- [ ] Search → Book → Read has no unnecessary intermediate step.
- [ ] Book Detail has an obvious `Start/Continue Reading` primary action.
- [ ] TOC can clearly identify the current/latest chapter.
- [ ] Reader prioritizes content and does not expose crawler/source internals by default.
- [ ] Previous/Next chapter and TOC controls are easy to reach.
- [ ] Mobile viewport is manually validated.
- [ ] Desktop viewport is manually validated.
- [ ] Wide desktop does not stretch chapter text to an uncomfortable reading width.
- [ ] Long title / missing cover / long author name edge cases do not break layout.
- [ ] Loading, Empty and Error states exist for the applicable pages.
- [ ] Keyboard focus is visible and primary flows can be operated without a mouse where applicable.
- [ ] Touch controls are large enough for comfortable mobile use.
- [ ] Reader text size/line-height/theme foundation can evolve into the full ReaderPreference model without replacing the entire layout.
- [ ] UI validation evidence is recorded in Progress/Handoff or the corresponding work package/PR.

Phase 1A must not be marked Completed merely because chapter HTML can render.

### 1.0 Frontend scope (mandatory release gate)

All user-visible frontend surfaces already shipped in this repository are part of the 1.0 scope, not post-1.0 polish:

- Web Reader: `/reader`, Book Detail/TOC and Chapter Reader.
- Reader/PWA: account, shelf, history, offline fallback, manifest, service worker and install enhancement.
- Operations Center: protected `/admin/operations` workbench for Operator/Administrator users.

The automated frontend gate must run against the current source-built Compose stack and cover the public HTML shell, responsive/accessibility markers, account/shelf/history/offline pages, Manifest, Service Worker, icons, Operations UI contracts and sensitive-data exclusions. The repository entrypoints are scripts/reader-frontend-runtime-smoke.sh for frontend contracts and scripts/reader-account-runtime-smoke.sh for authenticated Reader state runtime; their fixture/structure regressions live under scripts/tests/.

The automated gate does not replace human acceptance. Before 1.0 is accepted, the frontend still needs Mobile, Tablet, Desktop and Wide Desktop checks, primary-flow UX checks, keyboard/focus/contrast/touch checks, long-reading and chapter-navigation checks, plus real PWA install/offline and authenticated account-flow checks where applicable. Deferred real-device or manual checks remain explicitly `NOT RUN` until executed.
The authenticated Reader state runtime entrypoint is scripts/reader-account-runtime-smoke.sh; its structure regression is scripts/tests/reader-account-runtime-smoke.test.sh. It runs against the same source-built Compose stack and does not replace the remaining PWA page, install, cross-device or real-device acceptance.

### Prohibited shortcuts

- no hand-inserting book/chapter/content rows for acceptance
- no hand-editing the generated official Legado JSON after generation
- no direct third-party URL in the official Legado reading path
- no synchronous third-party fetch as the normal Chapter API path
- no desktop-only Web Reader acceptance
- no UI completion claim without checking actual user interaction and responsive layout

## Phase 1B — Dual Source Canonical Validation

Add a second Official Source containing at least one book also present in Source #1.

### Required evidence

- one CanonicalBook represents both SourceBooks
- same logical chapters map to stable CanonicalChapter identities
- at least one CanonicalChapter has >= 2 SourceChapter candidates
- at least one CanonicalChapter has >= 2 ContentVersion candidates
- Quality Engine selects a preferred version with recorded evidence

### Failover drill

1. Record BookId and ChapterId values.
2. Disable or make Source A unhealthy.
3. Verify Web Book/TOC/Content remain available from valid canonical data/source B.
4. Verify Legado Search/Book/TOC/Content remain available.
5. Verify BookId and ChapterId have not changed.
6. Restore Source A.
7. Verify it returns to health/candidate selection without creating duplicate canonical identities.

The deterministic automated baseline now covers capability-specific health transitions,
health-aware Content selection, selection evidence, keeping the stored current version when
all candidates are unavailable, and recovery after Source A becomes available again. A real
Official Source pair and the Web/Legado runtime steps above are still required for release acceptance.

The Web UI must present failover as a stable reading experience. A normal user should not need to understand `ContentVersion`, `SourceChapter` or internal source IDs to keep reading. Advanced source selection, if exposed, must remain secondary to the normal Auto path.

### Quality failure drill

Make Source A return or replay a deliberately truncated/low-quality chapter fixture. The Quality Engine must reject or de-prioritize it rather than silently replacing a better selected version.

The deterministic drill is now executable through `ensure-quality-failure-catalog` and
`scripts/quality-failure-runtime-smoke.sh`: the fixture persists a complete and a deliberately
truncated replay through the real publishing/quality/selection services, asserts that the
complete version has the higher score and remains selected, then verifies the selected content
through the Web API, Legado API, and Reader HTML. This closes the deterministic quality-failure
gate; it does not replace the real Official Source and manual/real-device acceptance gates below.

## Release gate

Phase 1 is not complete until:

- automated Contract/E2E tests cover the critical content chain
- at least one real-device or realistic Legado import/read validation has been performed
- the 1.0 frontend scope has completed its applicable Mobile/Tablet/Desktop/Wide Desktop, UX, Visual and Accessibility acceptance
- the frontend implementation has a recorded benchmark against current comparable reading products
- required Build/Test/Runtime/CI gates from `../engineering/development-workflow.md` are green
