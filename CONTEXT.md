# InkFlow Domain Language

## Identity and library

**User**: an authenticated InkFlow account that owns personal state and may access public catalog data.

_Avoid_: treating an authentication token, email address, or display name as the user's durable identity.

**Canonical Book**: the public, platform-wide identity of a work. It is the stable target for public chapters, source matching, reading history, and public content delivery.

_Avoid_: using a source URL or a user's private book as a Canonical Book identity.

**Private Book**: a book record owned by exactly one User and visible only within that user's private library. Its identity and metadata are independent from the public Canonical Book catalog.

_Avoid_: automatically matching, deduplicating, publishing, or exposing a Private Book through public search, Legado, or another user's library.

**BookId**: the identifier of a Canonical Book in public-facing contracts.

_Avoid_: calling a Private Book identifier a BookId when the contract needs to distinguish public and private ownership.

**PrivateBookId**: the identifier of a Private Book. It has no implied relationship to a Canonical Book.

_Avoid_: inferring public visibility or content access from a PrivateBookId alone.

**Owner**: the User whose authenticated identity is recorded as owning a Private Book or personal reading state.

_Avoid_: accepting an owner identifier from request payloads or route parameters as an authority decision.

**Reading Shelf Entry**: a User-scoped reading-state record that points at a public Canonical Book; it is not a private book record.

_Avoid_: using the Reading Shelf as a second storage location for private-library metadata.

**Private Chapter**: an ordered chapter owned by one Private Book and identified by an independent PrivateChapterId; its text belongs only to that book's owner.

_Avoid_: exposing a Private Chapter as a public ChapterId or treating its text as a Canonical ContentVersion.

**Private Content Document**: the normalized paragraph sequence stored for a Private Chapter and used by private reading and export formats.

_Avoid_: storing raw uploaded HTML/EPUB markup as the private reading document or assuming an exported file is a public content source.

**Import Snapshot**: a newly created Private Book and its immutable imported chapters produced by one accepted TXT or EPUB file.

_Avoid_: silently overwriting an existing Private Book when a file is imported again.

## Developer and commercial platform

**Developer Application**: a User-owned registration that identifies one external integration and its environment when it calls the Developer API.

_Avoid_: treating a User account or an API Key as the application identity.

**Developer API Key**: a revocable credential issued to one Developer Application and used to authenticate read-only Developer API requests.

_Avoid_: persisting or exposing the reusable secret after its one-time issuance, or accepting it in a URL.

**Entitlement**: a plan-granted capability that determines whether a User or application may use a named platform feature.

_Avoid_: collapsing plan capability into authentication, role, or a boolean `IsPremium` check.

**Quota Unit**: the versioned weighted cost charged for one admitted Developer API operation.

_Avoid_: treating every endpoint as equally expensive or using a cache counter as the authoritative usage fact.

**Usage Ledger**: the append-oriented record of admitted Quota Units for a User, Developer Application, and API Key within a UTC calendar billing period.

_Avoid_: reconstructing commercial usage only from Redis, logs, or mutable request summaries.
