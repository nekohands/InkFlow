# Canonical Domain Model

## Identity

对外稳定业务 ID 使用 UUIDv7 强类型包装，例如：

- BookId
- ChapterId
- SourceId
- SourceBookId
- SourceChapterId
- ContentVersionId
- UserId
- PrivateBookId
- OrganizationId
- TaskId

URL、第三方 ID、序号不得替代 InkFlow 业务身份。

Identity v1 的 `User` 以邮箱规范化值保持唯一，账号状态控制是否可认证；`RefreshSession` 与
`AccessToken` 分离保存，客户端只持有 opaque 原始 token，数据库只保存不可逆摘要。Refresh
轮换在存储层以事务行锁保证一次性成功，登出可撤销会话及其访问令牌。

## Developers & Commercial

`DeveloperApplication` 是用户拥有的生产环境外部集成注册；它与 `User`、API Key 和公共目录身份分离。`DeveloperApiKey` 绑定应用，使用 opaque 原文、Prefix、不可逆摘要、单一 `catalog.read` scope、创建/过期/最后使用/撤销元数据；原文只在签发或轮换响应中出现一次。撤销应用会使其密钥失效，API Key 认证不会接受 URL 或 Query 中的密钥。

`PlanDefinition` 是带版本和固定 `QuotaAlgorithmVersion` 的内置套餐定义（Free / Pro / Developer）；`EntitlementAssignment` 是管理员授予用户套餐的不可变历史，当前能力由最新记录派生，没有记录时默认 Free。业务代码检查 Entitlement，不直接依赖套餐名称。

`UsagePeriod` 是 PostgreSQL 中按 `(UserId, UTC month)` 唯一的可锁定累计配额行；`UsageLedgerEntry` 按用户、应用、API Key、操作、加权单位、算法版本和 TraceId 保存每次准入事实。Redis 只能缓存配额快照，不能决定是否准入。Developer API 只读已落库的 Canonical Catalog/Content，不触发第三方来源抓取，也不访问 Private Library。

## Library

### CanonicalBook

代表 InkFlow 对“作品”的稳定身份，不属于任何单一来源。

关联：BookAlias、CanonicalAuthor、Metadata Candidate/Provenance、SourceBook、CanonicalChapter。

支持：Merge、Split、Redirect、MatchDecision、Revision。

### SourceBook

代表某一个 Source 中的作品。

保存 Source 外部 ID、Canonical URL、历史 URL、原始/标准化元数据、抓取状态、更新时间和 Source 归属。

### CanonicalChapter

稳定章节身份。`Sequence` 只负责排序，不等于 ChapterId。

允许后续插章和重排而不改变稳定 ChapterId。

### SourceChapter / ChapterMapping

SourceChapter 属于 SourceBook。通过 ChapterMapping 与 CanonicalChapter 建立关系。

数据模型允许 1:N / N:1，以处理番外、插章、缺章、拆章与合章。

### PrivateBook

PrivateBook 是单一 User 所有的私有书目元数据，使用独立的 PrivateBookId；它不是 CanonicalBook，也不拥有公共 BookId/ChapterId 的语义。

私有书目查询和变更必须以认证主体 UserId 为范围。PrivateBook v1 覆盖书目元数据，PrivateChapter/PrivateContentDocument v2 覆盖私有阅读正文与 TXT/EPUB 导入导出；发布为公共 Canonical 内容仍需另行建模。

### PrivateChapter / PrivateContentDocument

PrivateChapter 属于 PrivateBook，使用独立的 PrivateChapterId、书内顺序和不可变的规范化段落正文。PrivateContentDocument 只保留经过边界校验的纯文本段落；TXT/EPUB 导入生成新的 Import Snapshot，私有章节读取和导出必须绑定认证主体 UserId，不复用公共 ChapterId、ContentVersion 或公共 ContentBlob。

## Author

CanonicalAuthor + AuthorAlias + SourceAuthor。第一阶段匹配以标准化名字与人工修正为主，模型预留更丰富证据。

## Content

### FetchArtifact

表示一次来源抓取证据，记录 Response/Headers/Encoding/FetchedAt/ParserVersion/RuleVersion/RawHash 等。Raw 可按生命周期保留并支持 Replay。

### ContentDocument

Canonical 内容不是任意 HTML，而是受控 AST。v1 Block：Paragraph、Heading、Image、Divider；Inline 至少 Text/Emphasis。

Web HTML、Legado 输出、EPUB、TXT 都从同一 Canonical Document Render。

### ContentBlob

根据 CanonicalHash/SHA-256 去重实际正文数据，存储可以是 Inline 或 Object Storage。Blob 身份不等同授权身份。

### ContentVersion

指向 SourceChapter、FetchArtifact、ContentBlob，并记录 Parser/Normalizer/Quality Algorithm Version、FetchedAt、PublishedAt 与 Quality Evidence。

ContentVersion 不可通过新抓取覆盖；有效变化产生新版本。

### ChapterSelection

CanonicalChapter 的当前默认内容选择。自动 Quality Selection 可变，但选择历史和证据必须保留；人工 Lock 优先于自动选择。

当前 v1 选择记录落在 `content.selection_decisions`，至少保存：

- CanonicalChapterId
- SelectedVersionId
- AlgorithmVersion
- Evidence（候选、排除来源、选中来源和 fallback）
- CreatedAt

Sources 的 Capability Health 以 `(SourceId, Capability)` 为独立事实，Content 选择只读取其可用性，
不把来源健康状态混入 CanonicalBook/CanonicalChapter 或 ContentVersion 历史快照。

### ContentPolicy

当前 v1 以 `CanonicalBook` 为策略目标。`ContentPolicyDecision` 保存不可变的
`CanonicalBookId`、`Action`（`Takedown` / `Restore`）、`ActorId`、`Reason` 和 `CreatedAt`；
同一本书的最新决策派生当前是否允许公开。重复同状态命令幂等，实际状态变化追加新决策，
不覆盖既有历史。公开目录、详情、正文与 Legado 查询必须通过策略读端口；管理员命令要求
Administrator、理由和命令级审计。`content.policy_decisions` 由数据库追加式触发器保护。

## Matching / Decision

核心自动决策统一记录：

- DecisionType
- Inputs/References
- Score
- Evidence
- AlgorithmVersion
- Result
- CreatedAt
- manual override / rollback relationship

用于 Book Match、Chapter Alignment、Content Selection 和 Source Failover。

## Sources

Source 定义能力、策略和所有权；RuleVersion 为不可变发布物。

CredentialReference 只标识非敏感引用。Provider 解析必须同时接收 SourceId、引用和
`SourceCredentialOwnerScope`；Platform 不带 OwnerId，User/Organization 必须绑定稳定身份。
来源默认引用属于 Platform，调用方显式引用才可携带用户/组织范围。Owner Scope 是授权边界，
不把 secret 或材料放入 Source、CrawlerTask、Rule JSON 或执行结果。

Source 状态和 Rule 状态使用明确生命周期而不是大量互相冲突的 boolean。

## Crawling

CrawlerTask 是执行事实，不拥有 Canonical 业务判断。

任务采用 Lease、IdempotencyKey、Attempt、Priority、Schedule 与 Error Classification。PostgreSQL 保存事实状态；Redis 只加速分发。

DeadLetterTask 保存重试耗尽后的失败事实。受控重放必须通过 Application 修复 seam 执行：在同一 PostgreSQL 事务中锁定死信与原任务，创建新的 `Pending` CrawlerTask，原任务继续保持 `DeadLettered`；原死信的失败原因/尝试次数不覆盖，只追加重放任务 ID、时间、操作者和理由。重复或并发请求返回同一个重放任务，已解决死信不再永久阻止后续入队。

## Reading

ReadingProgress 保存 BookId、ChapterId、Position/Progress、UpdatedAt、DeviceId。

ReaderPreference 是独立可同步值对象；匿名保存在本地，登录后与 Cloud Profile 合并。

正文版本发生变化时优先通过文本 Anchor/附近上下文映射阅读位置，失败才退化到 ProgressRatio。

## Provenance

Canonical metadata 和 Content 都应能回答“数据从哪里来”。至少保留 Source、Source Resource、抓取时间、规则/算法版本与置信信息。
