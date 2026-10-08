# Legado book-info metadata projection

Agent read trigger: read when continuing, implementing, verifying, or closing work package 5.80.

Date: 2026-10-08
Status: In Progress
Package: 5.80
Owner: InkFlow maintainers
Authority: user-requested continuous repository progress and push workflow, constrained by `AGENTS.md` and the project delivery profile.

## Objective

Make the Legado `BookInfo` read path bounded to canonical book identity and metadata. It must not materialize all canonical chapters merely to return the title, author, and TOC URL.

## Scope

- Add a Catalog application seam that reads an existing `CanonicalBookSummary` after the existing takedown check.
- Route `LegadoContractService.GetBookAsync` through that bounded seam.
- Preserve the full `CatalogQueryService.GetBookAsync` aggregate path for book detail and other intentional callers.
- Add a regression proving the Legado BookInfo path does not use a full-book read; keep the existing canonical summary SQL evidence authoritative.

## Non-goals

No public Legado JSON or URL change, no BookId/ChapterId change, no TOC or content-selection change, no schema/migration, cache, permission, or `.workbuddy-ai/` change, and no real-source/manual Legado client acceptance claim.

## Acceptance criteria

- AC-1: BookInfo fields, TOC URL, route-prefix validation, missing-book behavior, takedown behavior, and cancellation remain unchanged.
- AC-2: The production BookInfo path does not call full `ICanonicalBookRepository.GetAsync`; it uses the existing summary projection boundary.
- AC-3: The full aggregate `CatalogQueryService.GetBookAsync` path remains unchanged for book detail, and existing Unit/Contract/Release/CI gates remain applicable.

## Risk and verification plan

Low blast radius and reversible application read-path change. Public contract and persistence schema are frozen. Use TDD red/green, focused Catalog/Legado Unit tests, full Unit/Architecture/Contract, Restore/Release Build, migration model verification, script/diff/secret audits, applicable Integration/Runtime evidence, then exact-head CI/Docker/Security before Accepted.
