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
- OrganizationId
- TaskId

URL、第三方 ID、序号不得替代 InkFlow 业务身份。

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

Source 状态和 Rule 状态使用明确生命周期而不是大量互相冲突的 boolean。

## Crawling

CrawlerTask 是执行事实，不拥有 Canonical 业务判断。

任务采用 Lease、IdempotencyKey、Attempt、Priority、Schedule 与 Error Classification。PostgreSQL 保存事实状态；Redis 只加速分发。

## Reading

ReadingProgress 保存 BookId、ChapterId、Position/Progress、UpdatedAt、DeviceId。

ReaderPreference 是独立可同步值对象；匿名保存在本地，登录后与 Cloud Profile 合并。

正文版本发生变化时优先通过文本 Anchor/附近上下文映射阅读位置，失败才退化到 ProgressRatio。

## Provenance

Canonical metadata 和 Content 都应能回答“数据从哪里来”。至少保留 Source、Source Resource、抓取时间、规则/算法版本与置信信息。
