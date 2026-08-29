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

Personal Legado Token v1 follows the same boundary: `POST /api/v1/me/legado/tokens` requires an authenticated user, returns the raw `lf_lgd_...` value only in that successful issue response, and persists only its Prefix + SHA-256 Hash, scope, expiry and revocation metadata. `GET` returns metadata without the secret; `DELETE` revokes only a token owned by the current user. Legado requests use the dedicated `X-InkFlow-Legado-Token` header and `InkFlowLegadoToken` scheme, never a URL/query parameter. The `LegadoRead` policy requires an active token with the `read` scope and an active user.

Developer API v1 uses a separate `X-InkFlow-Api-Key` header and `InkFlowDeveloperApiKey` scheme. The key is production-only, opaque, application-bound and limited to `catalog.read`; it is never accepted from URL/query input. Self-service application/key issue, list, revoke and rotate endpoints are bearer-protected. The raw `lf_dev_...` secret is returned only from a successful issue/rotate response; PostgreSQL persists only a Prefix and SHA-256 hash. Application revocation, key revocation, expiry and user suspension all fail closed at authentication time.

Commercial access is layered: active users default to the versioned Free plan, an Administrator-only endpoint appends a new Entitlement Assignment for Pro/Developer, and Developer API operations require the `developer.catalog.read` entitlement. PostgreSQL is authoritative for the user-level UTC-month quota and immutable Usage Ledger; Redis quota snapshots are best-effort read acceleration only. Developer lifecycle caps are also enforced in PostgreSQL transactions (10 active applications per user, 5 active API keys per application) with transaction-scoped advisory locks shared by create and rotate paths. Content responses remain read-only public Canonical data and exclude Source credentials, private records and management operations.

Reader/PWA v1 has a deliberately bounded browser exception: to survive server-rendered page reloads while HttpOnly BFF sessions are not yet available, the current tab may keep the short-lived Web Access Token and Refresh Token in `sessionStorage`. The client never uses `localStorage`, URL/HTML/log transport, cookies or Service Worker Cache for those values; API calls use same-origin `Authorization` headers and `cache: no-store`, and failed/invalid refresh clears the tab session. The Service Worker caches only public Reader shell assets and never handles `/api/v1/me/*` or auth responses. This boundary is recorded in ADR 0006 and remains a v1 compatibility choice, not a cross-device or offline-private-content guarantee.

## 3. RBAC and Resource Policy

Administrative access uses Role -> Permission mapping. The Operations Center read model, crawler dead-letter listing and consistency/source-health queries use a dedicated `OperationsRead` policy for `Operator` / `Administrator`; repair and source-health commands keep separate policies, obtain the actor from the authenticated subject and require a reason where applicable. Content Takedown/Restore is protected by an Administrator-only `ContentModeration` policy, requires a reason, and writes an immutable policy decision plus command audit. Commercial plan assignment and source/permission changes require separate Administrator-only policies, explicit reason/audit, and later may support re-auth/four-eyes approval.

Source capability operations use a separate `SourceOperations` policy for `Operator` / `Administrator` roles. The protected health view and explicit disable/enable commands operate on one `(SourceId, Capability)` row at a time, require a reason for each command, and record the authenticated actor, bounded reason, result and resource reference in the command audit trail.

Resource-level Source Authorization v1 adds an explicit active grant boundary for individual sources. Only an `Administrator` may grant, list or revoke `source.read` / `source.manage` grants for active `Operator` users; grants are retained as revoked history, and an active `source.manage` grant implies `source.read`. Direct source health reads, source disable/enable commands, and the source-health portions of Operations overview/alerts enforce this resource boundary. Administrators bypass source grants, while Readers and Operators without an active grant are denied. Crawler and consistency sections of the Operations views remain platform-level under the existing `OperationsRead` policy in v1. Organization, tenant, billing and general private-resource permission governance are outside this scope.

Private/organization content additionally enforces ownership/resource policy.

Private Library content uses a separate user-scoped delivery path. TXT/EPUB imports are bounded by upload, archive-entry, decompressed-size, chapter-count and normalized-text limits; EPUB paths are validated without extracting files, XML DTD/external resolution is disabled, and imported markup is reduced to plain paragraphs. Private chapter and export responses use `private, no-store` cache semantics, and export generation never publishes a private file through public catalog, Legado, CDN or shared content paths.

Reading State v1 is private user data. The `/api/v1/me/reading/*` endpoints require an authenticated opaque bearer token and derive the owner only from the verified `sub` claim; callers cannot select another `UserId` through the route or request body. Reading tables use user-scoped composite keys, and the application repository requires `UserId` on every read/write. Book, chapter and Content Policy checks still apply before writing progress, shelf or history. UI-level export/delete and richer resource permissions remain future work; Personal Legado Token scoping is defined above and is independently enforced by the dedicated Legado scheme/policy.

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

Compose 中的 OTLP Collector 只在内部网络接收 API、Worker、Scheduler 的遥测；4317/4318 不发布到宿主机，健康端口 13133 仅绑定 loopback。Collector 配置只读挂载并以 read-only、`no-new-privileges`、`cap_drop: ALL` 运行；当前 debug exporter 仅用于本地/CI 诊断，不承担生产保留或告警。CI 可临时启用 signal-specific metrics 详细输出以确认低基数 instrument/服务面到达，默认 Compose 保持 basic，生产后端需要单独进行访问控制、保留和出口治理。

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

当前 API 基线已通过可替换的 policy/key seam 接入 ASP.NET Core fixed-window 限流：公共、Legado、Developer API 使用独立策略，匿名请求按连接层 IP 分桶，普通认证主体按 `sub` / `client_id`，Developer API 在专用认证预处理中先校验密钥，再按 `developer_api_key_id` 的不可逆短哈希分桶；缺失或无效密钥仍按 IP 分桶后由授权拒绝，避免用未经验证的 Header 内容制造任意分桶；尚未配置可信代理前不信任 `X-Forwarded-For`。计数由 Redis Lua 脚本原子完成检查、递增和过期，Redis 操作受 `RateLimiting:RedisOperationTimeoutMilliseconds` 有界超时约束，多个 API 实例共享同一配额；拒绝请求返回 `429` 并带窗口剩余时间的 `Retry-After`。Redis 暂时不可用时只使用相同配额/窗口的本地有界 fixed-window 降级并记录恢复感知日志，不会无界放行，但降级期间不提供跨实例全局一致性。Developer API 的业务配额另由 PostgreSQL 用户级 UTC 月度 Usage Period 事务锁定，按固定版本单位计费，超额返回 `429/Retry-After`；Redis 只缓存展示快照。当前另有受 OperationsRead policy 保护的 `GET /api/v1/admin/operations/alerts` 告警快照；未过滤的 Administrator 快照会把不含动态描述的 opened/resolved 转折写入 Operations PostgreSQL 历史，事务级 advisory lock 保证跨实例去重，`GET /api/v1/admin/operations/alerts/history` 仅允许 Administrator 查询并按有界游标分页。外部通知、生产路由和治理仍待后续。

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

当前已提供 `AuditEvent` 不可变数据模型、`IAuditEventSink` 追加写入端口和 API 请求审计中间件；审计范围为 `/api` 与 `/legado`，不记录 query string，且 `429` 等拒绝结果也进入轨迹。API 通过 `CompositeAuditEventSink` 同时写入结构化宿主日志与 PostgreSQL `audit.events`，`AddAuditEvents` Migration 安装数据库追加式触发器拒绝更新/删除；持久化失败不改变请求结果。Crawler dead-letter replay、Content Policy Takedown/Restore、Personal Legado Token issue/revoke、Source Permission grant/revoke、Developer Application/API Key 生命周期与 Entitlement assignment 已额外写入带认证操作者、结果和资源 reference 的命令级审计事件；所有密钥审计只记录脱敏引用，不记录原文。现在另有受独立 `AuditRead` policy 保护的 `GET /api/v1/admin/audit/events` 有界只读查询：支持时间范围、精确 action/outcome/actorId 过滤和时间戳+事件 ID 不透明游标，单页最多 100 条，查询异常只返回稳定错误码，不提供更新/删除路径。告警快照只读入口另受 `OperationsRead` policy 保护；告警内部历史只保存稳定身份和 opened/resolved 转折，Administrator-only 历史读端使用有界游标，外部告警路由、生产保留治理、组织/更广泛私有资源授权仍需后续实现。

Ordinary administrators cannot silently edit audit history through normal CRUD APIs.

## 12. Supply Chain and Runtime

CI 现已建立可回归的供应链扫描基线：`.github/workflows/security.yml` 执行 NuGet 传递依赖漏洞审计、Trivy 源码/配置/依赖的 HIGH/CRITICAL 漏洞、Secret 与 Misconfiguration 扫描、C# CodeQL SAST 和 CycloneDX 源码 SBOM，并将报告作为构建产物归档。`.github/workflows/docker.yml` 先扫描 Compose 使用的固定版本 OTLP Collector，再对 API、Migrations、Scheduler、Worker 四个镜像执行 Trivy HIGH/CRITICAL 漏洞扫描，只有全部通过后才发布业务镜像标签。

当前仓库未启用 GitHub Code Scanning API，CodeQL/Trivy 结果保留为工作流产物而不上传到代码扫描面板；`ignore-unfixed` 仍表示无法修复的漏洞不会阻塞本基线。生产镜像准入、扫描报告长期保留、动作版本固定、Secret 轮换和部署环境策略仍需后续治理。

Production containers run non-root where practical, drop unnecessary capabilities, use resource limits and avoid host mounts. Image/runtime versions are pinned rather than relying indefinitely on `latest`; the Compose Collector is likewise pinned to `otel/opentelemetry-collector:0.159.0` and does not expose its OTLP intake ports publicly.

## 13. Incident Response

Operational controls must eventually support rapid containment:

- revoke session/token/API-key classes
- disable Source/Rule
- block abusive user/organization/IP
- suspend affected feature using Feature Flag
- preserve evidence for investigation/postmortem

The current baseline exposes the source capability health view and reasoned disable/enable operations through the protected Source Operations API. The protected Operations Center read model aggregates bounded source health, dead-letter and consistency views; section failures return stable unavailable/partial states without infrastructure details. It does not silently alter rules, source identity or stored content; recovery still returns a capability to `Unknown` so a real probe must establish `Healthy`.

Security functionality is implemented progressively, but SSRF protection, secret handling, content sanitization, audit foundations and credential separation are Phase 0/1 concerns, not post-launch additions.
