# Canonical match query bounding

Status: Accepted
Name: Canonical match query bounding
Objective / user outcome: Canonical book matching no longer materializes every canonical book when resolving a normalized title/author pair.
In scope: PostgreSQL-backed `FindByTitleAuthorAsync`, parameterized normalized predicates, deterministic first-match ordering, and focused PostgreSQL regression coverage.
Non-goals: Book identity or matching semantics redesign, public API/Legado contract changes, schema/migration changes, duplicate cleanup, or full-text search.
Assumptions: The existing Book Matcher v1 normalization remains the contract: remove whitespace and compare case-insensitively. The Npgsql repository may use PostgreSQL scalar functions for the bounded read; test doubles keep their existing behavior.
Affected contracts/data/permissions/architecture: Library persistence read path only; `BookId` stability, Canonical/Source separation, module boundaries, ownership, and public response shapes remain unchanged.

Acceptance criteria:

- [x] Matching still ignores whitespace and case and returns the existing stable `BookId`.
- [x] A miss returns `null` without materializing the full `library.books` table.
- [x] The production PostgreSQL query uses parameterized normalized predicates and `LIMIT 1` with deterministic ordering.
- [x] Focused PostgreSQL regression and the applicable Release/Architecture/Unit/Contract/migration checks pass.

Risks and invariants: PostgreSQL expression syntax is provider-specific and must remain parameterized. The query must not create or merge Canonical identities, change title/author normalization, or expose source/private data.

Verification evidence: `dotnet restore InkFlow.sln`; Release solution build passed with 0 warnings / 0 errors; Unit `597/597`; Architecture `1/1`; Contract `12/12`; Windows migration model check `11/11`. The focused local PostgreSQL Integration attempt was BLOCKED by the unavailable Docker named pipe. Exact implementation SHA `2f20125806b1bc3464fc5d2e299d75f5623ad7a0` passed CI `37661014830`, Docker `37661014858`, and Security `37661014826`, including PostgreSQL/runtime smoke. The first candidate's fixture collision was isolated in `2f20125`; no production behavior change was required.

Delivery boundary: no schema/migration, API, Legado, UI, or source-runtime changes. `.workbuddy-ai/` remains user-owned and untracked.
