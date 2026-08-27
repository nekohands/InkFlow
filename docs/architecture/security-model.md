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

- Web Access Token: short-lived opaque bearer token, validated against a stored hash and revocable.
- Refresh Token: session renewal with one-time rotation/revocation; only a secure hash is persisted.
- Legado Access Token: long-lived, revocable, scoped.
- Developer API Key: application credential with scope/quota/environment.

Long-lived secrets are returned to users once. InkFlow's current Identity baseline uses PBKDF2-SHA256 password hashes with per-password salt and stores only SHA-256 hashes of opaque access/refresh tokens; never store directly reusable complete tokens by default.

## 3. RBAC and Resource Policy

Administrative access uses Role -> Permission mapping. The current baseline protects crawler dead-letter listing/replay with `Operator` / `Administrator` roles; replay obtains the actor from the authenticated subject and requires a reason. Content Takedown/Restore is now protected by an Administrator-only `ContentModeration` policy, requires a reason, and writes an immutable policy decision plus command audit. Other sensitive operations such as Book Merge/Split, Source Rule Publish, user suspension, billing and permission changes require explicit permission checks, reason/audit, and later may support re-auth/four-eyes approval.

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

当前来源请求由 `SsrfGuard` 做字面量与 DNS 全结果校验，并由
`SsrfSafeHttpMessageHandler` 在真实 TCP 连接回调中直接连接同一批已验证地址；环境代理关闭，
端口限制为 80/443，自动重定向最多 5 跳。API、Worker、Scheduler 的来源 HTTP typed client
均已接入该 Handler。真实网络重定向演练和生产策略扫描仍是独立验收项。

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

当前 API 基线已通过可替换的 policy/key seam 接入 ASP.NET Core fixed-window 限流：公共 API 与 Legado API 使用独立策略，匿名请求按连接层 IP 分桶，认证主体按 `sub` / `client_id` 的不可逆短哈希分桶；尚未配置可信代理前不信任 `X-Forwarded-For`。拒绝请求返回 `429` 并带 `Retry-After`。当前为单实例实现，Redis 分布式计数、按用户/组织的动态配额和加权成本仍待后续 Operations/Identity 能力接入。

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

当前已提供 `AuditEvent` 不可变数据模型、`IAuditEventSink` 追加写入端口和 API 请求审计中间件；审计范围为 `/api` 与 `/legado`，不记录 query string，且 `429` 等拒绝结果也进入轨迹。API 通过 `CompositeAuditEventSink` 同时写入结构化宿主日志与 PostgreSQL `audit.events`，`AddAuditEvents` Migration 安装数据库追加式触发器拒绝更新/删除；持久化失败不改变请求结果。Crawler dead-letter replay 与 Content Policy Takedown/Restore 已额外写入带认证操作者、理由、结果和资源 reference 的命令级审计事件；更完整的 before/after、保留策略、查询授权和告警仍需后续实现。

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
