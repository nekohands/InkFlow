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

## 2. 模块边界

目标模块：

- Identity：账号、Session、短期 AccessToken、RefreshToken、凭证、Role/Permission。
- Library：Canonical Book、Author、Chapter metadata、Alias、Matching/Alignment。
- Sources：Source 定义、Rule、Rule Version、Credential Reference、Capability、Health Policy。
- Crawling：Task、Lease、Retry、Dead Letter、Scheduler、Fetch Artifact、执行治理。
- Content：Content AST、Blob、Version、Quality、Selection、Normalization、Policy。
- Reading：阅读进度、Reader Preference、书架和阅读状态。
- Search：搜索抽象与可重建 Search Document。
- Legado：Legado Contract、Rule Generator、兼容性 Profile 与专用 API。
- Billing / Developers / Organizations / Operations：后续商业阶段渐进实现。

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

Source Health Operator Controls v1 已补齐受保护的来源能力健康查询以及带理由的单能力 disable/enable 命令；状态仍由 Sources 健康聚合和 PostgreSQL 事实表驱动，恢复只回到 `Unknown`，不绕过真实探针。Operations Center Read Model v1 已提供独立查询授权和有界聚合视图；完整 Center UI、自动修复、告警、保留策略和备份治理仍待后续实现。

## 7. Messaging 与一致性

采用 PostgreSQL Transaction + Transactional Outbox + Inbox：

- 数据写入与 Outbox Event 同事务提交。
- 跨进程消费采用 At-Least-Once + Idempotent Consumer。
- 不追求依赖 MQ 的理论 Exactly Once。

Domain Event 描述模块内部事实；Integration Event 仅暴露跨模块需要的稳定事实。

## 8. Read Model

Source of Truth 与 Projection 分离：

- Authoritative：Book、Chapter、Source、ContentVersion、User、Billing Ledger 等。
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

核心指标覆盖 API、Legado、Crawler、Source Health、内容质量与用户体验。第三方来源自身不可用不直接计入 InkFlow API 自身 SLA。

当前 CI 在 Runtime smoke 产生真实审计数据后执行 PostgreSQL custom-format 备份恢复演练：恢复到隔离数据库，并比较所有非系统表的行数签名与 `audit.events` 数量。该验证证明数据库归档可恢复，不等同于生产异地备份、保留策略、RPO/RTO 或告警治理。
