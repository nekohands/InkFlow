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
