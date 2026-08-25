# InkFlow Security Model

## 1. Security Layers

InkFlow separates:

- Authentication: who the caller is.
- Authorization: what action the caller may perform.
- Entitlement: what the current plan grants.
- Quota: how much of that capability remains.
- Content Policy: whether the specific resource may be exposed, cached, persisted or exported.

Business code must not collapse these into checks such as `IsPremium`.

## 2. Credentials

Separate credential families:

- Web Access Token: short-lived.
- Refresh Token: session renewal with rotation/revocation.
- Legado Access Token: long-lived, revocable, scoped.
- Developer API Key: application credential with scope/quota/environment.

Long-lived secrets are returned to users once. Database storage uses non-secret prefix + secure hash where validation semantics allow it; never store directly reusable complete tokens by default.

## 3. RBAC and Resource Policy

Administrative access uses Role -> Permission mapping. Sensitive operations such as Book Merge/Split, Source Rule Publish, Takedown, user suspension, billing and permission changes require explicit permission checks, reason/audit, and later may support re-auth/four-eyes approval.

Private/organization content additionally enforces ownership/resource policy.

## 4. Community Source Threat Boundary

Community Rule code runs in a restricted declaration runtime, not arbitrary application code.

Required controls:

- SafeHttpClient
- SSRF prevention
- redirect/DNS re-validation
- private/link-local/metadata/internal service denial
- max requests/bytes/redirects/execution time/result size
- regex timeout / non-backtracking where applicable
- no process/filesystem/reflection/dynamic code/arbitrary socket

## 5. Network Isolation

Separate trust zones conceptually and in production deployment where possible:

- Public API
- Database/internal data plane
- HTTP Crawler Worker
- Browser Worker

Browser Workers must not receive broad database/network access. They consume bounded task contracts and short-lived credential references.

## 6. Secrets

Application code depends on `ISecretProvider` or equivalent abstraction.

Development may use uncommitted local environment files. Production should support Docker Secret/Vault/Cloud Secret Manager style providers. Source records store references, not plaintext platform/user credentials.

## 7. API Abuse Protection

Rate limiting can combine:

- IP
- anonymous session
- user
- Legado token
- API key
- organization

Expensive endpoints use weighted quota rather than treating all requests equally. APIs return proper 429/Retry-After semantics; Legado/API clients are not forced through interactive CAPTCHA.

## 8. No Open Proxy

External callers operate on SourceId/BookId/ChapterId and authorized registered resources. InkFlow never exposes a generic `proxy?url=...` endpoint.

## 9. Content Output Safety

Third-party HTML is untrusted.

Pipeline must extract/sanitize into Canonical Content AST or tightly controlled safe markup. Web rendering does not directly execute upstream script/iframe/object/form/unsafe styles.

Media ingestion validates content before storage/CDN delivery; remote source URLs are not trusted as permanent client media endpoints.

## 10. Public vs Private Content

Public and private delivery paths must have explicit authorization/cache behavior. CDN/cache key mistakes must not allow private EPUB/TXT/user content to leak into public cache.

Physical ContentBlob dedup never grants logical access.

## 11. Audit

High-risk actions emit immutable/append-oriented AuditEvent data including actor, time, resource, action, before/after or reference, reason and TraceId where applicable.

Ordinary administrators cannot silently edit audit history through normal CRUD APIs.

## 12. Supply Chain and Runtime

CI progressively includes dependency review, secret scanning, SAST/container scan and SBOM generation.

Production containers run non-root where practical, drop unnecessary capabilities, use resource limits and avoid host mounts. Image/runtime versions are pinned rather than relying indefinitely on `latest`.

## 13. Incident Response

Operational controls must eventually support rapid containment:

- revoke session/token/API-key classes
- disable Source/Rule
- block abusive user/organization/IP
- suspend affected feature using Feature Flag
- preserve evidence for investigation/postmortem

Security functionality is implemented progressively, but SSRF protection, secret handling, content sanitization, audit foundations and credential separation are Phase 0/1 concerns, not post-launch additions.
