# Canonical match query bounding

Status: In Progress
Name: Canonical match query bounding
Objective / user outcome: Canonical book matching no longer materializes every canonical book when resolving a normalized title/author pair.
In scope: PostgreSQL-backed `FindByTitleAuthorAsync`, parameterized normalized predicates, deterministic first-match ordering, and focused PostgreSQL regression coverage.
Non-goals: Book identity or matching semantics redesign, public API/Legado contract changes, schema/migration changes, duplicate cleanup, or full-text search.
Assumptions: The existing Book Matcher v1 normalization remains the contract: remove whitespace and compare case-insensitively. The Npgsql repository may use PostgreSQL scalar functions for the bounded read; test doubles keep their existing behavior.
Affected contracts/data/permissions/architecture: Library persistence read path only; `BookId` stability, Canonical/Source separation, module boundaries, ownership, and public response shapes remain unchanged.

Acceptance criteria:

- [ ] Matching still ignores whitespace and case and returns the existing stable `BookId`.
- [ ] A miss returns `null` without materializing the full `library.books` table.
- [ ] The production PostgreSQL query uses parameterized normalized predicates and `LIMIT 1` with deterministic ordering.
- [ ] Focused PostgreSQL regression and the applicable Release/Architecture/Unit/Contract/migration checks pass.

Risks and invariants: PostgreSQL expression syntax is provider-specific and must remain parameterized. The query must not create or merge Canonical identities, change title/author normalization, or expose source/private data.

Verification plan: focused `CanonicalBookRepositoryTests`; `dotnet restore InkFlow.sln`; Release build; Unit, Architecture and Contract tests; migration model check; local Integration attempt (expected Docker named-pipe blocker on this host); remote CI/Docker/Security gates.

Delivery boundary: no schema/migration, API, Legado, UI, or source-runtime changes. `.workbuddy-ai/` remains user-owned and untracked.
