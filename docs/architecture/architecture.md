# InkFlow Architecture Specification

## 1. 总体架构

InkFlow 采用 Modular Monolith + 独立运行进程：

- `InkFlow.Api`：Web、Legado、Developer、Admin API。
- `InkFlow.Worker`：HTTP/Browser 采集、解析、内容处理。
- `InkFlow.Scheduler`：自适应追更和任务生成。
- `InkFlow.Migrations`：生产数据库迁移入口。
- PostgreSQL：Authoritative Data 与任务事实状态。
- Redis：缓存、速率限制、分布式协调与 Dispatch 加速。
- Object Storage：达到规模阈值后的正文/媒体 Blob。

第一阶段部署采用 Single-Region HA + Offsite DR；架构预留未来 Multi-Region，但不提前实现全球多活。

`InkFlow.Migrations` 对 11 个模块/基础设施 `DbContext` 逐一应用迁移；每个上下文在 `MigrateAsync` 前必须通过 `HasPendingModelChanges` 检查，发现模型与快照漂移即以非零退出码停止，不对数据库做隐式结构变更。Release Build 后由 `scripts/verify-migrations.sh` 使用仓库锁定的 `dotnet-ef` 版本逐一复核全部上下文；这项门禁不替代 Expand → Migrate → Contract 的破坏性变更评审。

## 2. 模块边界

目标模块：

- Identity：账号、Session、短期 AccessToken、RefreshToken、凭证、Role/Permission。
- Library：Canonical Book、Author、Chapter metadata、Private Book、Alias、Matching/Alignment。
- Sources：Source 定义、Rule、Rule Version、Credential Reference、Capability、Health Policy。
- Crawling：Task、Lease、Retry、Dead Letter、Scheduler、Fetch Artifact、执行治理。
- Content：Content AST、Blob、Version、Quality、Selection、Normalization、Policy。
- Reading：阅读进度、Reader Preference、书架和阅读状态。
- Search：搜索抽象与可重建 Search Document。
- Legado：Legado Contract、Rule Generator、兼容性 Profile 与专用 API。
- Billing：版本化内置套餐、Entitlement 历史、用户月度配额与 Usage Ledger。
- Developers：生产环境 Developer Application、可撤销 API Key 和只读 Developer API v1。
- Organizations / Operations：继续按后续商业与运维工作包渐进实现。

公共 Building Blocks 只包含稳定横切能力：强类型 ID、Result/Error、Messaging、Persistence primitives、Security primitives、Observability。

## 3. Canonical Library

第三方数据永远先落在 Source 语义：

`Source -> SourceBook -> SourceChapter -> Source Content`

平台通过可解释决策形成：

`CanonicalBook -> CanonicalChapter -> Content Versions -> Selected Content`

### Book Matching

综合 NormalizedTitle、Author、Alias、Metadata、章节重合、内容指纹和外部标识产生 MatchScore/Evidence。高置信度可自动关联，中等进入 Candidate，低置信度创建新 Canonical Book。

必须支持 Merge、Split、Alias、Redirect 与 Decision History。AI/Embedding 可以产生候选，但不得单独决定自动合并。

### Chapter Alignment

Canonical Chapter 与 Source Chapter 通过 Mapping 关联。数据模型允许 1:N / N:1，为缺章、插章、番外、拆章/合章保留空间。

Phase 1 先使用章节号、标准化标题和序列上下文；Phase 2 再加入内容指纹与跨源序列对齐。

### Private Library

Private Book 是绑定单一 UserId 的用户私有书目元数据，使用独立的 PrivateBookId，不进入 CanonicalBook、公共搜索、Legado、Source Match 或公共 Reading Shelf。所有读取和变更都以认证主体为范围；私有书目缺失与非所有者访问统一返回未找到。

当前 v1 提供书目元数据 CRUD；本轮补充私有 `PrivateChapter` 和规范化段落正文。TXT/EPUB 导入会原子创建新的私有书籍快照，导出只从用户范围的私有数据生成，不覆盖既有书籍，也不进入公共 Canonical 内容、搜索、Legado 或公共 Reading Shelf；正文编辑、版本恢复和发布为公共内容仍需另行设计。

## 4. Content Pipeline

统一管线：

`Raw -> Decode -> Extract -> Sanitize -> Normalize -> Content AST -> Fingerprint -> Quality -> Persist -> Select`

保存 RawHash 与 CanonicalHash。仅 Raw 变化而 CanonicalHash 不变时，不创建新的有效正文版本。

正文不覆盖旧版本：每次有效变化形成新的 `ContentVersion`。`SelectedContentVersionId` 只指向当前最佳版本。

Quality 决策必须输出可解释 Evidence 和 AlgorithmVersion，并支持人工 Lock。

公开策略由 Content 模块的 `ContentPolicyDecision` 追加式历史派生。书级 Takedown/Restore
命令只允许 Administrator，命令要求理由并写入审计；目录、书籍详情、章节正文、Web Reader
和 Legado 输出都必须经过策略门控，且正文门控先于正文列加载。策略历史不是覆盖式状态表，
数据库触发器拒绝更新/删除。

## 5. Source Runtime

统一 `ISourceAdapter`，支持：

- RuleAdapter：声明式 DSL，覆盖大多数普通站点。
- CodeAdapter：仅可信官方源，用于复杂签名、登录或 Playwright 场景。

RuleAdapter 的 Rule JSON 经过版本化严格 codec 和离线 Fixture 边界；未知属性/转换类型、缺失核心字段、
超大文档和超出集合/表达式上限的规则必须 fail-closed。当前执行基线覆盖 GET/POST、Header/Query/Form、路径
占位符、CSS、受控 XPath/JSONPath、带超时 Regex、Trim/Replace、列表绑定和三种受控 Pagination；路径、Header、
Query、Form 模板均支持有界的调用方变量上下文，执行器已应用有限的请求数、请求/响应字节、执行时间、正则
时间、结果大小和变量上下文预算，生产 HTTP 读取也有流式响应体上限。
任务级 `CredentialReferenceId` 已通过 `SourceExecutionContext` 贯通活动 Worker 的 TOC/Content 链路；
`Source.DefaultCredentialReferenceId` 提供来源级可选的非敏感默认绑定，RuleAdapter/Worker 仅在调用方
未提供显式引用时回退，显式引用优先。两者均由 `ISourceCredentialProvider` 解析并仅投影为 Bearer、Basic
或受限 API-Key Header；无效引用、提供器异常、超时、非法材料和头冲突均在出网前失败关闭，secret 不进入
任务/规则/日志/结果。4.59 已增加 Administrator-only 的来源默认引用设置/清除入口，并通过命令审计记录，
请求和响应仍只含非敏感引用 ID。配置提供器只是本地/容器适配器，Owner Scope、真实 SecretProvider、secret
材料管理和持久会话仍需独立治理；4.60 起 Provider 通过
`SourceCredentialResolutionContext` 接收 Source、引用和 `SourceCredentialOwnerScope`，范围为
Platform/User/Organization，后两者必须带稳定 OwnerId。Worker 与来源默认绑定显式使用 Platform，
只有显式引用才能携带用户/组织范围；当前配置提供器拒绝非 Platform 范围。CodeAdapter 不继承规则型
来源默认绑定。
统一 `RuleSelectorEvaluator` 对 CSS、XML/HTML XPath 和受限 JSONPath 做分派；不支持的选择器语法、
DTD/外部实体、超大文档或超量匹配必须 fail-closed。`RulePagination` 只为 Search/TOC 列表提供
同源 next-link、有限 page-number 或有限 cursor；next-link 后续以 GET 跟随，页码/游标只改写规则已声明
且唯一的 query/form 参数。循环、重复游标、跨源和请求/页面上限整体失败。`CapabilityRule.Session` 已提供
单次 RuleAdapter 执行内的受控 response-cookie 会话：只接收成功响应的 `Set-Cookie`，按同源最终响应及
Domain/Path/Secure、生命周期和数量/字节边界发送到后续请求，Cookie 不写入 Rule JSON 值、日志、任务载荷
或跨执行存储。`CapabilityRule.ResponseVariables` 仅在 page-number/cursor 续页存在时从当前响应派生
临时请求变量，复用同一变量预算，缺失/非法/超限在续页出网前整体失败且不进入结果或日志；不提供
持久化或跨执行状态。来源级默认 CredentialReference 回退已接入，但持久化 Session，以及三种受控分页之外的
多请求/递归执行所需的完整 Redirect/Depth 策略仍需
对应运行时引擎与独立回归，不能由解析通过替代真实执行验收。

抓取分层：

1. HttpClient + HTML/JSON
2. HTTP + Cookie/Session/Header/签名/代理
3. Playwright
4. 人工辅助首次登录/CAPTCHA 后会话

Playwright 不是默认路径，CAPTCHA 不自动破解。

## 6. Crawling

PostgreSQL 保存任务事实状态；Redis 仅用于 Dispatch 加速。

Task 支持：TaskId、Type、SourceId、Priority、Attempt、MaxAttempts、ScheduledAt、LeaseUntil、TraceId、IdempotencyKey。

状态：Pending -> Leased -> Running -> Completed，失败进入 Failed/DeadLetter。

所有任务必须幂等；错误分类决定 Retry、Backoff、Circuit Breaker 或人工介入。

死信修复通过 Crawling.Application 的受控 Repair/Replay seam 进入，而不是手工修改数据库：PostgreSQL 事务锁定原死信和任务，原任务保持 `DeadLettered`，只创建新的 `Pending` 重放任务并追加可追溯的操作者、理由、时间和任务 ID。当前 API 已通过 Identity opaque Bearer 认证和 `Operator` / `Administrator` policy 暴露受保护的死信列表与 replay 入口，命令额外写入 `crawler.dead_letter.replay` 审计事件；同一 Admin 组提供只读 `GET /api/v1/admin/consistency` 和 `GET /api/v1/admin/operations/overview`，前者汇总四个模块 schema 的最小关系快照，后者以有界读模型聚合来源健康、死信和一致性状态，并以稳定区块状态隔离查询故障。完整 Center UI、自动修复、细粒度权限管理与运维治理仍待后续实现；请求审计和一致性报告基线不等同于完整的管理平台。

Source Health Operator Controls v1 已补齐受保护的来源能力健康查询以及带理由的单能力 disable/enable 命令；状态仍由 Sources 健康聚合和 PostgreSQL 事实表驱动，恢复只回到 `Unknown`，不绕过真实探针。Operations Center Read Model v1 已提供独立查询授权和有界聚合视图，Center UI v1 已提供快照、受控操作和管理员告警历史展示；自动修复、外部通知路由、生产告警治理和备份治理仍待后续实现。

API 公共/Legado/Developer 限流由 ASP.NET Core policy 承载，计数通过 Redis Lua 原子脚本共享到多个 API 实例；Redis 操作使用配置化的有界超时（`RateLimiting:RedisOperationTimeoutMilliseconds`），连接故障时使用相同配额/窗口的有界本地降级并记录恢复转折，避免故障时无界放行。Redis 只保存可重建的限流计数，不承载任何业务事实。Developer API v1 使用生产环境 opaque API Key 和 `catalog.read` scope，仅暴露已落库公共目录/正文；Free/Pro/Developer 内置套餐通过 Entitlement 授予能力，PostgreSQL `usage_periods` 以用户+UTC 月份锁定累计加权单位，`usage_ledger` 按调用保存不可变事实，超额返回 `429/Retry-After`，Redis 配额快照只用于展示加速。

Operations Center 提供受 `Operator` / `Administrator` 保护的告警快照读端口：它从来源能力健康、死信、一致性检查和 Redis 限流存储健康汇总当前告警，并以配置化阈值和有界结果输出稳定代码。未过滤的管理员快照会把不含动态 message 的稳定告警身份记录到 `operations.alert_incidents`，并把 opened/resolved 转折追加到 `operations.alert_history`；事务级 PostgreSQL advisory lock 负责跨实例去重，partial 快照不误恢复，历史按配置保留期清理。历史查询仍只对 Administrator 开放，Center UI 以时间倒序表格展示转折并用不透明游标加载更早记录；它不执行修复、不写入业务事实、不发送外部通知；生产告警路由、通知渠道和治理仍由后续运维集成负责。

Developer API / Commercial Foundation v1 的组合根由 `InkFlow.Api` 组装：用户通过 `/api/v1/me/developer-applications` 自助创建应用、签发/轮换/撤销密钥，通过 `/api/v1/me/entitlement` 查看当前套餐和配额；Administrator 通过 `/api/v1/admin/plans` 与 `/api/v1/admin/users/{userId}/entitlement` 管理套餐。外部只读面为 `/api/developer/v1`，不触发来源抓取、不进入私人书库、不提供管理写入；密钥原文只在签发/轮换响应出现，列表和审计只保存脱敏引用。

## 7. Messaging 与一致性

采用 PostgreSQL Transaction + Transactional Outbox + Inbox：

- `InkFlow.BuildingBlocks.Messaging` 定义有界 JSON `IntegrationMessage`；消息 ID、类型和 PayloadHash 是消费幂等与身份核对的稳定字段。
- `messaging.outbox_messages` / `messaging.inbox_messages` 是 PostgreSQL 事实表，不由 Redis、缓存或 Projection 替代。
- `ITransactionalOutboxWriter` 只能在业务 DbContext 已开启的 PostgreSQL 事务中追加 Outbox 行，业务事实与 Outbox Event 必须同事务提交或回滚。
- Outbox Dispatcher 以 `FOR UPDATE SKIP LOCKED` 领取可投递行，用 lease、attempt、AvailableAt 和失败代码支持 At-Least-Once；发布成功后才写入 `ProcessedAt`。
- Inbox 以消息 ID 主键去重，处理成功后才写入 `ProcessedAt`；消费者崩溃或 lease 到期后允许再次领取。类型/载荷摘要不一致视为身份冲突并拒绝消费。
- `OutboxDispatcher` 与 `IntegrationMessageConsumer` 提供可复用的执行层：发布器/处理器成功返回后才确认事实表；发布失败使用稳定失败码和有界指数退避释放租约，未知消息类型或处理失败不伪造成功。传输适配器、宿主后台循环和具体 Handler 由各 Host/模块组合根接入，本 Building Block 不绑定未选定的 MQ。
- 当前 v1 由 Worker 选择 PostgreSQL 内部 relay：`PostgreSqlInboxMessagePublisher` 重建并核对类型、PayloadHash、TraceId 后幂等写入 `messaging.inbox_messages`，成功后才确认 Outbox；`OutboxRelayBackgroundService` 按 `Messaging:Relay` 的有界参数周期领取批次。该适配器不引入外部 MQ，重复投递由 Inbox 主键和身份核对吸收，具体 Inbox Handler/消费轮询仍需接收模块明确后接入。
- 已处理的 Outbox/Inbox 记录由 `MessageRetentionService` 以配置化 cutoff 和 `BatchSize` / `MaxBatchesPerRun` 上限分批清理；PostgreSQL 使用事务内 `FOR UPDATE SKIP LOCKED`，只匹配 `ProcessedAt` 已设置且已过期的记录，失败、待重试、未处理和锁定中的消息保留。Worker 负责 relay 和启动延迟后的每小时清理周期；清理周期不替代消息事实表，也不意味着已接入外部 MQ。
- `audit.events` 的普通路径保持追加式；`AuditRetentionService` 按 `Audit:Retention` 计算 UTC cutoff，以 `(OccurredAt, Id)` 索引和事务内 `FOR UPDATE SKIP LOCKED` 分批清理过期事件。每轮受 `BatchSize` / `MaxBatchesPerRun` 双重上限约束，数据库触发器只对 retention transaction-local 标记放行删除，Worker 启动延迟后每小时执行。该代码基线不替代生产法律保留、归档和删除授权治理，决策见 ADR 0014。
- 当前已接入 Crawler `AddAsync` 的 `crawler.task.created` 最小稳定事件；其他模块的业务写入必须在接入相同事务 seam 后才能发布事件。
- 不追求依赖 MQ 的理论 Exactly Once。

Domain Event 描述模块内部事实；Integration Event 仅暴露跨模块需要的稳定事实。

## 8. Read Model

Source of Truth 与 Projection 分离：

- Authoritative：Book、Chapter、Source、ContentVersion、User、Developer Application/API Key、Plan/Entitlement Assignment、Usage Period/Ledger 等。
- Derived：Search Index、Cache、Ranking、Legado Projection、Web Projection、Statistics。

Projection 必须可重建。

## 9. 存储

正文采用混合策略：Persistent、Cached、OnDemand、Archive；底层模型具备全量持久化能力。

初期允许 PostgreSQL Inline Content；达到阈值后迁移 Object Storage，并通过 StoragePointer 引用。Content Blob 使用 SHA-256 去重，但授权边界与 Blob 去重完全分离。

## 10. 搜索

第一阶段：`ISearchService -> PostgreSqlSearchService`，使用 PostgreSQL Full Text / GIN / pg_trgm。

规模和需求明确后切换 `OpenSearchSearchService`，领域层不依赖搜索引擎。

## 11. Observability

统一 OpenTelemetry，贯穿 Scheduler -> Queue -> Worker -> HTTP -> Parser -> DB -> Quality Engine。

Core SLO v1 以 `public_api`、`legado_api`、`developer_api` 和 `reader` 四个稳定服务面记录低基数可用性/延迟指标；`/health`、管理静态页、未知路径和第三方来源内部请求不进入 Core SLO。`inkflow.slo.requests`、`inkflow.slo.request.duration` 和 `inkflow.slo.server.errors` 不携带路径参数、用户、IP、Token 或异常原文，第三方来源自身不可用不直接计入 InkFlow API 自身 SLA。OTLP exporter 仅在显式配置 endpoint 时启用，目标与边界见 ADR 0010。

`CoreSloEvidenceEvaluator` 对外部聚合出的明确时间窗口执行纯函数评估：四个服务面必须具备正请求量、匹配的延迟样本和合法 p95，结果区分 Passed、Failed、InsufficientEvidence 与 InvalidEvidence，并计算错误预算剩余量。它不保存或发送观测事实，真实 Collector、探针、告警和保留策略仍属于部署治理。

两份 Compose 编排提供固定版本的官方 OTLP Collector 作为观测接收基线：API、Worker、Scheduler 默认经内部网络发送到 `otel-collector:4317`，4317/4318 不发布到宿主机，健康端口 13133 仅绑定 loopback。Collector 配置只读挂载，并启用只读文件系统、临时目录、`no-new-privileges` 和全量 capability drop。当前 debug exporter 只服务本地/CI 诊断，不是生产事实存储；生产 OTLP 后端、窗口聚合、告警与保留策略仍需单独治理，详见 ADR 0012。

CI Runtime smoke 使用 `scripts/core-slo-runtime-smoke.sh` 对四个服务面执行固定、有界的合成请求，并输出包含请求数、5xx 数、延迟样本数和 p95 的 UTC JSON 证据；空查询 Legado 与未授权 Developer API 用于避免真实来源和凭据依赖。该证据只能作为 Compose/CI 短窗口基线，不能替代生产 OTLP 窗口、告警、保留治理或人工验收，详见 ADR 0013。

CI 同时将 metrics 周期导出缩短到 1 秒，Collector metrics batch 缩短到 1 秒，并在临时 signal-specific debug 输出中校验 `inkflow.slo.requests`、`inkflow.slo.request.duration` 和四个服务面标签；默认 Compose 保持 basic 诊断，生产仍需受治理的后端。

当前 CI 在 Runtime smoke 产生真实审计数据后执行 PostgreSQL custom-format 备份恢复演练：恢复到隔离数据库，并比较所有非系统表的行数签名与 `audit.events` 数量。该验证证明数据库归档可恢复，不等同于生产异地备份、审计法律保留、删除授权、RPO/RTO 或告警治理。
