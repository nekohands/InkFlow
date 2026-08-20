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

### Prohibited shortcuts

- no hand-inserting book/chapter/content rows for acceptance
- no hand-editing the generated official Legado JSON after generation
- no direct third-party URL in the official Legado reading path
- no synchronous third-party fetch as the normal Chapter API path

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

### Quality failure drill

Make Source A return or replay a deliberately truncated/low-quality chapter fixture. The Quality Engine must reject or de-prioritize it rather than silently replacing a better selected version.

## Release gate

Phase 1 is not complete until automated Contract/E2E tests cover the critical chain and at least one real-device or realistic Legado import/read validation has been performed.
