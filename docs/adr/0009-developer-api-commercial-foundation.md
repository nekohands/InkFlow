# Developer API Uses Application Keys and PostgreSQL Quota Facts

- 状态：Accepted
- 日期：2026-08-29

Developer API v1 is a separate, read-only contract at `/api/developer/v1`. Users create Developer Applications and receive revocable production-only opaque API Keys with the single `catalog.read` scope; plan Entitlements decide capability and PostgreSQL Usage Ledger facts enforce weighted UTC-month quotas, while Redis is only an acceleration layer. This keeps public Reader/Legado contracts independent, makes key compromise containable, and preserves auditable quota decisions without making Redis a business source of truth; payment providers, OAuth, organizations, sandbox data, private content, and management writes remain outside this foundation.
