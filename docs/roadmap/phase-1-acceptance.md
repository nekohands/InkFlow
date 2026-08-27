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

## Release gate

Phase 1 is not complete until:

- automated Contract/E2E tests cover the critical content chain
- at least one real-device or realistic Legado import/read validation has been performed
- the Minimal Web Reader has completed its applicable Mobile/Desktop/UX/Visual/Accessibility acceptance
- the frontend implementation has a recorded benchmark against current comparable reading products
- required Build/Test/Runtime/CI gates from `../engineering/development-workflow.md` are green
