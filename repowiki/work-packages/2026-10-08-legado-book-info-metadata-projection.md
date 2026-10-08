# Legado book-info metadata projection

Agent read trigger: read when continuing, implementing, verifying, or closing work package 5.80.

Date: 2026-10-08
Status: Accepted
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

## Acceptance and evidence

- TDD red/green: the focused regression first failed to compile because `GetBookSummaryAsync` was absent, then passed after the bounded seam and Legado route were added.
- Focused Catalog/Legado tests: `24/24`; Unit: `626/626`; Architecture: `1/1`; Contract: `12/12`.
- Restore and Release Build: PASS, `0 warnings / 0 errors`; migration model verification: `11/11`; migration script syntax, `git diff --check`, and added-line secret audit: PASS.
- Focused PostgreSQL Integration regression: BLOCKED by local Windows Docker Engine endpoint `npipe://./pipe/docker_engine`; no product assertion failed.
- Candidate implementation SHA `4eb63e5a1a0a95872ec6dc9814ab0fcba126a0f0` passed exact-head [CI 37771778894](https://github.com/nekohands/InkFlow/actions/runs/37771778894), [Docker 37771778862](https://github.com/nekohands/InkFlow/actions/runs/37771778862), and [Security 37771778935](https://github.com/nekohands/InkFlow/actions/runs/37771778935).

## Boundary and handoff

Full aggregate reads remain intentional for book detail and other callers that require the complete model. Local Testcontainers and real-source/manual Legado client acceptance remain separate environment gates. `.workbuddy-ai/` remains untracked and untouched. The package is Accepted; continue with a fresh bounded intake.
