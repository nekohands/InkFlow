# Developer API v1

## Scope

Developer API v1 is a production-only, read-only catalog contract. It reads stored public Canonical data and never triggers source discovery, reads Private Library data, or exposes management writes.

The external base path is `/api/developer/v1`. The Web/API management surface remains under `/api/v1` and is bearer-authenticated.

## Authentication and authorization

- Header: `X-InkFlow-Api-Key`.
- Credential: opaque `lf_dev_...` API Key bound to one Developer Application.
- Environment: `production` only.
- Scope: `catalog.read` only.
- Entitlement: `developer.catalog.read`, granted by the active versioned plan.
- User and application status are checked on every authentication attempt.

The raw secret is returned only from a successful issue or rotate response. List and audit responses contain key metadata and a safe prefix, never the hash or raw secret.

Lifecycle limits are enforced by the PostgreSQL persistence boundary: one user may have at most 10 active applications and one application may have at most 5 active keys. Application creation and key issue use transaction-scoped advisory locks; key rotation uses the same application lock and cannot turn an expired key into an additional active key beyond the cap.

## Management endpoints

| Method | Path | Authorization | Purpose |
| --- | --- | --- | --- |
| `POST` | `/api/v1/me/developer-applications` | active Web user | Create an application |
| `GET` | `/api/v1/me/developer-applications` | active Web user | List owned applications |
| `DELETE` | `/api/v1/me/developer-applications/{applicationId}` | owner | Revoke an application and its keys |
| `POST` | `/api/v1/me/developer-applications/{applicationId}/keys` | owner | Issue one key; returns raw secret once |
| `GET` | `/api/v1/me/developer-applications/{applicationId}/keys` | owner | List key metadata |
| `POST` | `/api/v1/me/developer-applications/{applicationId}/keys/{keyId}/rotate` | owner | Revoke current key and issue replacement |
| `DELETE` | `/api/v1/me/developer-applications/{applicationId}/keys/{keyId}` | owner | Revoke one key |
| `GET` | `/api/v1/me/entitlement` | active Web user | Read plan and quota snapshot |
| `GET` | `/api/v1/admin/plans` | Administrator | List built-in plans |
| `PUT` | `/api/v1/admin/users/{userId}/entitlement` | Administrator | Append a plan assignment with reason |

## Read-only catalog endpoints

| Method | Path | Weight |
| --- | --- | ---: |
| `GET` | `/api/developer/v1/search?q=&limit=` | 1 |
| `GET` | `/api/developer/v1/books?limit=` | 1 |
| `GET` | `/api/developer/v1/books/{bookId}` | 1 |
| `GET` | `/api/developer/v1/books/{bookId}/chapters` | 1 |
| `GET` | `/api/developer/v1/chapters/{chapterId}/content` | 5 |

`limit` is bounded to 1–100. Content is returned from the current stored Canonical version and is subject to Content Policy. Responses use `Cache-Control: private, no-store` because access is key- and quota-scoped.

## Quota and errors

Each authorized operation appends a `UsageLedger` fact and increments the PostgreSQL user-level `UsagePeriod` for the current UTC calendar month in one transaction. The period row is locked with `FOR UPDATE`; multiple applications and keys share the same user quota. The fixed algorithm is `quota-v1`.

- `400 invalid_request`: malformed input or unsupported bounds.
- `403 developer_api_forbidden`: the user lacks the catalog entitlement.
- `404 book_not_found` / `chapter_not_found`: the public stored resource is absent or not visible.
- `429 quota_exceeded`: includes `Retry-After` until the UTC month boundary.
- `503 quota_unavailable`: authoritative quota storage is unavailable; the request fails closed.

Redis may cache the display snapshot but never authorizes a request or becomes the source of quota truth.
