# InkFlow 项目进度

> 持续进度账本。状态只以真实代码、测试、Runtime 和 CI 结果为准。

- 产品：墨流 / InkFlow
- 当前阶段：1.0 Release Candidate（含前端的自动化 Release Gate 已通过，人工及其他真实环境验收待定）
- 当前工作分支：`dev`（2026-08-25 起）
- 文档状态：5.32 的 CollectionRun 取消终态/幂等领域回归补强已同步；当前 HEAD 为 `7d60235`（测试提交 `3aab3e8`、文档同步提交 `7d60235`，业务行为不变），其 CI `33479935777`、Docker `33479935816`、Security `33479935776` 均 GREEN 且 head SHA 一致。
- 最后更新日期：2026-09-01

## 1. 总体状态

| 阶段 | 状态 | 说明 |
| --- | --- | --- |
| Grill Me / 产品与架构对齐 | ✅ Completed | 产品定位、核心领域、Legado、Source Runtime、安全、商业化和路线已文档化 |
| Repository Bootstrap | ✅ Completed | .NET 10 基础仓库与最初 CI 已建立 |
| Phase 0 — Foundation | ✅ Completed | 模块边界、Persistence、Migration、Outbox/Inbox、OTel、测试与 Runtime CI Gate 已验收 |
| Phase 1A — Single Source Vertical Slice | 🚧 Ready for Real-Device Acceptance | 自动化链路与 kanunu8 真实源验证已完成；阅读 3.0 真机导入/阅读及真实追更仍待人工验收 |
| Phase 1B — Dual Source Validation | 🚧 In Progress | 确定性双 Official Source 夹具已覆盖正典身份、章节对齐、质量选优、质量失败演练、健康感知切源及源码 Compose A→B→A 运行时；真实 Official Source 故障切源仍待后续验收 |
| Phase 2 — Multi-Source Production | 🚧 In Progress | Capability Health v1 与 Worker 任务可靠性基础已落地；自适应追更、健康评分、规则 Canary 仍待推进 |
| Phase 3 — User Product | 🚧 In Progress | Reading State v1、Web Reader v1、Reader/PWA 用户状态 v1 与 Private Library 私有正文/导入导出自动化基础已落地；Web/PWA/Operations 前端纳入 1.0 强制 Release Gate，PWA Service Worker/离线壳自动化已补齐，真实账户、安装和私有路径补充验收仍待推进 |
| Phase 4 — Commercial Platform | 🚧 Release Candidate | Developers/Billing/Entitlement/Developer API v1 自动化基线与远端门禁已通过；真实凭据、真实 PostgreSQL/Redis 与人工验收仍待推进 |

## 2. Phase 0 验收记录

### 2.1 模块化 Foundation

已完成：

- 目录重构为 `Apps / BuildingBlocks / Modules / tests`。
- API / Worker / Scheduler / Migrations 独立 Host。
- UUIDv7、`Result<T>`、`TimeProvider` 基础。
- Central Package Management。
- Architecture Tests 强制模块依赖矩阵。
- 业务模块禁止依赖 Application Host 与 Persistence 实现。

### 2.2 Persistence / Messaging

已完成：

- EF Core + Npgsql。
- PostgreSQL 模块化 DbContext。
- Schema：`identity / library / sources / crawler / content / reading / messaging`。
- 独立 `InkFlow.Migrations`。
- Transactional Outbox / Inbox。
- Inbox 唯一约束与消费幂等。

### 2.3 Observability / Runtime / CI

已完成：

- OpenTelemetry traces / metrics / runtime instrumentation 基础。
- Testcontainers PostgreSQL 18 Integration Tests。
- Docker Compose：PostgreSQL / Redis / Migration / API / Worker / Scheduler。
- API / Worker / Scheduler `/health`。
- 容器使用 read-only filesystem、`no-new-privileges`、drop capabilities 等约束。
- CI：Restore → Release Build → Tests → Compose Validation → Runtime Smoke。

### 2.4 实际验证证据

最近 Phase 0 技术验收 HEAD：`e0a2b3cebfc0aac8895555427f7cc172df2d3f37`。

GitHub Actions CI Run `32383751475`：**GREEN**。

验证结果：

- Restore：PASS
- Release Build：PASS，0 warnings / 0 errors
- Unit Tests：PASS
- Architecture Tests：PASS
- Integration Tests：PASS
- Contract Tests：PASS
- PostgreSQL 18 Migration：PASS
- Outbox transaction：PASS
- Inbox idempotency：PASS
- Compose Validation：PASS
- Runtime Smoke：PASS
- API `/health`：PASS
- Worker `/health`：PASS
- Scheduler `/health`：PASS

Phase 0 开发过程中真实发现并修复：

1. Npgsql 10.0.3 要求 EF Core `>= 10.0.4`，统一依赖版本后解决 restore downgrade。
2. OpenTelemetry DI extension namespace 缺失导致 Build 失败，补齐引用。
3. MSTest Analyzer 在 warnings-as-errors 下阻止恒真 smoke assertion，改为真实验证。
4. PostgreSQL 18 官方镜像改变 volume layout，卷从 `/var/lib/postgresql/data` 修正到 `/var/lib/postgresql`。

## 3. Phase 0 Exit Criteria

- [x] `dotnet restore` 成功。
- [x] `dotnet build -c Release` 成功且 warnings-as-errors 无警告。
- [x] `dotnet test -c Release` 全绿。
- [x] Architecture Tests 全绿。
- [x] PostgreSQL / Redis 可通过 Docker Compose 启动并通过健康检查。
- [x] Migration App 可对空数据库完成初始化。
- [x] API、Worker、Scheduler 可启动并通过健康检查。
- [x] Outbox / Inbox Integration Test 通过。
- [x] OpenTelemetry 基础 instrumentation 已接入。
- [x] CI 在 Phase 0 PR 上全绿。
- [x] README、Progress、Handoff 与实际状态同步。

**Phase 0：Accepted / Completed。**

## 4. 当前阶段 — 1.0 Release Candidate

> **分支说明（2026-08-29）**：项目已切换到 `dev` 分支重新起步，`dev` 为唯一开发主线，完成后经 PR 合入 `main`。Phase 1A/1B、用户产品和商业基础已在 `dev` 上按原设计文档重建；当前自动化 Release Gate 已通过，真实设备、真实来源和真实账户验收仍按第 6 节待定事项执行。

Phase 1A 自动化工作包状态：

1. ✅ Source DSL v1 与校验模型。（已实现，本地验证通过）
2. ✅ `RuleAdapter` 与 Fixture 驱动执行器。（已实现，本地验证通过）
3. ✅ Safe HTTP / SSRF 基础防线、请求预算与错误分类。（已实现，含连接级校验）
4. ✅ Crawler Task / Lease / Retry / DeadLetter。（已实现）
5. ✅ SourceBook / SourceChapter 持久化。
6. ✅ Canonical Book 创建与 Match Candidate 基础。
7. ✅ Canonical Chapter / Chapter Mapping。
8. ✅ Content AST / ContentVersion / ContentHash。
9. ✅ 最小 Quality Engine 与 Selected Version。
10. ✅ Public API：Search / Book / TOC / Chapter。
11. ✅ Legado v1 API Contract。
12. ✅ `ILegadoRuleGenerator` 与 `/legado/book-source.json`。
13. ✅ Web Reader 最小纵向体验（自动化基线已完成）。
14. ✅ 单来源自动追更链路（自动化基线已完成）。
15. 🚧 Phase 1A E2E / Contract / Runtime 验收（自动化门禁已通过，真实设备/来源/人工链路待定）。

### 4.1 Phase 1A 已完成工作包记录

**Source DSL v1 与校验模型**（`dev` @ `1f01918`）：

- `InkFlow.Modules.Sources.Domain`：`SourceCapability`、`RuleHttpMethod`、`RuleRequest`（路径模板 `{name}` 占位符 + Header/Query/Form）、`RuleSelector`（CSS/XPath/JSONPath）、`RuleRegex`（强制正超时，上限 2000ms）、`TrimTransform`/`ReplaceTransform`、`RuleField`、`CapabilityRule`、`SourceRuleDsl`。
- `SourceRuleDslValidator`：一次性返回全部违规——schemaVersion 固定为 "1"、sourceId 非空且无空白、能力规则不重复、路径模板以 `/` 开头且占位符合法、POST 必须带 Form、字段名唯一、每字段恰好一个抽取来源、replace 的 from 非空。
- 校验为纯声明检查：不触网、不执行正则。
- UnitTests 新增 16 个校验用例；Release Build 0 warnings / 0 errors；全部测试通过。

**Safe HTTP / SSRF 防线**（`dev` @ `5f3ae4b`）：

- `InkFlow.BuildingBlocks.Security`：`IpAddressClassification`（loopback/RFC1918/link-local 含云 metadata/CGNAT/ULA/组播/IPv4-mapped-IPv6 判定）、`IIpAddressResolver` 抽象 + DNS 实现、`SsrfGuard` 两阶段校验。
- 字面量检查：仅允许 http/https、端口白名单 80/443、拒绝字面内网 IP 与纯数字混淆主机；字面量失败短路，不发起任何网络请求。
- 解析后复检：DNS 全部结果逐一遍过网段判定，防 rebinding（执行器须配合使用已验证地址连接）。
- 新增 18 个单元测试覆盖阻断网段、scheme/port 规则与 DNS 路径；Release Build 0 warnings / 0 errors。

**RuleAdapter 与 Fixture 驱动执行器**（`dev` @ `6efdfcf`，CI Run `32834072324` GREEN）：

- `Sources.Application`：`ISourceHttpClient` / `ISelectorEvaluator` 抽象（生产 SafeHttpClient 与选择器引擎后续以适配器接入）、`RuleAdapter` 执行管道。
- 执行顺序固定：URL 构建（路径模板 + Query/Form 占位符填充、正确编码）→ `SsrfGuard` 字面量校验（内网目标拒绝且绝不出网）→ 发请求 → 状态码检查 → 字段抽取 → Trim/Replace 变换。
- 失败分类：模板变量缺失 / SSRF 拒绝 / 传输异常 / 上游状态码 / 字段抽取失败（含正则超时）；任一环节失败即整体失败，不产生部分结果。
- 正则抽取强制使用声明超时；灾难性回溯模式被报告为超时而非挂起。
- 9 个 Fixture 驱动测试全程内存执行，普通 CI 无真实第三方流量；Release Build 0 warnings / 0 errors，UnitTests 44 用例全绿。

**Crawler Task / Lease / Retry / DeadLetter**（`dev` @ `580f20e`，CI Run `32837556823` GREEN）：

- `Crawling.Domain`：`CrawlerTask` 聚合 + 强制状态机（Pending/Leased/Running/Completed/Failed/DeadLettered，非法流转抛异常）、`CrawlPayload`（仅能力 + 变量；凭据只传引用 ID 不进载荷）、`DeadLetterTask`（仅能从死信终态构造）。
- 租约语义：同一任务同时刻至多一个持有者；过期租约回收为 Pending 后重新领取计入新的尝试次数——反复超时的任务必然耗尽 `MaxAttempts` 进入死信，不会无限循环。
- `Crawling.Application`：`CrawlerLeaseService`（注入 TimeProvider）、`RetryPolicy`（全抖动指数退避、上限封顶）、`ICrawlerTaskExecutor` 契约（对 Rule/Code 适配器透明）。
- 持久化与仓储在 P1A-5 引入 EF Core 后落地。
- 新增 8 个单元测试覆盖状态机流转、租约互斥/回收、死信边界与退避策略。

**EF Core Persistence（Crawler 模块）**（`dev` @ `df7da3e`，CI Run `32839043804` GREEN）：

- `BuildingBlocks.Persistence`：`ModuleDbContext` 基类，每模块独立 Postgres schema，模块间禁止共享表。
- `CrawlingDbContext`（schema `crawler`）：`tasks` + `dead_letters` 表；Variables 存 jsonb（含 ValueComparer）；状态/租约复合索引；死信表 TaskId 唯一约束。
- `ICrawlerTaskRepository` 契约 + `EfCrawlerTaskRepository` 实现（可领取扫描、聚合写回、死信读写）。
- Migrations App 接入 `Database.MigrateAsync()`，连接串来自 `ConnectionStrings__Database`；`InitialCrawlerSchema` 迁移由 dotnet-ef 设计工厂生成。
- 5 个 Testcontainers PostgreSQL 18 集成测试：空库迁移、聚合 round-trip、状态流转持久化、可领取扫描、死信写入读取——已在远端 CI 容器环境全部通过。

**Library 基础（CanonicalBook / CanonicalChapter）**（`dev` @ `5408b12`，CI Run `32919854290` GREEN）：

- `Library.Domain`：`CanonicalBook` 聚合（稳定 BookId、追加式章节目录——序号必须连续且不可改号/插队、元数据可修订）、`CanonicalChapter` record（稳定 ChapterId）。
- `LibraryDbContext`（schema `library`）：books + chapters 表，书内章节序号唯一索引 + 级联外键。
- `ICanonicalBookRepository` + EF 实现：Save 时只增量插入新章节，已有章节 ID 永不改变（阅读历史依赖稳定 ID）。
- Migrations App 升级为多模块迁移执行器（crawler → library 依次应用）。
- 5 个领域单测 + 3 个 Testcontainers 集成测试（聚合 round-trip、章节增量写入且旧 ID 不变、缺失返回 null），远端 CI 全部通过。

**Sources 持久化 + 抓取执行器接线**（`dev` @ `740668a`，CI Run `32920940994` GREEN）：

- `Sources.Domain`：`Source` 聚合——BaseUrl 创建时经 `SsrfGuard` 字面量检查（内网/元数据地址拒绝建源）；规则文档变更必须通过 DSL 校验才能进入聚合。
- `SourcesDbContext`（schema `sources`）：规则文档存 jsonb；`ISourceRepository` + EF 实现。
- `RuleCrawlerTaskExecutor`：把 `ICrawlerTaskExecutor` 接到 `RuleAdapter`——来源不存在/未装规则/能力缺失均在不触网的前提下失败并归类原因，规则执行失败聚合为 outcome 原因。
- Migrations App 现依次应用 crawler / library / sources 三个模块的迁移。
- 测试：4 聚合单测 + 5 执行器接线测试（fixture HTTP 零真实流量）+ 3 Testcontainers 集成测试（jsonb 往返、规则整体覆盖替换）。修复了集成测试跨用例复用固定主键导致的唯一约束冲突。

**SourceBook / SourceChapter 目录与抓取结果落库**（`dev` @ `b650f82`，CI Run `32921898408` GREEN）：

- `Sources.Domain`：`SourceBook` 聚合——(sourceId, externalBookId) 身份；章节按 ExternalChapterId **幂等同步**（已存在不改动），新章节按批次顺序追加连续 Index，批内格式错误条目跳过。
- `SourceCatalogService`：抓取产物 → 持久化的转换层。BookInfo 的 title/author 字段 upsert 书目；Toc 的 `chapters` 字段（每行 `externalId TAB title`）同步目录；**未导入先同步目录**会被明确拒绝。
- 过渡协议说明：DSL v1 尚无列表选择器引擎，目录块文本协议为过渡方案，列表引擎接入后由结构化抽取取代（已在代码注释与本文档标注）。
- 持久化：sources schema 新增 source_books / source_chapters 表，(source, externalBookId) 与书内 (externalChapterId, chapterIndex) 均唯一约束；章节增量插入。
- 测试：4 领域单测 + 4 目录服务测试（fixture HTTP 零真实流量，覆盖创建/更新同 ID、幂等同步、坏行跳过、未导入拒绝）+ 2 Testcontainers 集成测试。

**Content 抓取产物落库（FetchArtifact + RawHash）**（`dev` @ `e31e94b`，CI Run `32923070031` GREEN）：

- `Sources.Domain`：`FetchArtifact` record——成功 Content 抓取的原始产物元数据，SHA-256 RawHash 创建时计算，确定性可复算。
- `SourceContentService`：触网前完成全部前置校验（来源/规则/书目/章节存在性）；内容哈希与最新产物一致 → 返回 Unchanged 不落新行；内容变化 → 新增产物版本。
- 持久化：sources schema 新增 fetch_artifacts 表，(sourceId, externalChapterId, fetchedAt) 索引支撑"最新产物"查询；`AddFetchArtifacts` 迁移入库。
- 正文清洗 / Content AST / CanonicalHash 属于 Content 模块职责，本工作包只保存原样抓取的产物元数据（架构边界）。
- 测试：3 哈希确定性单测 + 5 服务全链路测试（首次抓取/未变跳过/修订产生新版本/未知章节零触网/空抽取报错）+ 3 Testcontainers 集成测试。修复了集成测试共享容器导致的跨用例数据残留。

**Canonical Book 匹配与 Match Candidate**（`dev` @ `7458d21`，CI Run `32923929386` GREEN）：

- `Library.Domain`：`MatchCandidate`（Confirmed/Pending/Rejected）——不变量：同一来源书至多一条候选；Confirmed 映射永不改指向（对外 BookId 稳定），换绑 = 否决旧候选 + 新建。
- `CanonicalBookMatchingService`：v1 采用来源外部身份精确匹配——已有 Confirmed 候选幂等返回既有正典书；否则以来源元数据创建新正典书 + Confirmed 候选。多证据评分与人工审核属 Phase 2 / 审核流程。
- 持久化：library schema 新增 match_candidates 表，(sourceId, externalBookId) 唯一约束兜底。
- 测试：3 匹配服务单测（新建/幂等/缺失失败）+ 1 Testcontainers 集成测试。

**Canonical Chapter 映射**（`dev` @ `af1764d`，CI Run `32924655770` GREEN）：

- `Library.Domain`：`ChapterMapping`——来源章节 → 正典章节的稳定绑定；(sourceId, externalChapterId) 唯一；映射一经创建不可改指向。
- `CanonicalChapterMappingService`：书目级 Confirmed 匹配完成后，为每个未映射的 SourceChapter 在正典书上**追加式**创建稳定 CanonicalChapter 并写入映射；重复调用幂等（零新增）；来源后续新增的章节在再次同步时追加，既有映射不动。
- 持久化：library schema 新增 chapter_mappings 表，AddChapterMappings 迁移入库。
- 测试：4 映射服务单测（首次同步/幂等/增量追加/未匹配拒绝）+ 1 Testcontainers 集成测试。

**Content AST / ContentVersion / Quality v1**（`dev` @ `4bc1d7e`，CI Run `32926111385` GREEN）：

- `Content.Domain`：`ContentDocument`（段落序列 AST v1）、`ContentNormalizer`（块级标签边界 → 段落；内联标签剥离；HTML 实体解码——等价标记规范化为同一形态）、`QualityEngine` v1（可解释启发式评分：段落数/总字符/平均段长，0-100）、CanonicalHash = 规范化文本 SHA-256。
- `ContentVersion` 聚合：版本不可变；(canonicalChapterId, canonicalHash) 唯一；选优规则 = 质量分高者胜、平分取新者。
- `ContentPublishingService`：规范化 → 哈希去重 → 质量评估 → 落库 → 选优当前版本。IsCurrent 版本是"阅读不依赖实时抓取"不变量的数据基础。
- content schema：versions 表 + InitialContentSchema 迁移；Migrations App 现覆盖全部四个模块 schema（crawler/library/sources/content）。
- 测试：4 规范化/哈希单测（含等价标记哈希一致）+ 3 质量评分单测 + 4 发布服务单测。

**Public Content API v1**（`dev` @ `8493237`，CI Run `32928097955` GREEN）：

- `CatalogQueryService`(只读):书目列表(含章节数)、书目详情(有序目录)、章节正文(IsCurrent 版本)——普通阅读路径零实时抓取。
- Api 宿主 DI 接线:LibraryDbContext + Content 版本仓储 + 查询服务;端点 `GET /api/v1/books`、`/books/{id}`、`/chapters/{id}/content`,缺失返回 404。
- **修复 compose 缺陷**:api/worker/scheduler 容器此前未注入 `ConnectionStrings__Database`,首个查库请求必然失败(connection refused)——现已补齐,smoke test 增加 `/api/v1/books` 断言并附带失败时自动 dump API 日志的诊断路径。
- 测试:4 查询服务单测(列表含章节数/当前版本段落/未发布 null/缺失 null);CI 容器环境端到端验证目录端点。

**Legado v1 契约**（`dev` @ `9d53ffd`，CI Run `32929810277` GREEN）：

- `Legado.Application`：`LegadoContractService`（search/bookInfo/toc/chapterContent 四个只读翻译端点）+ `LegadoBookSourceManifest`（程序化生成 `/legado/book-source.json`,JSONPath 规则与 InkFlow 响应形态一一对应;非 http baseUrl 拒绝;searchUrl 携带 Legado `{{key}}` 占位符）。
- Api 端点:`/api/legado/v1/search|books/{id}|books/{id}/chapters|chapters/{id}` + `/legado/book-source.json`(baseUrl 取请求 scheme+host)。
- 全部只读已落库正典数据——Legado 阅读路径同样零实时抓取。
- 测试:6 契约/清单单测(搜索过滤、detailUrl/tocUrl/chapterUrl 形态、当前版本正文、清单结构与占位符、非法 baseUrl);CI smoke 增加 manifest 端点断言。

**Minimal Web Reader**（`dev` @ `ffea1a3`，CI Run `32930447713` GREEN）：

- Api 宿主服务端渲染 HTML 阅读页:`/reader`(书目列表 + 搜索表单)、`/reader/books/{id}`(详情 + 有序目录 + "开始阅读"主操作)、`/reader/read/{chapterId}`(正文段落 + 上一章/下一章导航)。
- 按 frontend-design.md 最小验收落地:移动 viewport、语义化标签(nav/main/article/role=status)、正文宽度受限(42em)、触控目标充足、空/缺失状态明确、内容与标题全部 HTML 转义(单测覆盖脚本注入转义)。
- 渲染器为纯函数(`ReaderHtml`),可离线单测;CI smoke 断言 reader 页面真实渲染。
- 测试:6 渲染器单测(转义、主操作、上下章导航、首章无上一章)。

**追更调度（Scheduler/Worker 闭环）**（`dev` @ `5c94b3f`，CI Run `32934543579` GREEN）：

- `UpdateScanService`：周期扫描已导入书目，为每本书入队 Toc 同步任务；活跃任务去重避免重复扫描。
- `TocSyncTaskHandler`：目录规则执行 → 来源章节落库 → 正典章节映射；`ContentFetchTaskHandler`：正文规则执行 → RawHash 幂等落库。按能力分派。
- Worker 宿主：轮询消费循环（租约领取 → 执行 → 完成/失败/死信落库）；生产适配器接入——`ProductionSafeSourceHttpClient`（DNS 解析级 SSRF 复检）与 `CssSelectorEvaluator`（AngleSharp 1.7.2）。
- Scheduler 宿主：30 分钟间隔追更扫描后台服务。
- **修复**：api/worker/scheduler 补回 `/health` 端点（compose healthcheck 依赖）；补注入连接串环境变量。
- 测试：116 单测全绿；容器环境 Runtime Smoke 全绿。

**第一个真实 Official Source 接入（kanunu8）**（`dev` @ `cf7b594`，CI Run `32942631739` GREEN）：

- `KanunuSourceAdapter`：首个代码型 CodeAdapter,实现 `ISourceAdapter` 统一契约——验证了兼容层的代码扩展路径。
- 处理 GB18030 编码(努努书坊为老式编码站点)与非标准页面结构;外部 ID 自包含定位(`book/{id}` / `book/{id}/{chapter}.html`)。
- 全部出网请求经 DNS 解析级 SSRF 校验;`SourceEncodings.Gb18030` 封装 CodePages 提供程序注册。
- **真实抓取验证 3/3 通过**(本机 `INKFLOW_LIVE_TESTS=1`):书目元数据(玉簟秋/灵希)、完整目录(12 章)、正文(13180 字符,GB18030 解码正确)。live 测试默认 opt-in,CI 环境自动跳过。
- 站点探测记录:17K(阿里云 WAF)、bqg70(JS 混淆)、七猫(JS 渲染)不可纯 HTTP 抓取;bige7 详情页开放但目录/正文有 JS cookie 挑战。

**Phase 1B 双来源正典验证（本轮，2026-08-27）**：

- 新增确定性 `official-a` / `official-b` Official Source 夹具；同书元数据归一化后复用一个 `CanonicalBook`，等价章节标题复用稳定 `CanonicalChapter`。
- `ChapterMapping` 持久化 `AlignmentAlgorithmVersion` 与 `AlignmentEvidence`，章节对齐采用章节序号 + 标题归一化，偏移时仅接受唯一标题匹配。
- `ContentVersion` 持久化 `QualityAlgorithmVersion` 与 `QualityEvidence`；低质量第二来源形成独立候选但不会替换已选正文，阅读查询继续读取已落库当前版本。
- 新增 EF 迁移并补齐 Designer 元数据；`dotnet ef migrations list` 已发现 Content 与 Library 新迁移。
- 自动化证据：双来源集成夹具 2/2、Unit 118/118、Architecture 1/1、Contract 1/1、Release Build 0 warnings / 0 errors。
- 真实设备 Legado 导入/阅读按用户决定暂缓，标记为后续人工验收；本机完整 PostgreSQL 集成测试因 Docker 不可用而未通过。远端 CI `33052498887` 与 Docker `33052498797` 已全绿。

**Source Capability Health 与健康感知切源（本轮，2026-08-27）**：

- `SourceCapabilityHealth` 按 `(SourceId, Capability)` 独立记录 `Unknown/Healthy/Degraded/Unhealthy/Disabled`，连续 3 次失败进入 `Unhealthy`；保存 `source-health-v1`、失败计数、时间戳和受限原因，并提供人工启用/禁用状态转移。
- `SourceCatalogService`、`SourceContentService` 记录能力成功/失败；不可用的 Toc 来源不再由 Scheduler 入队，不可用的 Content 来源不触发上游正文请求。
- `ContentSelectionService` 先排除 Content 能力不可用的版本，再按 `quality-v1` 选优；全来源不可用时保留已落库当前版本。每次选择追加 `content.selection_decisions`，保存 `content-selection-v1` 与候选/排除/回退证据。
- 新增官方 EF 迁移：`AddSourceCapabilityHealth`、`AddContentSelectionDecisions`；`dotnet ef migrations list` 已发现两者。
- 自动化证据：Unit 126/126、Architecture 1/1、Contract 1/1、双来源健康感知切源 2/2、Release Build 0 warnings / 0 errors。Sources/Content PostgreSQL 往返测试已加入；本机 Docker 阻塞，但远端 CI `33055478173` 已全绿，包含 Test、Compose Validation 与三服务 Runtime Smoke；Docker `33055478099` 的四个镜像也已全绿。
- 真实第二来源、真实运行时故障切源和 Legado 真机仍不纳入本轮自动完成，继续按第 6 节待定事项管理。

**API 安全基线与三宿主可观测性接入（本轮，2026-08-27）**：

- API 接入可替换的 fixed-window 限流策略：公共 API 默认 `120/60s`、Legado 默认 `60/60s`，支持 `RateLimiting` 配置；匿名请求按连接层 IP 分桶，认证主体按 `sub` / `client_id` 短哈希分桶，未配置可信代理前不信任 `X-Forwarded-For`。
- 限流拒绝返回 `429` 和 `Retry-After`；请求审计覆盖 `/api` 与 `/legado`，记录不可变且有长度边界的 `AuditEvent`，不写入 query string，`health`、`reader` 和搜索参数不进入审计事件。默认 sink 为结构化日志，持久化不可篡改存储仍是后续工作。
- API、Worker、Scheduler 均实际调用统一 OpenTelemetry 注册入口；Redis 分布式限流、认证/授权策略、高风险命令的持久化审计仍未宣称完成。
- 自动化证据：Unit 133/133、Architecture 1/1、Contract 1/1、Release Build 0 warnings / 0 errors。API 本地运行时烟测实际验证第二次请求返回 `429` 与 `Retry-After: 60`；首次业务请求因本机 PostgreSQL 不可用返回 500，未将其记为业务成功。
- 全量 `dotnet test InkFlow.sln -c Release`：162 个测试中 141 通过、20 个因本机 `docker_engine` 不可用在 Testcontainers 初始化阶段 BLOCKED、1 个跳过；远端 CI `33057431574` 与 Docker `33057431610` 均 GREEN（前者包含 Restore/Build/Test/Compose Validation/Runtime Smoke，后者四个镜像全部成功）。

**Worker 租约恢复与任务可靠性基础（本轮，2026-08-27）**：

- 过期 `Leased` 与 `Running` 任务现在会先回收为 `Pending` 再重新领取，重新领取计入新的尝试次数；持久化领取查询同步覆盖过期 `Running` 任务，避免 Worker 崩溃后任务永久卡住。
- 注册 `CompositeTaskExecutor` 到 Worker DI；单个任务执行异常进入失败/重试/死信路径，不再中断整个轮询循环；停止信号仍按取消语义向上传递。
- 自动化证据：新增租约恢复回归测试后 Unit 136/136、Architecture 1/1、Contract 1/1、Release Build 0 warnings / 0 errors；Worker Release 进程烟测 `/health` 返回 200。完整 PostgreSQL 集成测试仍有 20 个因本机 Docker 不可用而 BLOCKED、1 个跳过；修复后的候选提交 `f0f0d81` 已通过远端 CI `33059140418` 与 Docker `33059140552`（均 GREEN）。

**Worker 跨进程原子领取（本轮，2026-08-27）**：

- 新增 `ICrawlerTaskRepository.TryLeaseAsync`，把候选筛选、过期租约回收和新租约写入收敛为仓储操作；Worker 不再使用“先查询、再进程内领取、再保存”的竞态路径。
- PostgreSQL 仓储在事务内使用 `FOR UPDATE SKIP LOCKED` 锁定最早可领取任务；仍通过 `CrawlerTask` 完成状态流转，保留租约过期回收、尝试次数递增和失败/死信不变量。
- 新增跨上下文并发领取与过期 `Running` 回收集成用例；`FindLeasableAsync` 明确作为候选发现接口，真正领取必须走原子接口；本轮无 Schema/Migration 变更。
- 自动化证据：Unit 136/136、Architecture 1/1、Contract 1/1、Release Build 0 warnings / 0 errors；远端 PostgreSQL 集成测试 29 个中 28 通过、1 个 live 用例跳过；候选提交 `445d0bc` 的 CI `33060930049` 与 Docker `33060930029` 均 GREEN。

**Crawler Task 重试退避与持久化调度（本轮，2026-08-27）**：

- `CrawlerTask.ScheduledAt` 成为下一次可领取时间：新任务立即可领取，失败且未耗尽尝试次数时写入 `RetryPolicy` 计算的全抖动指数退避时间，死信/完成/租约回收会清除调度时间。
- `FindLeasableAsync` 与 PostgreSQL `TryLeaseAsync` 均排除尚未到 `ScheduledAt` 的 Pending 任务；Worker 失败路径使用当前尝试次数计算退避后再保存，避免失败任务立即重试。
- 新增官方 EF Migration `AddCrawlerTaskScheduling`，为 `crawler.tasks` 增加可空 `ScheduledAt` 与 `(Status, ScheduledAt)` 索引；旧记录保持 `NULL`，按兼容规则立即可领取。
- 自动化证据：Unit 137/137、Architecture 1/1、Contract 1/1、Release Build 0 warnings / 0 errors；远端 PostgreSQL 集成测试 30 个中 29 通过、1 个 live 用例跳过；Worker `/health` 本地返回 200。候选提交 `3372180` 的 CI `33062448255` 与 Docker `33062448243` 均 GREEN。
- 本机完整测试因 Docker Engine 未运行有 23 个 Testcontainers 用例在初始化阶段 BLOCKED，6 个通过、1 个跳过；该环境限制不替代远端证据。MuMu/阅读 3.0、Web Reader、真实追更与真实第二来源仍按第 6 节待定。

**抓取→发布桥与上游修订重扫（本轮，2026-08-28）**：

- 补上"任务只落 fetch_artifacts 元数据、正文从不进 content.versions"的最后一环:`ContentFetchTaskHandler` 抓取成功后把原文交给 `IChainedContentPublisher`(接口倒置——契约在 Crawling.Application,宿主 Worker 提供 `MappingContentPublisher`:经 `ChapterMapping` 定位正典身份后调 `ContentPublishingService` 发布,CanonicalHash 判重幂等 + 自带选优)。发布基础设施异常转任务失败走既有重试退避链;章节未映射返回 false 静默完成(避免无意义重试到死信)。
- 追更链式入队扩展修订重扫:`ContentFetchChainService` 现在对**零产物**(reason=new)或**最新产物过期**(早于 now - `DefaultStaleAfter`=7 天,reason=refetch)的章节入队;上游文本变化产生新 ContentVersion(版本追加不覆盖),未变化则复检行续期锚点且 Content 侧哈希幂等零新增。死信章节在下一保鲜周期自然获得一次重新入队机会(非无限复活)。
- 配套语义修正:`SourceContentService` 对 Unchanged 复检同样落一条相同哈希的真实产物行(复检是成功抓取的事实记录),使"最新产物时间"表示最近一次核查而非首次发现;原"未变不落行"行为变更已先改回归测试再实现(`Same_Content_Recheck_Is_Unchanged_And_Renews_Freshness_Anchor`)。
- 新增 `IFetchArtifactRepository.ListRecentlyFetchedExternalChapterIdsAsync(since)` 批量保鲜查询(服务端按时间+来源裁剪);无 Schema 变更、无新 Migration。构建中发现 publisher 异常最初依赖 Worker 顶层 catch 兜底,已改为 handler 内显式转 `CrawlOutcome.Fail`,边界自洽。
- 自动化证据:Unit 153/153(复检续期回归、stale/refetch 混合矩阵、发布桥编排含异常转重试与未装配兼容)、Architecture 1/1(接口倒置未破坏依赖矩阵)、Contract 1/1、Release Build 0 warnings / 0 errors;远端 Integration 34 中 33 通过 + 1 live 跳过;本机 Integration 因 docker_engine 缺失 27 例 BLOCKED 不记为通过;Worker 进程烟测 `/health` 200(DI 含发布桥解析正常)。候选提交 `3edb3dc` 的 CI `33066966836` 与 Docker `33066966966` 均 **GREEN**(含 Runtime Smoke 与四镜像)。
- 本轮不含:stale 任务错峰调度(整本同时到期时一次入队,由 Worker 短轮询串行消化)、多 Worker 并发消费、 publishing 失败的可观测告警。

**自适应健康自动恢复（本轮，2026-08-29）**：

- 缺口:Unhealthy 是死胡同——能力连续三次失败进入 Unhealthy 后,扫描/发现的健康门控永远跳过该来源,没有任何流量能把成功结果再送进健康表,恢复只能人工 Enable。
- 半开恢复,零 Schema 变更:`SourceHealthPolicy` 新增由持久化**失败计数 + UpdatedAt 推导**的探针冷却(30 分钟起步、随失败深度翻倍、封顶一天);`ConsecutiveFailures` 取消封顶(失败深度驱动退避而非被丢弃);`SourceHealthService.IsAvailableAsync` 对冷却期满的 Unhealthy 来源放行下一次真实抓取作探针——周期扫描/搜索发现天然充当探测驱动,成功回 Healthy 重置链,失败刷新锚点并延长冷却。Disabled 仍为人工终态。
- 自动化证据:Unit 163/163(冷却阶梯/边界含相等/深度增长不受阈值截断/服务级半开流程四向断言)、Architecture 1/1、Contract 1/1、Release Build 0 warnings / 0 errors;既有 159 例零破坏。候选提交 `ac0de64` 的 CI `33070869295` 与 Docker `33070869320` 均 **GREEN**(含 Runtime Smoke 与四镜像)。
- 本轮不含:主动巡检式探测(无自然流量时不发探针)、冷却参数配置化。

**冷却参数配置化（本轮，2026-08-29）**：

- 缺口:探针冷却曲线(3 次阈值/30 分钟起步/封顶一天)是编译期 const——linovelib 类 DNS 污染源的高频重试成本、低容忍场景的快速摘除,都要改代码重发布才能调整(4.9 明确记录的遗留)。
- 实现(无 Schema 变更、无 Migration、健康相关调用方零改动):曲线算法唯一实现移入 Domain 不可变 record `SourceHealthParameters.ProbeCooldown`;`SourceHealthPolicy` 变为「当前装载参数」只读视图,组合根启动时经 `Configure()` 装载。配置链:BuildingBlocks.Application 新增 `SourceHealthOptions.FromConfiguration`(节 `SourceHealth`,环境变量如 `SourceHealth__ProbeCooldownBaseMinutes`;缺省回退 v1,非整数/越界/max<base 启动即快速失败)→ Sources.Application `ToParameters()` 映射扩展 → Api/Scheduler/Worker 三宿主组合根装载(ADR 0005)。
- 持久化状态与 `source-health-v1` 算法版本不变;`Configure(null)` 经编译期常量(而非静态属性快照)恢复 v1 默认,规避「Default 捕获运行时快照」的静态初始化次序缺陷。
- 自动化证据:Unit 180/180(基线 175 + 新增 5:默认曲线一致、配置读取/回退、非法值拒绝、Configure 装载与 null 还原、进程内服务半开节奏随配置变化)、Architecture 1/1、Contract 1/1、Release Build 0 warnings / 0 errors;本机 Integration 与基线完全一致(29 例 docker_engine BLOCKED 不记为通过,6 通过 1 跳过)。候选提交 `86c250e` 的 CI `33080357611` 与 Docker `33080357613` 均 **GREEN**(含 Runtime Smoke 与四镜像);文档提交 `223e71c` 的 CI/Docker 亦 **GREEN**。
- 本轮不含:管理端运行时热更新(仅启动时装载一次)、per-source 冷却粒度。

**linovelib Search 规则与离线回归（本轮，2026-08-27）**：

- 补齐真实来源接入的确定性缺口：新增 `LinovelibSourceDefinition`，Search 使用 `POST /S6/` + 表单字段 `searchkey={key}` + 列表绑定；BookInfo、TOC、Content 三项既有能力保持不变。
- 修正来源链接 ID 归一化：Search/TOC 统一剥离 `/novel/` 与 `.html`，搜索结果可直接进入 BookInfo，章节 ID 可直接填充 Content 路径；原 TOC 规则的重复 `/novel/` 路径风险由回归测试锁定。
- 修正 `RuleAdapter` 表单模板编码：占位符先做原值替换，再在最终表单拼接处只编码一次，中文关键词不会产生 `%25` 重复编码。
- 自动化证据：本机 Unit 183/183、Architecture 1/1、Contract 1/1、Release Build 0 warnings / 0 errors；本机 Integration 36 中 29 项因 `docker_engine` 不可用而 BLOCKED、6 通过、1 跳过；Worker `/health` 本地返回 200（数据库链路因本机无 PostgreSQL 未执行）。候选提交 `52c36a4` 的 CI `33090147713` **GREEN**（Unit 183、Integration 35 通过 + 1 live 跳过、Compose 与 Runtime smoke 全通过），Docker `33090147561` **GREEN**（四镜像）。
- 本轮不含：linovelib 真实网络/搜索/BookInfo/TOC/Content 验证、阅读 3.0 真机导入、Web Reader 人工体验、真实追更与真实第二来源故障切换。

**Crawler 失败观测基线（本轮，2026-08-28）**：

- 缺口：Worker 任务失败此前只有进程级文本输出，无法按能力、失败类型和处理结果稳定聚合，也没有统一的告警入口。
- 实现：Observability BuildingBlock 新增 `CrawlerFailureObservation`、`CrawlerFailureReporter` 和 `ICrawlerFailureSink` seam；Worker 失败路径统一上报 `retry`、`dead_letter`、`not_running`。结构化日志使用 EventId `2201`；OpenTelemetry 新增 `inkflow.crawler.task.failures` 与 `inkflow.crawler.task.dead_letters` counters，仅使用低基数标签，原始失败原因不进入日志/指标标签。
- 可靠性：日志和指标 sink 逐个隔离，sink 自身异常不会改变原有重试、死信和持久化状态；死信现有原始 reason 持久化语义保持不变。本轮无 Schema、Migration 或公共 API Contract 变更。
- 自动化证据：本机 Restore PASS；Release Build PASS（0 warnings / 0 errors）；Unit 186/186、Architecture 1/1、Contract 1/1 PASS；本机 Integration 36 中 29 项因 `docker_engine` 不可用而 BLOCKED、6 通过、1 跳过；Worker `/health` 本地 200。远端 CI `33091872440` GREEN（Unit 186、Architecture 1、Contract 1、Integration 35 通过 + 1 跳过，含 Compose/Runtime smoke）；Docker `33091872458` GREEN（API、Migrations、Scheduler、Worker 四镜像）。
- 提交：`2747e2b`。本轮不含外部告警路由/阈值治理、死信人工重放与敏感信息清洗重设计，也不执行阅读 3.0 真机、真实来源和真实追更验收。

**Crawler 死信受控重放（本轮，2026-08-28）**：

- 缺口：死信此前只能停留在记录层，无法通过受控修复流程恢复执行；按架构约定不得把手工 SQL 作为正常修复路径。
- 实现：新增 `DeadLetterReplayCommand` / `DeadLetterReplayResult` 与 `ICrawlerTaskRepairRepository` seam；PostgreSQL 适配器在同一事务中锁定死信和原任务，创建全新的 `Pending` 重放任务，保留原死信失败事实，并追加 `ReplayTaskId`、`ReplayedAt`、`ReplayRequestedBy`、`ReplayReason` 修复轨迹。官方 Migration `AddDeadLetterReplay` 已生成并接入。
- 并发与幂等：重复请求返回同一重放任务；并发请求最多创建一个新任务；原死信仍保持 `DeadLettered`，已解决的原死信不再永久阻止后续同变量任务。Worker DI 已同时暴露任务仓储与修复 seam 的同一 scoped 实现。
- 边界：本轮提供 Application seam、EF 持久化与回归测试，不新增公开 Admin/Operations API；认证、命令级审计和 Repair Center 仍属后续安全/运维工作，请求审计持久化基线已在后续工作包完成。MuMu/阅读 3.0、真实来源和真实追更仍不执行。
- 自动化证据：本机 Restore PASS；Release Build PASS（0 warnings / 0 errors）；Unit 189/189、Architecture 1/1、Contract 1/1 PASS；本机 PostgreSQL Testcontainers 目标用例因 `docker_engine` 不可用而 BLOCKED；远端 CI `33094754193` GREEN（Test、Compose 校验、Runtime smoke、Diagnostics 全通过）；Docker `33094754210` GREEN（API、Migrations、Scheduler、Worker 四镜像）。
- 提交：实现 `20f75fb`，测试隔离修复 `c2d4aeb`。

**安全审计持久化基线（本轮，2026-08-28）**：

- 缺口：API/Legado 请求此前只写结构化宿主日志，无法在 PostgreSQL 中保留可查询、追加式的审计事实。
- 实现：Persistence BuildingBlock 新增独立 `audit` schema 的 `AuditDbContext` / `audit.events` 表与 `PersistentAuditEventSink`；API 使用 `CompositeAuditEventSink` 同时写 PostgreSQL 和结构化日志，保留 `429`、错误和成功请求轨迹。Migrations App 已统一应用官方 `AddAuditEvents` Migration。
- 安全边界：字段长度与换行已归一化，审计资源不包含 query string；数据库触发器拒绝 `UPDATE` / `DELETE`，保证普通应用路径只能追加。Token、Cookie、正文等秘密不进入事件；持久化失败不改变用户请求结果，但会记录运维错误。
- 边界：本轮只完成请求审计持久化基线，不新增公开 Admin API；认证/授权、命令级 before/after 审计、查询授权、保留策略、告警和 Repair Center 仍待后续。MuMu/阅读 3.0、真实来源和真实追更仍不执行。
- 自动化证据：本机 Restore PASS；Release Build PASS（0 warnings / 0 errors）；Unit 189/189、Architecture 1/1、Contract 1/1 PASS；本机审计 PostgreSQL Testcontainers 因 `docker_engine` 不可用而 BLOCKED；远端 CI `33096635143` GREEN（审计集成测试通过，Integration 38 通过 + 1 跳过，含 Compose/Runtime smoke）；Docker `33096635237` GREEN（API、Migrations、Scheduler、Worker 四镜像）。
- 提交：`cc2a089`。

**SSRF / SafeHttpClient 连接级约束（本轮，2026-08-28）**：

- 缺口：原实现只在请求前单独解析并检查 DNS 结果，随后仍交给默认 `HttpClient` 自行解析；自动重定向也没有在执行器层明确绑定校验后的目标，不能充分证明防 DNS rebinding 和重定向绕过。
- 实现：Security BuildingBlock 新增 `SsrfSafeHttpMessageHandler`，关闭环境代理，在每次新 TCP 连接时解析并检查全部地址，拒绝任一私网/环回/link-local/ULA/映射地址，直接连接同一批已校验 IP；限制 80/443 端口并将自动重定向限制为 5 跳，每个重定向连接重复校验。API、Worker、Scheduler 的来源 HTTP typed client 与 Kanunu8 客户端均接入该 Handler。
- 测试边界：新增 5 个确定性回归用例，覆盖私网地址、混合公网+私网 DNS 答案、私网字面量绕过、非标准端口和 DNS 失败；真实来源网络/阅读 3.0 真机仍按用户决定跳过，留待人工验收。
- 自动化证据：本机 Restore PASS；Release Build PASS（0 warnings / 0 errors）；Unit 194/194、Architecture 1/1、Contract 1/1 PASS；三宿主 `/health` 均返回 200。本机完整 Integration 因 `npipe://./pipe/docker_engine` 不可用而 BLOCKED（32 个类初始化失败、6 个通过、1 个跳过，退出码 1，不记为通过）；远端 CI `33099136084` GREEN（Integration 39 中 38 通过 + 1 跳过，含 Compose/Runtime smoke）；Docker `33099135992` GREEN（四镜像）。
- 提交：`379cf79`。

**Identity 认证/授权与受保护 Repair 基线（本轮，2026-08-28）**：

- 缺口：Identity 模块此前只有空壳；Crawler 死信已有事务化 Repair/Replay seam，但没有可供运维使用、且受授权保护的入口。
- 实现：新增 `User`、`RefreshSession`、`AccessToken` 聚合及 Identity Application seam；注册、登录、短期 opaque access token、refresh token 一次性轮换、登出撤销、当前用户查询均已接入 API。密码使用 PBKDF2-SHA256（带随机 salt 和迭代次数），数据库只保存密码/令牌摘要，不保存可复用的原始 token。
- 持久化：新增 `identity` schema 的 users、sessions、access_tokens 表及官方 `AddIdentityFoundation` Migration；refresh 轮换在 PostgreSQL 行锁事务内完成，旧 refresh token 并发至多成功一次。Migrations App 已纳入 Identity context。
- 授权与修复：自定义 opaque Bearer 认证建立 `sub`/`role`/`sid` 主体；`Operator` / `Administrator` 才能访问 `GET /api/v1/admin/crawler/dead-letters` 与 POST replay 入口。Replay 由认证主体提供操作者 ID，要求理由，并额外写入 `crawler.dead_letter.replay` 命令审计及死信/重放任务 reference；原死信仍保持 `DeadLettered`。
- 自动化证据：本机 Restore PASS；Release Build PASS（0 warnings / 0 errors）；Unit 209/209、Architecture 1/1、Contract 1/1 PASS；API `/health` 200，未认证 `/api/v1/auth/me` 与 Repair 入口均返回 401。本机完整 Integration 42 项中 6 通过、1 跳过、35 项因 `npipe://./pipe/docker_engine` 不可用在 Testcontainers 初始化阶段 BLOCKED，不记为通过。修复 `refresh_token` JSON 字段契约后，远端 CI `33102831333` GREEN（含 Restore/Build/Test/Compose/Runtime smoke/Diagnostics），Docker `33102831388` GREEN（API、Migrations、Scheduler、Worker 四镜像）。
- 提交：实现 `09ea265`；远端 Runtime 首跑发现并修复 `refresh_token` snake-case 绑定问题，修复提交 `9f9d5c7` 已重新通过远端验证。
- 边界：本轮不执行 MuMu/阅读 3.0 真机、真实来源、真实追更或真实第二来源故障切换；公开 Repair/Consistency Center、Redis 分布式限流、用户/组织级权限管理和更完整 Operations 能力仍待后续。

**跨模块一致性检查 v1（本轮，2026-08-28）**：

- 缺口：跨源映射、正文版本、选择审计和抓取死信此前只能依赖各模块局部约束，缺少一次性、可解释、只读的 Repair/Operations 一致性扫描入口。
- 实现：新增 `IConsistencyCheckService` 深接口和 `IConsistencySnapshotReader` Adapter seam；`EfConsistencySnapshotReader` 从 Library、Sources、Content、Crawling 四个 PostgreSQL schema 读取最小关系快照，正文只投影长度，不读取正文内容。`ConsistencyCheckValidator` 集中检查孤儿引用、父级错配、重复稳定身份、当前版本唯一性、Selection Decision 漂移、Source Health/Crawler 状态和死信重放引用。
- 入口与安全：新增受 `Operator` / `Administrator` policy 保护的 `GET /api/v1/admin/consistency`，返回 `healthy` / `issues_found`、稳定错误码、资源定位、可解释信息和有上限的 issue 列表；不自动修复、不写数据库、不新增 Migration。现有请求审计覆盖该入口。
- 测试：Unit 新增 3 例，覆盖健康快照、跨模块孤儿/错配、报告截断；Integration 新增真实 PostgreSQL 四 schema 快照投影与孤儿 `ContentVersion` 检查。
- 自动化证据：本机 Restore PASS；Release Build PASS（0 warnings / 0 errors）；Unit 212/212、Architecture 1/1、Contract 1/1 PASS；API `/health` 200，未认证 `/api/v1/auth/me` 与 `/api/v1/admin/consistency` 均返回 401。本机完整 Integration 43 项中 6 通过、1 跳过、36 项因 `npipe://./pipe/docker_engine` 不可用在 Testcontainers 初始化阶段 BLOCKED，不记为通过；远端首跑 CI `33105564941` 仅因新增集成测试把 11 字符夹具误断言为 12 而失败，未涉及实现逻辑；修复提交 `7dac6ce` 后 CI `33106044634` GREEN（43 项：42 通过、1 跳过，含 Restore/Build/Compose/Runtime smoke/Diagnostics），Docker `33106044677` GREEN（API、Migrations、Scheduler、Worker 四镜像）。
- 边界：本轮不执行 MuMu/阅读 3.0 真机、真实来源、真实追更或真实第二来源故障切换；自动修复、完整 Repair Center UI、查询授权/保留策略、告警和备份恢复仍待后续。

**Content Policy / Takedown v1（本轮，2026-08-28）**：

- 缺口：公开目录、详情、正文和 Legado 输出此前没有统一的内容下架策略，管理员也没有可追溯的下架/恢复命令入口。
- 实现：Content 新增书级 `ContentPolicyDecision`（`Takedown` / `Restore`）不可变决策历史；最新决策派生当前公开状态，同状态命令幂等，理由/操作者有长度边界并拒绝空值与日志换行。
- 持久化：新增 `content.policy_decisions` 与 `AddContentPolicyDecisions` Migration；数据库触发器拒绝 UPDATE/DELETE。Migrations App 会按既有 Content context 自动应用迁移。
- 公开读取：`CatalogQueryService` 在书目列表、详情和章节正文路径统一门控；章节正文先读取当前版本关联的 `CanonicalBookId` 再加载正文；`/api/v1/search` 发现结果与 Legado 共用隐藏语义，Web Reader 通过 Catalog 继承门控。
- 管理入口与安全：新增 Administrator-only `ContentModeration` policy；`GET /api/v1/admin/content/takedowns`、POST 下架与 POST 恢复均要求认证主体和理由，并写入 `content.policy.takedown` / `content.policy.restore` 命令审计。
- 自动化证据：本机 Restore PASS；Release Build PASS（0 warnings / 0 errors）；Unit 219/219、Architecture 1/1、Contract 1/1 PASS；API `/health` 200，未认证 Content Policy 管理入口返回 401。本机完整 Integration 45 项中 6 通过、1 跳过、38 项因 `npipe://./pipe/docker_engine` 不可用而在 Testcontainers 初始化阶段 BLOCKED，不记为通过；远端 CI `33109068649` GREEN（45 项：44 通过、1 跳过，含 Restore/Build/Compose/Runtime smoke/Diagnostics），Docker `33109068630` GREEN（API、Migrations、Scheduler、Worker 四镜像）。
- 边界：按用户决定不执行 MuMu/阅读 3.0 真机、真实来源、真实追更或真实第二来源故障切换；Content Policy 管理命令的人工管理员验收加入第 6 节待定事项。

**Source Health Operator Controls v1（本轮，2026-08-28）**：

- 缺口：Capability Health 已有自动状态机和持久化事实，但缺少受保护的运维查询与单能力人工停用/恢复入口，故障处置只能依赖内部调用。
- 实现：新增 `ISourceHealthOperations` 深接口和 API 组合根接线；`GET /api/v1/admin/sources/{sourceId}/health` 查看已记录能力状态，`POST .../{capability}/disable` 与 `POST .../{capability}/enable` 控制单个能力。所有命令要求 `Operator` / `Administrator`、认证 `sub` 和非空有界理由；停用由健康聚合阻止候选，恢复回到 `Unknown` 等待真实探针。
- 安全与审计：新增独立 `SourceOperations` policy；响应只暴露受限健康字段，命令写入 `source.health.disable` / `source.health.enable` 审计及来源/能力 reference，不修改 Source 身份、Rule 或 Canonical 内容。
- 自动化证据：本机 Release Build PASS（0 warnings / 0 errors）；Unit 221/221、Architecture 1/1、Contract 1/1 PASS；API `/health` 200，新入口未认证返回 401；本机完整 Integration 45 项中 6 通过、1 跳过、38 项因 `npipe://./pipe/docker_engine` 不可用而 BLOCKED，不记为通过。远端 CI `33110684551` GREEN（Unit 221、Integration 45 项 44 通过/1 跳过，含 Compose/Runtime smoke/Diagnostics），Docker `33110684410` GREEN（API、Migrations、Scheduler、Worker 四镜像）。
- 边界：按用户决定不执行 MuMu/阅读 3.0 真机、真实来源、真实追更或真实第二来源故障切换；管理员实际停用/恢复、告警路由、完整 Repair Center 和本机 Docker 集成复验继续列入待定/后续工程事项。

**Operations/Repair Center Read Model v1（本轮，2026-08-28）**：

- 缺口：死信、跨模块一致性和来源健康已有分散的受保护入口，但缺少一个可供运维首页消费的统一只读快照；查询授权也与修复命令共用策略，无法明确区分读权限和写权限。
- 实现：新增 `IOperationsCenterReader` 深接口及 API 组合根实现；`GET /api/v1/admin/operations/overview` 聚合来源元数据/能力健康、有限死信列表和一致性报告。死信多取一条判断 `HasMore`，对外始终只返回有界数据。
- 安全与韧性：新增 `OperationsRead` policy（`Operator` / `Administrator`），并将死信列表、一致性检查、Source Health 查询与 Operations overview 统一到该只读 policy；replay/disable/enable 保留独立命令 policy。读模型不暴露任务 Variables、CredentialReferenceId 或正文；来源健康、Crawler、Consistency 区块分别隔离异常，返回 `ready` / `partial` / `unavailable` 和稳定错误码，不泄漏基础设施异常。
- 自动化证据：本机 Release Build PASS（0 warnings / 0 errors）；Unit 223/223、Architecture 1/1、Contract 1/1 PASS；API `/health` 200，未认证 Operations overview、Consistency、Source Health 查询均返回 401。远端 CI `33112741068` GREEN（Restore/Build/Test/Compose/Runtime smoke/Diagnostics），Docker `33112741039` GREEN（API、Migrations、Scheduler、Worker 四镜像）。MuMu/阅读 3.0 真机、真实来源和 Docker Testcontainers 集成仍按既定范围跳过/阻塞，未记为通过。
- 边界：本轮只提供 API 读模型和授权 seam，不实现自动修复、Center UI、告警、备份治理或真实业务验收；人工 Operations Center 操作验收加入下方待定事项。

**Reading State v1（本轮，2026-08-28）**：

- 缺口：Identity 已具备认证主体，但用户书架、阅读进度、历史和阅读器偏好还没有独立的用户范围数据边界与 API。
- 实现：新增 Reading 领域模型、应用服务和 PostgreSQL `reading` schema；提供用户书架增删/状态更新、最近阅读历史、按书进度、阅读器偏好 API。进度写入把当前进度和章节历史放在同一事务边界内，重复请求幂等，旧时间戳不能覆盖新进度。
- 安全与数据边界：`/api/v1/me/reading/*` 只从认证 `sub` 取得用户 ID，不接受调用方传入用户 ID；所有持久化主键和查询都带用户范围。书架/进度/历史写入前复用 Canonical Book 与 Content Policy 可见性校验；缓存、Source URL 和第三方正文不进入 Reading 状态。
- 自动化证据：本机 Release Build 0 warnings / 0 errors；Unit 230/230、Architecture 1/1、Contract 1/1 PASS；API `/health` 200，未认证 Reading 入口返回 401。远端 CI `33115433510` GREEN（Unit 230、Integration 48 项 47 通过/1 跳过，含 Reading PostgreSQL migration/upsert、Compose、Runtime smoke 与 diagnostics）；Docker `33115433490` GREEN（API、Migrations、Scheduler、Worker 四镜像）。
- 边界：按用户决定不执行 MuMu/阅读 3.0 真机、真实来源和真实故障切源；私人书库、TXT/EPUB 导入导出、PWA 能力与人工 UX/视觉验收继续列入后续事项。Personal Legado Token v1 与 Web Reader v1 已分别落地，阅读 3.0 导入与撤销后失效仍列入人工验收。

**Personal Legado Token v1（本轮，2026-08-28）**：

- 缺口：阅读 3.0 只有公共书源契约，缺少用户可撤销、可过期且不暴露长期秘密的个人模式；Identity 也没有独立的 Legado 凭证边界。
- 实现：新增 `LegadoAccessToken` 聚合和 `identity.legado_tokens` 表；原始令牌使用 `lf_lgd_` 前缀，仅在 `POST /api/v1/me/legado/tokens` 成功响应中出现一次，数据库只保存 Prefix + SHA-256 Hash。`GET /api/v1/me/legado/tokens` 只返回元数据，`DELETE /api/v1/me/legado/tokens/{tokenId}` 按用户隔离且幂等撤销。
- Legado 接入：Personal 书源清单随签发响应返回，搜索/详情/目录/正文使用 `/api/legado/v1/personal/*`；阅读 3.0 通过 `header` JSON 发送 `X-InkFlow-Legado-Token`，令牌不进入 URL。公共 `/api/legado/v1/*` 与公共清单保持兼容。
- 授权与审计：新增独立 `InkFlowLegadoToken` authentication scheme 和 `LegadoRead` policy；每次请求验证过期、撤销、scope 和用户状态。签发/撤销写入脱敏命令审计，不记录原始令牌；Personal API 仍受既有限流和请求审计覆盖。
- 测试：新增领域、服务、认证 handler、端点审计回归；扩展 Legado 单测/Contract、Identity PostgreSQL migration roundtrip。覆盖摘要存储、用户隔离、过期/撤销、header 认证、公共/个人 URL 和审计无秘密。
- 自动化证据：本机 Restore PASS；Release Build PASS（0 warnings / 0 errors）；Unit 245/245、Architecture 1/1、Contract 2/2 PASS。本机 Identity PostgreSQL Testcontainers 3 个目标用例因 `npipe://./pipe/docker_engine` 不可用而 BLOCKED，不记为通过；远端 CI `33118314796` GREEN（Restore/Build/Test/Compose/Personal Legado Runtime smoke/Diagnostics 全通过），Docker `33118314789` GREEN（四镜像）。
- 提交：`fbe0c62`。
- 边界：按用户决定不执行 MuMu/阅读 3.0 真机、真实来源、真实追更和真实第二来源切换；Personal 书源在阅读 3.0 中的导入、四步阅读和撤销后失效仍列入人工验收。

**Reader 搜索接入发现流（本轮，2026-08-29）**：

- 缺口（上一轮明确记录的遗留）:`/reader` 的搜索表单不过滤结果也不触发来源发现——Web 阅读路径搜任何书都返回全库列表或空手而归,「搜索→详情→阅读」主路径在 Web 端是断的,与已接发现流的公共 API/Legado 不一致。
- 实现:`GET /reader?q=` 非空时先经 `BookDiscoveryService` 幂等发现(逐源失败隔离),再从**落库正典数据**过滤返回——阅读路径零实时抓取不变量不破;过滤语义下沉为 `CatalogQueryService.SearchBooksAsync`(书名/作者大小写不敏感包含匹配,空白=浏览全部),`LegadoContractService.SearchAsync` 改为委托复用,三端共用同一过滤语义。
- 用户体验(遵循 frontend-design.md):区分「书库为空」(引导搜索自动收录)与「搜索无结果」(建议换词)两种空态并附 `role="status"`;命中显示结果计数;来源部分失败渲染为人话降级提示「部分线上来源暂时无法访问」,**SourceId 与内部异常细节零泄漏**(单测断言);发现环节整体异常也只降级不阻断页面。
- 安全:同步触网的 `/reader` 端点接入既有公共 fixed-window 限流策略(匿名按连接层 IP 分桶)。
- 附带:提交了工作区中既有的 `ListUnhealthyAsync` 测试局部变量重命名残留(c8f7af0 未清理的暂存改动,语义无变化)。
- 自动化证据:Unit 175/175(基线 169 + 新增 6:过滤语义×2、双空态/命中计数/降级零泄漏/搜索词转义等)、Architecture 1/1、Contract 1/1(Legado DTO 形态未变)、Release Build 0 warnings / 0 errors。本机 Integration 因 docker_engine 缺失不执行,以远端 CI 为准。候选提交 `48c05a2` 的 CI `33076415164` 与 Docker `33076415247` 均 **GREEN**(含 Runtime Smoke 与四镜像);文档提交 `94075c4` 的 CI `33076614633` 与 Docker `33076614570` 亦 **GREEN**。
- 本轮不含:搜索排序/分页与全文检索(v2)、BookListPage 分页、Discovery 异步化。

**Web Reader v1 体验基线（本轮，2026-08-28）**：

- 缺口：既有 `/reader` 三页面流只有极简样式，缺少阅读设置、响应式信息组织、可访问工具栏、主题/字号/行高控制和长文阅读进度反馈。
- 实现：保留服务端渲染与 Canonical Content 只读边界，重做书目列表、书籍详情和章节页的语义结构与统一 token；章节页增加目录、阅读设置、上一章/下一章、滚动进度条和空正文状态。设置提供 System/Light/Sepia/Dark、字号和行高，使用受限值并保存在当前设备 `localStorage`；脚本不可用时正文和章节导航仍可用。
- 安全与可访问性：书名、作者、章节标题和正文全部 HTML 转义；不渲染 SourceId 或上游 HTML。页面使用语义 landmark、skip link、可见焦点、`dialog` 设置面板、键盘关闭和 `prefers-reduced-motion` 降级；移动端触控目标和窄屏章节导航单列适配。
- Benchmark：按 `docs/engineering/benchmarks/web-reader-v1.md` 对照 Royal Road、Kobo Web Reader 与 Wuxiaworld 的官方阅读说明，采用“正文页一跳打开设置、主题/字号/行高、目录/上下章直达、设备本地偏好”的模式，不复制竞品视觉。
- 测试与证据：新增 Reader HTML 结构、设置控件、空正文状态和 SourceId 不泄漏回归；本机 Restore、Unit 247/247、Architecture 1/1、Contract 2/2、Release Build 0 warnings / 0 errors PASS。候选提交 `a8d1c23` 的远端 CI `33120844695` GREEN（Restore/Build/Test/Compose/Runtime smoke/Diagnostics），Docker `33120844685` GREEN（API、Migrations、Scheduler、Worker 四镜像）；CI smoke 已增加 `/reader` 搜索语义、Reader v1 样式和 reduced-motion 标记检查；浏览器截图、四尺寸人工视觉验收按用户决定保留待定。
- 边界：本轮不实现 PWA 安装/离线缓存、服务端 Reading State 同步、评论/书签、分页阅读或真实设备验收；Web Reader 浏览器视觉与长时间阅读体验仍需人工验收。

**Reader/PWA 用户状态 v1（本轮，2026-08-28）**：

- 实现：Reader 共享导航接入账户、书架和历史入口；新增 `/reader/account`、`/reader/shelf`、`/reader/history`、`/reader/offline` 以及同源 Manifest、SVG 图标和 Service Worker 路由。登录/注册复用既有 Identity API，登录后渐进增强加载 Reading State 书架/历史，书籍详情可加入书架，章节页记录进度/历史并同步阅读偏好。
- PWA 边界：Manifest 使用 `/reader` 作为启动入口，Service Worker 只缓存公开 Reader 壳和离线提示，导航网络失败时返回离线页；不缓存 `/api/v1/me/*`、认证响应、令牌或私人内容，注册失败不影响普通服务端页面阅读。
- 会话与安全：当前标签页仅以 `sessionStorage` 保存短期 Web Access/Refresh Token；API 只用同源 `Authorization` Header，401 最多尝试一次刷新，失败即清理会话。该受限例外已记录在 ADR 0006 和 `security-model.md`，未新增 Migration 或改变 Legado 认证边界。
- 自动化证据：本机 Restore PASS；Release Build 0 warnings / 0 errors；Unit 252/252、Architecture 1/1、Contract 2/2 PASS。全量 `dotnet test InkFlow.sln -c Release --no-build` 的 IntegrationTests 为 48 项：6 通过、41 项因本机 `npipe://./pipe/docker_engine` 不可用而 BLOCKED、1 项跳过，进程因此 FAIL，不记为本机集成通过。候选提交 `b3561a2` 的远端 CI `33123325151` 与 Docker `33123325184` 均 GREEN，Runtime smoke 已覆盖 Manifest、Service Worker、账户、书架和历史公开路由。
- 边界：PWA 实际安装、离线行为、登录/注册、跨页面刷新、书架/历史/进度和偏好同步的浏览器验收尚未执行；当前不承诺跨标签页/跨设备同步、离线私人正文、评论/书签、私人书库或 TXT/EPUB 导入导出，均列入第 6 节待定事项。

**搜索发现接入：冷启动主路径打通（上一轮，2026-08-29）**：



- 缺口:Legado/公共 API 的搜索只过滤已入库书目——用户搜任何新书都一无所获,且 v1 自动匹配(`CanonicalBookMatchingService`)自实现以来无生产调用方,Phase 1A 验收第 1 项"从来源搜索书籍"始终停留在机制层。
- 新增 `BookDiscoveryService`(Crawling.Application):枚举已登记来源 → Search 能力健康过滤(不健康跳过并警告)→ 多源关键词搜索(**失败隔离**:单源异常只产生 warning 不影响其他来源)→ 命中后幂等导入 BookInfo → 走 v1 匹配(Confirmed 幂等 / 同名同作者挂接既有正典书 / 新建)→ 按正典身份归并,多源命中合一条并带 SourceIds 与 AlreadyInLibrary 标记。
- API:`GET /api/v1/search` 返回归并结果+逐源 warnings;`GET /api/legado/v1/search` 改为先发现再从落库数据返回 Legado DTO——**契约形态不变**,阅读 3.0 冷启动从此可发现未入库书目。导入的书自动进入 Scheduler→Worker 追更链路,发现即建档。
- 组合根:ISourceRepository.ListAsync(EF 实现);Api 宿主补引 Sources/Crawling/Kanunu8 项目并注册适配器组合根。**Api 进程烟测实测抓到真实缺陷**:ProductionSafeSourceHttpClient 缺 IIpAddressResolver 注册导致 search 端点必然 DI 失败(与数据库无关)——补注册后复测通过,该缺陷若仅靠单测永远暴露不了。
- 测试:发现服务 6 例(双源同名同作者归并+双 Confirmed、不健康跳过、异常隔离、无适配器跳过、重复发现幂等、空查询零触达)+ListAsync EF 集成用例;首次 CI 运行暴露 List 用例误按空库断言总数(共享容器跨用例数据残留的既有教训),改为专属 ID 存在性断言后复绿。
- 自动化证据:Unit 159/159、Architecture 1/1、Contract 1/1(Legado DTO 未变)、Release Build 0 warnings / 0 errors;远端 Integration 35 中 34 通过 + 1 live 跳过;本机 docker_engine 缺失致 Integration 28 例 BLOCKED 不记为通过。候选提交 `66fc150` 初跑 CI RED(上述 List 断言),修复提交 `42ac47e` 的 CI `33069358438` 与 Docker `33069358437` 均 **GREEN**(Unit 159 Passed、Integration Total 35 Passed 34,含 Runtime Smoke 与四镜像)。
- 本轮不含:搜索结果排序/分页与全文检索(v2 评分)、Discovery 的异步化(当前同步触网由限流保护)、Reader 页接入发现流(已于下一轮补齐,见「Reader 搜索接入发现流」)。

**追更正文闭环：目录联动正文抓取（上一轮，2026-08-28）**：

- 补齐"事件触发"缺口：`TocSyncTaskHandler` 在目录同步 + 正典映射成功后调用 `ContentFetchChainService`，为**该来源从未抓取过正文**的章节自动入队 Content 抓取任务——"检测新章 → 抓取"不再依赖人工种子或额外扫描周期。
- 入队判定四个不变量全部满足才触发：书目存在且有章节；Content 能力健康（不可用来源零上游请求）；该章节在该来源下无 FetchArtifact；无同 `(source, content, chapter)` 的阻止性任务。
- 新增 `ICrawlerTaskRepository.HasConflictingTaskAsync`：Pending/Leased/Running 视为在途冲突去重，**DeadLettered 同样阻止**——死信任务不会被周期扫描反复复活，只能走人工处理路径；Completed 不阻止。EF 实现按 `(source, capability, 状态)` 服务端裁剪后内存匹配 jsonb 变量，无 Schema 变更、无新 Migration。
- 新增 `IFetchArtifactRepository.ListFetchedExternalChapterIdsAsync` 批量存在性查询（单次往返甄别整本书的未抓章节）；修复了本轮开发中发现的新文件误覆盖既有 FetchArtifactRepositoryTests 的操作失误，原 3 个用例已按 HEAD 原样恢复。
- Worker 轮询节奏配套：有任务时 250ms 短轮询尽快消化整批联动任务，空闲回退 15s 低成本等待。
- 测试：新增链式服务 7 例 + Handler 编排 3 例（含重复同步不重复入队、映射缺失不联动）+ EF 语义集成 3 例（阻止态矩阵、批量存在性、跨源隔离）。Unit 147/147、Architecture 1/1、Contract 1/1、Integration 远端 33 中 32 通过 + 1 live 跳过、Release Build 0 warnings / 0 errors；Worker 进程烟测 `/health` 200（本机无库时轮询错误被捕获不崩进程）。本机 Integration 因 docker_engine 缺失 24 例 BLOCKED，不记为通过。候选提交 `94c8be9` CI `33065212994` 与 Docker `33065212936` 均 **GREEN**（含 Runtime Smoke 与四镜像构建）。
- 本轮不含：上游已抓正文的修订重扫（RawHash 已有产物即跳过）、死信人工重放工具、多 Worker 并发消费。

**Crawler Task / Lease / Retry / DeadLetter**（旧 main 记录，已由上方条目取代）：

- Domain：`CrawlerTask` 状态机、`CrawlerTaskStatus`。
- Application：`CrawlerLeaseService`、`RetryPolicy`、`DeadLetterTask`、`ICrawlerTaskExecutor`。
- Infrastructure：`CrawlerTaskRepository` 契约 + EF 实体/配置/`CrawlingDbContext`/`DeadLetterEntity` 骨架。

**Source DSL v1 与校验模型**（本轮）：

- `InkFlow.Modules.Sources.Domain`：`SourceCapability`、`RuleHttpMethod`、`SelectorKind`/`RuleSelector`、`RuleRegex`（强制 Timeout）、`RuleTransform`（Replace/Trim）、`RuleField`、`RuleRequest`、`CapabilityRule`、`SourceRuleDsl`（含 `schemaVersion`）。
- `SourceRuleDslValidator`：结构与安全校验（schemaVersion 受支持、SourceId 必填、能力/字段唯一、正则必须正 Timeout、字段必须有抽取来源、Replace 必须有 from/to）。
- 非目标（留待后续）：JSON 序列化、Safe HTTP/SSRF、RuleVersion 发布流程。
- 验证：Release Build 0 warnings / 0 errors；Unit Tests 通过；Architecture Tests 3 通过。
- CI：本地验证通过，远端 CI 未触发（`CI Pending / Not Triggered`）。

**RuleAdapter 与 Fixture 驱动执行器**（本轮）：

- `InkFlow.Modules.Sources.Application`：`RuleAdapter`（URL 模板替换、请求构建、regex 提取 + Timeout、selector 抽象、Replace/Trim 变换）、`ISourceHttpClient`、`ISelectorEvaluator`、`SourceHttpRequest`/`SourceHttpResponse`、`CapabilityResult`。
- 执行通过 `ISourceHttpClient` 抽象（生产为 SafeHttpClient，测试用内存 Fixture），普通 PR CI 不依赖真实第三方站点。
- 非目标（留待后续）：CSS/XPath/JSONPath 具体引擎、Safe HTTP/SSRF。
- 验证：Release Build 0 warnings / 0 errors；Unit Tests 24 通过；Architecture Tests 3 通过。
- CI：本地验证通过，远端 CI 未触发（`CI Pending / Not Triggered`）。

**Operations/Repair Center UI v1（本轮，2026-08-28）**：

- 缺口：Operations Read Model v1 已提供统一快照，但运维人员仍需直接调用多个 API，缺少按来源健康、死信和一致性分组的可操作界面。
- 实现：新增受保护数据渐进加载的 /admin/operations 静态页面；登录后先验证 Operator / Administrator 角色，再读取有限的 admin operations overview 快照。页面分组呈现整体状态、来源能力、死信和一致性问题，区分 ready / partial / unavailable、合法空状态、401/403 和稳定错误。
- 受控操作：来源能力停用/恢复和死信重放使用可访问确认对话框，要求 1–512 字符理由，结果展示 Replayed / AlreadyReplayed、重放任务 ID 或恢复后的待探测语义；服务端 policy、审计和状态机仍是最终边界。
- 安全与体验：动态数据只通过 DOM textContent 写入；不缓存管理数据，不显示凭据引用、任务 Variables 或正文载荷；支持键盘焦点、语义标题/表格、aria-live、颜色之外的文字状态、窄屏布局和 prefers-reduced-motion。基准记录见 docs/engineering/benchmarks/operations-center-v1.md。
- 测试与证据：ReaderHtml 回归 19/19；本机 Unit 254/254、Architecture 1/1、Contract 2/2、Release Build 0 warnings / 0 errors PASS。API 运行时静态页面 200、/health 200、未认证 overview 401；本机全量 Integration 48 项为 6 通过、41 项因 docker_engine 不可用而 BLOCKED、1 项跳过，不记为集成通过。远端 CI 33125476460 GREEN（Restore/Build/Test/Compose/Runtime smoke/Diagnostics），Docker 33125476441 GREEN（API、Migrations、Scheduler、Worker 四镜像）。
- 边界：本轮未执行 Operator/Administrator 浏览器操作、跨尺寸视觉、键盘/对比度截图和真实修复命令；这些人工验收继续列入第 6 节。自动修复、告警、备份治理、私人书库和真实来源验收仍未实现。

**Admin Audit Read v1（本轮，2026-08-28）**：

- 缺口：审计事实已经通过 PostgreSQL 追加式 sink 持久化，但此前没有受保护的读端，Operations/Security 无法按条件复核事件；查询也缺少有界分页和稳定游标约束。
- 实现：Persistence BuildingBlock 新增 `IAuditEventReader` / `EfAuditEventReader`，只读使用 `AsNoTracking`、精确过滤、最多 `limit + 1` 取数和 `(OccurredAt, Id)` 稳定游标；API 新增 `GET /api/v1/admin/audit/events`，使用独立 `AuditRead` policy（`Operator` / `Administrator`），默认 50、最多 100 条，支持 `from`、`to`、`action`、`outcome`、`actorId`、`cursor`，异常映射为 `audit_unavailable`。
- 安全边界：游标为不透明 Base64Url 值并重新校验时间戳/事件 ID；过滤器拒绝控制字符和超长输入；读端没有 CRUD 更新/删除能力，数据库追加式触发器仍是最终不可篡改边界。
- 自动化证据：本机 `dotnet restore InkFlow.sln` PASS；Release Build 0 warnings / 0 errors PASS；Unit 263/263、Architecture 1/1、Contract 2/2 PASS。API `/health` 200，未认证审计查询返回 401；本机完整 Integration 49 项为 6 通过、42 项因 `npipe://./pipe/docker_engine` 不可用而 BLOCKED、1 项跳过，不记为本机集成通过。远端 CI `33128764947` GREEN（Restore/Build/Test：48 通过、1 跳过，Compose/Runtime smoke/Diagnostics 全部通过），Docker `33128764869` GREEN（API、Migrations、Scheduler、Worker 四镜像）。
- 边界：本轮未使用 Operator/Administrator 凭证执行审计查询和来源授权人工验收；组织/更广泛资源权限、保留/清理策略、告警路由和真实运维演练仍待后续工作包，不能据此宣称 Security/Operations Release Gate 完整关闭。

**PostgreSQL Backup/Restore Drill v1（本轮，2026-08-28）**：

- 缺口：Compose 已提供 PostgreSQL 与独立 Migrations App，但此前没有在 CI 中对真实运行数据执行可复核的备份恢复验证；“有备份命令”不能替代恢复证据。
- 实现：新增 `scripts/backup-restore-drill.sh`，在 Runtime smoke 后使用 PostgreSQL custom format 导出当前数据库，创建隔离恢复库并执行 `pg_restore`；脚本枚举所有非系统基础表比较行数签名，同时强制确认 `audit.events` 非空且恢复后数量一致，结束时删除隔离库和临时归档。CI 在 Runtime smoke 后接入该步骤，并继续执行统一 Diagnostics 清理。
- 安全/边界：数据库名、用户和恢复库标识符经过严格校验；导出/恢复使用 `--no-owner --no-acl`，不打印连接串或归档内容；脚本不修改源数据库。该步骤验证可恢复性，不声明生产异地备份、加密、保留策略、RPO/RTO 或告警已完成。
- 自动化证据：本机 `bash -n scripts/backup-restore-drill.sh` PASS；本机实际 Docker/Compose 演练因 `docker` 命令不可用 BLOCKED，不记为通过。远端 CI `33129734525` GREEN，Test 48 通过、1 跳过，Runtime smoke、PostgreSQL backup and restore drill、Diagnostics 全部通过；演练日志记录 `archive=49125 bytes, audit_events=22`。Docker `33129734604` GREEN（API、Migrations、Scheduler、Worker 四镜像）。
- 边界：生产备份调度、异地存储/加密、备份保留与删除治理、恢复授权、RPO/RTO 目标和告警仍是后续 1.0 Operations 工作；本轮只关闭 CI 级恢复可用性证据。

**Redis Distributed Rate Limit v1（本轮，2026-08-28）**：

- 缺口：API/Legado 原有 fixed-window 仅存在于单个 API 进程内，多个实例会各自放大配额，不能满足 1.0 的 Rate Limit Release Gate。
- 实现：新增 `RedisRateLimitCounter` 与 `RedisFixedWindowRateLimiter`；Redis Lua 脚本在服务端原子执行读取、配额判断、递增和过期，公共 API/Legado policy 保持原有边界。Compose API 显式注入 `ConnectionStrings__Redis`，客户端身份只以短哈希进入 Redis key，不写入原始 token 或 IP。
- 故障边界：Redis 暂不可用时切换到同配额/窗口的本地 fixed-window limiter，并以恢复感知日志记录，不无界放行；该降级期间不保证跨实例全局一致性。动态用户/组织配额、加权成本和 Redis 告警不属于本轮。
- 自动化证据：本机 Restore PASS；Release Build 0 warnings / 0 errors PASS；API 安全单元测试 11/11 PASS，包含跨 limiter 共享计数、限流键脱敏和有界降级；新增真实 Redis Integration 用例在未提供 `INKFLOW_REDIS_CONNECTION` 时明确跳过。本机完整测试为 Unit 267/267、Architecture 1/1、Contract 2/2 PASS；Integration 50 项中 6 通过、42 项因 `npipe://./pipe/docker_engine` 不可用而 BLOCKED、2 项跳过，不记为本机 Redis 集成通过。
- 远端验收：提交 `2bace7d` 的 CI `33131258779` **GREEN**；完整 Test 为 50 项、48 通过、2 跳过，Runtime smoke、Redis distributed rate-limit integration（真实 Redis 两条独立连接，1/1 通过）、PostgreSQL backup/restore 与 Diagnostics 全部通过；备份日志为 `archive=49204 bytes, audit_events=23`。Docker `33131258754` **GREEN**（API、Migrations、Scheduler、Worker 四镜像）。
- 工作包状态：Redis 分布式 fixed-window 计数与真实 Compose 验收已完成；动态用户/组织配额、加权成本、Redis 告警与故障降级期间的跨实例全局一致性仍不在本版本范围内。

**Operations Alert Snapshot v1（本轮，2026-08-28）**：

- 缺口：已有失败 counters、来源能力健康、死信和一致性读模型，但没有统一、受保护且可被外部监控轮询的当前告警入口；Redis 限流存储故障也没有可读取的健康状态。
- 实现：新增 `OperationsAlertReader` / `OperationsAlertEvaluator` 与 `GET /api/v1/admin/operations/alerts`；按配置化阈值汇总来源能力不可用、死信、一致性问题、Operations 区块不可用和 Redis 限流存储不可用，结果包含稳定 code/severity/resource、总数和截断标记。
- 安全/边界：入口复用 `OperationsRead`（仅 Operator/Administrator）；告警只返回稳定错误描述，不返回异常文本、连接串、Token、IP 或来源失败原文。快照不执行修复、不写业务事实、不保存历史、不去重、不发送外部通知。
- 自动化证据：本机 `dotnet restore InkFlow.sln` PASS；Release Build 0 warnings / 0 errors；Unit 272/272、Architecture 1/1、Contract 3/3 PASS；本机完整 Integration 50 项中 6 通过、42 项因 `npipe://./pipe/docker_engine` 不可用而 BLOCKED、2 项按环境跳过，不记为本机集成通过。CI Runtime smoke 新增匿名 401 与 Reader 403 守卫。
- 远端验收：提交 `7e03def` 的 CI `33132755108` **GREEN**；Test 分项目为 Unit 272/272、Architecture 1/1、Contract 3/3、Integration 50 项中 48 通过/2 跳过；Runtime smoke、Redis distributed rate-limit integration（真实 Redis 1/1）和 PostgreSQL backup/restore（`archive=49249 bytes, audit_events=24`）全部通过。Docker `33132755124` **GREEN**（API、Migrations、Scheduler、Worker 四镜像）。
- 工作包状态：告警快照和阈值治理基线已完成；外部通知路由、历史/去重、保留策略、生产告警渠道和完整人工 Operations 验收仍待后续 1.0 Operations Gate。

**CI Security Scan 基线 v1（本轮，2026-08-28）**：

- 缺口：1.0 已要求依赖漏洞、Secret、SAST、容器扫描和 SBOM 证据，但此前只有构建/测试/Compose 验证，没有独立的安全扫描工作流，也没有发布前镜像阻断。
- 实现：新增 `.github/workflows/security.yml`，执行 NuGet 传递依赖漏洞审计、Trivy 源码/配置/依赖的 HIGH/CRITICAL 漏洞、Secret 与 Misconfiguration 扫描、C# CodeQL SAST 和 CycloneDX 源码 SBOM；审计结果、Trivy SARIF、CodeQL 结果和 SBOM 均作为工作流产物保留。由于仓库未启用 GitHub Code Scanning API，结果不上传到代码扫描面板。
- 发布保护：`.github/workflows/docker.yml` 先构建并加载 API、Migrations、Scheduler、Worker 四个镜像，逐一执行 Trivy HIGH/CRITICAL 漏洞扫描，全部通过后才推送所有镜像标签。
- 远端验收：提交 `f58599b` 的 CI `33134804300` **GREEN**，Security `33134804292` 的 Source SBOM、Filesystem security scan、NuGet dependency audit 和 CodeQL SAST 全部通过，Docker `33134804238` **GREEN**（四镜像均完成发布前扫描与发布）。
- 边界：本轮不宣称生产镜像准入、扫描报告长期保留、Secret 轮换、动作版本治理或部署环境策略已完成；`ignore-unfixed` 仅使不可修复漏洞不阻塞当前基线。真实来源、真机/阅读 3.0 和人工验收继续按第 6 节待定清单执行。

**Resource-level Source Authorization v1（本轮，2026-08-28）**：

- 缺口：原有 `OperationsRead` / `SourceOperations` 只能表达 Operator/Administrator 的平台级角色边界，无法限制 Operator 只能读取或控制被明确授权的来源，也缺少授权变更的可追溯管理入口。
- 实现：Identity 新增 `PermissionGrant` 聚合、`permission_grants` 表及官方 EF Migration；新增管理员专用的来源授权列表/授予/撤销 API。授权仅面向 active Operator，支持 `source.read` 与 `source.manage`，active `source.manage` 隐含 `source.read`；撤销保留历史，active grant 由部分唯一索引保证幂等。
- 接入边界：直接来源健康查询、来源能力停用/恢复，以及 Operations overview/alerts 中的来源健康区块均执行来源级授权；Administrator 绕过授权。Crawler 与 consistency 区块在 v1 仍按现有 `OperationsRead` 作为平台级视图，不伪装成来源级过滤；组织、租户、计费和通用私有资源权限不在本轮范围。
- 审计/安全：授权授予、撤销和拒绝结果记录认证操作者、来源、理由、结果和资源 reference；输入理由有界并拒绝控制字符，响应不返回密钥、凭据或正文。匿名、Reader 以及没有对应 active grant 的 Operator 均不能读取或管理目标来源。
- 自动化证据：本机 `dotnet restore InkFlow.sln` PASS；Release Build 0 warnings / 0 errors PASS；Unit 279/279、Architecture 1/1、Contract 3/3 PASS。完整 Integration 51 项中 6 通过、43 项因 `npipe://./pipe/docker_engine` 不可用而 BLOCKED、2 项跳过，不记为本机集成通过；本轮未执行带凭据的本地 Runtime/人工验收。
- 远端验收：修复后的提交 `a663cef` 的 CI `33137358470`、Docker `33137358485`、Security `33137358428` 均 **GREEN**；CI 含 Runtime smoke、Redis 分布式限流集成、PostgreSQL 备份恢复演练和 Diagnostics，Docker 完成四镜像发布前扫描，Security 的 NuGet、SBOM、Trivy 和 CodeQL 全部通过。
- 边界：本轮完成来源级授权机制和自动化验证，但 MuMu/阅读 3.0、真实来源/故障切换、Operations/授权凭据人工验收仍按第 6 节待定；更广泛资源、组织/租户权限治理以及审计生产法律/合同保留、归档和删除授权仍未完成。

**Legado Contract Release Gate v1（本轮，2026-08-28）**：

- 缺口：Legado 模块已有独立 DTO、端点和书源生成逻辑，但 ContractTests 只覆盖程序集加载与 Personal header，未把生产 Release Gate 要求的完整链路作为一个可重复门禁锁定。
- 实现：新增 `LegadoCompatibilityProfile`（`legado-book-source-v1`、客户端 3.0、能力集合）和 `ILegadoRuleGenerator`/`LegadoRuleGenerator` seam；API 书源清单及 Personal Token 签发均通过该生成器生成，保留旧静态入口兼容已有调用方。
- Contract Gate：新增 `LegadoContractReleaseGateTests`，按 `Generate Rule → JSON Validate → Search → BookInfo → TOC → Content` 顺序验证规则字段/JSONPath、HTTP Web JSON 命名形态、稳定 BookId/ChapterId、链接前缀、正文内容和当前版本读取；夹具只使用内存中的已落库正典数据，不触网。
- 自动化证据：本机 `dotnet restore InkFlow.sln` PASS；Release Build 0 warnings / 0 errors；Contract 5/5、Unit 279/279、Architecture 1/1 PASS。完整 Integration 51 项中 6 通过、43 项因 `npipe://./pipe/docker_engine` 不可用而 BLOCKED、2 项跳过；本次全量 Test 命令因该环境阻塞返回非零，不视为业务逻辑失败。
- 远端验收：提交 `aae5295` 的 CI `33138900850`、Docker `33138900845`、Security `33138900869` 均 **GREEN**；CI 的 Runtime smoke、Redis 分布式限流、PostgreSQL 备份恢复和 Diagnostics，Docker 四镜像发布前扫描，以及 Security 的 NuGet/SBOM/Trivy/CodeQL 均通过。Security 仅有既有 Actions Node 20 弃用提示与仓库未启用 Code Scanning 的非阻断告警。
- 验收边界：本轮未执行 MuMu/阅读 3.0、真实来源、真实 HTTP 客户端导入或人工验收；这些仍按第 6 节待定。Profile/Contract Gate 自动化通过不等于 Phase 1A/1B 外部验收或 1.0 完成。

**Private Library v1 后端基础（本轮，2026-08-28）**：

- 缺口：用户身份和 Reading State 已有基础能力，但没有与公共 Canonical Library 隔离的私人书目实体、迁移和用户范围 API。
- 实现：Library 新增独立 `PrivateBook` 聚合和 `private_books` 表；PrivateBookId 与公共 BookId 分离，复合主键包含 UserId，所有仓储读取/更新/删除显式按认证主体范围执行。非所有者与不存在记录统一返回 NotFound。
- API：新增受认证保护的 `GET/POST/GET/{id}/PUT/{id}/DELETE/{id} /api/v1/me/private-library/books`；仅允许书名和可选作者元数据，创建/更新输入有界，删除为当前元数据阶段的所有者直接删除。
- 边界：本轮不把私有书目写入 Canonical、公共搜索、Legado、Content Policy 或公共 Reading Shelf；TXT/EPUB 导入、私有正文/章节、导出恢复、浏览器 UI 和人工验收另行处理。领域词汇见根目录 `CONTEXT.md`，边界决策见 ADR 0007。
- 自动化证据：本机 `dotnet restore InkFlow.sln` PASS；Release Build 0 warnings / 0 errors；Unit 289/289、Architecture 1/1、Contract 5/5 PASS。全量 Integration 54 项中 6 通过、46 项因本机 `npipe://./pipe/docker_engine` 不可用而 BLOCKED、2 项跳过；新增 PrivateBook PostgreSQL 集成 3 项已编译并实际尝试，未获得容器执行证据。
- Runtime：API `/health` 200；私有书库路由已注册并命中匿名认证门控，但本机 Redis/PostgreSQL 不可用导致受限请求等待/审计失败，未宣称完整端到端 Runtime 通过。
- 远端验收：提交 `204c651` 的 CI `33150804876`、Docker `33150804885`、Security `33150804900` 均 **GREEN**；CI 实际通过 Restore/Build/Test、Compose、Runtime smoke、Redis 限流、PostgreSQL 备份恢复和 Runtime diagnostics，Docker 完成四镜像构建/扫描，Security 完成 NuGet、Trivy、CodeQL 和 SBOM。
- 验收边界：本轮按用户决定不执行 MuMu/阅读 3.0、真实来源/切源、真实追更和人工操作；Private Library API 的登录、跨用户隔离、创建/更新/删除及不进入公共路径仍列入第 6 节人工验收。

**Private Library v2 私有正文与 TXT/EPUB 导入导出（本轮，2026-08-28）**：

- 缺口：PrivateBook 元数据基础已经落地，但缺少独立私有章节、正文持久化、文件导入和可回收的导出闭环。
- 实现：新增独立 `PrivateChapter` / `PrivateContentDocument` 与 `private_chapters` Migration；正文保存为规范化段落和 SHA-256 校验，不复用公共 `ChapterId` / `ContentVersion`。TXT 支持 UTF-8/GB18030、章节标题和导出元数据；EPUB 读取 container/OPF/spine/XHTML，并拒绝路径穿越、DTD/外部实体和超出归档预算的输入。
- 导入语义：每次导入创建新的 PrivateBook 快照，章节与正文在一个持久化事务中落库；解析或校验失败不产生半本书，重复导入不覆盖既有书籍。
- API：新增受保护的 `POST /api/v1/me/private-library/import`、章节列表/正文读取和 `GET /api/v1/me/private-library/books/{id}/export?format=txt|epub`；所有读取显式按 UserId 限定，私有正文和导出响应使用 `Cache-Control: private, no-store`。
- 边界：私有正文不进入 Canonical Content、公共搜索、Legado、Content Policy、公共 Reading Shelf、共享缓存或 CDN；本轮不做私有正文编辑、版本恢复、发布为公共内容、浏览器 UI 或真实设备验收。决策见 ADR 0008，词汇见根目录 `CONTEXT.md`。
- 自动化证据：本机 `dotnet restore InkFlow.sln` PASS；Release Build 0 warnings / 0 errors；Unit 299/299、Architecture 1/1、Contract 7/7 PASS。全量 Integration 55 项中 6 通过、2 跳过、47 项因本机 `npipe://./pipe/docker_engine` 不可用而 BLOCKED；新增 PrivateBook/PrivateChapter PostgreSQL 集成 4 项均实际尝试但未取得容器证据。`git diff --check` PASS。
- Runtime：本机 API `/health` 200；私有章节路由已注册，匿名请求最终返回 401。由于本机 Redis/PostgreSQL 未运行，限流和审计分别触发等待/降级，未宣称完整认证账户端到端通过。
- 远端验收：提交 `f83476a` 的 CI `33163145132`、Docker `33163145104`、Security `33163144984` 均 **GREEN**；CI 的 Restore/Build/Test、Compose、Runtime smoke、Redis 限流、PostgreSQL 备份恢复和 Diagnostics，Docker 的四镜像构建/扫描，Security 的 CodeQL/NuGet/Trivy/SBOM 均通过。Security 仅保留既有 Actions Node 20 弃用提示。
- 验收边界：按用户决定跳过 MuMu/阅读 3.0、真实来源、真实追更和人工操作；真实账户导入 TXT/EPUB、跨用户正文隔离、导出文件可读性和公共路径不泄漏继续列入第 6 节待定事项。

### 4.34 Developer API / Commercial Foundation v1（本轮，2026-08-29）

- 缺口：1.0 Release Candidate 还缺少 Developer Application、生产 API Key、套餐 Entitlement、用户级月度配额和只读外部目录 API 的可审查基础。
- 决策：新增 ADR 0009。只提供 production opaque API Key 与 `catalog.read`；Free/Pro/Developer 为内置版本化套餐；活跃用户默认 Free；PostgreSQL 保存用户+UTC 月度 Usage Period 与不可变 Usage Ledger，Redis 只做快照加速；不接支付、OAuth、组织、sandbox、Community Marketplace 或管理型 Developer API。
- 实现：新增 Developers/Billing 模块与独立 schema/migrations；完成应用/密钥自助创建、列表、撤销、轮换，Administrator 套餐授予，Developer API `/api/developer/v1` 的 Search/Books/Chapters/Content 只读契约，`429/Retry-After` 配额超限和 `503` 配额故障闭合；应用撤销、用户停用、密钥撤销/过期均使认证失败。
- 安全与边界：密钥原文只在签发/轮换响应出现一次，持久化与审计不保存原文；Developer API 不触发来源抓取，不读取私人书库，不返回 SourceId/凭据；命令写入带资源引用的审计事件。公共/Legado/Developer 限流独立，Developer 专用认证先校验密钥，再按 API Key 短哈希分桶；缺失/无效密钥按 IP 分桶，Redis 操作超时配置化且有界。
- 自动化：新增 Developers/Billing 领域/服务单测、Developer API 契约门禁、认证 Handler 安全测试、模块加载边界和 PostgreSQL Testcontainers 迁移/密钥撤销/跨密钥用户级配额测试；本机新增集成用例已实际尝试，但 Docker Engine `npipe://./pipe/docker_engine` 不可用而 BLOCKED，不能记为通过。
- 最终本地证据：`dotnet restore InkFlow.sln` PASS；完整 Release Build 0 warnings / 0 errors PASS；Unit 311/311、Architecture 1/1、Contract 9/9 PASS；完整 Integration 58 项中 6 通过、2 跳过、50 项因 `npipe://./pipe/docker_engine` 不可用而 BLOCKED。API `/health` 返回 200，匿名 Developer 管理/目录接口返回 401；本机 Redis/PostgreSQL 未运行，未宣称完整 Runtime 或 PostgreSQL/Redis 端到端通过；`git diff --check` PASS。
- 远端验收：候选提交 `a0cc247` 的 CI `33241178943`、Docker `33241178942`、Security `33241178945` 均 **GREEN**。CI 的 Restore/Build/Test、Compose、Runtime smoke、Redis 限流、PostgreSQL 备份恢复和 Diagnostics，Docker 的四镜像构建/扫描，Security 的 CodeQL/NuGet/Trivy/SBOM 均通过；Security 仅保留既有 Actions Node 20 弃用提示。
- 当前状态：此工作包为 `1.0 Release Candidate`，自动化 Release Gate 已通过；仍是 `Implemented`，不是 `Accepted/Completed`。人工/真实环境验收按第 6 节待定事项执行。
- 验收边界：按用户决定不执行 MuMu/阅读 3.0、真实来源、真实追更和人工验收；Developer API 生产凭据创建/轮换/撤销、套餐管理、配额超限、跨账户隔离、真实 PostgreSQL/Redis/Compose 运行验收仍列入第 6 节待定事项。

### 4.35 Developer 生命周期上限并发加固（本轮，2026-08-29）

- 缺口：应用创建和 API Key 签发原先采用“先查询再写入”，多 API 实例并发请求可能突破每用户 10 个应用、每应用 5 个活跃 Key 的既定上限；过期 Key 轮换也可能额外制造活跃 Key。
- 实现：Developer PostgreSQL Repository 在创建应用时按 UserId、创建/签发 Key 时按 ApplicationId 获取事务级 advisory lock，在同一事务内检查活跃数量后写入；Key 轮换复用同一 ApplicationId 锁，并拒绝在活跃 Key 已满时把过期 Key 轮换为新活跃 Key。服务层将持久化边界返回的拒绝映射为 `LimitReached`，不暴露生成中的原文密钥。
- 自动化：新增服务层上限拒绝回归 2 项、PostgreSQL 跨连接并发应用/Key 上限测试和过期 Key 轮换上限测试；本机 Release Build 0 warnings / 0 errors，Unit 313/313、Architecture 1/1、Contract 9/9 PASS。DeveloperBillingPersistenceTests 5 项实际尝试但因 `npipe://./pipe/docker_engine` 不可用而 BLOCKED；完整 Integration 60 项中 6 通过、2 跳过、52 项因 Docker Engine 不可用而 BLOCKED。
- 远端首轮 CI（`33242551277`）已实际启动真实 PostgreSQL：60 项中 57 通过、2 跳过，1 项失败原因为测试夹具生成的 seed 前缀不足 16 位并触发 `ArgumentOutOfRangeException`；过期 Key 轮换回归已在首轮远端通过。
- 修正提交 `638a18e` 的远端验证已通过：CI `33242669065` 中 60 项 58 通过、2 跳过，应用/Key 并发上限和过期 Key 轮换两个新增测试均通过；Docker `33242669053`、Security `33242669075` 均 GREEN。
- 当前状态：代码实现和远端真实 PostgreSQL 并发验证已完成，自动化 Release Gate 保持通过；不改变 `1.0 Release Candidate` 状态，也不替代第 6 节人工/真实环境验收。

### 4.36 Operations 告警历史、去重与恢复状态（本轮，2026-08-29）

- 缺口：Operations Alert Snapshot v1 只能返回当前快照，重复轮询会反复看到同一告警；此前没有可追溯的 opened/resolved 转折、并发协调、保留清理或受保护历史查询。
- 设计：新增独立 `InkFlow.Modules.Operations` 模块与 `operations` schema；告警指纹只由稳定 code/resource 坐标计算，排除动态 message。`alert_incidents` 保存当前状态与出现次数，`alert_history` 只保存 opened/resolved 转折；完整快照才允许恢复缺失 incident，partial/unavailable 快照不误恢复。
- 实现：PostgreSQL Migration 创建两张表、索引和禁止 UPDATE 的追加式触发器；仓储在事务内使用 PostgreSQL advisory lock 协调多 API 实例，重复快照只更新 last-seen/occurrence，不新增历史行；按 `HistoryRetentionDays`（默认 30 天，范围 1–3650）清理旧历史和过期 resolved 状态。未过滤的管理员告警快照接入记录，新增 Administrator-only `GET /api/v1/admin/operations/alerts/history`，默认 50、最多 100 条并使用时间戳+事件 ID 不透明游标。
- 安全与边界：历史不写入动态描述、异常原文、Token、IP、连接串或正文；Operator 继续只能获取来源过滤快照，不能查询平台级历史；历史读取/存储失败分别返回稳定 `operations_alert_history_unavailable` 或保留当前快照可用。外部通知渠道、生产路由与治理仍不虚构，继续列为后续 Operations 工作。
- 自动化证据：本机 `dotnet restore InkFlow.sln` PASS；Release Build 0 warnings / 0 errors PASS；Unit 317/317、Architecture 1/1、Contract 10/10 PASS；完整 Integration 64 项中 6 通过、2 跳过、56 项因 `npipe://./pipe/docker_engine` 不可用而 BLOCKED，其中新增 Operations PostgreSQL Testcontainers 4 项均已实际尝试；`dotnet ef migrations has-pending-model-changes` PASS，`git diff --check` PASS。API 本地 Runtime smoke：`/health` 200，匿名历史入口 401。
- 远端验收：候选提交 `4ef206f` 的 CI `33244304809` GREEN，64 项集成测试 62 通过、2 跳过；Docker `33244304814` GREEN；Security `33244304804` GREEN，NuGet、SBOM、Trivy 文件系统扫描和 CodeQL 均通过。
- 当前状态：本工作包保持 `1.0 Release Candidate`，自动化 Release Gate 已通过；不等同于 `Accepted/Completed`，人工 Operations Center、真实 PostgreSQL/Redis、真实来源和阅读 3.0 验收仍按第 6 节待定清单执行。

### 4.37 Operations Center 告警历史 UI 增量（本轮，2026-08-29）

- 目标：将 4.36 已完成的告警历史 API 接入运维中心页面，形成当前快照、历史转折和恢复状态的连续排查路径。
- 实现：管理员可在 `/admin/operations` 刷新最新告警历史并使用不透明游标加载更早记录；页面展示稳定告警代码、资源坐标、发生时间、出现次数及“已触发/已恢复”状态。Operator 不发起平台级历史请求，只看到权限提示，后端 Administrator-only 边界保持不变。
- UX/安全：历史表格沿用 Operations Center 既有 token、响应式横向承载、可见键盘焦点和 `aria-live` 状态；数据全部通过 `textContent`/DOM 节点写入，前端不缓存认证响应，不展示动态 message、异常原文、Token、任务变量或正文。
- 自动化证据：本机 Release Build 0 warnings / 0 errors PASS；Unit 317/317、Architecture 1/1、Contract 10/10 PASS；Operations 页面包含历史 API、分页控件和恢复文案，匿名历史 API 仍返回 401；脚本通过 Node syntax check；完整 Integration 64 项仍为 6 通过、2 跳过、56 项因本机 Docker Engine 不可用而 BLOCKED。
- 远端验收：候选提交 `734c626` 的 CI `33245390370` GREEN（64 项集成测试 62 通过、2 跳过，含 Restore/Build/Test/Compose/Runtime smoke/Redis 限流/备份恢复/Diagnostics），Docker `33245390354` GREEN，Security `33245390350` GREEN（NuGet、SBOM、Trivy 和 CodeQL）。
- 当前状态：本工作包保持 `1.0 Release Candidate`，自动化 Release Gate 已通过；真实凭据下的管理员/Operator 操作、移动/桌面/宽屏视觉与截图仍按第 6 节待定事项执行，不等同于 `Accepted/Completed`。

### 4.38 Core SLO 可观测性指标基线 v1（本轮，2026-08-29）

- 目标：补齐 1.0 “Core SLO 达标”所需的稳定服务面、可用性结果、请求延迟和 5xx 观测契约；既有 OpenTelemetry 自动 instrumentation 继续保留，但不把动态 URL、用户或外部来源细节直接变成指标维度。
- 设计与实现：新增 `CoreSloPolicy` 和 `CoreSloMetricsMiddleware`，覆盖 `public_api`、`legado_api`、`developer_api`、`reader` 四个服务面；100–499 记为 good、5xx 记为 bad，记录 `inkflow.slo.requests`、`inkflow.slo.request.duration`（毫秒）和 `inkflow.slo.server.errors`。延迟目标为 public/developer 750ms、Legado/reader 1000ms，可用性目标为 99.5%。限流等预期 4xx 会被观测但不算服务端错误；`/health`、管理静态页、未知路径和来源内部请求排除。
- 安全与出口：指标只携带稳定服务面和有限 outcome 标签，不写入路径参数、用户、IP、Token、异常原文或正文；OTLP traces/metrics exporter 只有在通用或对应 signal 的 endpoint 显式配置时才启用，没有 Collector 时不会默认连接本机端口；应用不新增公开 `/metrics` 端点。
- 自动化证据：Release Build 0 warnings / 0 errors PASS；Unit 320/320、Architecture 1/1、Contract 10/10 PASS；完整 Integration 64 项中 6 通过、2 跳过、56 项因本机 `npipe://./pipe/docker_engine` 不可用而 BLOCKED。API `/health` 200，`/metrics` 按设计 404；本机 PostgreSQL/Redis 未运行，`/reader` 数据链路未宣称端到端通过。
- 远端验收：候选提交 `a87c5ae` 的 CI `33246490603` GREEN（Unit 320/320、Architecture 1/1、Contract 10/10、Integration 64 项 62 通过/2 跳过，另有 Redis 限流集成 1/1；含 Restore/Build/Compose/Runtime smoke/备份恢复/Diagnostics），Docker `33246490571` GREEN，Security `33246490589` GREEN（NuGet、SBOM、Trivy 和 CodeQL）。
- 当前状态：本工作包保持 `1.0 Release Candidate`，自动化 Release Gate 已通过；真实 OTLP Collector、SLO 窗口/合成探针、错误预算告警与生产保留治理，以及按用户决定延后的 MuMu/阅读 3.0、真实来源和人工验收，继续列入第 6 节待定事项，不等同于 `Accepted/Completed`。

### 4.39 Core SLO 窗口证据评估契约（本轮，2026-08-29）

- 缺口：指标基线能记录请求，但没有统一的窗口报告语义；零流量、缺失 p95、直方图样本不匹配或未知服务面不能被安全地区分为“失败”与“尚无证据”。
- 实现：Observability Building Block 新增 `CoreSloEvidenceEvaluator`、窗口/服务面聚合输入、四态评估结果和稳定 reason code。四个 Core SLO 服务面必须都有正请求量、请求数与延迟样本数匹配、合法 p95 才可能通过；评估结果计算可用性、99.5% 错误预算事件数、剩余预算和 p95 目标，超预算保留负数证据。
- 安全/边界：评估器无状态，不新增数据库、公开 API 或 Collector 连接；reason code 不携带路径、用户、Token、异常原文或其他高基数信息。OTLP/合成探针只需将聚合结果映射到统一输入，真实月度窗口、探针覆盖、告警和保留治理仍待部署环境验收。决策见 ADR 0011。
- 自动化证据：本机 `dotnet restore InkFlow.sln` PASS；Release Build 0 warnings / 0 errors PASS；Unit 324/324、Architecture 1/1、Contract 10/10 PASS；完整 Integration 64 项中 6 通过、56 项因 `npipe://./pipe/docker_engine` 不可用而 BLOCKED、2 项跳过；API `/health` 200、`/metrics` 404（按设计），PostgreSQL/Redis 依赖请求未宣称通过；`git diff --check` PASS。
- 远端验收：提交 `71aa1a8` 的 CI `33247413751`、Docker `33247413755`、Security `33247413756` 均 **GREEN**；CI 的 Restore/Build/Test、Compose、Runtime smoke、Redis 限流和备份恢复演练，Docker 四镜像构建/扫描，以及 Security 的 NuGet、Trivy、CodeQL、SBOM 均完成。Security 保留既有 `upload-artifact@v4` Node 20 弃用提示，未影响结论。
- 当前状态：本工作包继续保持 `1.0 Release Candidate`，代码/自动化契约已完成但不等同于生产 SLO 达标；真实 OTLP Collector、合成探针、窗口证据和错误预算告警仍按第 6 节待定事项执行。

### 4.40 Compose OTLP Collector 监控基线（本轮，2026-08-29）

- 缺口：应用已经能够按配置导出 OTLP，Core SLO 窗口评估契约也已建立，但两份 Compose 没有接收端，Runtime smoke 无法验证 telemetry 出口具备明确的运行时边界。
- 实现：新增固定版本 `otel/opentelemetry-collector:0.159.0`，配置文件为 `deploy/observability/otel-collector-config.yaml`；OTLP gRPC/HTTP 仅在 Compose 内部监听，健康端口 `13133` 只绑定宿主机 loopback。API、Worker、Scheduler 默认指向 `http://otel-collector:4317`，同时保留 `OTEL_EXPORTER_OTLP_ENDPOINT` 覆盖入口；Collector 使用只读挂载、read-only、tmpfs、`no-new-privileges` 和 `cap_drop: ALL`。
- 验证边界：CI Runtime smoke 新增 Collector 健康请求，Docker 门禁先扫描固定版本 Collector 再扫描四个业务镜像；当前 `debug` exporter 只用于本地/CI 接收与诊断，不提供生产持久化、查询、告警或长期保留。Collector 健康通过不等同于四个 Core SLO 服务面有生产窗口证据或月度达标，决策见 ADR 0012。
- 本地证据：`Get-Command docker` 显示 Docker CLI 不存在，因此 Compose config、Collector Runtime smoke 和依赖 Docker 的 Integration 仍为 **BLOCKED**；未以本地应用测试替代该证据。
- 远端证据：候选提交 `3a891ef` 的 CI `33248301675`、Docker `33248301684`、Security `33248301664` 均 **GREEN**。CI 实际通过 Compose config、Collector loopback 健康 Runtime smoke、Restore/Build/Test、Redis 限流、PostgreSQL 备份恢复和 Runtime diagnostics；Docker 先通过 Collector 镜像 Trivy 扫描，再完成四个业务镜像构建/扫描/发布；Security 的 NuGet、Trivy、CodeQL、SBOM 均通过，仅保留既有 Actions Node 20 弃用提示。
- 当前状态：保持 `1.0 Release Candidate`；Compose 接收/健康基线进入自动化门禁，但生产 OTLP 后端、SLO 窗口、合成探针、错误预算告警与保留治理仍属于 Release Gate。

### 4.41 Core SLO Runtime 合成探针基线（本轮，2026-08-29）

- 缺口：Collector 已能在 Compose 内部接收遥测，但 Runtime smoke 还没有对四个 Core SLO 服务面形成统一、可复核的请求与 p95 证据。
- 实现：新增 `scripts/core-slo-runtime-smoke.sh`，固定探测公共目录（200）、空查询 Legado（200）、未授权 Developer API（预期 401）和 Reader 页面（200）。每面默认 5 次请求，单请求默认 10 秒超时且配置上限为 60 秒，失败/超时/非预期状态立即失败；脚本不重试、不保存响应正文。
- 证据：脚本生成 UTC 窗口 JSON，包含四面的 `requestCount`、`serverErrorCount`、`durationSampleCount` 和最近秩 `p95LatencyMilliseconds`，并在 CI 上传 30 天构建产物。空 Legado 查询不触发真实来源，Developer 探针不使用真实凭据。远端 CI `33249393448` 已实际通过脚本回归、Compose Runtime smoke、四面探针和 artifact 上传；artifact 解析确认 schemaVersion=1、四面各 5 requests/5 samples/0 server errors。
- 边界：这是 Compose/CI 短窗口合成基线，不是生产月度 SLO 达标证明；真实 OTLP 后端到达、长窗口聚合、错误预算告警、保留治理以及用户决定延后的 MuMu/阅读 3.0、真实来源和人工验收仍列入第 6 节。决策见 ADR 0013。
- 本地证据：脚本 Bash 语法、fixture 回归和 `git diff --check` PASS；Release Build 0 warnings / 0 errors，Unit 324/324、Architecture 1/1、Contract 10/10 PASS。全量 Integration 64 项为 6 通过、2 跳过、56 项因本机 Docker Engine 不可用而 BLOCKED。
- 远端证据：提交 `d5a8ef3` 的 CI `33249393448`、Docker `33249393438`、Security `33249393437` 均 **GREEN**；CI 的 Runtime smoke、四面探针和 evidence artifact 上传均通过。
- 当前状态：自动化合成探针基线已进入 Release Gate，仍保持 `1.0 Release Candidate`，不标记 `Accepted/Completed`；生产 OTLP 后端、长窗口 SLO、错误预算治理及人工/真实来源验收继续待定。

### 4.42 Core SLO Collector metrics 到达验证（本轮，2026-08-29）

- 缺口：上一轮合成探针已经产生四面 HTTP/延迟 JSON，但 Collector 日志只看到 traces；.NET metrics 的默认周期可能晚于短 Runtime smoke，无法仅凭 Collector 健康或 traces 证明 Core SLO metrics 已到达。
- 实现：Compose 将 `OTEL_METRIC_EXPORT_INTERVAL` 透传，默认保持 60000 毫秒；CI Runtime smoke 显式使用 1000 毫秒。Collector 为 metrics 使用独立 1 秒 batch 和 signal-specific `debug/metrics`，默认 basic；CI 临时切换 detailed，并由 receipt smoke 校验 `inkflow.slo.requests`、`inkflow.slo.request.duration` 及 `public_api`、`legado_api`、`developer_api`、`reader` 四个服务面标签。
- 安全/边界：详细输出只在 CI 运行时打开，仍只读取已有低基数 metrics；不新增公开 `/metrics`、不保存响应正文、不携带真实凭据或触发真实来源。debug exporter 仍不是生产持久化、查询、告警或保留后端。
- 本地证据：配置/工作流 diff 检查、Restore、Release Build（0 warnings / 0 errors）、Unit 324/324、Architecture 1/1、Contract 10/10 和脚本回归 PASS；本机 Docker CLI 不存在，Compose config、Collector receipt 和 Docker 相关集成本地仍为 **BLOCKED**。
- 远端证据：候选提交 `0a1200e` 的 CI `33250749036`、Docker `33250749038`、Security `33250749023` 均 **GREEN**。CI receipt 实际匹配 `inkflow.slo.requests`、`inkflow.slo.request.duration` 以及四个 surface；本轮 artifact 解析确认 schemaVersion=1、四面各 5 requests/5 samples/0 server errors，p95 分别为 public 12.219ms、Legado 29.613ms、Developer 13.070ms、Reader 4.092ms。
- 当前状态：Compose/CI metrics 到达验证已进入自动化 Release Gate，但整体仍保持 `1.0 Release Candidate`；本轮不代表生产 SLO 窗口、告警/保留治理或人工/真实来源验收完成。决策延续 ADR 0013。

### 4.43 Transactional Outbox / Inbox 基础恢复（本轮，2026-08-29）

- 缺口：仓库文档原先声称 Phase 0 已完成 Outbox/Inbox，但 `InkFlow.BuildingBlocks.Messaging` 实际只有空项目文件，缺少消息契约、PostgreSQL 表、租约投递和消费幂等实现；这会使跨模块一致性只能停留在文档层。
- 实现：新增有界 JSON `IntegrationMessage`（稳定消息 ID、类型、TraceId、SHA-256 PayloadHash），`messaging.outbox_messages` / `messaging.inbox_messages` 及官方 EF Migration；Outbox 使用 PostgreSQL `FOR UPDATE SKIP LOCKED`、lease、attempt、失败退避和发布确认，Inbox 以消息 ID 主键、类型/载荷摘要核对和 lease 实现重复消费保护。
- 事务边界：`ITransactionalOutboxWriter` 强制调用方先开启业务 DbContext 事务；Crawler `AddAsync` 现在将 `crawler.tasks` 与最小化 `crawler.task.created` 消息在同一 PostgreSQL 事务中提交或回滚。消息只含 task/source/capability/status 等稳定字段，不携带变量、章节 ID 或凭据引用。
- 自动化证据：本机 `dotnet build InkFlow.sln -c Release --no-restore` PASS（0 warnings / 0 errors）；Unit 327/327、Architecture 1/1、Contract 10/10 PASS；新增 7 个 PostgreSQL 18 Testcontainers 用例已编译，但本机执行因 `npipe://./pipe/docker_engine` 不可用而 BLOCKED，不记为通过；`git diff --check` PASS（仅保留 Git 的换行提示）。
- 远端证据：提交 `dd80e2d` 的 CI `33252929657`、Docker `33252929642`、Security `33252929646` 均 **GREEN**。CI 真实 PostgreSQL 集成共 71 项，69 通过、2 跳过；本工作包新增 7 项 Messaging 用例全部通过。CI 同时通过 Compose、Runtime smoke、Core SLO synthetic/telemetry receipt、Redis 分布式限流、PostgreSQL 备份恢复和 diagnostics；Docker 完成 Collector 与四业务镜像扫描/构建，Security 的 NuGet、SBOM、Trivy、CodeQL 全部通过（保留既有 Actions Node 20 弃用提示）。
- 当前状态：Outbox/Inbox 基础和一个 Crawler 生产写入点已取得远端 PostgreSQL/CI/Docker/Security 证据，整体继续保持 `1.0 Release Candidate`；真实来源、阅读 3.0、人工 UX、生产 OTLP/SLO 长窗口和通知治理仍按第 6 节待定事项执行。

### 4.44 Transactional Outbox / Inbox 执行层（本轮，2026-08-29）

- 缺口：上一工作包已恢复消息事实表和事务写入，但尚缺可复用的 Dispatcher / Consumer 执行闭环；如果没有成功确认、失败退避和 Inbox 消费状态转换，Outbox/Inbox 仍只能作为存储基础。
- 实现：在 `InkFlow.BuildingBlocks.Messaging` 新增 `OutboxDispatcher`、`IntegrationMessageConsumer`、Handler Registry、Publisher/Handler 接口、稳定失败码和有界指数退避策略。Dispatcher 仅在发布成功后确认 Outbox；发布失败记录 `publish_failed` 并释放租约以便重试，确认异常不伪造成功。Consumer 仅在 Handler 成功后确认 Inbox；未知类型和 Handler 异常分别记录稳定失败码，异常文本不落库。
- 边界：发布传输适配器和宿主轮询/后台生命周期仍由后续宿主选型接入，本轮不虚构或绑定未选定的 MQ；执行层保持 at-least-once 语义，依靠 lease、attempt 和幂等 Handler 应对重复投递。
- 自动化证据：本机 `dotnet build InkFlow.sln -c Release --no-restore` PASS（0 warnings / 0 errors）；Unit 334/334、Architecture 1/1、Contract 10/10 PASS；完整 Integration 74 项中 6 项通过、2 项跳过、66 项因本机 `npipe://./pipe/docker_engine` 不可用而 BLOCKED，不记为本机集成通过；其中 MessagingPersistence 10 项已实际尝试；`git diff --check` PASS（仅保留 Git 换行提示）。
- 远端证据：候选提交 `fa81db7` 的 CI `33253938424`、Docker `33253938404`、Security `33253938443` 均 **GREEN**。CI 真实 PostgreSQL 集成共 74 项，72 通过、2 跳过；Messaging Persistence/Execution 10 项全部通过，新增 Dispatcher/Consumer 单测随 Unit 334/334 通过；Compose、Runtime smoke、Core SLO receipt、Redis、备份恢复和 diagnostics 也全部通过。Docker 四镜像与 Collector 扫描通过，Security 的 NuGet、SBOM、Trivy、CodeQL 全部通过；保留既有 Actions Node 20 弃用提示。
- 当前状态：Outbox/Inbox 已具备消息事实、事务写入和可测试执行层的远端证据，但传输适配、宿主后台接线、真实来源、阅读 3.0、人工 UX、生产 OTLP/SLO 长窗口和通知治理仍按第 6 节待定，整体继续保持 `1.0 Release Candidate`。

### 4.45 Messaging Outbox/Inbox 保留清理与 Worker 周期接线（本轮，2026-08-29）

- 缺口：Outbox/Inbox 已有成功确认和失败重试语义，但已处理历史记录没有有界保留清理；长期积压会增加事实表和索引维护成本。
- 实现：新增 `MessageRetentionOptions`、`MessageRetentionService` 和 `IMessageRetentionStore`。每轮按 `BatchSize` 与 `MaxBatchesPerRun` 双重上限计算 Outbox/Inbox cutoff，只删除 `ProcessedAt` 已设置且早于 cutoff 的记录；失败、待重试、未处理和仍被锁定的消息不在清理条件内。PostgreSQL 实现使用事务内 `FOR UPDATE SKIP LOCKED` 分批删除，Worker 以 `Messaging:Retention` 配置并在启动延迟后每小时执行。
- 边界：本轮只接入消息事实表保留清理，不选择 MQ、Publisher 或业务 Handler，也不把清理结果作为消息事实的唯一来源；具体传输和执行宿主仍需后续按模块推进。
- 自动化证据：本机 `dotnet restore InkFlow.sln` PASS；Release Build 0 warnings / 0 errors PASS；Unit 338/338、Architecture 1/1、Contract 10/10 PASS；完整 Integration 76 项中 6 项通过、2 项跳过、68 项因本机 `npipe://./pipe/docker_engine` 不可用而 BLOCKED，不记为本机集成通过；新增 MessagingRetention 单测 4/4 通过，新增 MessagingPersistence 保留清理集成用例 2 项已实际尝试但被 Docker 阻断；`git diff --check` PASS（仅保留 Git 换行提示）。
- 远端证据：候选提交 `bf6eae1` 的 CI `33255354693`、Docker `33255354699`、Security `33255354684` 均 **GREEN**。CI 真实 PostgreSQL 集成共 76 项，74 通过、2 跳过；Messaging Persistence/Execution/Retention 12 项全部通过，Unit 338/338、Compose、Runtime smoke、Core SLO receipt、Redis、备份恢复和 diagnostics 也全部通过。Docker 的 Collector 与四业务镜像构建/扫描/发布通过；Security 的 NuGet、Trivy、CodeQL、SBOM 全部通过，保留既有 `actions/upload-artifact@v4` Node 20 弃用提示。
- 当前状态：保留清理代码与 Worker 周期接线已完成，整体继续保持 `1.0 Release Candidate`；本机 Docker、真实来源/切源、阅读 3.0、人工 UX、生产 OTLP/SLO 长窗口和外部通知治理仍按第 6 节待定。

### 4.46 Migration 漂移安全与完整性门禁（本轮，2026-08-29）

- 缺口：Migrations 入口会对 11 个上下文自动执行 `MigrateAsync`，此前没有在执行前拒绝模型快照漂移，CI 也没有逐一覆盖全部上下文。
- 实现：`InkFlow.Migrations` 在每个上下文的 `MigrateAsync` 前调用 `HasPendingModelChanges()`；检测到漂移时输出稳定错误并以退出码 1 停止。新增 `.config/dotnet-tools.json` 锁定 `dotnet-ef` 10.0.4，`scripts/verify-migrations.sh` 通过 API 启动项目、Release 产物和 `--no-build` 逐一检查 Identity、Audit、Messaging、Developers、Billing、Operations、Crawling、Library、Reading、Sources、Content 11 个上下文。
- 本地证据：`dotnet tool restore` PASS；Migrations Release Build 0 warnings / 0 errors PASS；PowerShell 等价执行的 11 个 `has-pending-model-changes` 检查全部 PASS；`bash -n scripts/verify-migrations.sh` 与 `git diff --check` PASS。完整 Solution Test 为 76 项 Integration 中 6 项通过、2 项跳过、68 项因本机 Docker/数据库环境不可用而 BLOCKED；Unit 338/338、Architecture 1/1、Contract 10/10 PASS。
- 边界：本轮建立模型漂移 fail-closed 与上下文覆盖门禁，不替代生产 Expand → Migrate → Contract 评审、真实数据库迁移演练或后续人工/真实来源验收。
- 远端证据：候选提交 `5878652` 的 CI `33256728058`、Docker `33256728051`、Security `33256728081` 均 **GREEN**；CI 新增 `Verify migrations` 实际逐一检查 11 个上下文并通过，Docker 的 Migrations/API/Worker/Scheduler 镜像与 Collector 检查通过，Security 的 NuGet、Filesystem、CodeQL、SBOM 全部通过。保留既有 `actions/upload-artifact@v4` Node 20 弃用提示。
- 当前状态：Migration 自动安全门禁已实现并取得三类远端门禁证据，整体仍保持 `1.0 Release Candidate`；真实 PostgreSQL Migrations/Compose、人工验收和真实来源验收仍按第 6 节待定。

### 4.47 审计事实保留治理与 Worker 周期接线（本轮，2026-08-29）

- 缺口：`audit.events` 已具备追加式持久化和受保护查询，但此前没有可配置的过期清理；无限增长会增加审计表和索引维护成本，同时普通删除必须继续被数据库拒绝。
- 实现：新增 `AuditRetentionOptions`、`AuditRetentionService`、`IAuditRetentionStore` 和 PostgreSQL `EfAuditRetentionStore`。默认保留 365 天，按 `BatchSize` / `MaxBatchesPerRun` 双重上限，以 `(OccurredAt, Id)` 索引、事务和 `FOR UPDATE SKIP LOCKED` 分批删除 `OccurredAt < cutoff` 的事件；Worker 启动延迟后每小时执行。
- 安全/边界：新增 Migration 将追加式触发器调整为只对 retention transaction-local 标记放行删除，更新和普通直接删除仍失败；没有新增 API 或用户触发入口。生产法律保留、归档、恢复授权、删除审批和实际策略仍需部署治理，决策见 ADR 0014。
- 自动化证据：本机 `dotnet restore InkFlow.sln` PASS；Release Build 0 warnings / 0 errors；Unit 342/342、Architecture 1/1、Contract 10/10 PASS；`bash -n scripts/verify-migrations.sh` 与 PowerShell 等价的 11 个迁移模型检查 PASS；完整 Integration 78 项中 6 项通过、2 项跳过、70 项因本机 `npipe://./pipe/docker_engine` 不可用而 BLOCKED，新增 2 项审计保留集成测试已实际尝试但未取得本机容器证据；`git diff --check` PASS。
- 远端证据：候选提交 `b8046af` 的 CI `33257996992`、Docker `33257996951`、Security `33257996953` 均 **GREEN**。CI Test 为 78 项、76 通过/2 跳过，11 个迁移模型检查、Compose、Runtime smoke、Core SLO receipt、Redis、备份恢复和 diagnostics 全部通过；Docker 的 Collector 与 API/Migrations/Scheduler/Worker 四镜像构建、扫描和发布通过；Security 的 NuGet、Filesystem、CodeQL、SBOM 全部通过，保留既有 Actions Node 20 弃用提示。
- 当前状态：审计保留代码基线和 Worker 周期接线已完成并取得远端证据，整体继续保持 `1.0 Release Candidate`；生产法律/合同保留策略、归档与删除授权治理、本机 Docker 集成、真实来源、阅读 3.0 和人工验收仍按第 6 节待定。

### 4.48 Source Rule DSL v1 严格 JSON 契约与 Fixture 基线（本轮，2026-08-29）

- 缺口：Source DSL 已有 typed AST 与领域校验，但持久化仍使用默认 JSON 序列化；抽象 `RuleTransform` 没有稳定 wire shape，未知字段/转换类型、缺失必需字段和过大文档也没有统一的版本化 fail-closed 边界。
- 实现：新增 `SourceRuleDslJson` 版本化编解码器与 `docs/contracts/source-rule-dsl-v1.schema.json`。JSON 边界拒绝未知属性，要求构造参数对应的核心字段，限制文档大小、规则/字段/转换/映射及各类表达式长度；`trim` / `replace` 使用显式 `kind` AST，输出统一 camel-case 字符枚举，兼容读取既有数字枚举但不以数字写出。领域 Validator 同步空值、枚举、集合、长度、POST 表单和列表绑定约束。
- 持久化与回归：Sources EF 仓储统一经过该 codec；非法已存规则读取时 fail-closed，不静默执行。新增无第三方网络依赖的 `source-rule-dsl-v1.json` Fixture、内置 linovelib 定义往返测试、未知属性/未知转换/必需字段/超大文档测试，以及 PostgreSQL `RuleTransform` 往返集成测试；未新增 API 或 Migration。
- 执行边界：本工作包只建立最小可测试 schema/AST 与持久化契约，不宣称完整 DSL 引擎。Schema 保留 CSS/XPath/JSONPath 的 AST 枚举；当前 RuleAdapter 执行基线仍为 CSS，单请求的请求/响应字节、执行时间、正则时间和结果大小预算已在后续 4.49 接入；XPath/JSONPath、Cookie/Session、Pagination、通用变量扩展及多请求/递归的完整预算仍需单独回归，不能仅凭解析通过标记为 Published 或真实来源可用。
- 本地证据：`dotnet build InkFlow.sln -c Release --no-restore` PASS（0 warnings / 0 errors）；Unit 353/353、Architecture 1/1、Contract 10/10 PASS；Schema JSON 语法检查与 `git diff --check` PASS。新增 Sources PostgreSQL 集成目标已编译，但本机 `npipe://./pipe/docker_engine` 不可用，实际容器执行 BLOCKED。
- 远端证据：`2451c72` 首次 CI 暴露既有 Search 仓储 Fixture 缺少列表绑定的问题，已在 `2966088` 修复并重新验证；最终 CI `33259952185`、Docker `33259952247`、Security `33259952205` 均 GREEN。CI Test 为 Unit 353/353、Architecture 1/1、Contract 10/10、Integration 79 项中 77 通过/2 跳过，新增 `Source_With_Transform_Rule_Dsl_Roundtrips` 通过；11 个 Migration 检查、Compose、Runtime/SLO telemetry、Redis、PostgreSQL 备份恢复和 diagnostics 全部通过。Security 保留既有 Actions Node 20 弃用提示，未影响门禁。
- 当前状态：Source DSL v1 最小 schema/AST、Fixture 和仓储边界已取得三类远端门禁证据，整体继续保持 `1.0 Release Candidate`；XPath/JSONPath 等执行能力、真实来源/故障切换、阅读 3.0 与人工验收和生产治理仍按第 6 节待定，不等同于 `Accepted/Completed`。

### 4.49 Source Rule 单请求执行预算与响应体边界（本轮，2026-08-29）

- 缺口：Source Rule 执行虽已有 DSL 严格解析和 SSRF 连接约束，但请求数、请求/响应体大小、执行时间、正则时间和结构化结果大小此前没有在执行器统一 fail-closed；生产客户端读取响应时也会先完整载入再解码。
- 实现：新增不可变 `SourceRuleExecutionLimits`，默认限制为 MaxRequests=1、MaxBytes=2 MiB、MaxExecutionTime=20 秒、MaxRegexTime=2 秒、MaxResultSize=512 KiB，并在 API/Worker/Scheduler 组合根共享同一快照。`RuleAdapter` 对零请求预算、表单/解码后响应字节、HTTP 总等待时间、严格正则超时和字段聚合结果统一返回稳定错误；`RuleBasedSourceAdapter` 对 Search/TOC 列表结果超限整体返回空结果。`ProductionSafeSourceHttpClient` 先检查 Content-Length，再以有界流读取，未知长度超限也不会继续累积。
- 安全与非目标：超限不暴露部分结果，调用方取消仍按原语义传播；自动重定向仍由 SSRF Handler 固定最多 5 跳，XPath/JSONPath、Cookie/Session、Pagination 及递归 MaxDepth 仍未进入执行器，本轮不以 AST 解析替代真实来源验收。
- 本地证据：`dotnet restore InkFlow.sln` PASS；`dotnet build InkFlow.sln -c Release --no-restore` PASS（0 warnings / 0 errors）；Unit 363/363、Architecture 1/1、Contract 10/10 PASS；完整 Solution Test 的 Integration 为 79 项，其中 6 通过、2 跳过、71 项在类初始化时因本机 `npipe://./pipe/docker_engine` 不可用而 BLOCKED；`git diff --check` PASS。
- 远端证据：候选提交 `143685f` 的 CI `33261900485`、Docker `33261900470`、Security `33261900542` 均 **GREEN**。CI Test 为 79 项、77 通过/2 跳过，Restore/Build、11 个迁移模型检查、Compose、Runtime smoke、Core SLO probe/telemetry、Redis 分布式限流、PostgreSQL 备份恢复和 diagnostics 均通过；Docker 四业务镜像构建/扫描通过；Security 的 NuGet、Filesystem/Trivy、CodeQL 和 SBOM 均通过，仅保留既有 Actions Node 20 弃用提示。
- 当前状态：执行预算与响应体内存边界已实现，整体继续保持 `1.0 Release Candidate`；真实来源、真实故障切换、阅读 3.0、人工验收和生产治理仍按第 6 节待定，不等同于 `Accepted/Completed`。

### 4.50 Capability Health 并发变更串行化（本轮，2026-08-30）

- 缺口：`SourceHealthService` 原先先读取能力健康行再执行领域变更并保存；并发 API/Worker 上报连续失败时，多个请求可能基于同一旧计数写回，导致失败深度丢失并延迟自动故障切换。
- 实现：新增 `SourceHealthMutationKind` 与 `ISourceHealthRepository.MutateAsync` 原子变更契约；成功、失败、停用、恢复均由 `SourceHealthService` 通过该入口提交。`EfSourceHealthRepository` 在 PostgreSQL 事务内按稳定 `(SourceId, Capability)` 摘要获取 transaction-scoped advisory lock，锁内重新读取、执行领域方法、保存并提交；未新增模型字段或 Migration。
- 安全与边界：锁键只由来源 ID 与能力枚举的 SHA-256 摘要派生，不把来源输入拼入 SQL；Domain 仍拥有状态转移与失败原因裁剪，Redis/缓存不参与事实写入。本轮不触发真实来源、阅读 3.0 或人工验收。
- 本地证据：`dotnet restore InkFlow.sln` PASS；`dotnet build InkFlow.sln -c Release --no-restore` PASS（0 warnings / 0 errors）；Unit 364/364、Architecture 1/1、Contract 10/10 PASS；11 个迁移模型检查 PASS；完整 Solution Test 的 Integration 为 80 项，其中 6 通过、2 跳过、72 项在类初始化时因本机 `npipe://./pipe/docker_engine` 不可用而 BLOCKED；新增 PostgreSQL 并发测试已编译并实际尝试，`git diff --check` PASS。
- 远端证据：候选提交 `3ba51a1` 的 CI `33263255422`、Docker `33263255437`、Security `33263255420` 均 **GREEN**。CI Test 为 80 项、78 通过/2 跳过，`Concurrent_Health_Mutations_Preserve_All_Failures` 通过；迁移检查、Compose、Runtime smoke、SLO、Redis、PostgreSQL 备份恢复和 diagnostics 均通过；Docker 四业务镜像构建/扫描/发布通过；Security 的 NuGet、Filesystem、CodeQL、SBOM 均通过，仅保留既有 Actions Node 20 弃用提示。
- 当前状态：并发健康状态写入已取得本地单元与远端真实 PostgreSQL 证据，整体继续保持 `1.0 Release Candidate`；真实来源故障切换、阅读 3.0、人工验收和生产治理仍按第 6 节待定，不等同于 `Accepted/Completed`。

### 4.51 Source Rule 受控 XPath/JSONPath 运行时（本轮，2026-08-30）

- 缺口：Source Rule DSL 已声明 CSS/XPath/JSONPath，但此前执行器只接入 CSS；Search/TOC 列表绑定也固定按 CSS 解释，JSONPath 规则无法形成可执行的列表结果。
- 实现：新增统一 `RuleSelectorEvaluator` 并接入 API、Worker、Scheduler。CSS 继续由 AngleSharp 处理；XML 兼容文档使用禁止 DTD/外部实体的 XML 导航，非 XML HTML 使用受限的 `//`/子路径、属性/文本谓词和属性终端；JSONPath 开放 `$` root、property、quoted property、array index、wildcard、recursive-property 子集。列表绑定新增可选 `itemsSelectorKind` 与 `textAttribute`，缺省仍为 CSS/条目文本，保持既有 Rule JSON 兼容。
- 安全与失败关闭：选择器表达式、文档大小、JSON 深度、HTML 路径深度、遍历元素数和结果数均有上限；不支持的过滤/联合/脚本语法、非法 CSS、DTD/实体输入和超限结果返回空值/空集合，不把第三方响应当作可执行代码。
- 回归：新增 JSONPath 标量/列表绑定、XML XPath 节点、HTML 常见谓词、属性终端、非法 CSS、非法 JSONPath、DTD 失败关闭和 DSL 元数据往返测试；修复非法 CSS 异常向上冒泡及列表绑定硬编码 CSS 的问题。
- 本地证据：`dotnet restore InkFlow.sln` PASS；`dotnet build InkFlow.sln -c Release --no-restore` PASS（0 warnings / 0 errors）；Unit 376/376、Architecture 1/1、Contract 10/10 PASS；PowerShell 等价迁移模型检查 11/11 PASS；Schema/Fixture JSON 语法和 API `/health` 200 PASS；完整 Integration 80 项中 6 项通过、2 项跳过、72 项因本机 `npipe://./pipe/docker_engine` 不可用而 BLOCKED。Git Bash wrapper 在 Windows 中因找不到 `dotnet` 未通过，但同一 11 个上下文的等价流程已通过；`git diff --check` PASS。
- 远端证据：候选提交 `2f16b6e` 的 CI `33265352562`、Docker `33265352563`、Security `33265352595` 均 **GREEN**。CI Unit 376/376、Architecture 1/1、Contract 10/10、Integration 80 项 78 通过/2 跳过；Restore/Build、11 个迁移检查、Compose、Runtime smoke、SLO/Telemetry、Redis、PostgreSQL 备份恢复和 diagnostics 均通过。Security 保留既有 Actions Node 20 弃用提示，未影响门禁。
- 当前状态：受控 XPath/JSONPath 执行基线已取得本地与远端门禁证据，整体继续保持 `1.0 Release Candidate`；完整 XPath/JSONPath 语法、Cookie/Session、Pagination、通用变量、多请求/递归预算、真实来源/故障切换、阅读 3.0 和人工验收仍按第 6 节待定，不等同于 `Accepted/Completed`。

### 4.52 Source Rule 受控 next-link Pagination / 多请求预算（本轮，2026-08-30）

- 缺口：RuleAdapter 之前即使规则声明了列表能力也只执行一次 HTTP 请求，无法安全聚合带有“下一页”链接的 Search/TOC 响应；单次预算也没有覆盖页面循环、重复链接和跨页累计响应。
- 实现：新增可选 `RulePagination(nextPageSelector, nextPageAttribute, maxPages)` DSL。仅允许 Search/TOC 的 List 绑定；首个请求保留规则配置的方法和表单，后续 next-link 固定使用 GET。CSS 选择器必须明确链接属性，XPath/JSONPath 使用既有受控求值器；`RuleBasedSourceAdapter` 将所有已验证页面统一投影为 Search/TOC 结果，旧的非分页 Rule JSON 仍保持单请求兼容。
- 安全与失败关闭：后续链接必须与首请求保持完全相同的 scheme/host/port，并再次经过 SSRF 字面量检查；拒绝 userinfo、fragment、控制字符、过长/非法链接、循环、跨源链接和无效选择器。`maxPages` 为有限的 1..32，默认 8；全执行共享 MaxRequests、累计响应字节和一个执行超时。达到页面/请求边界、累计响应超限或传输失败时整次执行失败，不向上游暴露部分页面或部分结果。
- 回归：新增 RuleAdapter 分页聚合、请求耗尽、页数耗尽、跨源、循环、POST→GET、跨页累计字节预算回归；新增 Search/TOC HTML 与 JSON 多页投影、DSL JSON 往返及 Validator/schema 边界回归。当前定向结果为 RuleAdapter 分页 6/6、分页列表 2/2、Validator 类 20/20、JSON 往返 1/1、累计字节 1/1 PASS。
- 非目标：本轮不实现 page-number/cursor 分页、通用变量或 Cookie/Session、超出 next-link 的多请求编排、递归 MaxDepth、完整 XPath/JSONPath 语法；真实 Official Source、故障切换、阅读 3.0 和人工验收继续按待定清单处理。
- 本地证据：`dotnet restore InkFlow.sln` PASS；`dotnet build InkFlow.sln -c Release --no-restore` PASS（0 warnings / 0 errors）；Unit 389/389、Architecture 1/1、Contract 10/10 PASS；PowerShell 等价迁移模型检查 11/11 PASS；Schema/Fixture JSON 语法和 API `/health` 200 PASS；完整 Solution Test 的 Integration 为 80 项，其中 6 通过、2 跳过、72 项因本机 `npipe://./pipe/docker_engine` 不可用而 BLOCKED；`git diff --check` PASS。
- 远端证据：首个候选提交 `83aa68d` 的 CI `33267442596` 曾因 Linux 将 `/search?page=2` 这类相对链接按 `file:` 解析而失败 6 项；Docker `33267442625` 与 Security `33267442628` 仍为 GREEN。修复提交 `c4cddcd` 已通过 CI `33267729513`、Docker `33267729544`、Security `33267729548`。CI 报告 Unit 389/389、Architecture 1/1、Contract 10/10、Integration 80 项 78 通过/2 跳过；迁移 11/11、Compose、Runtime smoke、Core SLO probe/telemetry、Redis、PostgreSQL 备份恢复和 diagnostics 均通过。Security 的 NuGet、Filesystem、CodeQL、SBOM 均通过，保留既有 Node 20 弃用和 CodeQL API 权限提示，未影响门禁。
- 当前状态：受控 next-link Pagination 与多请求预算已形成候选实现，整体继续保持 `1.0 Release Candidate`；仍不等同于 `Accepted/Completed`，真实来源、故障切换、阅读 3.0 和人工验收继续按待定清单处理。

### 4.53 Source Rule page-number / cursor Pagination（本轮，2026-08-30）

- 缺口：4.52 只覆盖响应中的 next-link；API 型来源常用页码或游标续页，若没有统一参数注入和终止边界，容易把规则扩展成无界多请求。
- 实现：`RulePaginationMode` 新增 `pageNumber` 与 `cursor`。页码模式要求在 RuleRequest 的 query/form 中声明唯一 `parameterName`，按有限 `startPage`/`pageStep` 生成后续请求，并由 `nextPageSelector` 判断是否还有下一页；游标模式由 `cursorSelector` 读取下一游标，写回同一已声明参数，保留首请求 method。旧省略 `mode` 的规则继续按 `nextLink` 解析。
- 安全与失败关闭：GET 续页只能使用 query，query/form 不得同时声明同一续页参数；页码值限制在 0..1,000,000，游标限制 2,048 字符且拒绝控制字符。所有模式共享 MaxRequests、MaxPages、累计响应字节和执行超时；重复游标、参数配置错误、SSRF/来源不一致和任一边界超限均整体失败，不暴露部分页面。
- 回归：新增页码 query 与 POST form 参数递增、游标 URL 编码/停止、重复游标 fail-closed、续页参数声明校验、页码边界以及 page-number/cursor JSON codec 往返测试；本轮定向 RuleAdapter/Validator/JSON 69/69 PASS。
- 本地证据：`dotnet restore InkFlow.sln` PASS；Release Build 0 warnings / 0 errors；Unit 399/399、Architecture 1/1、Contract 10/10 PASS；完整 Solution Test 的 Integration 为 80 项，其中 6 通过、2 跳过、72 项因本机 `npipe://./pipe/docker_engine` 不可用而 BLOCKED；PowerShell 等价迁移模型检查 11/11、Schema/Fixture JSON 语法、API `/health` 200 和 `git diff --check` PASS。Git Bash 迁移 wrapper 在 Windows 中因找不到 `dotnet` 未通过，不能替代已通过的等价检查。
- 远端证据：提交 `0e9164b` 的 CI `33269606086`、Docker `33269606076`、Security `33269606147` 均 GREEN。CI 真实 PostgreSQL 集成共 80 项，78 通过、2 跳过；Unit 399/399、Architecture 1/1、Contract 10/10，11 个迁移检查、Compose、Runtime smoke、Core SLO/Telemetry、Redis、PostgreSQL 备份恢复和 diagnostics 全部通过。Docker 的 Collector 与 API/Migrations/Scheduler/Worker 镜像构建、扫描和发布全部通过；Security 的 NuGet、Filesystem、CodeQL、SBOM 全部通过，保留既有 Actions Node 20 弃用提示。
- 非目标：Cookie/Session、通用变量扩展、完整 XPath/JSONPath 语法、三种受控分页之外的多请求/递归编排，以及真实来源、故障切换、阅读 3.0 和人工验收仍按待定清单处理。
- 当前状态：page-number/cursor 受控分页已形成可执行基线，并取得本地与远端三类门禁证据；整体仍为 `1.0 Release Candidate`，不等同于 `Accepted/Completed`。

### 4.54 Source Rule 受控 response-cookie Session（本轮，2026-08-30）

- 缺口：来源有时需要先通过响应 `Set-Cookie` 建立短期会话，再访问同源的后续分页请求；此前既没有 Rule DSL 声明边界，也没有安全的 Cookie 传递 seam。
- 实现：新增可选 `CapabilityRule.Session` / `RuleSession`，只声明 Cookie 数量、累计字节和生命周期上限，不保存 Cookie 值。`RuleAdapter` 在一次执行内维护内存 Cookie jar，按同源最终响应、Domain/Path/Secure 和 Max-Age/Expires 规则向后续 next-link/page-number/cursor 请求发送；`SourceHttpResponse` 暴露原始 `Set-Cookie` 与最终响应 URI，生产 HTTP 客户端使用显式临时 Cookie 头。
- 安全与失败关闭：生产 `SocketsHttpHandler` 关闭共享 CookieContainer；静态 `Cookie` / `Set-Cookie` 请求头被 Rule Validator/Adapter 拒绝；最多 32 个 Cookie、累计 4 KiB、最长 3600 秒，跨执行不复用。跨源重定向响应、Cookie 数量/字节上限会使整次 Rule 执行失败，Cookie 值不进入 Rule JSON、任务载荷、日志或结果。
- 回归：新增 RuleAdapter Cookie 传播、执行隔离、路径匹配、过期删除、跨源响应和边界测试；新增生产 HTTP Cookie 头/响应头测试、Validator 上限/静态头测试和 JSON Contract 往返测试。Unit 全量 410/410、定向 Session/HTTP 回归通过。
- 非目标：本轮不实现 CredentialReference/ISecretProvider 的初始账号或 Token 注入、跨任务/跨来源持久会话、完整 RFC Cookie/公共后缀策略、自动重定向中间响应 Cookie 或带 Cookie 请求的自动重定向、通用变量或递归多请求编排；真实来源、故障切换、阅读 3.0 和人工验收继续按第 6 节待定清单处理。
- 本地证据：`dotnet restore InkFlow.sln` PASS；Release Build 0 warnings / 0 errors；Unit 410/410、Architecture 1/1、Contract 10/10 PASS；Schema/Fixture JSON 语法、PowerShell 等价迁移模型检查 11/11、API `/health` 200 与 `git diff --check` PASS。Git Bash 迁移 wrapper 仅完成 `bash -n` 语法检查，完整 wrapper 在 Windows 因找不到 `dotnet` 未执行；本机完整 Integration 80 项为 6 通过、2 跳过、72 项因 `npipe://./pipe/docker_engine` 不可用而 BLOCKED。
- 远端证据：候选提交 `6f52719` 已推送；[CI 33271405103](https://github.com/nekohands/InkFlow/actions/runs/33271405103)、[Docker 33271405122](https://github.com/nekohands/InkFlow/actions/runs/33271405122)、[Security 33271405107](https://github.com/nekohands/InkFlow/actions/runs/33271405107) 均为 GREEN，包含 Restore/Build/Test/Compose/Runtime smoke/Diagnostics、四镜像构建和 SBOM/Filesystem/CodeQL/NuGet 检查。
- 当前状态：受控 response-cookie Session 已形成可审查的执行期基线，候选提交门禁已通过，整体继续保持 `1.0 Release Candidate`；Credential 初始认证、真实来源、真实故障切换、阅读 3.0 和人工验收仍未关闭。

### 4.55 Source Rule 有界请求模板变量（本轮，2026-08-30）

- 缺口：此前只有路径模板稳定填充变量，Header 值仍是静态文本；路径之外的模板花括号校验不统一，调用方变量上下文也没有统一的数量、名称、长度和总量边界。
- 实现：`RuleRequest` 的路径、Header、Query、Form 模板值统一支持 `{name}` 占位符；`RuleAdapter` 在 HTTP seam 前渲染 Header 值（不做 URL 编码），并复用模板语法校验。`SourceRuleExecutionLimits` 新增调用方临时变量上下文的数量、名称长度、单值长度和累计 UTF-8 字节预算。
- 安全与失败关闭：变量名必须符合 `[A-Za-z_][A-Za-z0-9_]*`，最多 32 个变量、单名 128 字符、单值 2,048 字符、累计 16 KiB；变量值和渲染 Header 名/值拒绝控制字符，未闭合/非法占位符在发布期与执行期均拒绝。所有变量边界在进入 HTTP seam 前检查，错误不回显变量值；不改变 Cookie/Session 的临时状态边界。
- 回归：新增 Header 模板渲染、控制字符、变量数量/名称/单值/总字节预算及失败不出网测试；新增 Header 模板语法与静态控制字符发布校验测试。当前本地 Unit 418/418、Architecture 1/1、Contract 10/10 通过。
- 非目标：本轮不实现响应派生变量、CredentialReference/ISecretProvider 的初始认证或持久会话、递归/通用多请求编排、完整 XPath/JSONPath 语法；真实来源、故障切换、阅读 3.0 和人工验收继续按第 6 节待定清单处理。
- 本地证据：`dotnet restore InkFlow.sln` PASS；Release Build 0 warnings / 0 errors；Unit 418/418、Architecture 1/1、Contract 10/10 PASS；完整 Integration 80 项为 6 通过、2 跳过、72 项因本机 `npipe://./pipe/docker_engine` 不可用而 BLOCKED；PowerShell 等价迁移模型检查 11/11、Schema/Fixture JSON 语法、API `/health` 200 和 `git diff --check` PASS。Git Bash 迁移 wrapper 在 Windows 中仍因找不到 `dotnet` 未完成执行。
- 远端证据：候选提交 `dd39396` 已推送；[CI 33272774115](https://github.com/nekohands/InkFlow/actions/runs/33272774115)、[Docker 33272774105](https://github.com/nekohands/InkFlow/actions/runs/33272774105)、[Security 33272774138](https://github.com/nekohands/InkFlow/actions/runs/33272774138) 均为 GREEN。CI 真实测试为 Unit 418/418、Architecture 1/1、Contract 10/10、Integration 80 项 78 通过/2 跳过，并完成迁移、Compose、Runtime smoke、Core SLO、Redis、PostgreSQL 备份恢复与 diagnostics；Docker 四业务镜像及 Collector 扫描/发布通过；Security 的 NuGet、Filesystem、CodeQL、SBOM 通过，保留既有 Actions Node 20 弃用提示。
- 当前状态：有界请求模板变量已形成通过候选门禁的 `Implemented` 基线，整体仍为 `1.0 Release Candidate`，不等同于 `Accepted/Completed`；响应派生变量、Credential 初始认证、真实来源/切源、阅读 3.0 和人工验收仍未关闭。

### 4.56 Source CredentialReference 有界初始认证（本轮，2026-08-30）

- 缺口：`CrawlPayload.CredentialReferenceId` 之前只作为预留字段，活动 Worker 的 TOC、联动正文和 RuleAdapter 链路没有统一的安全解析与请求头投影。
- 实现：新增 `ISourceCredentialProvider`、`ConfigurationSourceCredentialProvider` 和非敏感 `SourceExecutionContext`；任务级引用贯通 TOC → 联动 Content → `RuleBasedSourceAdapter` → `RuleAdapter`，仅允许 typed Bearer、Basic 或受限 API-Key Header。配置适配器读取 `SourceCredentials:<sourceId>:<referenceId>`，生产接入仍应替换为 Docker Secret、Vault 或云 Secret Manager 等安全提供器；未实现凭据能力的 CodeAdapter 会显式拒绝带引用的执行上下文。
- 安全与失败关闭：引用 ID 最长 256 字符并拒绝路径注入；secret 不进入 Task Payload、Variables、Rule JSON、日志、错误文本、结果或 `ToString()`。凭据只在 URL/SSRF/请求预算通过后解析，并受 `MaxExecutionTime` 约束；Bearer/Basic/API-Key 的头名、头值、材料长度和禁止头名均有界，缺失提供器、解析异常、超时、非法材料或规则头冲突均在 HTTP seam 前失败关闭。自定义 Provider 仍必须执行 Owner Scope 与跨租户授权。
- 回归：新增 Bearer/Basic/API-Key 头注入、分页复用、配置解析、TOC/Content 任务传播、CodeAdapter 拒绝、提供器超时/异常脱敏、非法引用和头冲突不出网测试；本轮本地 Unit 430/430、Architecture 1/1、Contract 10/10 通过。
- 非目标：不实现来源级默认凭据绑定、Scheduler/Admin 凭据管理、真实 Vault/Docker Secret SDK、跨任务/跨来源持久会话、响应派生变量、递归/通用多请求、完整 XPath/JSONPath 语法；真实来源、故障切换、阅读 3.0 和人工验收继续按第 6 节待定清单处理。
- 本地证据：`dotnet restore InkFlow.sln` PASS；`dotnet build InkFlow.sln -c Release --no-restore` PASS（0 warnings / 0 errors）；Unit 430/430、Architecture 1/1、Contract 10/10 PASS；完整 Integration 80 项中 6 项通过、2 项跳过、72 项因本机 `npipe://./pipe/docker_engine` 不可用而 BLOCKED；PowerShell 等价迁移模型检查 11/11、Schema/Fixture JSON 解析、API `/health` 200 和 `git diff --check` PASS。Git Bash 迁移 wrapper 在 Windows 中仍因找不到 `dotnet` 未完成执行。
- 远端证据：最终提交 `47992d7`（代码实现提交 `c32dc80`，本提交仅补写验证证据）已推送；[CI 33275310547](https://github.com/nekohands/InkFlow/actions/runs/33275310547)、[Docker 33275266875](https://github.com/nekohands/InkFlow/actions/runs/33275266875)、[Security 33275266884](https://github.com/nekohands/InkFlow/actions/runs/33275266884) 均为 GREEN，且三项 Run 的 headSha 均为 `47992d7`。CI 真实测试为 Unit 430/430、Architecture 1/1、Contract 10/10、Integration 80 项 78 通过/2 跳过，并完成 11 个迁移检查、Compose、Runtime smoke、Core SLO、Redis、PostgreSQL 备份恢复与 diagnostics；Docker 四业务镜像及 Collector 扫描/发布通过；Security 的 NuGet、Filesystem、CodeQL、SBOM 通过，保留既有 Node 20 弃用与 CodeQL 权限提示。
- 当前状态：任务级 CredentialReference 初始认证已形成通过候选门禁的 `Implemented` 基线，整体仍为 `1.0 Release Candidate`，不等同于 `Accepted/Completed`；来源默认绑定、持久会话、真实凭据后端、真实来源/切源、阅读 3.0 和人工验收仍未关闭。

### 4.57 Source Rule 有界响应派生变量（本轮，2026-08-30）

- 缺口：4.55 已支持调用方把变量填入请求模板，但 API 型来源的续页 token/cursor 辅助值仍不能从当前响应派生；若扩展成通用多请求，容易突破既有请求数、字节和执行期边界。
- 实现：新增 `CapabilityRule.ResponseVariables` / `RuleResponseVariable`，仅允许 page-number/cursor 分页在确认存在下一页时，从当前响应用受控 Selector 或带超时 Regex 提取，并应用既有 Trim/Replace 后填入同一次执行的临时模板变量上下文；后续 query/header/form/path 请求复用该上下文，首请求仍使用调用方变量。
- 安全与失败关闭：发布期和执行期都要求响应派生变量只出现在 page-number/cursor 规则中，变量名唯一且有界，提取结果复用变量数量、标识符、单值长度、累计 UTF-8 字节和控制字符预算。缺失、非法、正则超时或超限在下一次续页出网前整体失败，不暴露已抓页面、派生值、部分结果或错误中的敏感值；最后一页不要求派生变量，派生上下文不跨执行持久化。
- 回归：新增页码续页 Selector 派生、游标续页 Regex/Header 派生、值缺失、值超限不出网/不泄露、声明模式/重复名称 Validator 及 JSON codec 往返测试；定向 RuleAdapter/Validator/JSON 回归 91/91 通过。
- 非目标：不实现响应派生变量的持久化、跨执行状态、next-link 中的派生模板、多步通用请求序列、递归/MaxDepth 或完整 XPath/JSONPath 语法；真实来源、故障切换、阅读 3.0 和人工验收仍按待定清单处理。
- 本地证据：`dotnet restore InkFlow.sln` PASS；`dotnet build InkFlow.sln -c Release --no-restore` PASS（0 warnings / 0 errors）；Unit 437/437、Architecture 1/1、Contract 10/10 PASS；Integration 80 项中 6 项通过、2 项跳过、72 项因本机 `npipe://./pipe/docker_engine` 不可用而 BLOCKED；迁移模型检查 11/11、Schema/Fixture JSON、API `/health` 200 和 `git diff --check` PASS。
- 远端证据：候选提交 `8977a42` 的 [CI 33276544113](https://github.com/nekohands/InkFlow/actions/runs/33276544113)、[Docker 33276544229](https://github.com/nekohands/InkFlow/actions/runs/33276544229)、[Security 33276544165](https://github.com/nekohands/InkFlow/actions/runs/33276544165) 均 GREEN，且三项 Run 的 headSha 均为 `8977a425560f20bde38a162a598816a9cd56c1e7`。CI 真实测试为 Unit 437/437、Architecture 1/1、Contract 10/10、Integration 80 项 78 通过/2 跳过，并完成 11 个迁移检查、Compose、Runtime smoke、Core SLO、Redis、PostgreSQL 备份恢复与 diagnostics；Docker 四业务镜像/Collector 通过，Security 的 NuGet、Filesystem、CodeQL、SBOM 通过，仅保留既有 Node 20 弃用与 CodeQL 权限提示。
- 当前状态：有界响应派生变量已形成通过候选门禁的 `Implemented` 基线，整体仍为 `1.0 Release Candidate`，不等同于 `Accepted/Completed`；来源级默认绑定、持久会话、真实 SecretProvider、真实来源/切源、阅读 3.0 和人工验收仍未关闭。

### 4.58 Source 级默认 CredentialReference 绑定（本轮，2026-08-30）

- 缺口：4.56 仅支持任务显式携带 CredentialReferenceId；来源型规则/Worker 在没有显式引用时无法复用来源自身的非敏感默认绑定。
- 实现：Source 增加可选 DefaultCredentialReferenceId、设置/清除与显式优先解析 seam；RuleBasedSourceAdapter 和 RuleCrawlerTaskExecutor 在未给出显式引用时回退到来源默认引用，任务载荷仍保持原样。引用校验下沉到 Domain，Application 旧验证入口保持兼容；Sources 持久化新增可空、最长 256 字符列与 Migration。
- 安全与失败关闭：默认值只保存引用 ID，不保存 secret；最终仍由 ISourceCredentialProvider 解析并投影为受限 typed Header，Provider 继续负责 Owner Scope/跨租户授权；CodeAdapter 不继承规则型来源默认值，显式非法引用不被默认值静默覆盖。
- 回归：新增 Source 设置/清除/优先级、RuleBasedSourceAdapter 默认回退/显式覆盖、Crawler Task 默认回退/显式覆盖和 PostgreSQL roundtrip/清除测试。
- 非目标：不实现 Owner/Admin 凭据管理、真实 Vault/SecretManager、跨任务/跨来源持久会话或通用多请求递归；真实来源、阅读 3.0 和人工验收继续按第 6 节待定清单处理。
- 本地证据：Restore PASS；Release Build 0 warnings / 0 errors；Unit 442/442、Architecture 1/1、Contract 10/10 PASS；完整 Integration 81 项中 6 项通过、2 项跳过、73 项因本机 `npipe://./pipe/docker_engine` 不可用而 BLOCKED；迁移模型检查 11/11、API `/health` 200 和 `git diff --check` PASS。
- 远端证据：候选提交 `6d9c2ec` 的 [CI 33277737624](https://github.com/nekohands/InkFlow/actions/runs/33277737624)、[Docker 33277737577](https://github.com/nekohands/InkFlow/actions/runs/33277737577)、[Security 33277737675](https://github.com/nekohands/InkFlow/actions/runs/33277737675) 均 GREEN，headSha 均为 `6d9c2ec01e20f40932ecba4ea9e0835842caf54a`；CI 真实测试为 Unit 442/442、Architecture 1/1、Contract 10/10、Integration 81 项 78 通过/2 跳过，并完成 11 个迁移检查、Compose、Runtime smoke、Core SLO、Redis、PostgreSQL 备份恢复和 diagnostics。
- 当前状态：来源级默认 CredentialReference 已形成通过候选门禁的 Implemented 基线，整体仍为 `1.0 Release Candidate`，不等同于 `Accepted/Completed`；Owner/Admin 凭据管理、真实 SecretProvider、持久会话、真实来源/切源、阅读 3.0 和人工验收仍未关闭。

### 4.59 Source 默认 CredentialReference Administrator 管理入口（本轮，2026-08-30）

- 缺口：4.58 已能在执行侧使用来源默认引用，但还没有受保护的设置/清除入口，运营方只能依赖底层数据操作，缺少统一理由和命令审计。
- 实现：新增 `ISourceCredentialBindingService` 与 `PUT /api/v1/admin/sources/{sourceId}/credential-binding`；`null` 清除默认绑定，非空合法引用设置默认绑定，写入仍复用既有 `Source.DefaultCredentialReferenceId` 和 Sources Repository，无新 Migration。入口使用独立 Administrator-only policy，不扩大 Operator 的来源运维权限。
- 安全与审计：请求只接受非敏感 `CredentialReferenceId` 和有界理由，不接收 secret、Token、Cookie 或密码；响应只返回引用 ID，命令审计记录 set/clear、操作者、理由、状态和脱敏来源引用，实际 secret 仍由 Provider 按 Owner Scope/跨租户策略解析。
- 回归：新增服务层设置/清除/无效引用不读写/来源不存在测试，以及响应/错误码/理由边界/审计脱敏测试，定向回归 8/8 通过。
- 非目标：不实现 secret 材料保存、Vault/Cloud SecretProvider、Credential Owner 管理、持久会话、真实来源/切源、阅读 3.0 或人工验收；这些继续列入第 6 节待定事项。
- 本地证据：`dotnet restore InkFlow.sln` PASS；`dotnet build InkFlow.sln -c Release --no-restore` PASS（0 warnings / 0 errors）；Unit 450/450、Architecture 1/1、Contract 10/10 PASS；Integration 81 项中 6 项通过、2 项跳过、73 项因本机 `npipe://./pipe/docker_engine` 不可用而 BLOCKED；API `/health` 200、匿名访问该管理路由 401、`git diff --check` PASS。运行时 smoke 未执行真实设置写入，避免触碰本地缺失的 PostgreSQL/Redis。
- 远端证据：提交 `dee61d3` 的 [CI 33279039667](https://github.com/nekohands/InkFlow/actions/runs/33279039667)、[Docker 33279039645](https://github.com/nekohands/InkFlow/actions/runs/33279039645)、[Security 33279039666](https://github.com/nekohands/InkFlow/actions/runs/33279039666) 均 GREEN，三项 Run 的 headSha 均为 `dee61d31fdd9983c7cc30f57ea091cd016c5a6db`。
- 当前状态：来源默认 CredentialReference 的 Administrator 管理入口已形成通过候选门禁的 Implemented 基线，整体仍为 `1.0 Release Candidate`，不等同于 `Accepted/Completed`；真实 secret 管理、真实 Provider、持久会话、真实来源/切源、阅读 3.0 和人工验收仍未关闭。

### 4.60 Source Credential Owner Scope 契约（本轮，2026-08-30）

- 缺口：4.56–4.59 的 Provider 解析输入只有 SourceId 与 CredentialReferenceId，未来用户/组织与平台引用重名时，无法在 Provider seam 强制区分所有者范围。
- 实现：新增 `SourceCredentialOwnerKind`（Platform/User/Organization）、`SourceCredentialOwnerScope` 与 `SourceCredentialResolutionContext`；`ISourceCredentialProvider` 现在必须接收包含 Source、引用和 Owner Scope 的非敏感上下文。Worker/Crawler 显式使用 Platform；RuleBasedSourceAdapter 仅在调用方显式提供引用时透传用户/组织范围，来源默认引用始终回到 Platform。
- 安全与失败关闭：User/Organization 必须绑定非空稳定 OwnerId，Platform 不带 OwnerId；Source/Reference/Scope 组合上下文有界校验，非法范围在 Provider/HTTP seam 前拒绝。现有 `ConfigurationSourceCredentialProvider` 只接受 Platform，不能把配置中的 secret 暴露给用户/组织范围；secret 仍不进入任务载荷、规则 JSON、日志、错误、结果或解析上下文。
- 回归：新增用户/组织/平台范围透传、默认绑定不继承用户范围、空身份/非法 Source 上下文、配置 Provider 拒绝非 Platform 范围测试；目标凭据回归 24/24 通过。
- 非目标：不新增用户/组织/租户实体，不实现真实 Vault/Cloud SecretProvider、secret 材料管理、轮换、持久会话或用户/组织凭据管理；真实来源、切源、阅读 3.0 和人工验收继续按待定清单处理。
- 本地证据：`dotnet restore InkFlow.sln` PASS；`dotnet build InkFlow.sln -c Release --no-restore` PASS（0 warnings / 0 errors）；Unit 455/455、Architecture 1/1、Contract 10/10 PASS；三宿主 `/health` 均返回 200；完整 Integration 81 项中 6 项通过、2 项跳过、73 项因本机 `npipe://./pipe/docker_engine` 不可用而 BLOCKED；`git diff --check` PASS。未写入真实凭据。
- 远端证据：候选提交 `ee20afe` 已推送；[CI 33280448686](https://github.com/nekohands/InkFlow/actions/runs/33280448686)、[Docker 33280448680](https://github.com/nekohands/InkFlow/actions/runs/33280448680)、[Security 33280448687](https://github.com/nekohands/InkFlow/actions/runs/33280448687) 均为 GREEN，且三项 Run 的 headSha 均为 `ee20afef2f9247fdb774ca6dda35a0f81b7452fe`。
- 当前状态：本工作包为 `Implemented`，尚未达到 `Accepted/Completed`；整体继续保持 `1.0 Release Candidate`。真实 SecretProvider、secret 材料管理、Owner/Admin 凭据管理、真实来源/切源、阅读 3.0 和人工验收仍未关闭。

### 4.61 PostgreSQL Outbox Relay 与 Worker 宿主接线（本轮，2026-08-30）

- 缺口：4.43–4.45 已有 Outbox/Inbox 事实、事务写入、执行层和保留清理，但没有实际 Publisher 与 Worker 后台循环，消息无法在运行时从 Outbox 进入 Inbox。
- 实现：新增 `IInboxTransportStore` 与 `PostgreSqlInboxMessagePublisher`，按类型、PayloadHash、TraceId 重建并核对 Envelope，使用消息 ID 幂等写入 Inbox，成功写入后由 `OutboxDispatcher` 确认 Outbox；为抵抗 PostgreSQL `jsonb` 读回规范化，Outbox/Inbox 追加受消息大小上限约束的 `RawPayload` 原文列，旧记录缺少原文时使用显式持久化恢复路径。Inbox 新增可选 TraceId 和独立 `AddInboxMessageTraceId` Migration，RawPayload 由独立 Migration 增加。Worker 注册有界 `OutboxRelayOptions` 与 `OutboxRelayBackgroundService`，按 `Messaging:Relay` 配置运行批量 relay，owner 含实例名和随机 ID；日志不记录载荷或异常文本。
- 边界：本轮只选择同一 PostgreSQL 事实库作为内部 relay，不引入未选定的外部 MQ；Inbox 消费轮询和具体 Handler 等待接收模块/消息类型明确后再接入，不扩大为“所有 Integration Event 已消费”。
- 回归：新增 Publisher 身份/TraceId/接收时间和配置上限 Unit；新增 PostgreSQL Outbox→Inbox、重复投递不覆盖接收时间、Outbox 成功确认 Integration 用例。
- 本地证据：修复后 `dotnet restore` PASS；Release Build 0 warnings / 0 errors；Unit 460/460、Architecture 1/1、Contract 10/10、迁移模型 11/11 PASS；API/Worker/Scheduler `/health` 均返回 200；NuGet 漏洞审计与 `git diff --check` PASS。PostgreSQL Relay 定向 Integration 已实际重跑，但因本机 `npipe://./pipe/docker_engine` 不可用而 BLOCKED，未将其记为通过。
- 远端证据：修复候选 `ed4a7a7abc70732df5310546c0af01909b54ac96` 的 [CI 33282208833](https://github.com/nekohands/InkFlow/actions/runs/33282208833)、[Docker 33282208841](https://github.com/nekohands/InkFlow/actions/runs/33282208841)、[Security 33282208838](https://github.com/nekohands/InkFlow/actions/runs/33282208838) 均为 GREEN，三项 headSha 一致；CI 包含迁移模型 11 个上下文、Unit 460/460、Architecture 1/1、Contract 10/10、Integration 82 项（80 passed / 2 skipped）及 Redis Integration 1/1。首次候选 `a3741cf` 曾因 PostgreSQL `jsonb` 读回规范化造成 hash 断言失败，已由 `RawPayload` 原文列修复并回归。
- 当前状态：代码已 `Implemented`，整体继续保持 `1.0 Release Candidate`，尚未达到 `Accepted/Completed`；本机 Docker、Inbox Handler/消费模块、真实来源、阅读 3.0、人工验收、生产 SLO/告警和其他第 6 节事项仍待关闭。

### 4.62 Inbox Consumer 轮询与 Worker 消费宿主（本轮，2026-08-30）

- 缺口：4.61 已能把 Outbox 耐久转入 Inbox，但没有持久化批量领取和 Worker 消费循环；若直接领取所有 Inbox，未注册消息会进入无界失败重试。
- 实现：新增 `IInboxStore.ClaimBatchAsync` 与 `InboxMessageRecord`，PostgreSQL 使用 `FOR UPDATE SKIP LOCKED`、MessageType allowlist、lease 和有界 batch；新增 nullable `OccurredAt` 与领取索引 Migration，旧行缺失 `OccurredAt` 时回退 `ReceivedAt`，无 `RawPayload` 时保留已存 PayloadHash 恢复。`InboxConsumerPump` 在 Handler 成功后确认，失败只写稳定失败码；Worker 新增 `InboxConsumerBackgroundService` 和 `Messaging:Inbox` 有界配置，每轮独立 scope。
- 安全边界：当前 Worker 没有注册业务 Inbox Handler，空注册表安全 idle，不领取未知消息；本轮不新增 `crawler.task.created` 业务 Handler，不引入外部 MQ，不改变 Crawler 任务轮询和阅读路径。决策见 ADR 0017。
- 回归：新增 Inbox pump 成功/空 Handler 单元测试、Inbox 配置边界测试、批量领取 Envelope/类型过滤和旧行兼容恢复 Integration 用例；现有 Consumer/Relay 回归继续通过。
- 本地证据：`dotnet restore InkFlow.sln` PASS；`dotnet build InkFlow.sln -c Release --no-restore` PASS（0 warnings / 0 errors）；Unit 464/464、Architecture 1/1、Contract 10/10、Windows .NET 等价迁移模型检查 11/11 PASS；API/Worker/Scheduler `/health` 均 HTTP 200；漏洞审计与 `git diff --check` PASS。定向 PostgreSQL Inbox Integration 两项均因本机 `npipe://./pipe/docker_engine` 不可用而 BLOCKED；WSL 包装脚本因 WSL 未提供 dotnet 未执行成功，不影响上述 Windows 等价检查。
- 远端证据：候选提交 `fa50c07b6eee042644ea72a331c75e9f61e0ba81` 的 [CI 33283884681](https://github.com/nekohands/InkFlow/actions/runs/33283884681)、[Docker 33283884682](https://github.com/nekohands/InkFlow/actions/runs/33283884682)、[Security 33283884688](https://github.com/nekohands/InkFlow/actions/runs/33283884688) 均 GREEN 且指向同一 headSha；CI 包含迁移模型 11 个上下文、Unit 464/464、Architecture 1/1、Contract 10/10、Integration 84 项（82 passed / 2 skipped）、Redis Integration 1/1、Compose/Runtime/SLO/备份恢复/diagnostics，Security 包含 NuGet/Trivy/SBOM/CodeQL。
- 当前状态：代码与远端门禁为 `CI Green / Implemented`，仍不等于 `Accepted/Completed`；业务 Inbox Handler、完整业务消费闭环、本机 Docker、真实来源、阅读 3.0 和人工验收继续列入待定事项。

### 4.63 Inbox 消费失败有界退避与终态死信（本轮，2026-08-30）

- 缺口：4.62 失败消息释放 lease 后会在下一轮立即重试，缺少统一退避和终态；持续失败可能形成热循环，也无法向 Operations 暴露明确的死信计数。
- 实现：`InboxConsumerOptions` 新增默认 5 次、1–100 有界的 `MaxAttempts`，失败复用有界指数 `IMessageRetryPolicy` 写入 `AvailableAt`；达到上限写入 `DeadLetteredAt`，清除 lease 和可重试时间，保留原始消息、attempt、稳定失败码和身份字段。单条/批量 claim 均排除未到时间、已处理和终态死信；Worker 输出并告警 dead-lettered 计数。
- Migration/兼容：新增官方 `AddInboxFailurePolicy` Migration 和复合领取索引；新列均 nullable，旧 Inbox 行 `AvailableAt = NULL` 视为立即可领取，保留既有 `OccurredAt` / `RawPayload` 恢复兼容。普通 retention 仍只清理 `ProcessedAt` 已设置的消息。
- 边界：当前 Worker 仍没有注册业务 Inbox Handler，空 registry 安全 idle；本轮不新增 `crawler.task.created` 业务消费、不引入外部 MQ、不新增自动重放或管理 API。业务 Handler 的事务边界和完整事件消费闭环仍是后续工程事项，决策见 ADR 0018。
- 回归：新增 Unit 重试→退避→死信→死信不再执行 Handler 测试、Pump dead-letter 计数测试、配置读取/边界测试，以及 PostgreSQL 真实容器端到端死信持久化测试；新集成用例在远端实际通过。
- 本地证据：`dotnet tool restore`、`dotnet restore InkFlow.sln` PASS；Release Build 0 warnings / 0 errors；Unit 466/466、Architecture 1/1、Contract 10/10 PASS；11 个迁移模型检查 PASS；API/Worker/Scheduler `/health` 均 HTTP 200；`bash -n scripts/verify-migrations.sh`、NuGet 漏洞审计、敏感信息模式检查和 `git diff --check` PASS。完整 `dotnet test InkFlow.sln --no-restore` 的 Unit/Architecture/Contract 通过，但 Integration 85 项为 6 通过、2 跳过、77 项因本机 `npipe://./pipe/docker_engine` 不可用而 BLOCKED；定向新 Inbox Integration 也因同一 Docker 阻塞，未将本机容器结果记为通过。
- 远端证据：候选提交 `622446264c9dbee09298e8001aef6c092d235211` 的 [CI 33285403134](https://github.com/nekohands/InkFlow/actions/runs/33285403134)、[Docker 33285403140](https://github.com/nekohands/InkFlow/actions/runs/33285403140)、[Security 33285403125](https://github.com/nekohands/InkFlow/actions/runs/33285403125) 均 GREEN 且指向同一 headSha。CI Integration 85 项为 83 passed / 2 skipped，`Inbox_Handler_Failure_Uses_Bounded_Retry_And_Persists_DeadLetter` 实际通过；Docker Collector 与四业务镜像构建/扫描通过，Security NuGet、Filesystem、CodeQL、SBOM 全部通过。
- 当前状态：本工作包为 `CI Green / Implemented`，整体仍为 `1.0 Release Candidate`，不等同于 `Accepted/Completed`；业务 Handler/完整消费闭环、本机 Docker、真实来源、阅读 3.0、人工验收和生产治理继续待定。

### 4.64 `crawler.task.created` Inbox 业务消费闭环（本轮，2026-08-30）

- 缺口：4.61–4.63 已完成 Outbox→Inbox relay、按类型领取和失败死信，但 Worker 没有具体业务 Handler；Crawler 任务创建事件不能触发完整的任务执行链路。
- 实现：Crawling Application 新增稳定载荷解析/校验和 `CrawlerTaskCreatedMessageHandler`；Handler 回读 `CrawlerTask` 权威事实，校验 Source/Capability/CreatedAt 后调用按任务 ID 的 PostgreSQL `FOR UPDATE SKIP LOCKED` 原子租约。新增 `CrawlerTaskProcessor` 统一周期轮询与 Inbox 触发的 Running、成功、任务级重试和死信状态机；Worker 注册 Handler，并补齐 Canonical Book 仓储依赖。
- 事务与安全边界：任务表仍是执行权威事实；任务行与 Outbox 继续同事务写入，Inbox 确认与任务状态提交分离，重复投递由 Inbox 主键、任务终态和租约吸收。事件不携带 Variables、CredentialReference、secret 或正文；身份不匹配/任务缺失进入通用 Inbox 稳定失败、退避和死信。
- 回归：新增 Handler 的原子领取/终态幂等/权威事实不匹配测试，新增 Processor 的成功/重试/死信测试，以及 Outbox→Inbox→Handler→任务完成端到端 PostgreSQL 用例。
- 本地证据：Release Build PASS（0 warnings / 0 errors）；Unit 472/472、Architecture 1/1、Contract 10/10 PASS；Windows 直接迁移模型检查 11/11 PASS；Worker `/health` HTTP 200。完整 Integration 当前 86 项为 6 通过、2 跳过、78 项因本机 `npipe://./pipe/docker_engine` 不可用而 BLOCKED；定向本轮端到端用例同样 BLOCKED。Bash 迁移包装脚本因当前 WSL 找不到 `dotnet` 未执行成功，不影响等价 Windows 检查；NuGet 漏洞审计无漏洞，`git diff --check` PASS。
- 远端证据：代码候选提交 `acbbd10dd67e350f2bf6b2ae1080c54f7b725d91` 的 [CI 33290137667](https://github.com/nekohands/InkFlow/actions/runs/33290137667)、[Docker 33290137676](https://github.com/nekohands/InkFlow/actions/runs/33290137676)、[Security 33290137668](https://github.com/nekohands/InkFlow/actions/runs/33290137668) 均 GREEN 且 headSha 一致；CI 为 Unit 472/472、Architecture 1/1、Contract 10/10、Integration 86（84 passed / 2 skipped），Docker 四业务镜像和 Collector 扫描通过，Security 的 CodeQL、Filesystem、SBOM、NuGet 审计通过。
- 当前状态：本工作包为 `CI Green / Implemented`，不等同于 `Accepted/Completed`；其他 Integration Event、Docker 本机复验、真实来源、阅读 3.0 和人工验收继续按第 6 节待定。

### 4.65 Inbox 终态死信纳入 Operations 告警观测（本轮，2026-08-30）

- 缺口：4.63 已将 Inbox Handler 失败收敛为 `DeadLetteredAt` 终态，但 Operations 告警快照只读取 Crawler 死信；消息消费持续失败无法进入统一运维告警与历史链路。
- 实现：新增 `IInboxDeadLetterReader` 与 PostgreSQL `EfMessagingMessageStore` 有界摘要读取，只统计 `DeadLetteredAt IS NOT NULL AND ProcessedAt IS NULL`，返回数量和 `HasMore`，不带载荷、失败文本或 TraceId；新增 `(ProcessedAt, DeadLetteredAt, Id)` 索引 Migration。Operations 增加 `InboxDeadLetterCountThreshold`，平台级快照产生 `inbox_dead_letters_present`，读取失败产生稳定 `inbox_dead_letter_snapshot_unavailable` 并将快照标为 partial；来源过滤的 Operator 视图不查询平台级 Inbox 状态。决策见 [ADR 0020](../adr/0020-inbox-dead-letter-operations-observation.md)。
- 回归：新增告警阈值/稳定错误/partial 历史语义/来源过滤单元回归，以及 PostgreSQL 有界死信读取与已处理行排除集成回归。
- 本地证据：Windows Release Build 0 warnings / 0 errors，Unit 475/475、Architecture 1/1、Contract 10/10 PASS；Windows 本机完整 Integration 因 `npipe://./pipe/docker_engine` 不可用而 BLOCKED。随后在 Ubuntu VM 以源码构建 Compose 实际启动 PostgreSQL、Redis、OTel Collector 与四个应用镜像，Migration 退出码 0，API/Worker/Scheduler 健康检查均 HTTP 200，Collector loopback 健康检查通过；Linux SDK 容器完整 `Restore → Build → Test` 为 Unit 475/475、Architecture 1/1、Contract 10/10、Integration 85 passed / 2 skipped / 0 failed，新增 Inbox 有界死信读取回归实际通过；Core SLO Runtime smoke 的 public、Legado、Developer 未授权和 Reader 四个服务面均通过。漏洞审计、敏感信息检查与 `git diff --check` PASS。
- 远端证据：代码候选提交 `72e49b30f36e78d0405b984580e1ce2a43381b32` 的 [CI 33291943661](https://github.com/nekohands/InkFlow/actions/runs/33291943661)、[Docker 33291943632](https://github.com/nekohands/InkFlow/actions/runs/33291943632)、[Security 33291943645](https://github.com/nekohands/InkFlow/actions/runs/33291943645) 均 GREEN 且 headSha 一致；CI Integration 87 项为 85 passed / 2 skipped，包含本轮 Inbox 回归，Docker 四业务镜像与 Collector 构建/扫描/发布通过，Security 的 NuGet、Filesystem、CodeQL、SBOM 全部通过。
- 当前状态：本工作包为 `Implemented`，整体继续保持 `1.0 Release Candidate`，不等同于 `Accepted/Completed`；外部通知、真实来源、阅读 3.0、人工验收和本机 Docker 仍按第 6 节待定。

### 4.66 GHCR 发布 Compose 与 Core SLO 证据文件健壮性（本轮，2026-08-30）

- 缺口：Docker 发布工作流实际推送到 `ghcr.io/nekohands/inkflow/inkflow-*`，默认 Compose 少了 `/inkflow/` 路径，导致已登录 GHCR 的部署机仍无法拉取应用镜像；Core SLO 探针默认证据文件使用固定名称，跨用户重复执行时可能被 `/tmp` 粘滞位阻止覆盖。
- 实现：修正四个 GHCR 应用镜像引用；Docker 工作流新增 `Verify published Compose images`，在四个业务镜像全部发布后真实执行默认 Compose 拉取；Core SLO 探针在未显式指定路径时生成随机后缀证据文件，保留 `INKFLOW_SLO_EVIDENCE_FILE` 显式路径契约。
- 回归：新增默认证据路径遇到既有不可写同名文件的 shell 回归，并隔离 GitHub Runner 的 `RUNNER_TEMP`；本地 bash 语法、脚本回归和 `git diff --check` 通过。首次候选 `623077c` 的 CI 曾因测试未隔离 `RUNNER_TEMP` 失败，读取日志后以 `ff7ba52` 修复并重新全量回归。
- Ubuntu VM 证据：`e5af3c5` 修复后的默认 GHCR Compose 全部镜像拉取成功，Migration 退出码 0，PostgreSQL/Redis/OTel Collector 与 API/Worker/Scheduler 健康；默认 Core SLO 四服务面、公开 API/Legado/Reader/PWA 入口和脚本回归均通过。备份恢复演练通过，结果为 `archive=80181 bytes, audit_events=63`；栈已停止，验证卷保留。备份脚本首次无 sudo 因 Docker socket 权限失败，使用 VM 既有授权重跑通过，不属于应用故障。
- 远端证据：最终提交 `ff7ba52eeee2d817c17fcb08e88cb0d2c087cf12` 的 [CI 33294167216](https://github.com/nekohands/InkFlow/actions/runs/33294167216)、[Docker 33294167310](https://github.com/nekohands/InkFlow/actions/runs/33294167310)、[Security 33294167234](https://github.com/nekohands/InkFlow/actions/runs/33294167234) 均 GREEN 且 headSha 一致。CI 真实测试为 Unit 475/475、Architecture 1/1、Contract 10/10、Integration 87（85 passed / 2 skipped）和 Redis 1/1；包含 Compose、Runtime smoke、Core SLO、备份恢复与 diagnostics。Docker 新增的发布后 Compose 拉取门禁通过，Security 的 SBOM、Filesystem、NuGet、CodeQL 全部通过，保留既有 Node/CodeQL 权限提示。
- 当前状态：本工作包为 `Implemented`，整体继续保持 `1.0 Release Candidate`，不等同于 `Accepted/Completed`；真实来源/故障切换、阅读 3.0、浏览器与私有库人工验收，以及生产 OTLP/告警/备份治理仍按第 6 节待定。

### 4.67 ContentVersion 当前版本切换边界修复（本轮，2026-08-30）

- 缺口：`EfContentVersionRepository.SetCurrentAsync` 原先分两条更新语句执行，且第二条只按 `versionId` 设置 `IsCurrent`，未验证目标版本属于请求章节；跨章节 ID 可能被选中，第二条失败或并发切换还可能留下错误的当前状态。
- 实现：在同一数据库事务内先校验 `(chapterId, versionId)` 归属，再以单条按章节 UPDATE 同时清除其它当前标记并设置目标版本，保持版本历史不可变且拒绝无效目标。
- 回归：新增 `ContentVersionRepositoryTests` 两个 PostgreSQL Testcontainers 用例，覆盖跨章节拒绝且保留原当前版本，以及同章节切换后仅有一个当前版本。本机 Release Build 0 warnings / 0 errors；本机 Integration 因 Windows Docker 管道不可用仍 BLOCKED，Ubuntu VM 真实 Testcontainers 2/2 通过，测试后无遗留容器。
- 远端证据：提交 `74b0d536af9d37f282c64fb78f6041987841300d` 的 [CI 33294984996](https://github.com/nekohands/InkFlow/actions/runs/33294984996)、[Docker 33294984938](https://github.com/nekohands/InkFlow/actions/runs/33294984938)、[Security 33294984918](https://github.com/nekohands/InkFlow/actions/runs/33294984918) 均 GREEN 且 headSha 一致；CI Unit 475/475、Architecture 1/1、Contract 10/10、Integration 89（87 passed / 2 skipped）和 Redis 1/1 全部通过。
- 当前状态：本工作包为 `Implemented`，整体仍保持 `1.0 Release Candidate`，不等同于 `Accepted/Completed`；真实来源/故障切换、阅读 3.0、浏览器/私有库人工验收和生产治理仍按第 6 节待定。

### 4.68 前端纳入 1.0 Release Gate 与源码构建验证策略（本轮，2026-08-30）

- 范围：明确 Web Reader（书库/详情/目录/正文）、Reader/PWA（账户/书架/历史/离线壳/Manifest/Service Worker）和受保护 Operations Center UI 都属于 1.0 必选前端范围，不再作为 Phase 3 完成后的可选项。
- 自动化门禁：新增 `scripts/reader-frontend-runtime-smoke.sh` 及 fixture 回归，独立验证源码构建 Compose 运行时的 Reader/PWA/Operations 页面、响应式与可访问性标记、Manifest、Service Worker、图标和 Token/内部字段不泄漏；CI 新增明确的 `Frontend 1.0 runtime smoke` 步骤。`ReaderHtml` 既有单元回归继续作为渲染层门禁。
- Docker 策略：日常开发和 Runtime 验证统一使用 `docker-compose.build.yml` 从当前源码构建；GHCR `docker-compose.yml` 仅用于发布镜像、镜像一致性或明确要求的镜像复验。CI Runtime smoke 的日志、清理和健康验证均固定指向源码构建编排。
- 验收边界：前端自动化门禁不等于浏览器人工验收；移动/平板/桌面/宽屏视觉、UX、键盘焦点、对比度、触控、长时间阅读、PWA 安装/离线和真实账户链路仍按用户决定 `NOT RUN`，继续作为 1.0 Release Gate 待定项。
- 远端证据：候选提交 `1b1149d4f1bdbb3369c3c3e84baea913ef275437` 的 [CI 33295992063](https://github.com/nekohands/InkFlow/actions/runs/33295992063)、[Docker 33295992049](https://github.com/nekohands/InkFlow/actions/runs/33295992049)、[Security 33295992045](https://github.com/nekohands/InkFlow/actions/runs/33295992045) 均 GREEN 且 headSha 一致。CI 的 Restore/Build/迁移校验、Unit 475/475、Architecture 1/1、Contract 10/10、Integration 89（87 passed / 2 skipped）、Redis 1/1、源码构建 Compose Runtime、`Frontend 1.0 runtime smoke`、SLO、备份恢复和 diagnostics 全部通过；Docker 的四业务镜像构建/扫描/发布与发布 Compose 拉取复验通过；Security 的 NuGet、Filesystem、SBOM、CodeQL 全部通过。
- 当前状态：本工作包自动化门禁已通过，整体保持 `1.0 Release Candidate`，不标记 `Accepted/Completed`；浏览器/真实设备、真实来源、真实账户和其他人工/生产验收仍待定。

### 4.69 Ubuntu VM 源码构建 Runtime 复验（本轮，2026-08-30）

- 目标：在独立 Ubuntu 验证环境用当前 `dev` 源码执行 `docker-compose.build.yml`，补足本机 Docker CLI 不可用时缺失的真实 Compose 证据；本轮不使用 GHCR 镜像替代源码验证。
- 证据：远端工作区快进到 `81019f9bf638f11d2ef1d719ad14d8bfea5b034c`；四个业务镜像由源码构建，Migration 退出码 0，PostgreSQL/Redis/API/Worker/Scheduler 健康，OTel Collector 正常运行。
- Runtime：`reader-frontend-runtime-smoke.sh` PASS；Core SLO 四面 PASS（public_api p95 908.170ms、legado_api p95 15.611ms、developer_api p95 10.931ms、reader p95 3.602ms）；备份恢复 PASS（`archive=80959 bytes, audit_events=78`）。验证完成后已停止容器，`inkflow-postgres` / `inkflow-redis` 数据卷保留。
- 配置边界：本机根目录 `.env` 仅用于读取 Compose、验证和远端连接配置，已由 `.gitignore` 忽略；敏感值不进入仓库、Progress、Handoff 或 CI 日志，GHCR Token 不复制。
- 当前状态：源码构建 Runtime 证据已补齐，但浏览器/真实设备、阅读 3.0、真实来源/账户和其他人工/生产验收仍是 1.0 Release Gate 待定项。

### 4.70 本地验证配置模板（本轮，2026-08-30）

- 新增根目录 [`.env.example`](../../.env.example)，集中列出源码 Compose、发布 Compose、前端/SLO smoke、OTel、GHCR 备注和 Ubuntu VM 连接配置的键名与安全占位符。
- 真实 `.env` 继续只保留在本机并由 `.gitignore` 忽略；模板不包含密码、Token、Cookie 或实际部署地址。README 已补充复制模板和敏感值边界说明。
- 当前状态：配置读取入口已具备可复制模板，不改变 1.0 的人工/真实环境验收边界。

### 4.71 SourceAdapterFactory 来源分派回归（本轮，2026-08-30）

- 补充 `SourceAdapterFactoryTests`，覆盖可信 CodeAdapter 优先于仓储查询、带有效 Rule DSL 的来源构建 `RuleBasedSourceAdapter`，以及缺失/无规则来源返回空值。
- 回归保持完全离线：使用内存仓储和 No-op HTTP seam，不触网、不读取真实凭据，避免把测试证据误当作真实来源验收。
- 本地定向结果：3/3 通过；完整 Build/Test/Runtime/CI 将随候选提交重新执行。
- 当前状态：来源分派关键 seam 的自动化覆盖已补齐，整体仍为 `1.0 Release Candidate`；真实来源/故障切换和人工验收边界不变。

### 4.72 本地 `.env` 验证脚本加载（本轮，2026-08-30）

- 补充 `scripts/load-local-env.sh`，前端 smoke、Core SLO smoke 和 PostgreSQL 备份恢复脚本会自动读取根目录 `.env`；已有导出环境变量优先，支持本地文件覆盖和显式跳过。
- 加载器只解析受限的 `KEY=value` / `export KEY=value` 赋值，不执行 `.env` 内容，也不打印值；新增脚本回归覆盖注释、空值、引号、`#`、字面量命令文本和环境变量优先级。
- `.env.example` 增补前端 curl 超时配置和加载说明；`.env` 继续由 `.gitignore` 忽略，敏感值不进入提交、文档或 CI 日志。
- 本地脚本语法/回归均通过；候选提交 `b9c2f70` 的远端 [CI 33298192655](https://github.com/nekohands/InkFlow/actions/runs/33298192655)、[Docker 33298192688](https://github.com/nekohands/InkFlow/actions/runs/33298192688)、[Security 33298192776](https://github.com/nekohands/InkFlow/actions/runs/33298192776) 均 GREEN 且 head SHA 一致。CI 中 Unit 478/478、Architecture 1/1、Contract 10/10、Integration 89（87 passed / 2 skipped）、Redis 1/1、源码 Compose Runtime、前端 smoke、SLO、备份恢复和新增 env-loader 回归均通过。
- 当前状态：本地验证入口可直接复用 `.env`，不改变源码 Compose 为日常默认和 GHCR Compose 仅用于发布/镜像复验的边界；整体仍为 `1.0 Release Candidate`。

### 4.73 BookDiscoveryService 单来源异常隔离（本轮，2026-08-30）

- 缺口：搜索适配器异常原先已按来源隔离，但健康检查、适配器工厂、书目导入或 Canonical 匹配阶段的异常仍可能中止整个发现循环，导致其他来源无法继续返回结果。
- 实现：将每个来源的发现流程置于独立异常边界；保留 `OperationCanceledException` 取消传播，其他异常转换为有界来源 warning 并继续处理后续来源，不改变公共 API、数据库结构或关键实体不变量。
- 回归：先补充会在导入阶段抛错的 `ThrowingSourceBooks` 测试并确认旧实现红灯，再以修复后的实现验证“坏来源产生 warning、好来源仍返回命中”。本地 `dotnet restore`、Release Build（0 warnings / 0 errors）、Unit 479/479、Architecture 1/1、Contract 10/10 均通过。
- 远端证据：候选提交 `ea562d6` 的 [CI 33299004055](https://github.com/nekohands/InkFlow/actions/runs/33299004055)、[Docker 33299004053](https://github.com/nekohands/InkFlow/actions/runs/33299004053)、[Security 33299004087](https://github.com/nekohands/InkFlow/actions/runs/33299004087) 均 GREEN 且 head SHA 一致。CI 中 Unit 479/479、Architecture 1/1、Contract 10/10、Integration 89（87 passed / 2 skipped）、Redis 1/1、源码 Compose Runtime、前端 1.0 smoke、SLO、备份恢复和 Runtime diagnostics 均通过；Docker 发布前扫描、四业务镜像和发布 Compose 拉取复验通过；Security 的 NuGet、Filesystem、CodeQL、SBOM 全部通过。
- 验收边界：本轮不触发真实来源、真实追更、MuMu/阅读 3.0、浏览器人工验收或真实凭据操作；`.env` 仍只在本机使用并保持 ignored/untracked。整体保持 `1.0 Release Candidate`，不标记 `Accepted/Completed`。

### 4.74 公共发现 warning 脱敏与 Ubuntu VM 全量本地验证（本轮，2026-08-30）

- 缺口：`DiscoveryOutcome.Warnings` 会直接进入 `/api/v1/search` 响应；来源搜索或导入阶段若把底层异常原文写入 warning，可能泄露连接细节、内部路径或其他实现信息。
- 修复：公共 warning 统一为包含阶段和来源标识的稳定提示（`search` / `discovery`），不再返回底层异常文本；`OperationCanceledException` 仍按原语义传播，单来源隔离和后续来源继续处理保持不变。
- TDD 回归：先用包含内部路径的异常文本确认旧实现红灯，再加入安全 warning 断言并验证修复后 Unit 定向测试 7/7 通过；不改变 API schema、数据库结构或领域不变量。
- Ubuntu VM 本地证据：将根目录 `.env` 以仅属主可读权限放入 VM（内容不输出且不纳入 Git），使用源码构建 Compose 完成四业务镜像构建；Migration 退出码 0，PostgreSQL、Redis、OTel Collector、API、Worker、Scheduler 健康。运行时认证/授权、阅读偏好、Personal Legado Token 创建/撤销、Reader/PWA/Operations 前端契约、四面 Core SLO 探针、OTel 指标接收和备份恢复均通过；备份归档 `84366` bytes，恢复库审计事件 `119` 条。
- VM 全量测试：在 Ubuntu VM 的 .NET 10 SDK 容器中完成 `Restore → Release Build → Test`；Build 为 0 warnings / 0 errors，Unit 479/479、Architecture 1/1、Contract 10/10、Integration 89（87 passed / 2 skipped / 0 failed）。验证结束后已停止源码 Compose，删除本轮临时测试卷，保留 Compose 持久卷；Windows 开发机 Docker Engine 仍不可用，但不再阻塞本轮 VM 本地证据。
- 远端证据：代码候选提交 `689a79c` 的 [CI 33299762844](https://github.com/nekohands/InkFlow/actions/runs/33299762844)、[Docker 33299762806](https://github.com/nekohands/InkFlow/actions/runs/33299762806)、[Security 33299762799](https://github.com/nekohands/InkFlow/actions/runs/33299762799) 均 GREEN 且 head SHA 为 `689a79c72c6b3ae73df1e5c4b37e95a7f9658bfa`；CI 同步覆盖源码 Compose、前端 1.0 smoke、SLO、Redis、备份恢复和 Runtime diagnostics，Docker 四业务镜像与发布前扫描、Security 的 NuGet/Filesystem/CodeQL/SBOM 门禁均通过。
- 验收边界：按用户决定不执行 MuMu/阅读 3.0 真机、真实来源/真实追更/真实故障切换、浏览器视觉/安装/长时间阅读和真实凭据操作；`.env` 在 Windows 与 VM 均保持本地 ignored/untracked，敏感值不进入提交、文档或 CI 日志。整体保持 `1.0 Release Candidate`，不标记 `Accepted/Completed`。

### 4.75 GPT 内置浏览器自动化验收与非阅读 App 门禁复验（本轮，2026-08-30）

- 范围：按本轮要求，除 MuMu/阅读 3.0 之外，优先使用可重复的源码、fixture、运行时脚本和 GPT 内置浏览器完成验收；不把阅读 App、真实第三方来源或真实生产账户操作伪装成自动化通过。
- 浏览器路由矩阵：使用 GPT 内置浏览器访问 Reader 书库、账户、书架、历史、离线壳、Operations Center 和未发布章节状态；各页面的主内容锚点、跳转链接、viewport 标记和无横向溢出检查通过，Reader 与 Operations 页面浏览器错误日志为空。
- 浏览器交互：Reader 搜索按钮提交、无结果与部分来源不可用提示、匿名账户页面、书架/历史登录提示、离线返回入口和 Operations 匿名角色保护均通过；登录/注册表单的 required、email/password 类型及 autocomplete 约束已自动读取确认，未创建真实账户。
- 响应式与可访问性：在 375×812、768×1024、1280×720、1920×1080 四个视口完成自动检查；均无横向溢出，宽屏内容壳保持 1152px 最大宽度，移动端搜索控件仍保持可用尺寸；跳过主要内容链接、语义区域、状态区和键盘焦点可见性通过。提交按钮点击路径通过；内置浏览器对 Enter 的合成按键未触发表单提交，归类为浏览器驱动能力限制，不据此改动生产代码。
- PWA 资源：浏览器页面确认 Manifest 链接存在；当前 VM 源码构建栈的 reader-frontend-runtime-smoke 已通过 Manifest、Service Worker、图标和敏感字段契约。内置浏览器直接导航非 HTML 资源时返回 ERR_BLOCKED_BY_CLIENT，故不把该工具限制当作应用失败；实际安装、Service Worker 注册和断网切换仍需支持安全上下文的真实浏览器证据。
- VM 运行时与回归：源码构建 Compose 已完成四业务镜像构建、Migration 退出码 0、健康检查、前端冒烟和浏览器验收；随后已停止容器并删除本轮隔离测试卷，保留 Compose 持久卷。Linux SDK 容器完成 Restore → Release Build → Test：Build 0 warnings / 0 errors，Unit 482/482、Architecture 1/1、Contract 10/10、Integration 89（87 passed / 2 skipped / 0 failed）。
- 代码回归：候选提交 01d09ab 为 ContentPublishingService 注入式选优路径的三条回归测试，覆盖新版本发布、重复发布和选优失败不误标当前版本；Windows 定向/全量 Unit、Architecture、Contract 与 Release Build 均通过。
- 远端证据：候选提交 01d09ab94788e65ecc937ec3b31e36fdab67f755 的 CI 33301352344、Docker 33301352352、Security 33301352365 均 GREEN 且 head SHA 一致。
- 当前状态：非阅读 App 的源码、fixture、VM runtime 和浏览器自动化门禁已取得证据；真实来源/追更/第二来源切换、真实账户/生产凭据、PWA 安装断网、阅读 3.0 真机和长时间阅读仍是明确待定项。整体保持 1.0 Release Candidate，不标记 Accepted/Completed。

### 4.76 Kanunu8 真实来源只读自动验证（本轮，2026-08-30）

- 在 Ubuntu VM 的临时 .NET 10 SDK 容器中启用现有 opt-in live 测试，使用源码当前候选验证 Kanunu8 真实网络适配器；不创建账号、不写业务数据，测试结束后临时 NuGet 卷已删除。
- 真实结果：BookInfo、TOC、章节正文 3/3 通过（固定书目 book/3441）；正文解析和 GB18030 解码均取得真实响应证据。
- 边界：Kanunu8 当前 Search 能力按适配器契约返回空结果，因此这组证据不等于完整 Search → BookInfo → TOC → Content 或真实追更验收；linovelib POST 搜索返回 200 但空响应体，17K 搜索 API TLS 证书校验失败，真实第二来源/切换仍未闭合。
- 当前状态：真实 Kanunu8 只读适配器证据已补齐；真实 Search、真实追更和稳定第二 Official Source 继续列入待定事项，整体保持 1.0 Release Candidate。

### 4.77 linovelib 真实公开站点只读链路自动验证（本轮，2026-08-30）

- 使用 GPT 内置浏览器在公开站点提交 `恶魔高校` 搜索，页面返回 3 个结果并定位到 `novel/1`；未登录、未创建账号、未写入站点数据。
- 书籍详情页自动读取标题 `恶魔高校DxD`、作者 `石踏一荣` 和目录入口；目录页显示 482 章，并自动定位首卷首章 `novel/1/3.html`。
- 正文页自动读取 `Life.1 不当人类。` 标题及非空段落，形成公开站点层面的 Search → BookInfo → TOC → Content 只读证据。
- 验收边界：该证据验证真实站点页面链路，不等于 InkFlow 的 RuleAdapter 后端直连已通过。当前普通 HTTP POST 请求仍可能得到 200/空响应体，疑似受站点会话或反爬挑战影响；不读取 Cookie、不绕过挑战、不把浏览器结果伪装成服务端适配器结果。
- 当前状态：linovelib 公开页面只读自动验收已完成；RuleAdapter 后端直连、真实第二来源故障切换和真实追更仍列入待定事项，整体保持 1.0 Release Candidate。

### 4.78 Private Library 非阅读 App 自动化运行验收（本轮，2026-08-30）

- 缺口：Private Library v1/v2 已有 Unit、Contract 和 PostgreSQL 集成覆盖，但缺少可在源码构建 Compose 上重复执行的 API 运行时验收，尤其是跨用户隔离、私有缓存策略、导入/导出和公共路径不泄漏。
- 实现：新增 `scripts/private-library-runtime-smoke.sh`，在 CI 与本地可用环境中使用 `curl` + `jq` 验证未认证 401、两个唯一临时用户、书目创建/列表/详情/更新/删除、TXT 两章导入、章节顺序/正文、`private, no-store`、TXT 导出、跨用户 404，以及公共 API/公共 Legado 详情/正文 404、公共 Catalog/Reading Shelf 不含私有书名。新增脚本语法/结构回归并接入 `.github/workflows/ci.yml`。
- TDD/回归：VM 首轮实跑发现 smoke 脚本把元数据更新误发为 POST，API 正确返回 405；修复为 PUT 后重新执行。该问题只影响验收脚本，不涉及产品代码；临时书目由脚本清理。
- Ubuntu VM 证据：工作区快进到代码候选 `8e69b46`，使用 `docker-compose.build.yml` 从源码构建四个业务镜像并以 `--wait` 启动健康栈；临时 SDK 工具容器执行 smoke，结果为 `private-library-runtime-smoke: PASS (auth, ownership, CRUD, TXT import/read/export)`；完成后已停止 Compose，仅保留持久数据卷。
- 远端证据：代码候选 `8e69b469ee0a56c28d3ba24ec99817cdf1a1f86a` 的 [CI 33305236784](https://github.com/nekohands/InkFlow/actions/runs/33305236784)、[Docker 33305236750](https://github.com/nekohands/InkFlow/actions/runs/33305236750)、[Security 33305236817](https://github.com/nekohands/InkFlow/actions/runs/33305236817) 均 GREEN 且 head SHA 一致；CI 的 Restore/Release Build/迁移校验/全量测试、源码 Compose Runtime、前端 smoke、私有库 smoke、SLO、备份恢复和 diagnostics 均通过，Docker 四业务镜像构建/扫描通过，Security 的 NuGet/Filesystem/SBOM/CodeQL 均通过。
- 验收边界：本轮只创建唯一临时测试账户，未使用真实账户、阅读 3.0 或第三方登录；因当前没有账号删除 API，两个测试账户保留在 VM 持久数据库，测试书目已清理。真实账户可读性和阅读 App 流程继续列入待定事项，EPUB/重复导入/失败导入已由 4.79 自动化关闭。

### 4.79 Private Library 文件边界与失败一致性自动化验收（本轮，2026-08-30）

- 缺口：4.78 已覆盖 TXT 导入和基本运行链路，但 EPUB round-trip、重复导入不覆盖原书以及损坏文件失败后不留下半本书尚未取得源码构建运行证据。
- 实现：扩展 `scripts/private-library-runtime-smoke.sh`，验证 TXT 导出 EPUB 的 Content-Type/非空文件、EPUB 再导入后的元数据/章节顺序/正文、重复导入产生独立稳定 PrivateBook 身份且原书仍可读，以及损坏 EPUB 返回稳定 `invalid_file`/HTTP 400 且所有者书目数量不变。失败清理同时覆盖新增临时书目；脚本结构回归同步更新。
- Ubuntu VM 证据：候选 `5b13d8e` 使用 `docker-compose.build.yml` 从源码构建 API/Worker/Scheduler/Migrations，Migration 和四服务健康检查通过；临时 SDK 工具容器执行结果为 `private-library-runtime-smoke: PASS (auth, ownership, CRUD, TXT/EPUB import/read/export, duplicate isolation, failed-import rollback)`，随后停止 Compose，仅保留持久数据卷。
- 远端证据：最终文档同步提交 `fd9f8b13e723ab74a9f850ecd2557959eac082b1` 的 [CI 33306505203](https://github.com/nekohands/InkFlow/actions/runs/33306505203)、[Docker 33306505188](https://github.com/nekohands/InkFlow/actions/runs/33306505188)、[Security 33306505230](https://github.com/nekohands/InkFlow/actions/runs/33306505230) 均 GREEN 且 head SHA 一致；CI 新增脚本回归与运行时 EPUB/重复/失败导入断言均通过。
- 验收边界：本轮使用唯一临时注册账户和程序生成文件，不使用真实账户、阅读 3.0 或第三方登录；VM 上测试书目已清理，临时测试账户因没有账号删除 API 继续保留。真实账户/人工体验仍可作为补充，但本项非阅读 App 自动化门禁已闭合；整体仍为 1.0 Release Candidate。

### 4.80 17K 真实公开接口只读探测（本轮，2026-08-30）

- 按“除阅读 App 外尽量自动化”的要求，在 Ubuntu VM 对 17K 公开接口执行了只读探测；不创建账户、不写入站点数据、不关闭 TLS 校验，也没有把外部接口响应伪装成适配器通过。
- `api.ali.17k.com` 的 Search 入口在当前 VM 因上游证书链无法由系统 CA 验证而失败；保留严格 TLS 校验，未使用 `-k` 绕过。备用 `api.17k.com` Search 虽返回 HTTP 200，但响应状态为“请升级版本”，无法取得可用搜索结果；固定书目详情接口返回“图书信息不存在”。Web 章节地址可达但只取得压缩页面响应，尚不足以形成稳定 Search → BookInfo → TOC → Content 证据。
- 结论：17K 真实链路仍为 `BLOCKED / 待定`，本轮只保留离线 Fixture 回归和上述可复核网络结论；真实免费正文、VIP 边界、安全重定向和第二来源故障切换不标记通过。

### 4.81 Developer API / 商业基础非阅读 App 自动化运行验收（本轮，2026-08-30）

- 新增 `scripts/developer-api-runtime-smoke.sh` 及结构回归，并接入 `.github/workflows/ci.yml` 的源码构建 Runtime。脚本覆盖未认证拒绝、唯一临时账户、默认 Free 套餐和 `developer.catalog.read` 权限、应用创建/列表、API Key 首次签发、列表脱敏、目录读取及 `private, no-store`。
- 安全与生命周期断言覆盖：查询参数和 Bearer 不可替代 `X-InkFlow-Api-Key`，密钥轮换后旧密钥立即失效、新密钥可读目录，轮换/撤销状态在列表中可追踪，撤销后的密钥不可用；全程不输出原始密钥，临时应用在退出清理时撤销。
- Ubuntu VM 已快进到候选 `f235c8e`，使用 `docker-compose.build.yml` 源码构建 API/Worker/Scheduler/Migrations 并通过健康等待；临时 .NET 10 SDK 工具容器输出 `developer-api-runtime-smoke: PASS (account, entitlement, app/key lifecycle, redaction, header-only auth, catalog quota path, rotation, revoke)`，随后自动停止 Compose，持久卷保留。
- 本轮只使用程序生成的临时账户和凭据，不触碰真实生产凭据；由于当前没有账户删除 API，临时账户仍可能保留在 VM 数据库，但应用和密钥已由脚本撤销。真实 Web 账户、Administrator 套餐授予、超额 `429/Retry-After`、跨账户配额和用户停用仍列入人工/真实环境验收。
- 代码候选 `f235c8e` 已推送；最终文档同步提交后的 [CI](https://github.com/nekohands/InkFlow/actions)、[Docker](https://github.com/nekohands/InkFlow/actions) 和 [Security](https://github.com/nekohands/InkFlow/actions) Run 必须与最终 HEAD 对齐并全部 GREEN 后，才可关闭本工作包门禁。

### 4.82 Reader/PWA Service Worker 与离线壳非阅读 App 自动化验收（本轮，2026-08-30）

- 按“除阅读 App 外尽量自动化”的要求，本轮继续使用 Ubuntu VM 的 `docker-compose.build.yml` 源码构建栈，并用 GPT 内置浏览器完成安全上下文、Service Worker、缓存和断网回退验收；未使用 MuMu/阅读 3.0、真实账户或生产凭据。
- 直接访问 VM 的 `http://172.19.31.153:8080/reader` 明确得到 `secureContext=false`、Service Worker 不可用；为验证应用本身，临时建立本地 SSH 转发访问同一 VM 的 `http://localhost:18080/reader/`。该转发只改变浏览器安全上下文，不改变应用代码或数据源，且验证结束已关闭。
- 浏览器证据通过：`secureContext=true`；Manifest 的 `start_url=/reader`、`scope=/reader/`、`display=standalone` 与两枚图标均可读取；Service Worker `/reader/sw.js` 状态为 `activated`，刷新 `/reader/` 后 `navigator.serviceWorker.controller` 为真；`inkflow-reader-shell-v1` 缓存包含 `/reader/offline`、Manifest 和两枚图标。
- 真实断网回退通过：停止 VM API 容器后，浏览器访问 `/reader/account` 仍由 Service Worker 控制并显示“当前处于离线状态”；恢复 API 后回到正常账户表单且无离线提示。浏览器错误/警告日志为空。验证结束已停止全部 Compose 容器并关闭转发，持久卷保留。
- 边界：内置浏览器未执行安装提示/独立窗口启动、真实账户登录/状态同步和跨设备同步；VM IP 的明文 HTTP 也不能作为生产 PWA 安全上下文证据。生产 HTTPS、安装体验、真实账户及跨设备验收继续列入待定事项；本项不替代阅读 3.0 真机验收。

### 4.83 管理端/运维/权限非阅读 App 自动化运行验收（本轮，2026-08-30）

- 按“除阅读 App 外尽量自动化”的要求，新增 `InkFlow.AcceptanceFixtures` fixture 工具和 `scripts/admin-runtime-smoke.sh`；fixture 只通过现有 Domain/EF Repository 准备临时用户、来源和 CanonicalBook，不手写 SQL、不增加生产后门 API。源码构建 Compose 以 acceptance profile 提供只读源码挂载、临时构建目录和最小权限 launcher。
- Admin runtime smoke 自动验证：管理员套餐列表、Operations 概览/告警/历史、Audit Read；Operator 授权前 403、授予 `source.manage` 后可读健康状态、重复授予幂等、能力停用/恢复、撤销后再次 403；默认 CredentialReference 的非 secret 引用 set/clear；Content Policy 下架/恢复及公共详情可见性；Administrator 为临时 Operator 授予 Pro 后的 Entitlement/quota；命令审计过滤。
- 本机证据：`dotnet build InkFlow.sln -c Release --no-restore` PASS（0 warnings / 0 errors）；Unit 482/482、Architecture 1/1、Contract 10/10 已通过；Admin smoke launcher/script 回归 PASS；`git diff --check` PASS。Windows Docker Engine 仍不可用。
- Ubuntu VM 证据：候选 `b7019b7` 拉取后使用 `docker-compose.build.yml` 源码构建 API/Worker/Scheduler/Migrations，健康检查通过；Admin runtime smoke 输出 `PASS (admin/operations, audit, source permissions and health, credential binding, content policy, entitlement)`；临时用户由 fixture 清理为 disabled，验证结束 Compose 已停止，持久卷保留，VM 工作区 CLEAN。
- 远端证据：候选 `b7019b7c6ef7f2999a800ec65b668372c9e7643d` 的 [CI 33311294258](https://github.com/nekohands/InkFlow/actions/runs/33311294258)、[Docker 33311294256](https://github.com/nekohands/InkFlow/actions/runs/33311294256)、[Security 33311294239](https://github.com/nekohands/InkFlow/actions/runs/33311294239) 均 GREEN；CI 的 Admin runtime smoke、前端 1.0、SLO、Redis、PostgreSQL 备份恢复和 diagnostics 均实际通过。
- 边界：未启动 MuMu/阅读 3.0，未使用真实 Web 账户、生产凭据或真实第三方来源；真实管理员/Operator 体验、生产通知/Provider、跨设备与阅读 App 流程继续保留为人工或真实环境补充，不标记整体 `Accepted/Completed`。

### 4.84 Reader/PWA 账户与阅读状态 API 非阅读 App 自动化运行验收（本轮，2026-08-30）

- 按“除阅读 App 外尽量自动化”的要求，新增 scripts/reader-account-runtime-smoke.sh 及结构回归，并接入 .github/workflows/ci.yml 的源码构建 Runtime；账户页面仍不在脚本中输入真实或人工凭据。
- 自动化覆盖：匿名 401、临时 Reader 注册与 auth/me、注册会话登出、登录、Refresh Token 轮换及旧 refresh 失效；阅读偏好默认值/持久化/边界拒绝；空书架/历史/进度 404；CanonicalBook fixture 书架加入/查询/移除、进度保存/读取、书架当前章节回显、历史联动、非法章节拒绝，以及最终登出后的认证失效。
- 本机证据：Release Build 0 warnings / 0 errors；reader-account smoke 结构回归、bash -n 和 git diff --check PASS。Windows Docker Engine 仍不可用。
- Ubuntu VM 证据：候选 0597332 使用 docker-compose.build.yml 源码构建 API/Worker/Scheduler/Migrations，健康等待通过；reader-account-runtime-smoke: PASS (register, login, refresh rotation, logout, preferences, shelf, progress, history)；临时账户由 fixture 清理为 disabled，验证结束 Compose 已停止，持久卷保留。
- 远端证据：候选 05973324870386e67bd3cf6e8c45479b3288f4cf 的 CI 33312963081、Docker 33312963065、Security 33312963084 均 GREEN；CI 的 Reader account smoke script regression 与 Reader account runtime smoke 均实际通过。
- 边界：本轮不启动 MuMu/阅读 3.0，不输入真实 Web 账户，不把 API smoke 等同于 PWA 页面内的真实登录、安装/独立窗口、跨设备同步或长期体验；这些项目继续保留为待定事项。

### 4.85 Reader/PWA 页面临时账户内置浏览器自动化验收（本轮，2026-08-30）

- 按“除阅读 App 外尽量自动化”的要求，在 Ubuntu VM 的 `docker-compose.build.yml` 源码构建栈上使用 GPT 内置浏览器完成一次性临时账户页面验收；账户使用随机本地邮箱和随机密码，仅发送到本地转发的 InkFlow API，不使用真实账户或第三方凭据。
- 页面链路通过：注册并返回书库、账户页刷新后保持登录态、打开 Catalog fixture、加入书架、书架列表显示书目、有效章节页显示“该章节尚未发布内容”的正确空状态、退出登录后恢复登录表单、书架/历史显示匿名登录提示、离线壳显示“当前处于离线状态”。
- 运行边界：没有读取浏览器 Cookie/Storage，没有操作 MuMu/阅读 3.0，没有触碰第三方写入；临时账户已通过 AcceptanceFixtures 禁用，Compose 和临时 SSH 转发均已停止。
- 验收边界：真实生产账户、HTTPS 安全上下文、安装提示/独立窗口、跨设备同步、长时间阅读和真实章节正文仍需真实部署或人工补充；本轮不关闭阅读 3.0 待定项，也不宣称整体 1.0 `Accepted/Completed`。

### 4.86 Reader/PWA 已发布正文自动化验收（本轮，2026-08-30）

- 为补齐 4.85 中 Catalog fixture 只有“未发布正文”空状态的问题，`InkFlow.AcceptanceFixtures` 新增独立的 `ensure-reader-catalog` 命令；它复用稳定的书目/章节 ID，并通过正式 `ContentPublishingService` 幂等发布短正文，原 `ensure-catalog` 继续保持无正文语义。
- 新增 `scripts/reader-content-runtime-smoke.sh` 及 fixture 回归，并接入 CI 的源码构建 Runtime：使用已发布夹具检查章节 HTML 的正文、阅读进度同步契约和结束标记，同时明确拒绝“未发布内容”状态；Reader account smoke 改用 `ensure-reader-catalog`。
- 本机证据：`dotnet restore InkFlow.sln`、`dotnet build InkFlow.sln -c Release --no-restore` PASS（0 warnings / 0 errors）；Unit 482/482、Architecture 1/1、Contract 10/10 PASS；新脚本 Bash 语法/fixture 回归 PASS。全量 `dotnet test` 的 IntegrationTests 因 Windows Docker Engine 不可用而为 6 passed / 2 skipped / 81 blocked，进程退出码 1，不记为本机集成通过。
- Ubuntu VM 证据：候选 `593f093` 使用 `docker-compose.build.yml` 源码构建 API/Worker/Scheduler/Migrations，PostgreSQL、Redis、服务健康检查通过；`ensure-reader-catalog` 连续执行两次返回相同稳定 ID，`reader-content-runtime-smoke: PASS (published content, reader progress contract)`；此前内置浏览器经本地 SSH 转发访问同一章节，实际读取 3 段已发布正文、阅读进度元素存在且未出现未发布提示。验证结束已禁用临时账户、停止 Compose 和 SSH 转发，持久卷保留。
- 边界：进度/历史认证写入继续由 4.84 的 API runtime smoke 自动验证，本轮浏览器新增验证已发布正文页面；未启动 MuMu/阅读 3.0，不使用真实凭据。真实生产 HTTPS、PWA 安装/独立窗口、跨设备同步、长期阅读和阅读 3.0 真机验收仍列为待定，不标记整体 1.0 `Accepted/Completed`。

### 4.87 Kanunu8 真实来源 Scheduler/Worker 内容链自动验收（本轮，2026-08-30）

- 为关闭“真实来源只测适配器、未测任务编排”的缺口，新增 opt-in 的 `Live_Scheduler_And_Worker_Complete_Current_Source_Content_Chain`：真实 Kanunu8 目录经过 `UpdateScanService` 入队、`TocSyncTaskHandler` 同步/映射、`ContentFetchChainService` 联动入队，再由 `CrawlerTaskProcessor` 和 `ContentFetchTaskHandler` 消费；正文实际写入 `FetchArtifact` 内存仓储、经 `IChainedContentPublisher` 发布为 `ContentVersion`，最后从公共查询读取。
- 该测试同时验证周期重扫的 TOC 任务去重、在途 Content 任务阻止重复入队、来源目录章节 ID 去重以及真实正文发布后的可读性；新增 `scripts/kanunu-live-acceptance.sh` 作为强制开启 `INKFLOW_LIVE_TESTS=1` 的可重复入口，常规 CI 仅做脚本语法回归，不依赖第三方站点波动。
- 本机：`dotnet build InkFlow.sln -c Release --no-restore` 0 警告/0 错误；Unit 482/482、Architecture 1/1、Contract 10/10；受影响 IntegrationTests 默认安全跳过真实网络项；脚本 `bash -n` 通过。
- Ubuntu VM：候选 `d819935` 在一次性 .NET 10 SDK 容器中执行 `kanunu-live-acceptance.sh`，5/5 通过（完整内存编排 2 项 + Kanunu8 BookInfo/TOC/Content 3 项）；只读复制源码，不创建账号、不写 InkFlow 业务数据库、不使用 MuMu/阅读 3.0。
- 边界：本轮已自动化当前真实来源快照的 Scheduler/Worker 应用链和重复扫描幂等性；由于外部站点在测试窗口没有可控新章事件，真实“上游新增章节 → 下次周期扫描发现”的时序仍保留为待定，不能据此宣称真实追更或真实第二来源故障切换完成。

### 4.88 采集工作台与书籍打包 1.0 运行复验（本轮，2026-08-31）

- 本轮修复了两项验收发现：CollectionRun API 阶段值统一返回契约要求的 `bookInfo`；迁移回归断言同步新增的 `crawler.runs` 表。新增回归提交为 `7365163`。
- 共享包目录的首次启动权限问题已修复：`docker-compose.yml` 与 `docker-compose.build.yml` 均由一次性 `packages-init` 以 root 初始化目录并交给 `app`，API/Worker 仅在初始化成功后启动；包文件仍通过临时文件校验后原子发布。
- 本轮并发审计补齐活跃运行复用的竞态：`crawler.runs` 新增 `(SourceId, ExternalBookId)` 活跃状态部分唯一索引，EF 仓储通过 `TryAddAsync` 将唯一键竞争折叠为一次成功插入，服务端竞争失败后读取并复用已存在运行；新增跨连接 PostgreSQL 回归 `Concurrent_Active_Collection_Runs_Allow_Only_One_Insert`。
- 本机：`dotnet build InkFlow.sln -c Release --no-restore` PASS；Unit 494/494 PASS；`git diff --check` PASS。Windows Docker Engine 不可用，因此本机 Testcontainers 不作为集成证据。
- Ubuntu VM：使用 `docker-compose.build.yml` 源码构建 API/Worker/Scheduler/Migrations，健康检查和迁移通过；Linux SDK 容器中的 Crawler PostgreSQL 集成测试 12/12 通过（含新增并发回归）；`collection-package-runtime-smoke: PASS (direct URL, durable controls, ZIP/EPUB/TXT packages, integrity, audit)`。验证覆盖 URL 安全边界、暂停/恢复/停止/取消幂等、前置阶段不伪造百分比、三种包的生成/下载/哈希长度完整性及审计；临时数据按 fixture 清理/禁用，Compose 停止后持久卷保留。
- 远端：提交 `c6c4d25` 的 [CI 33328060988](https://github.com/nekohands/InkFlow/actions/runs/33328060988)、[Docker 33328060997](https://github.com/nekohands/InkFlow/actions/runs/33328060997)、[Security 33328060984](https://github.com/nekohands/InkFlow/actions/runs/33328060984) 均 GREEN，且三者 head SHA 一致。浏览器受保护运维页登录后的本轮输入验收尚未执行；阅读 3.0/MuMu 真机仍按用户决定列入人工待定事项，因此整体保持 `1.0 Release Candidate`，不标记 `Accepted/Completed`。

### 4.89 书籍包一致性快照与租约崩溃恢复加固（本轮，2026-08-31）

- 修复书籍包构建的一致性窗口：`BookPackageService` 现在对整本书一次读取所有当前 `ContentVersion`，再按章节建立固定快照，避免构建过程中章节版本切换造成混合版本包；新增服务级回归覆盖单次整书读取和版本/正文一致性。
- 修复包任务崩溃恢复的重试预算：过期 `Running` 租约重新领取时消耗一次 `AttemptCount`；达到 `MaxAttempts` 的过期任务转为 `Failed`，不再无限重复领取；EF 仓储在同一事务中使用行锁、跳过竞争行并处理耗尽预算的任务。新增 Unit 与 PostgreSQL 集成回归。
- 本机：Release Build 0 warnings / 0 errors；Unit 497/497、Architecture 1/1、Contract 10/10 PASS；EF migrations pending-model-changes 检查 PASS；`git diff --check` PASS。Windows Docker Engine 仍不可用，本机 Testcontainers 不作为集成证据。
- Ubuntu VM：`docker-compose.build.yml` 源码构建和健康启动通过；`BookPackageJobRepositoryTests` 2/2 通过；整轮 `collection-package-runtime-smoke: PASS (direct URL, durable controls, ZIP/EPUB/TXT packages, integrity, audit)`，结束后临时账号已禁用、Compose 已停止、持久卷保留。
- 远端：候选 `ecd8533` 的 [CI 33329741035](https://github.com/nekohands/InkFlow/actions/runs/33329741035)、[Docker 33329741037](https://github.com/nekohands/InkFlow/actions/runs/33329741037)、[Security 33329741041](https://github.com/nekohands/InkFlow/actions/runs/33329741041) 均 GREEN，三者 head SHA 一致。受保护运维页登录后的浏览器输入验收仍需用户在输入临时凭据前明确确认；阅读 3.0/MuMu 真机按用户决定保留人工待定，整体仍为 `1.0 Release Candidate`。

### 4.90 多书籍增量目录扫描冲突隔离（本轮，2026-08-31）

- 缺陷：`UpdateScanService` 原按 `source + capability` 判断活动 TOC 任务；同一来源下任意一本书有任务时，会错误跳过该来源的其他书，导致多书籍增量扫描漏入队。
- TDD 修复：先新增 `Active_Toc_Task_For_One_Book_Does_Not_Block_Other_Books_From_Same_Source` 回归测试并确认旧实现失败，再改用 `HasConflictingTaskAsync(sourceId, Toc, bookId, externalBookId)`，将冲突范围收敛到具体来源书籍；测试转绿，未改变任务变量和公共 API 契约。
- 本机证据：Release Build 0 warnings / 0 errors；Unit 498/498、Architecture 1/1、Contract 10/10 PASS；`git diff --check` PASS。Windows Docker Engine 不可用，Testcontainers 集成不作为本机通过证据。
- Ubuntu VM 证据：候选 `98e3725` 已同步；Linux SDK 容器中的受影响 Unit 2/2 通过；`docker-compose.build.yml` 源码构建、健康启动和迁移通过；前端契约、管理员/运营审计权限、采集控制及 ZIP/EPUB/TXT 打包完整性烟测均 PASS。临时账号已禁用，Compose 已停止，持久卷保留。
- 远端门禁：文档同步候选 `b791c69` 的 [CI 33333159334](https://github.com/nekohands/InkFlow/actions/runs/33333159334)、[Docker 33333159291](https://github.com/nekohands/InkFlow/actions/runs/33333159291)、[Security 33333159324](https://github.com/nekohands/InkFlow/actions/runs/33333159324) 均 GREEN，三者 head SHA 均为 `b791c6940d4a3418c7e43dce8ece6f2714339d09`。Security 仅有既有 CodeQL 权限/Action 运行时提示，不影响门禁结论；阅读 3.0/MuMu、真实可控新增章节、真实第二来源故障切换、真实账户/生产凭据及其他第 6 节事项继续待定，本轮不标记整体 `Accepted/Completed`。

### 4.91 Scheduler TOC 任务去重原子化（本轮，2026-08-31）

- 缺陷：`UpdateScanService` 原先先查询再插入 TOC 任务；多个 Scheduler 实例并发扫描同一来源书籍时，可能同时观察到“无冲突”并重复入队，造成重复抓取。
- TDD 修复：先加入 `Update_Scan_Uses_Atomic_Task_Dedupe_Gate` 单元回归并确认旧实现失败，再接入 `TryAddIfNoConflictingTaskAsync`；新增跨连接 PostgreSQL 回归 `Concurrent_Toc_Dedupe_Gate_Allows_Only_One_Task_Insert`，验证并发调用只有一个任务插入成功。
- 实现：EF 仓储在同一 PostgreSQL 事务内按 `(source, capability, variable, value)` 获取稳定 advisory lock，再执行既有活动任务/死信冲突判断、任务插入和 `TaskCreated` Outbox 写入；冲突范围保持在具体来源书籍，旧测试替身仍可通过接口默认回退实现。
- 本机证据：Release Build 0 warnings / 0 errors；Unit 499/499、Architecture 1/1、Contract 10/10 PASS；`git diff --check` PASS。Windows Docker Engine 不可用，受影响的 Testcontainers 集成测试在本机为 BLOCKED，不作为本机通过证据。
- Ubuntu VM 证据：候选代码 `8cb2211` 的 Linux SDK 容器定向 `CrawlerTaskRepositoryTests` 13/13 通过；完整测试为 Unit 499/499、Architecture 1/1、Contract 10/10、Integration 92 passed / 2 skipped / 0 failed；`docker-compose.build.yml` 源码构建、Migration、API/Worker/Scheduler/Redis/PostgreSQL 健康启动通过。前端、正文、账号、私有书库、开发者 API、管理运维、采集/打包、SLO 和备份恢复 Runtime smoke 全部 PASS；临时账号已禁用，Compose 已停止，持久卷保留。
- 远端门禁：代码候选 `8cb2211` 的 [CI 33334393155](https://github.com/nekohands/InkFlow/actions/runs/33334393155)、[Docker 33334393053](https://github.com/nekohands/InkFlow/actions/runs/33334393053)、[Security 33334393020](https://github.com/nekohands/InkFlow/actions/runs/33334393020) 均 GREEN，三者 head SHA 一致。
- 当前状态：自动化 Release Gate 已通过，但整体仍为 `1.0 Release Candidate`，不标记 `Accepted/Completed`。真实新增章节追更事件、真实第二来源故障切换、真实账户/Provider/生产通知、受保护 Operations 页面登录后的浏览器输入验收和 MuMu/阅读 3.0 真机验收继续按第 6 节待定事项执行。

### 4.92 正文联动 Content 任务去重原子化（本轮，2026-08-31）

- 缺陷：`ContentFetchChainService` 原先先查询再插入 Content 任务；同一本书的 TOC 同步并发推进章节正文时，多个 Worker 可能同时观察到“无冲突”并重复入队，造成重复抓取。
- TDD 修复：先加入 `Uses_Atomic_Dedupe_Gate_For_Content_Tasks` 单元回归并确认旧实现失败，再改用 `TryAddIfNoConflictingTaskAsync`；新增跨连接 PostgreSQL 回归 `Concurrent_Content_Dedupe_Gate_Allows_Only_One_Task_Insert`，验证并发调用只有一个任务插入成功。
- 实现：复用既有 `(source, capability, variable, value)` advisory lock、活动任务/死信冲突判断、任务插入与 `TaskCreated` Outbox 同事务边界；`ignoreDeadLettered` 保持 CollectionRun 场景的既有语义，Content 链路只在原子插入成功时递增入队计数。
- 本机证据：Release Build 0 warnings / 0 errors；Unit 500/500 PASS；`git diff --check` PASS。Windows Docker Engine 不可用，受影响的 Testcontainers 集成测试在本机为 BLOCKED，不作为本机通过证据。
- Ubuntu VM 证据：候选 `6b4b256` 已同步；Linux SDK 容器中的 Crawler PostgreSQL 集成测试 14/14 通过；完整测试为 Architecture 1/1、Integration 93 passed / 2 skipped / 0 failed、Contract 10/10、Unit 500/500；`docker-compose.build.yml` 源码构建、迁移、API/Worker/Scheduler/Redis/PostgreSQL 健康启动通过；`reader-content-runtime-smoke` 与 `reader-frontend-runtime-smoke` 均 PASS。验证结束后 Compose 已停止，持久卷保留。
- 远端门禁：代码候选 `6b4b256` 的 [CI 33336335560](https://github.com/nekohands/InkFlow/actions/runs/33336335560)、[Docker 33336335553](https://github.com/nekohands/InkFlow/actions/runs/33336335553)、[Security 33336335552](https://github.com/nekohands/InkFlow/actions/runs/33336335552) 均 GREEN，三者 head SHA 一致。
- 当前状态：自动化 Release Gate 已通过，但整体仍为 `1.0 Release Candidate`，不标记 `Accepted/Completed`。真实新增章节追更事件、真实第二来源故障切换、真实账户/Provider/生产通知、受保护 Operations 页面登录后的浏览器输入验收和 MuMu/阅读 3.0 真机验收继续按待定事项执行。

### 4.93 采集运行 Reconcile 与控制状态原子化（本轮，2026-08-31）

- 缺陷：`CollectionRunService.ReconcileAsync` 原先先读取运行和子任务进度再保存；暂停/停止/取消命令若在读取后提交，陈旧快照可能把已持久化的控制状态覆写回 `Running`，违反控制状态以数据库为准的约束。
- TDD 修复：先加入 `Reconcile_Does_Not_Overwrite_Control_State_Changed_After_Read` 回归并确认旧实现会把 `Paused` 写回 `Running`，再增加 `ICollectionRunRepository.ReconcileAsync` 原子 seam；新增跨连接 PostgreSQL 回归 `Concurrent_Reconcile_Does_Not_Overwrite_Control_State`。
- 实现：EF 仓储在同一 PostgreSQL 事务内对运行行执行 `FOR UPDATE`，随后读取子任务状态、调用领域 `Reconcile` 并保存；控制命令与进度折叠按同一行锁串行化。默认接口实现保留内存仓储和旧替身兼容，不改变状态机或公共 API。
- 本机证据：红色回归已复现旧缺陷；修复后 Release Build 0 warnings / 0 errors、Unit 501/501 PASS、`git diff --check` PASS。Windows Docker Engine 不可用，新增 Testcontainers 集成回归在本机为 BLOCKED，不作为本机通过证据。
- Ubuntu VM 证据：候选 `0133775` 已同步；Crawler PostgreSQL 集成测试 15/15 通过；完整测试为 Architecture 1/1、Integration 94 passed / 2 skipped / 0 failed、Contract 10/10、Unit 501/501；`docker-compose.build.yml` 源码构建、迁移和 API/Worker/Scheduler/PostgreSQL/Redis 健康检查通过；`collection-package-runtime-smoke`、`reader-content-runtime-smoke` 与 `reader-frontend-runtime-smoke` 均 PASS。临时验收账号已禁用，Compose 已停止，持久卷保留。
- 远端门禁：代码候选 `0133775` 的 [CI 33337767070](https://github.com/nekohands/InkFlow/actions/runs/33337767070)、[Docker 33337767065](https://github.com/nekohands/InkFlow/actions/runs/33337767065)、[Security 33337767076](https://github.com/nekohands/InkFlow/actions/runs/33337767076) 均 GREEN，三者 head SHA 一致。
- 当前状态：自动化 Release Gate 已通过，但整体仍为 `1.0 Release Candidate`，不标记 `Accepted/Completed`。真实新增章节追更事件、真实第二来源故障切换、真实账户/Provider/生产通知、受保护 Operations 页面输入验收和 MuMu/阅读 3.0 真机验收继续按待定事项执行。

### 4.94 采集运行聚合写入口原子化（本轮，2026-08-31）

- 缺陷：`SetCanonicalBookAsync`、`AdvanceStageAsync` 和 `MarkWorkStartedAsync` 原先采用“先读运行、调用领域方法、再保存”的陈旧快照写入路径；控制命令在读取后提交时，后续聚合写入可能把已持久化的暂停/停止/取消状态覆写回旧状态。
- TDD 修复：新增 `Run_Mutation_Does_Not_Overwrite_Control_State_Changed_After_Read`、`Stage_And_Work_Mutations_Preserve_Concurrent_Control_State` 单元回归并先确认旧实现失败；新增跨连接 PostgreSQL 回归 `Concurrent_Run_Mutation_Does_Not_Overwrite_Control_State`。
- 实现：`ICollectionRunRepository` 增加 `MutateAsync` 原子 seam；EF 仓储在同一 PostgreSQL 事务中对 `crawler.runs` 执行 `FOR UPDATE`，重新装载运行聚合、应用领域变更并保存。CanonicalBook、阶段推进、工作启动、控制命令和 Reconcile 现在共享同一行锁边界；默认接口回退保持内存仓储与旧测试替身兼容。
- 本机证据：旧红色回归已复现；Release Build 0 warnings / 0 errors；Unit 503/503、`git diff --check` PASS。Windows Docker Engine 的 `npipe://./pipe/docker_engine` 不可用，因此本机 Testcontainers 回归为 BLOCKED，不作为集成通过证据。
- Ubuntu VM 证据：候选 `f3be335` 已同步；Crawler PostgreSQL 集成测试 16/16 通过；完整测试为 Architecture 1/1、Integration 95 passed / 2 skipped / 0 failed、Contract 10/10、Unit 503/503；源码构建 Compose、Migration、API/Worker/Scheduler/PostgreSQL/Redis 健康检查通过；`collection-package-runtime-smoke` 通过直接 URL、持久控制、ZIP/EPUB/TXT、完整性和审计；临时账号已禁用，Compose 已停止且无残留服务容器。
- 远端门禁：代码候选 `f3be335` 的 [CI 33339150508](https://github.com/nekohands/InkFlow/actions/runs/33339150508)、[Docker 33339150530](https://github.com/nekohands/InkFlow/actions/runs/33339150530)、[Security 33339150520](https://github.com/nekohands/InkFlow/actions/runs/33339150520) 均 GREEN，三者 head SHA 一致。
- 当前状态：本工作包自动化 Release Gate 已通过，整体保持 `1.0 Release Candidate`，不标记 `Accepted/Completed`。真实追更新增事件、真实第二来源故障切换、真实账户/Provider/生产通知、受保护 Operations 页面输入验收和 MuMu/阅读 3.0 真机验收继续按第 6 节待定事项执行。

### 4.95 PWA 安装增强自动化契约覆盖（本轮，2026-08-31）

- 缺口：既有 Manifest/Service Worker smoke 已覆盖 PWA 基础资源，但未明确断言浏览器安装增强入口和 `beforeinstallprompt`/`appinstalled` 处理存在。
- 实现：新增 ReaderHtml 单测和 `reader-frontend-runtime-smoke`/fixture 回归，断言 `reader-install`、`beforeinstallprompt`、`preventDefault`、安装提示/完成后的隐藏逻辑；不改变产品行为。
- 本机证据：Release Build 0 warnings / 0 errors；Unit 504/504；Reader 前端 smoke 回归 PASS；`git diff --check` PASS。
- Ubuntu VM 证据：候选 `b01f32d` 已同步；`docker-compose.build.yml` 源码构建、Migration、API/Worker/Scheduler/PostgreSQL/Redis 健康启动通过；`reader-frontend-runtime-smoke: PASS (Reader/PWA/Operations frontend contracts)`；验证后 Compose 已停止且无残留服务容器。
- 远端门禁：候选 `b01f32d` 的 [CI 33341287060](https://github.com/nekohands/InkFlow/actions/runs/33341287060)、[Docker 33341287043](https://github.com/nekohands/InkFlow/actions/runs/33341287043)、[Security 33341287049](https://github.com/nekohands/InkFlow/actions/runs/33341287049) 均 GREEN，三者 head SHA 一致。
- 当前状态：本工作包自动化 Release Gate 已通过；整体保持 `1.0 Release Candidate`。受保护 Operations 页面登录后输入/交互、真实账户/PWA 安装/跨设备、真实来源/追更/故障切换和 MuMu/阅读 3.0 仍按第 6 节待定事项执行，不标记 `Accepted/Completed`。

### 4.96 BookInfo 采集子任务编排回归覆盖（本轮，2026-08-31）

- 缺口：直接地址采集的 BookInfo handler 已在运行时链路覆盖，但缺少独立单元回归锁定“来源书目导入 → Canonical 匹配 → 同一 RunId 创建 Toc 子任务”的边界。
- 实现：新增 `BookInfoSyncTaskHandler` 单测，覆盖成功编排、缺少 `bookId`、来源导入失败、悬空 Confirmed 匹配和 Stopping 运行不创建后续任务；同时断言平台凭据引用只通过非敏感 execution context 传递，停止态仍保留已导入/已匹配事实。
- 本机证据：Release Build 0 warnings / 0 errors；新增定向测试 5/5、Unit 509/509；`git diff --check` PASS。
- Ubuntu VM 证据：候选 `3ffebf2` 已同步；首次源码 Compose 重建遇到 VM 到 NuGet 的瞬时外部下载超时（AngleSharp、StackExchange.Redis），NuGet 连通性恢复后重试成功，`docker-compose.build.yml` 源码构建、Migration、PostgreSQL、Redis、OTel Collector、API、Worker、Scheduler 健康启动均通过。当前提交栈上的 `reader-frontend-runtime-smoke`、Reader 账号/正文、Core SLO、Developer API、Private Library（含 TXT/EPUB 导入/阅读/导出）和 `collection-package-runtime-smoke` 均 PASS；后者按 CI 同源方式临时准备管理员/操作员及控制运行夹具，覆盖直接地址、持久控制、ZIP/EPUB/TXT、完整性与审计。验证后 Compose 已停止，`ps --all` 无残留服务容器，持久卷保留。
- 远端门禁：候选 `3ffebf2` 的 [CI 33342649568](https://github.com/nekohands/InkFlow/actions/runs/33342649568)、[Docker 33342649537](https://github.com/nekohands/InkFlow/actions/runs/33342649537)、[Security 33342649534](https://github.com/nekohands/InkFlow/actions/runs/33342649534) 均 GREEN，三者 head SHA 一致；Docker 首次 Migrations 推送遇到 `unknown blob`，失败 Job 重跑后通过。
- 当前状态：本工作包自动化回归与远端 Release Gate 已通过；整体保持 `1.0 Release Candidate`，不标记 `Accepted/Completed`。真实追更新增事件、真实第二来源故障切换、受保护 Operations 页面输入、真实账户/Provider/PWA 安装跨设备和 MuMu/阅读 3.0 真机验收继续按第 6 节待定事项执行。

### 4.97 Operations 登录后控制请求与浏览器验收（本轮，2026-08-31）

- 缺口：内置浏览器验收发现 Operations 控制对话框虽然要求填写理由，但前端请求只发送 `reason`，遗漏后端契约要求的 `action`，导致取消/暂停/恢复/停止均会被 API 拒绝。
- TDD 修复：先加入 `Operations_Page_Includes_Run_Control_Action_In_Request_Body` 回归并确认旧实现失败，再让 run-control 请求发送 `{ action, reason }`，来源健康/死信重放请求继续只发送 `{ reason }`。
- 本机证据：`dotnet restore InkFlow.sln`、Release Build 0 warnings / 0 errors、定向回归红→绿、Unit 510/510、Reader 前端脚本回归和 `git diff --check` 均通过。
- Ubuntu VM 证据：候选 `d5e8322` 已同步并以 `docker-compose.build.yml` 完成源码重建、Migration、PostgreSQL、Redis、OTel Collector、API、Worker、Scheduler 健康启动；`reader-frontend-runtime-smoke: PASS`。内置浏览器经临时 SSH 转发使用一次性 Operator 账户验证登录后的 Operations 页面、直接地址创建采集运行、取消、暂停、恢复，以及 EPUB 打包完成 100% 和下载入口；验收产生的运行已取消、临时账户已禁用，Compose 已停止且 `ps --all` 无残留服务容器。
- 远端门禁：候选 `d5e8322` 的 [CI 33344939033](https://github.com/nekohands/InkFlow/actions/runs/33344939033)、[Docker 33344939099](https://github.com/nekohands/InkFlow/actions/runs/33344939099)、[Security 33344939034](https://github.com/nekohands/InkFlow/actions/runs/33344939034) 均 GREEN，三者 head SHA 一致。
- 当前状态：本工作包自动化回归、浏览器验收与远端 Release Gate 已通过；整体保持 `1.0 Release Candidate`，不标记 `Accepted/Completed`。真实来源/追更/切源、真实凭据/生产环境 Operations 操作、PWA 真实安装跨设备和 MuMu/阅读 3.0 真机验收继续按第 6 节待定事项执行。

### 4.98 Legado 四步运行时门禁（本轮，2026-08-31）

- 缺口：此前 Legado 的 Search → BookInfo → TOC → Content 主要由内存契约测试和空查询 Core SLO 覆盖，缺少独立脚本在源码构建 Compose 上逐路由验证公开运行时响应及 `book-source.json` 映射。
- 实现：新增 `scripts/legado-runtime-smoke.sh`，逐项校验书源 Manifest、`GET /api/legado/v1/search`、BookInfo、TOC、Content 的稳定 ID、URL、标题/作者、正文标记和纯文本输出；新增确定性 curl fixture 与脚本回归，并接入 CI 的 Runtime smoke。脚本默认使用空查询，避免门禁触发已注册真实来源的外部网络请求；非空关键字过滤仍由应用/契约/单元测试及既有真实来源证据覆盖。
- 本机证据：新增脚本通过 `bash -n` 和 `git diff --check`；Windows 本机缺少 `jq`，因此脚本功能回归转由 Ubuntu VM 与 CI 执行。
- Ubuntu VM 证据：候选 `df35d5e` 已同步，以 `docker-compose.build.yml` 从源码完成镜像构建、Migration、PostgreSQL、Redis、OTel Collector、API、Worker、Scheduler 健康启动；确定性 Reader 夹具已发布，Legado 四步运行时 smoke 输出 `legado-runtime-smoke: PASS (manifest, Search, BookInfo, TOC, Content)`，脚本回归也通过。验证后 Compose 已停止，`ps --all` 无服务容器残留，持久卷保留。
- 远端门禁：文档候选 `e4a9ea5` 的 [CI 33349225217](https://github.com/nekohands/InkFlow/actions/runs/33349225217)、[Docker 33349225212](https://github.com/nekohands/InkFlow/actions/runs/33349225212)、[Security 33349225202](https://github.com/nekohands/InkFlow/actions/runs/33349225202) 均 GREEN，三者 head SHA 一致；CI 新增的脚本回归和 Legado contract runtime smoke 均通过。
- 验收边界：本轮不执行阅读 3.0 / MuMu 真机、真实凭据、真实来源访问或真实第二来源故障切换；这些继续列在第 6 节待定事项。整体保持 `1.0 Release Candidate`，不标记 `Accepted/Completed`。

### 4.99 双来源故障切换运行时门禁与正文读取重选（本轮，2026-08-31）

- 缺口：健康停用只影响显式的 Content Selection 调用，公共 Web/Legado 正文读取此前直接读取已存 `IsCurrent` 版本；仅通过管理员停用 Source A 不能让真实公开读取自动转到 Source B。
- 修复与决策：`CatalogQueryService.GetChapterContentAsync` 在加载正文前通过 `IContentSelectionService` 基于已持久化候选和能力健康重新选择，再复查 Content Policy 与当前正典身份；不触发第三方网络请求。API 组合根补齐 EF Selection Decision 仓储和服务注册，决策记录见 [ADR 0022](../adr/0022-canonical-content-read-reselection.md)。
- 自动化：新增双来源 failover fixture、`scripts/source-failover-runtime-smoke.sh` 及 curl fixture 回归。脚本在初始、Source A 停用、Source A 恢复三个状态，分别验证 Web Book/Content 与 Legado Search/BookInfo/TOC/Content，断言 A→B→A、稳定 BookId/ChapterId、Manifest 和选择恢复。
- 本机证据：Release Build 0 warnings / 0 errors；Unit 511/511、Architecture 1/1、Contract 10/10 PASS；脚本语法 PASS。Windows 本机缺少 `jq`，新脚本功能回归在本机为 `BLOCKED`，未将其记为通过。
- Ubuntu VM 证据：候选 `80962fb` 以 `docker-compose.build.yml` 源码构建并通过 PostgreSQL、Redis、OTel Collector、API、Worker、Scheduler 健康检查；VM 内 `Restore → Release Build`（0 warnings / 0 errors）及全量测试通过：Unit 511/511、Architecture 1/1、Contract 10/10、Integration 95 passed / 2 skipped / 0 failed。Legado 与 failover fixture 回归均 PASS；真实 API 运行时 `source-failover-runtime-smoke: PASS (stable Web/Legado identities, A→B failover, A recovery)`。临时管理员已清理，Compose 已停止，`ps --all` 无 InkFlow 容器残留，持久卷保留。
- 远端门禁：候选 `80962fb` 的 [CI 33351257794](https://github.com/nekohands/InkFlow/actions/runs/33351257794)、[Docker 33351257775](https://github.com/nekohands/InkFlow/actions/runs/33351257775)、[Security 33351257773](https://github.com/nekohands/InkFlow/actions/runs/33351257773) 均 GREEN，三者 head SHA 一致；新增脚本回归、failover Runtime 和既有 Legado Runtime 均通过。
- 验收边界：本轮关闭的是确定性、无第三方流量的 Web/Legado 运行时切源基线，不等同于真实 Official Source pair 验收；真实来源追更、真实第二来源切换、真实凭据、阅读 3.0/MuMu 和其他人工/生产事项继续列在第 6 节，整体保持 `1.0 Release Candidate`，不标记 `Accepted/Completed`。

### 5.0 Core SLO p95 目标门禁与冷启动隔离（本轮，2026-08-31）

- 缺口：原 Core SLO 合成探针只生成 p95 证据，没有把 `public_api` / `developer_api` 750ms、`legado_api` / `reader` 1000ms 目标作为退出门禁；源码 Compose 刚启动时的首个 JIT、连接池和序列化初始化还会污染短窗口。
- 实现：脚本新增按服务面的 fail-closed p95 目标检查；每面先执行一次不计入统计但必须返回预期状态的预热请求，再对 1–20 个测量样本计算最近秩 p95。fixture 回归覆盖恰好 750ms 的边界通过和 751ms 的超目标失败，避免门禁只“输出数字”而不真正阻断。
- 本机证据：`bash -n scripts/core-slo-runtime-smoke.sh`、脚本回归和 `git diff --check` PASS；Release Build 0 warnings / 0 errors；Unit 511/511、Architecture 1/1、Contract 10/10 PASS。
- Ubuntu VM 证据：`61f739e` 以 `docker-compose.build.yml` 从源码构建并通过 PostgreSQL、Redis、OTel Collector、API、Worker、Scheduler 健康检查；重启 API/Worker/Scheduler 后重新执行真实四面 Core SLO 门禁，输出 `public_api=6.887ms`、`legado_api=8.120ms`、`developer_api=4.848ms`、`reader=5.984ms`，均低于目标；JSON 为 schemaVersion=1、四面各 5 个测量样本、0 个服务端错误。VM 脚本回归 PASS，验证后 Compose 已停止，`ps --all` 无服务容器残留，持久卷保留。
- 远端门禁：候选 `8818390` 的 [CI 33354062102](https://github.com/nekohands/InkFlow/actions/runs/33354062102)、[Docker 33354062087](https://github.com/nekohands/InkFlow/actions/runs/33354062087)、[Security 33354062065](https://github.com/nekohands/InkFlow/actions/runs/33354062065) 均 GREEN 且指向同一 head SHA；CI 的 Core SLO 脚本回归、真实合成探针、telemetry receipt、证据上传和既有前端/运行时门禁均通过，Security 仅保留既有 Node 20 弃用提示。
- 验收边界：本轮只关闭合成运行时的 p95 目标和冷启动隔离，不把短窗口证据扩大解释为生产月度 SLO；生产 OTLP 后端、长窗口聚合、错误预算告警/保留治理、真实来源/切源、真实凭据、Operations 受保护页面和阅读 3.0/MuMu 真机仍按第 6 节待定，整体保持 `1.0 Release Candidate`，不标记 `Accepted/Completed`。

### 5.1 Source Rule 有界串行前置请求链（本轮，2026-08-31）

- 缺口：Source Rule 原有响应派生变量只能服务分页续页，无法表达登录/初始化/令牌交换等有限的同源前置步骤；本轮不把 DSL 扩展为递归、分支或任意脚本执行。
- 实现：`CapabilityRule.PreRequests` 与 `RuleRequestStep` 支持最多 8 个按声明顺序执行的前置请求；每步可通过既有 selector/regex、`Trim`/`Replace` 提取临时响应变量，后续步骤和主请求可使用这些变量。前置请求与主请求共享一次执行的 CredentialReference、Session Cookie、请求数、响应字节、结果大小和超时预算，前置响应正文不进入结果或持久化状态。
- 安全/失败关闭：发布边界限制步骤数、步骤名、请求结构和变量数量；执行前后均校验绝对 URL、userinfo/fragment、SSRF 与同源响应。变量缺失、跨源、传输、解析或任一共享预算失败时，在主请求前整体失败，不返回部分结果；不支持动态 URL、递归、循环、分支或跨任务持久会话。
- 契约/回归：`docs/contracts/source-rule-dsl-v1.schema.json`、版本化 JSON codec、Domain Validator、RuleAdapter 回归同步覆盖有界链、步骤间变量、Cookie 转发、共享请求/字节预算和跨源失败；旧的单请求/分页规则保持兼容。
- 本机证据：`dotnet restore InkFlow.sln` PASS；Release Build 0 warnings / 0 errors；Unit 522/522、Architecture 1/1、Contract 10/10 PASS；Schema JSON 解析、定向 Source Rule 回归和 `git diff --check` PASS。
- Ubuntu VM 证据：候选 `bcf8889` 使用 `docker-compose.build.yml` 从源码构建 API/Worker/Scheduler/Migrations，Migration 退出 0，四服务、PostgreSQL、Redis、OTel Collector 健康。Linux SDK 容器完整 `Restore → Build → Test` 为 Unit 522/522、Architecture 1/1、Contract 10/10、Integration 95 passed / 2 skipped / 0 failed；Reader/Legado、双来源 failover、Private Library、Developer API、Admin、collection/package（含暂停/恢复/停止/取消及 ZIP/EPUB/TXT）smoke、Redis 限流 1/1、Core SLO 四面与 telemetry receipt、PostgreSQL backup/restore 均 PASS。Core SLO p95 为 public 60.375ms、Legado 13.887ms、developer 11.865ms、reader 6.705ms；验证后 Compose 已停止，`ps --all` 无残留服务容器，持久卷保留。源码构建期间曾出现 NuGet 瞬时超时，重试后成功，不构成应用失败。
- 远端证据：候选 `bcf8889` 的 [CI 33357094411](https://github.com/nekohands/InkFlow/actions/runs/33357094411)、[Docker 33357094410](https://github.com/nekohands/InkFlow/actions/runs/33357094410)、[Security 33357094388](https://github.com/nekohands/InkFlow/actions/runs/33357094388) 均 GREEN 且指向同一 head SHA；Restore/Build/Test、Compose/Runtime/SLO、Docker 四镜像、NuGet/Filesystem/SBOM/CodeQL 门禁全部通过。
- 当前状态：本工作包为 `Implemented`，整体继续保持 `1.0 Release Candidate`，不等同 `Accepted/Completed`。真实来源/追更/动态多请求与递归、真实 SecretProvider/生产凭据、Operations 生产账号、PWA 安装/跨设备和阅读 3.0/MuMu 真机验收仍按第 6 节待定事项处理。

### 5.2 书籍包租约栅栏与尝试隔离（本轮，2026-08-31）

- 缺陷：书籍包 Worker 在租约过期并被新 Worker 回收后，旧快照仍可通过通用 `SaveAsync` 覆写进度/完成状态；旧/新 Worker 还共享临时文件和最终文件名，可能互相删除或竞争发布。
- TDD 修复：新增跨连接 PostgreSQL 回归 `Stale_Lease_Cannot_Overwrite_Reclaimed_Job` 与服务层租约丢失回归；`IBookPackageJobRepository.SaveLeasedAsync` 使用租约所有者、`AttemptCount` 和未过期条件做单条原子更新，Running 任务禁止走无栅栏 `SaveAsync`。
- 文件隔离：临时文件按 `jobId + leaseAttempt` 隔离，最终文件按 `jobId-attempt.ext` 发布；租约丢失时旧 Worker 只清理自己的文件，不把新任务标记失败。保留旧路径/文件名读取兼容，不新增 Migration。
- 本机证据：`dotnet restore InkFlow.sln`、Release Build 0 warnings / 0 errors、Unit 523/523 PASS；Integration 项目 Release 编译 0 warnings / 0 errors；`git diff --check` PASS。Windows Docker Engine 不可用，本机 Testcontainers 运行未记为通过。
- Ubuntu VM 证据：候选 `052d34e` 以 `docker-compose.build.yml` 从源码构建；Migration 退出 0，API/Worker/Scheduler/PostgreSQL/Redis/OTel 健康。Linux SDK 容器真实 PostgreSQL `BookPackageJobRepositoryTests` 3/3 通过；`collection-package-runtime-smoke: PASS (direct URL, durable controls, ZIP/EPUB/TXT packages, integrity, audit)`。临时验收账号已禁用，Compose 已停止，`ps --all` 无残留容器，持久卷保留。
- 远端门禁：候选 `052d34e` 的 [CI 33362042359](https://github.com/nekohands/InkFlow/actions/runs/33362042359)、[Docker 33362042406](https://github.com/nekohands/InkFlow/actions/runs/33362042406)、[Security 33362042372](https://github.com/nekohands/InkFlow/actions/runs/33362042372) 均 GREEN 且指向同一 head SHA。
- 当前状态：本工作包只收口书籍包并发租约可靠性，整体保持 `1.0 Release Candidate`，不等同 `Accepted/Completed`。真实来源/追更/切源、真实凭据/Provider/生产运维、PWA 安装跨设备、受保护 Operations 登录后人工验收和 MuMu/阅读 3.0 真机验收继续按第 6 节待定。

### 5.3 直接地址采集启动原子一致性（本轮，2026-08-31）

- 缺陷：直接地址入口原先先提交 `CollectionRun`，再单独插入首个 `BookInfo` 任务；首任务写入失败时可能留下没有子任务的活动运行，后续相同地址会一直复用该空运行。
- TDD 修复：新增“首任务插入失败不留下空运行”和并发启动回归；将启动入口收敛到 `ICollectionRunRepository.TryAddWithInitialTaskAsync`，只把 PostgreSQL 活动运行唯一键冲突解释为并发复用，其他持久化失败继续失败并回滚。
- 实现：EF 仓储在同一 PostgreSQL 事务内写入运行、首个 `BookInfo` 任务和 `crawler.task.created` Outbox；成功提交后才返回新运行，冲突时回读现有活动运行，首任务或 Outbox 写入失败时不产生半成品事实。服务层移除直接地址路径对独立 Task Repository 的两步依赖。
- 本机证据：红色回归已复现旧缺陷；`dotnet restore InkFlow.sln`、Release Build（0 warnings / 0 errors）、Unit 523/523、Architecture 1/1、Contract 10/10 和 Integration 项目 Release 编译通过。完整本机 Integration 99 项中 7 项通过、2 项跳过、90 项因 Windows `npipe://./pipe/docker_engine` 不可用而 BLOCKED，未将其记为本机通过。
- Ubuntu VM 证据：候选 `ef2b8dd` 以 `docker-compose.build.yml` 源码构建并健康启动；真实 PostgreSQL `CrawlerTaskRepositoryTests` 17/17 通过；同一 Linux SDK 容器内完整 Restore → Test 为 Unit 523/523、Architecture 1/1、Contract 10/10、Integration 97 passed / 2 skipped / 0 failed；第二轮 `collection-package-runtime-smoke` 通过直接地址、暂停/恢复/停止/取消、ZIP/EPUB/TXT、哈希/长度完整性和审计。临时账号已禁用，Compose 已停止，`ps --all` 无服务容器残留，持久卷保留。
- 验证说明：VM 曾有一次临时 SDK 容器先 Restore、后以 `--no-restore` 测试的无效尝试，因资产指向已销毁的临时 NuGet 缓存而未进入测试；随后改为同一容器内连续 Restore → Test，取得上述完整通过证据，不构成应用失败。
- 远端门禁：候选 `ef2b8dd` 的 [CI 33367713458](https://github.com/nekohands/InkFlow/actions/runs/33367713458)、[Docker 33367713401](https://github.com/nekohands/InkFlow/actions/runs/33367713401)、[Security 33367713423](https://github.com/nekohands/InkFlow/actions/runs/33367713423) 均 GREEN 且指向同一 head SHA。
- 当前状态：本工作包自动化 Release Gate 已通过，整体保持 `1.0 Release Candidate`，不等同 `Accepted/Completed`。真实来源/追更/第二来源故障切换、真实凭据/Provider/生产运维、PWA 安装跨设备、受保护 Operations 登录后人工验收和 MuMu/阅读 3.0 真机验收继续按第 6 节待定。

### 5.4 采集任务租约与运行控制并发栅栏（本轮，2026-08-31）

- 缺陷：`TryLeaseAsync` 的候选查询先按父 `CollectionRun` 状态筛选任务；如果暂停/停止/取消事务在筛选后提交，Worker 可能按旧快照继续领取并提交任务状态，控制状态与任务租约出现竞态。
- 回归与实现：新增跨连接 PostgreSQL 回归 `Lease_Rechecks_Parent_Run_After_Control_Transaction_Commits`，固定按任务 ID 验证控制事务提交前的锁等待以及提交后的暂停排除；领取任务锁定后，对有 `RunId` 的父运行执行 `FOR UPDATE` 并重新读取状态，仅 `Pending/Running` 允许继续租约，其他状态原子返回空结果。无父运行的历史任务路径保持兼容。
- 本机证据：Release 构建 0 warnings / 0 errors、Unit 523/523 PASS、Integration 项目 Release 编译 0 warnings / 0 errors、`git diff --check` PASS；Windows Docker Engine 不可用，本机 Testcontainers 集成运行仍为 BLOCKED。
- Ubuntu VM 证据：候选 `da04e8e` 在 Linux SDK 容器内定向回归 1/1 通过；同一容器完整 `Restore → Build → Test` 为 Unit 523/523、Architecture 1/1、Contract 10/10、Integration 98 passed / 2 skipped / 0 failed。当前提交重新以 `docker-compose.build.yml` 源码构建四个业务镜像，Migration 退出 0，API/Worker/Scheduler/PostgreSQL/Redis/OTel 健康；三个服务 `/health` 均返回 200。
- 远端门禁：候选 `da04e8e` 的 [CI 33372702168](https://github.com/nekohands/InkFlow/actions/runs/33372702168)、[Docker 33372702149](https://github.com/nekohands/InkFlow/actions/runs/33372702149)、[Security 33372702139](https://github.com/nekohands/InkFlow/actions/runs/33372702139) 均 GREEN 且指向同一 head SHA。
- 当前状态：本工作包自动化 Release Gate 已通过，整体保持 `1.0 Release Candidate`，不等同 `Accepted/Completed`。真实来源/追更/第二来源故障切换、真实凭据/Provider/生产运维、PWA 安装跨设备、受保护 Operations 登录后人工验收和 MuMu/阅读 3.0 真机验收继续按第 6 节待定；本轮不启动 MuMu/阅读 3.0 测试。

### 5.5 采集子任务入队与执行启动事务门禁（本轮，2026-08-31）

- 缺陷：内容抓取链和 BookInfo 处理器先读取父 `CollectionRun` 状态、再分别做去重/插入子任务；任务处理器也先读取状态、标记工作开始并保存任务，随后才调用执行器。暂停、停止或取消事务可能在这些步骤之间提交，导致已受控运行仍追加任务或调用外部执行器。
- TDD 与实现：新增 `TryAddIfNoConflictingTaskForCollectionRunAsync` 与 `TryMarkRunningAsync` 仓储 seam；生产 EF 实现在同一 PostgreSQL 事务内锁定父运行 `FOR UPDATE` 并重新读取状态，将带 `RunId` 的去重、子任务和 Outbox 写入置于运行门禁之后。执行启动按任务→父运行顺序加锁，只允许 `Pending/Running/Paused` 入队、允许 `Pending/Running/Paused/Stopping` 启动；父运行终态/缺失时原子取消任务且不调用执行器，`Pending` 父运行与任务启动在同一事务内推进为 `Running`。不新增 Migration，不改变无 `RunId` 历史任务兼容路径。
- 回归：新增内容链、BookInfo 子任务入队和执行启动的红→绿回归；跨连接 PostgreSQL 回归覆盖 `Collection_Run_Enqueue_Rechecks_Parent_Run_After_Control_Transaction_Commits`、`Task_Start_Rechecks_Parent_Run_After_Control_Transaction_Commits` 及 Pending 父运行正向启动。二次审计又补充 `Rejected_Atomic_Start_Does_Not_Advance_Pending_Collection_Run`，确保启动门禁拒绝时父运行仍为 Pending。VM 定向控制竞态 3/3、任务启动 2/2 通过；Unit 中 ContentFetchChain 10/10、BookInfo 5/5、Processor 5/5 通过。
- Ubuntu VM 证据：同一 Linux SDK 容器完整 `Restore → Build → Test` 为 Release Build 0 warnings / 0 errors、Architecture 1/1、Contract 10/10、Unit 526/526、Integration 101 passed / 2 skipped / 0 failed。当前 `835ccd5` 以 `docker-compose.build.yml` 源码构建四个业务镜像；Migration 与 packages-init 正常退出，API/Worker/Scheduler/PostgreSQL/Redis 健康，三个服务 `/health` 均返回 200。按本轮要求已执行 `docker compose down --remove-orphans`，服务容器和网络已清理，持久卷保留。
- 远端门禁：代码候选 `835ccd5` 的 [CI 33380404527](https://github.com/nekohands/InkFlow/actions/runs/33380404527)、[Docker 33380404455](https://github.com/nekohands/InkFlow/actions/runs/33380404455)、[Security 33380404474](https://github.com/nekohands/InkFlow/actions/runs/33380404474) 均 GREEN 且指向同一 head SHA。
- 当前状态：本工作包自动化 Release Gate 已通过，整体保持 `1.0 Release Candidate`，不等同 `Accepted/Completed`。真实 Official Source/追更/第二来源故障切换、真实凭据/Provider/生产运维、PWA 安装跨设备、受保护 Operations 登录后人工验收和 MuMu/阅读 3.0 真机验收继续按第 6 节待定；本轮不启动 MuMu/阅读 3.0 测试。

### 5.6 Rule 主请求最终响应同源门禁（本轮，2026-08-31）

- 缺陷：无 `Session` 的 Rule 主请求此前没有核对 Safe HTTP 返回的最终 `ResponseUri`；当 Handler 安全跟随重定向到其他 origin 后，成功正文可能进入 Rule 提取结果，未满足既有同源响应契约。
- TDD 与实现：先新增 `Main_Request_Rejects_Cross_Origin_Final_Response` 红测，再让 `RuleAdapter` 在主请求成功响应进入 `ResponseBodies`/字段提取前统一校验最终 URI；前置请求也复用同一带上下文的校验。绝对 URI、userinfo、fragment 和 source origin 不满足约束时 fail-closed，不返回部分结果。
- 安全边界：本修复阻止跨源最终响应被 Rule 结果消费，不把它表述为阻止网络层发起安全重定向；连接级 `SsrfSafeHttpMessageHandler` 仍负责每跳解析、地址和端口约束，现有最多 5 跳自动重定向边界不变。
- 本机证据：`dotnet restore InkFlow.sln` PASS；Release Build 0 warnings / 0 errors；RuleAdapter 52/52、Unit 527/527、Architecture 1/1、Contract 10/10 PASS；`git diff --check` PASS。
- Ubuntu VM 证据：候选 `c0ad1dc` 在 Linux SDK 容器完成 `Restore → Build → Test`，Release Build 0 warnings / 0 errors、Unit 527/527、Architecture 1/1、Contract 10/10、Integration 101 passed / 2 skipped / 0 failed；`verify-migrations.sh` 通过（11 contexts）。随后以 `docker-compose.build.yml` 源码构建并启动 Compose，Migration/packages-init 正常退出，API/Worker/Scheduler/PostgreSQL/Redis/OTel 健康，三个服务 `/health` 均返回 200；验证后已执行 `docker compose down --remove-orphans`，服务容器和网络清理，持久卷保留。
- 远端门禁：候选 `c0ad1dc` 的 [CI 33382784197](https://github.com/nekohands/InkFlow/actions/runs/33382784197)、[Docker 33382783508](https://github.com/nekohands/InkFlow/actions/runs/33382783508)、[Security 33382783564](https://github.com/nekohands/InkFlow/actions/runs/33382783564) 均 GREEN 且指向同一 head SHA。
- 当前状态：本工作包自动化 Release Gate 已通过，整体保持 `1.0 Release Candidate`，不等同 `Accepted/Completed`。真实 Official Source/追更/第二来源故障切换、真实凭据/Provider/生产运维、PWA 安装跨设备、受保护 Operations 登录后人工验收和 MuMu/阅读 3.0 真机验收继续按第 6 节待定；本轮不启动 MuMu/阅读 3.0 测试。

### 5.7 执行失败信息稳定化与敏感细节边界（本轮，2026-08-31）

- 缺陷：安全复审发现部分 Rule、Crawler、Content、Health、Book Package、Collection Run 和宿主执行失败路径仍可能把 `Exception.Message` 作为 API 结果、持久化失败原因或控制台文本传播；这会造成低基数契约不稳定，并可能暴露上游、正则或基础设施细节。
- TDD 与实现：先补充 Rule transport/invalid-regex、Crawler executor、Content publisher、Health probe 和 Book Package builder 的细节不泄漏回归，再统一使用稳定的低基数失败文本；`CollectionRun` 控制结果以及 Worker/Scheduler/SourceSeed 的宿主文本同步收敛。该工作包不改变重试、死信、状态机、审计时序、公开 API 形状或 Migration。
- 安全边界：上述执行结果、持久化失败原因和宿主控制台路径不再直接拼接原始 `Exception.Message`；异常对象仍可由既有结构化日志设施按其访问控制记录，诊断不通过业务错误文本回显给调用方。
- 回归与本机证据：定向安全回归 6/6 PASS；`dotnet restore InkFlow.sln`、Release Build（0 warnings / 0 errors）、Unit 530/530、Architecture 1/1、Contract 10/10 和 `git diff --check` 均 PASS。Windows Docker Engine 不可用，未将本机 Testcontainers 结果计为通过。
- Ubuntu VM 证据：候选 `e167a1f` 在 Linux SDK 容器完成完整 `Restore → Build → Test`，Release Build 0 warnings / 0 errors、Unit 530/530、Architecture 1/1、Contract 10/10、Integration 101 passed / 2 skipped / 0 failed；`verify-migrations.sh` 通过 11 个 contexts。随后以 `docker-compose.build.yml` 低并发源码构建四个业务镜像并健康启动，Migration/packages-init 正常退出，API/Worker/Scheduler/PostgreSQL/Redis/OTel 健康，三个服务 `/health` 均返回 healthy；验证后已执行 `docker compose down --remove-orphans`，服务容器和网络清理，持久卷保留。
- 远端门禁：代码候选 `e167a1f` 已推送；最终文档候选的 CI、Docker、Security 必须重新查询并以同一最终 head SHA 为准，不以旧提交门禁替代。
- 当前状态：本工作包自动化 Release Gate 已通过，整体保持 `1.0 Release Candidate`，不等同 `Accepted/Completed`。真实 Official Source/追更/第二来源故障切换、真实凭据/Provider/生产运维、PWA 安装跨设备、受保护 Operations 登录后人工验收和 MuMu/阅读 3.0 真机验收继续按第 6 节待定；本轮不启动 MuMu/阅读 3.0 测试。

### 5.8 Quality failure drill 运行时门禁（本轮，2026-08-31）

- 工作包：关闭 Phase 1B 明确列出的质量失败演练缺口；模拟同一来源先返回完整高质量章节、随后重放故意截断的低质量正文，确认低质量候选不会静默替换已选版本。
- 实现：`InkFlow.AcceptanceFixtures` 新增 `ensure-quality-failure-catalog`，通过真实 `ContentPublishingService → QualityEngine → ContentSelectionService` 持久化两个不可变 `ContentVersion`，输出分数、版本 ID 和选择证据；新增 `scripts/quality-failure-runtime-smoke.sh`，从 Web API、Legado API 和 Reader HTML 三个公共出口核验高质量标记仍可见、低质量标记不可见；脚本回归已接入 CI。
- 回归：本机脚本语法、`git diff --check`、Release Build 0 warnings / 0 errors、Unit 530/530、Architecture 1/1、Contract 10/10 PASS；Windows 本机缺少 `jq`，脚本功能回归改由 VM/CI 执行。
- Ubuntu VM：当前源码挂载的 acceptance fixture 实际输出高质量分 `100`、低质量重放分 `30`，`selectedVersionId == goodVersionId`；源码 Compose 运行态 API/Worker/Scheduler 健康，质量门禁在 Web/Legado/Reader 三出口均输出 PASS，脚本回归 PASS。为避免 NuGet 网络超时，本轮复用此前已验证的源码构建业务镜像；新增 fixture 代码在容器内从当前源码编译执行。
- 远端门槛：代码候选 `f29256e` 尚未推送；推送后必须重新确认 CI、Docker、Security 三者均指向最终 head SHA 并通过。
- 当前状态：本工作包为 `Implemented`，质量失败自动化门禁已补齐，整体仍为 `1.0 Release Candidate`，不标记 `Accepted/Completed`。真实 Official Source/追更/第二来源故障切换、真实凭据/Provider/生产运维、PWA 安装跨设备、受保护 Operations 登录后操作和 MuMu/阅读 3.0 真机验收继续按第 6 节待定；本轮不启动 MuMu/阅读 3.0 测试。

### 5.9 1.0 前端自动化证据矩阵对齐（本轮，2026-08-31）

- 发现：`phase-1-acceptance.md` 的 Phase 1A UX 清单仍全部使用未勾选状态，未区分已经完成的浏览器/运行时自动化门禁与仍需人工视觉、真实账户、PWA 安装和阅读 3.0 验收的项目，容易造成证据状态误读。
- 修复：在 Phase 1A 验收文档新增独立的 Automated evidence 小节，明确记录 Web Reader 多视口、Reader/PWA shell 与离线、Operations/采集打包、敏感字段排除和契约门禁的自动化范围；原 UX 清单改标为 Human / visual acceptance evidence，保持未完成项不被自动化结果替代。
- 证据入口：`scripts/reader-frontend-runtime-smoke.sh`、`scripts/reader-account-runtime-smoke.sh`、`scripts/collection-package-runtime-smoke.sh` 及 `scripts/tests/` 回归；对应运行和浏览器证据见 4.75、4.82–4.86、4.97–4.99、5.8。
- 当前状态：文档证据已对齐；真实 PWA 安装/跨设备、长时间阅读、真实账户、人工视觉/触控/对比度和阅读 3.0/MuMu 仍保持 `NOT RUN`/待定，整体仍为 `1.0 Release Candidate`，不标记 `Accepted/Completed`。

### 5.10 Scheduler/Worker 新章节确定性追更验收（本轮，2026-08-31）

- 发现：此前 `4.87` 只验证了真实 Kanunu8 当前快照的扫描、Worker 消费、任务去重和正文发布；周期扫描遇到“上游目录新增章节”这一关键增量事件仍只有 opt-in live 测试，没有稳定的默认自动证据。
- 实现：`EndToEndDataFlowTests.Automated_Scheduler_Discovers_New_Chapter_And_Publishes_Content` 使用可控 `ISourceAdapter` 模拟两次周期扫描之间新增第三章，驱动 `UpdateScanService`、真实 `TocSyncTaskHandler`、`ContentFetchChainService`、Canonical 章节映射、`FetchArtifact` 和 `ContentVersion` 发布；最后重复扫描确认已抓取章节不重复创建 Content 任务。
- 本机/VM 证据：Windows 定向 `dotnet test` 1/1 PASS；Ubuntu VM 中从 `origin/dev` 创建的隔离 worktree 通过 .NET 10 SDK 容器执行同一定向测试，1/1 PASS。VM 原工作树已有的质量演练未提交改动未被触碰；临时 worktree 已清理。
- 远端门禁：代码候选 `5875479` 的 [CI 33397704667](https://github.com/nekohands/InkFlow/actions/runs/33397704667)、[Docker 33397704675](https://github.com/nekohands/InkFlow/actions/runs/33397704675)、[Security 33397704619](https://github.com/nekohands/InkFlow/actions/runs/33397704619) 均 GREEN 且指向同一 head SHA；CI 的完整 Test/Compose/Runtime smoke 也通过。
- 边界：确定性自动化已关闭“新增章节链路无默认回归”的工程证据缺口，但不替代真实 Official Source 上游新增/修订事件、真实第二来源故障切换、阅读 3.0/MuMu 和其他人工验收；真实追更仍保持第 6 节待定。

### 5.11 最新 dev 全量 VM 与源码 Compose Release Gate 复验（本轮，2026-08-31）

- 范围：针对最新 `dev` 代码栈（代码候选 `5875479`，当前头 `361fe18` 为文档性修订）在 Ubuntu VM 使用隔离 worktree 和源码构建 Compose 重新执行适用门禁；不启动 MuMu/阅读 3.0，不触发真实来源网络请求。
- Build/Test：Linux .NET 10 SDK 容器完成 `dotnet restore InkFlow.sln`、Release Build（0 warnings / 0 errors）和全量测试；Unit `530/530`、Architecture `1/1`、Contract `10/10`、Integration `104` 项（`102 passed / 2 skipped / 0 failed`）。新增确定性 Scheduler 追更用例在全量运行中通过。
- Runtime：`docker-compose.build.yml` 源码构建四个业务镜像，Migration/packages-init 正常退出，PostgreSQL、Redis、OTel Collector、API、Worker、Scheduler 健康。Reader/PWA 前端、账户/阅读状态、已发布正文、Legado 四步、双来源 A→B→A、Quality failure、Private Library TXT/EPUB、Developer API、Admin、collection/package（直接地址、暂停/恢复/停止/取消、ZIP/EPUB/TXT、完整性和审计）均 PASS。
- Observability/Recovery：Core SLO 四面门禁 PASS，p95 为 public `28.058ms`、Legado `15.181ms`、developer `7.840ms`、reader `7.639ms`；在 1 秒指标导出和 detailed debug 配置下，两个 `inkflow.slo.*` 指标及四个 surface 均通过 Collector receipt；PostgreSQL custom-format backup/restore PASS（archive `108510 bytes`，`audit_events=271`）。
- 过程说明：一次初始运行脚本误先注册了由脚本自身负责注册的测试账户，得到 409；修正编排后相关 smoke 全部通过。首次 OTel 检查早于默认 60 秒导出周期，随后调整为 1 秒导出并复验通过；两次均不构成产品失败。
- 清理与边界：临时账户已禁用，隔离 Compose 服务/网络/卷、fixture SDK 容器和 worktree 已清理；VM 原工作树未覆盖。该轮关闭的是最新代码栈的自动化 VM Release Gate，不替代真实追更、真实第二来源、真实凭据/Provider、生产 OTLP/SLO、PWA 跨设备、人工视觉验收和 MuMu/阅读 3.0，整体仍为 `1.0 Release Candidate`，不标记 `Accepted/Completed`。

### 5.12 配额快照缓存身份与损坏值 fail-closed 加固（本轮，2026-08-31）

- 缺口：Redis 配额快照只是 PostgreSQL 事实数据的读取加速层；此前损坏/不兼容 JSON 可能让查询抛出反序列化异常，缓存污染时也缺少对 `UserId` 与计费周期起点的命中校验。
- 实现：`RedisQuotaSnapshotCache` 将 `JsonException` 视为 cache miss 并回源；`QuotaService` 只有在用户 ID、周期起点、周期结束时间和套餐字段均匹配时才采用快照，否则回源 PostgreSQL 并刷新缓存。未改变数据库 schema、公开 API 或 PostgreSQL 权威性。
- 回归：新增 Developer API Key 生成器安全属性测试 2/2、损坏 Redis 快照单测 1/1；新增 PostgreSQL 持久化回归验证跨用户快照不会被返回 1/1。Ubuntu VM Linux SDK 容器完整 Restore → Release Build（0 warnings / 0 errors）→ Test 为 Unit `533/533`、Architecture `1/1`、Contract `10/10`、Integration `105` 项（`103 passed / 2 skipped / 0 failed`）。
- 运行态与远端：Ubuntu VM 以 `docker-compose.build.yml` 源码构建四个业务镜像并健康启动，受影响的 `developer-api-runtime-smoke` PASS（账户/权益、应用与密钥生命周期、脱敏、Header-only 鉴权、目录配额路径、轮换/撤销）；验证后隔离 Compose 服务/网络、容器和 worktree 已清理，持久卷未删除。代码候选 `a111c9a` 的 [CI 33405514000](https://github.com/nekohands/InkFlow/actions/runs/33405514000)、[Docker 33405514007](https://github.com/nekohands/InkFlow/actions/runs/33405514007)、[Security 33405514040](https://github.com/nekohands/InkFlow/actions/runs/33405514040) 均 GREEN 且指向同一 head SHA。
- 边界：本轮关闭配额缓存 fail-closed 与跨用户隔离的自动化缺口，不替代真实账户、套餐/超额/停用场景、生产 Redis 故障演练和阅读 3.0/MuMu 等第 6 节待定项；整体仍为 `1.0 Release Candidate`，不标记 `Accepted/Completed`。

### 5.13 Developer API 配额超额、账户隔离与停用自动化运行验收（本轮，2026-09-01）

- 缺口：5.12 的运行 smoke 已覆盖 Developer API 生命周期和缓存安全，但尚未在源码构建 Compose 中真正消耗 Free 配额并验证超额响应、跨账户隔离和停用用户拒绝。
- 实现：扩展 `scripts/developer-api-runtime-smoke.sh`，使用四个活跃 API Key 分摊请求，在内容读取成本为 5 的路径上动态消耗当月剩余配额，断言 `429`、`quota_exceeded`、`periodEnd`、`remainingUnits` 和正值 `Retry-After`；新增第二临时账户验证独立新配额仍可读目录，随后通过 `AcceptanceFixtures disable-user` 验证 Bearer 与 Developer API Key 均返回 `401`。新增 `scripts/disable-acceptance-user.sh`，同时支持 CI Compose 和 SDK 容器 fixture runner；未改变生产 API、数据库 schema 或计费事实模型。
- 回归：`bash scripts/tests/developer-api-runtime-smoke.test.sh` PASS，`git diff --check` PASS；首次 SDK smoke 缺少 `jq`、首次迁移检查未准备 EF 设计程序集，均在验证编排中修正后重跑，不构成产品失败。
- Ubuntu VM：从 `origin/dev` 的代码候选 `f7b8e27` 建立隔离 worktree；Linux .NET 10 SDK 容器 `Restore`、Release Build（0 warnings / 0 errors）和全量测试通过：Unit `533/533`、Architecture `1/1`、Contract `10/10`、Integration `105` 项（`103 passed / 2 skipped / 0 failed`）。`docker-compose.build.yml` 源码构建四个业务镜像，Migration/packages-init 正常退出，PostgreSQL/Redis/OTel/API/Worker/Scheduler 健康；扩展 `developer-api-runtime-smoke` PASS；`dotnet tool restore` 后 `verify-migrations.sh` 验证 11 个上下文 PASS。验证后隔离容器/网络/worktree 已清理，持久卷保留，VM 原工作树的用户改动未被覆盖。
- 远端门禁：代码候选 `f7b8e27` 的 [CI 33409960296](https://github.com/nekohands/InkFlow/actions/runs/33409960296)、[Docker 33409960193](https://github.com/nekohands/InkFlow/actions/runs/33409960193)、[Security 33409960204](https://github.com/nekohands/InkFlow/actions/runs/33409960204) 均 GREEN 且指向同一 head SHA。
- 边界：本轮关闭 Developer API 补充场景的自动化证据缺口；真实 Web 账户/真实套餐与生产 Provider、生产 Redis 故障、审计人工核对，以及阅读 3.0/MuMu、真实来源、PWA 跨设备和生产 OTLP/SLO 仍按第 6 节待定，整体保持 `1.0 Release Candidate`，不标记 `Accepted/Completed`。

### 5.14 linovelib RuleAdapter 后端直连复核与上游阻塞记录（本轮，2026-09-01）

- 目标：把 4.77 的 GPT 内置浏览器页面证据与服务端 RuleAdapter 真实链路分开复核，直接验证现行规则 `POST /S6/` + `searchkey={key}` 是否能在 Ubuntu VM 的生产出网链路中得到可解析搜索结果。
- 实际探测：Ubuntu VM 先后读取 `GET /novel/1.html`（HTTP 200，38811 bytes）和 `GET /novel/1/catalog`（HTTP 200，74342 bytes）；随后使用浏览器常见 User-Agent、Accept、Accept-Language、Origin、Referer、Sec-Fetch 和表单 Content-Type 发送 `POST https://www.linovelib.com/S6/`，返回 `HTTP/2 200`、`0` bytes，正文中没有 `/novel/` 结果链接，响应标记为 Cloudflare。
- 结论：搜索 POST 的空响应发生在上游/站点挑战层，不能作为 RuleAdapter 成功，也不能通过注入 Cookie、关闭 TLS/SSRF 校验或其他绕过方式制造通过证据。4.77 的浏览器 Search → BookInfo → TOC → Content 页面链路仍有效，但不等同于服务端 RuleAdapter 直连验收。
- 验证边界：本轮没有修改规则、公共 Contract、数据模型或安全策略；`linovelib RuleAdapter 后端直连链路` 继续保持第 6.2 节未完成，解除条件是取得允许服务端只读访问的稳定网络/站点响应后，再运行 Search → BookInfo → TOC → Content 的真实适配器测试。

### 5.15 linovelib RuleAdapter 可选真实验收 harness 与 VM Release Gate（本轮，2026-09-01）

- 缺口：4.77 只有 GPT 内置浏览器的公开页面证据，5.14 已确认普通 HTTP 搜索请求被上游 Cloudflare 返回空响应；此前缺少一条可复用、默认不触发网络的服务端 RuleAdapter 真实验收入口。
- 实现：新增 `tests/InkFlow.IntegrationTests/LinovelibSourceAdapterLiveTests.cs`，通过生产安全 HTTP 客户端、SSRF 安全处理器和当前 Rule DSL 验证 Search → BookInfo → TOC → Content；新增 `scripts/linovelib-live-acceptance.sh`，只有显式设置 `INKFLOW_LIVE_TESTS=1` 才运行该测试，未设置时返回明确的 `NOT RUN` 门槛。CI 增加 `bash -n` 回归检查，脚本已标记为可执行。
- VM 证据：Ubuntu VM 隔离 worktree 中完成源码构建四业务镜像；Linux .NET 10 SDK 容器完成 Restore、Release Build（0 warnings / 0 errors）和全量测试：Unit `533/533`、Architecture `1/1`、Contract `10/10`、Integration `106` 项（`103 passed / 3 skipped / 0 failed`）。`dotnet tool restore` 后 `verify-migrations.sh` 验证 11 个上下文 PASS；源码 Compose 中 Migration/packages-init 正常退出，PostgreSQL/Redis/OTel/API/Worker/Scheduler 健康，三个 `/health` 均返回 `healthy`。
- 远端门禁：最终文档前的代码提交 `8673bff` 已通过 [CI 33418330334](https://github.com/nekohands/InkFlow/actions/runs/33418330334)、[Docker 33418330294](https://github.com/nekohands/InkFlow/actions/runs/33418330294) 和 [Security 33418330318](https://github.com/nekohands/InkFlow/actions/runs/33418330318)，三者均 GREEN 且指向同一 head SHA；CI 还完成前端、运行态、SLO、Redis/PostgreSQL 和备份恢复门禁。
- 验收边界：按用户决定未设置 `INKFLOW_LIVE_TESTS=1`，真实 linovelib Search → BookInfo → TOC → Content 未运行；脚本门槛、语法和离线回归已验证。代码提交 `2ec2a43` 后仅补充脚本可执行权限为 `b50001c`，未改变测试或产品行为；验证后隔离 Compose、网络、容器和临时 worktree 已清理，VM 原工作树及持久卷保留。
- 结论：新增了可审查的真实适配器验收入口，但不能把它的跳过状态或浏览器页面证据升级为 RuleAdapter 通过；`linovelib RuleAdapter 后端直连链路` 继续按第 6.2 节 BLOCKED，整体保持 `1.0 Release Candidate`，不标记 `Accepted/Completed`。

### 5.16 GPT 内置浏览器 VM Web 入口复核与客户端网络边界（本轮，2026-09-01）

- 目标：继续按“除阅读 App 外尽量自动化”的要求，复核最新源码构建 Compose 栈是否能通过 GPT 内置浏览器完成 Web Reader 页面验收；本轮仍不启动 MuMu/阅读 3.0。
- VM 运行证据：从最新 `origin/dev`（`8652c99`）建立隔离 worktree，源码构建 `docker-compose.build.yml` 的四个业务镜像并启动 Compose；Migration/packages-init 正常退出，PostgreSQL/Redis/OTel/API/Worker/Scheduler 健康。通过 SSH 本地转发访问 API `/health` 返回 HTTP 200 和 `{"status":"healthy","service":"InkFlow.Api"}`；验证后 Compose、网络、容器、转发和临时 worktree 已清理，VM 原工作树未覆盖。
- 浏览器通道结果：GPT 内置浏览器访问公共 HTTPS 页面正常，但访问 `172.19.31.153:8080`、SSH 转发的 `127.0.0.1:18080/3000`、`localhost:18080/3000` 等本地/私网 HTTP 地址均被客户端以 `net::ERR_BLOCKED_BY_CLIENT` 拦截。这是浏览器通道的本地网络限制，不是 Compose `/health` 或应用健康检查失败；未创建公共隧道，也未降低 HTTPS/SSRF 安全边界。
- 远端门禁：文档提交 `f4583c2` 的 [CI 33422098715](https://github.com/nekohands/InkFlow/actions/runs/33422098715)、[Docker 33422098588](https://github.com/nekohands/InkFlow/actions/runs/33422098588) 和 [Security 33422098584](https://github.com/nekohands/InkFlow/actions/runs/33422098584) 均 GREEN 且指向同一 head SHA；CI 完成全量测试、Compose、前端/运行态、SLO、Redis/PostgreSQL 和备份恢复门禁，Security 完成依赖、SBOM、Trivy 与 CodeQL 门禁。
- 结论与边界：本轮没有修改代码、Contract、Migration 或产品行为；既有 4.75/4.85 的 Web Reader 自动化证据仍有效，本轮不能新增页面级浏览器证据。视觉、真实账户/PWA 安装/跨设备、阅读 3.0 和其他人工验收继续按第 6 节待定，整体保持 `1.0 Release Candidate`，不标记 `Accepted/Completed`。

### 5.17 CollectionRun 直接地址采集 HTTP 状态契约与 VM Release Gate（本轮，2026-09-01）

- 缺口：`docs/roadmap/collection-workbench-requirements.md` 的 11.2 已规定直接地址无法解析时返回 `422`，但 `CollectionRunEndpoints.StartAudited` 原先把所有启动失败统一映射为 `400`，调用方无法区分非法输入与来源解析失败。
- TDD 回归：先加入 resolver failure 的 HTTP 状态测试，确认缺少映射 seam 的红态编译失败；随后新增 `CollectionRunEndpoints.GetStartStatusCode` 并接入端点，定向 `CollectionRunEndpointTests` 为 `2/2 PASS`。新建运行返回 `202`，复用活跃运行返回 `200`，来源地址无法解析返回 `422`；非法输入仍返回 `400`。同步更新 `collection-package-runtime-smoke.sh` 的 `javascript:` 地址断言和采集需求文档。
- 本机证据：`dotnet restore InkFlow.sln` PASS；`dotnet build InkFlow.sln -c Release --no-restore` PASS（0 warnings / 0 errors）；Unit `534/534`、Architecture `1/1`、Contract `10/10` PASS；脚本语法、采集/打包脚本回归和 `git diff --check` PASS。完整 Integration 106 项中 8 项通过、3 项跳过、95 项因 Windows 本机 `npipe://./pipe/docker_engine` 不可用而 BLOCKED；该结果不计为本机 Integration 全量通过。
- Ubuntu VM 证据：候选代码 `9bda886` 的隔离 worktree 使用 Linux .NET 10 SDK 完成 Restore、`dotnet tool restore`、Release Build（0 warnings / 0 errors）和全量测试：Unit `534/534`、Architecture `1/1`、Contract `10/10`、Integration `106` 项（`103 passed / 3 skipped / 0 failed`）。`verify-migrations.sh` 的 11 个上下文全部 PASS；源码构建 Compose 的四个业务镜像成功构建，Migration/packages-init 正常退出，PostgreSQL/Redis/OTel/API/Worker/Scheduler 健康，三个服务 `/health` 均为 `healthy`。
- 运行链路：`collection-package-runtime-smoke` 在 VM 源码 Compose 上 PASS，覆盖直接地址采集、暂停/恢复/停止/取消及幂等、ZIP/EPUB/TXT 打包、完整性和审计，并实际复核无法解析地址的 `422`。临时账户、隔离 Compose、网络、容器、临时 NuGet 卷和 worktree 已清理；VM 原工作树仅保留原有用户改动，`.env` 未进入提交。
- 远端门禁：代码候选 `9bda886` 的 [CI 33425459672](https://github.com/nekohands/InkFlow/actions/runs/33425459672)、[Docker 33425459913](https://github.com/nekohands/InkFlow/actions/runs/33425459913) 和 [Security 33425459745](https://github.com/nekohands/InkFlow/actions/runs/33425459745) 均 GREEN 且指向同一 head SHA。
- 结论与边界：本轮关闭了直接地址采集启动状态的自动化语义缺口，整体仍为 `1.0 Release Candidate`，不标记 `Accepted/Completed`。阅读 3.0/MuMu、真实 linovelib/17K/追更/第二来源、真实账户/Provider、PWA 安装跨设备、受保护页面人工操作和生产 OTLP/SLO/告警/备份治理仍按第 6 节待定；本轮不启动真实设备或第三方 live 测试。

### 5.18 CollectionRun 非法输入状态映射修复与最终 VM/Compose 回归（本轮，2026-09-01）

- 缺口：5.17 将启动失败统一映射为 `422`，覆盖了来源解析失败，但也误把空/格式非法 URL 当成 `422`；11.2 要求非法输入 `400`、无法解析的来源地址 `422`。
- TDD 与实现：先加入 `source-url.invalid` failure 应返回 `400` 的红态回归（期望 400、实际 422），再实现 `GetStartStatusCode` 分流；`source-url.empty`/`source-url.invalid` → `400`，resolver semantic failures（如 `javascript:`/未登记来源）→ `422`。定向 `CollectionRunEndpointTests` `3/3 PASS`，采集/打包脚本回归、shell 语法和 `git diff --check` PASS。
- 本机证据：Restore PASS；Release Build 0 warnings / 0 errors；Unit `535/535`、Architecture `1/1`、Contract `10/10` PASS；完整 Integration 106 项为 8 通过、3 跳过、95 项因 Windows `npipe://./pipe/docker_engine` 不可用而 BLOCKED。
- Ubuntu VM 证据：候选 `c85975f` 隔离 worktree 使用 Linux .NET 10 SDK 完成 Restore、`dotnet tool restore`、Release Build 0 warnings / 0 errors；Unit `535/535`、Architecture `1/1`、Contract `10/10`、Integration `103 passed / 3 skipped / 0 failed`；11 个 migration contexts PASS；源码构建 Compose 四业务镜像成功，Migration/packages-init 正常退出，PostgreSQL/Redis/OTel/API/Worker/Scheduler 健康；collection-package-runtime smoke PASS，覆盖 direct URL、空 URL `400`、无法解析地址 `422`、四类控制、ZIP/EPUB/TXT、完整性和审计。
- 远端门禁：最终代码/安全策略候选 `bf4b09f` 的 [CI 33436420368](https://github.com/nekohands/InkFlow/actions/runs/33436420368)、[Docker 33436420254](https://github.com/nekohands/InkFlow/actions/runs/33436420254)、[Security 33436420383](https://github.com/nekohands/InkFlow/actions/runs/33436420383) 均 GREEN 且 head SHA 一致。
- 安全边界：官方 Collector `0.159.0` 的上游镜像扫描命中 `CVE-2026-56854`；当前仅对核心 Collector 镜像使用到期日为 2026-09-30 的 `.trivyignore-collector` VEX 例外，理由是配置管线未启用 SSH receiver/exporter 或 SSH 认证回调；应用镜像和文件系统扫描仍不继承该例外。官方修复镜像可用后必须更新版本、删除例外并重新跑门禁。
- 结论：自动化 collection/package contract gap closed；整体仍为 `1.0 Release Candidate`，不标记 `Accepted/Completed`。阅读 3.0/MuMu、真实来源/追更/第二来源、真实凭据/Provider、PWA/账户跨设备、受保护页面人工操作和生产 OTLP/SLO/告警/备份治理仍按第 6 节待定；本轮未启动 ADB、阅读 App 或 live source。

### 5.19 ReadingProgress 领域状态变更回归覆盖（本轮，2026-09-01）

- 缺口：代码审计发现 `ReadingProgress.Update` 只有应用服务间接覆盖，缺少直接领域回归，无法独立证明换章、段落/百分比、时间戳更新及非法输入的原子不变性。
- 实现：新增两个 Unit 用例，覆盖合法更新替换章节位置与 `UpdatedAt`，以及非法段落/百分比拒绝后保持原章节、位置、百分比和时间戳；不改变 API、数据模型或运行时行为。
- 本机证据：`git diff --check` PASS；定向 `ReadingStateTests` `7/7 PASS`；完整 Unit `537/537 PASS`；Release Build 0 warnings / 0 errors；Architecture `1/1`、Contract `10/10` PASS。
- 远端门禁：测试候选 `3ac8110` 的 [CI 33439541455](https://github.com/nekohands/InkFlow/actions/runs/33439541455)、[Docker 33439541466](https://github.com/nekohands/InkFlow/actions/runs/33439541466)、[Security 33439541469](https://github.com/nekohands/InkFlow/actions/runs/33439541469) 均 GREEN 且 head SHA 一致。
- 结论与边界：本轮关闭阅读状态领域测试盲区，不改变 1.0 功能范围；真实阅读 3.0/MuMu、真实来源/凭据、PWA 安装跨设备及人工视觉验收仍按第 6 节待定，整体保持 `1.0 Release Candidate`，不标记 `Accepted/Completed`。

### 5.20 最新 HEAD Ubuntu VM SDK 复验与 Compose 网络阻塞记录（本轮，2026-09-01）

- 目标：对当前 `dev` 最新 HEAD `5673dfc`（代码候选仍为 `3ac8110`，其后仅有文档提交）再次执行可自动化的 Ubuntu VM 复验；按用户决定不启动 ADB、MuMu/阅读 3.0 或第三方 live source。
- SDK 证据：从 `origin/dev` 建立隔离 worktree，Linux .NET 10 SDK 完成 `dotnet restore`、工具恢复、Release Build（0 warnings / 0 errors）、`verify-migrations.sh` 的 11 个 context 校验；Unit `537/537` PASS，Integration `106` 项为 `103 passed / 3 skipped / 0 failed`。
- Compose 结果：按源码构建默认策略启动 `docker-compose.build.yml` 的 API/Worker/Scheduler/OTel 服务时，Worker/Migrations 镜像构建完成，API 与 Scheduler 的恢复阶段多次遭遇 `api.nuget.org` 包下载 60 秒无数据超时（涉及 OpenTelemetry、Npgsql、EF Core、AngleSharp 等包）。等待约 16 分钟后按环境阻塞中止，未进入健康检查或业务 smoke；这不是代码失败证据，也不把本轮记为 Runtime PASS。
- 清理与隔离：中止后的清理钩子完成；隔离 Compose 容器、临时目录和 worktree 均已移除，VM `/home/nekohands/InkFlow` 原有 5 项用户改动保持不变，未读取或提交敏感 `.env`。
- 远端门禁：当前文档 HEAD `5673dfc` 的 [CI 33440663763](https://github.com/nekohands/InkFlow/actions/runs/33440663763)、[Docker 33440663778](https://github.com/nekohands/InkFlow/actions/runs/33440663778)、[Security 33440663728](https://github.com/nekohands/InkFlow/actions/runs/33440663728) 均 GREEN 且 head SHA 一致。
- 结论与边界：最新 HEAD 的 SDK/测试证据保持通过，Compose 运行态复验因 VM 到 NuGet 的外部网络可达性受阻；此前 5.18/5.19 的源码 Compose 与运行态证据仍按其候选提交记录有效。整体继续保持 `1.0 Release Candidate`，不标记 `Accepted/Completed`；阅读 3.0/MuMu、真实来源/追更/第二来源、真实凭据/PWA 跨设备、人工验收和生产治理继续按第 6 节待定。

### 5.21 源码 Dockerfile NuGet 缓存与 VM Compose 复验（本轮，2026-09-01）

- 缺口：源码 Compose 的四个业务 Dockerfile 将 `dotnet publish` 的 NuGet 恢复放在单次构建层内；网络短暂抖动会让已下载依赖无法跨重试复用，降低 VM 源码构建验证的可靠性。
- 实现：API、Worker、Scheduler、Migrations 四个 Dockerfile 增加 BuildKit `type=cache` 挂载，分别持久化 `/root/.nuget/packages` 与 NuGet HTTP cache，并使用 `sharing=locked` 保护并行恢复；缓存只属于构建器，不进入最终运行镜像，不改变运行时行为或依赖版本。
- VM 证据：候选 `26e5d82` 在 Ubuntu VM 隔离 worktree 按源码构建 Compose；四业务镜像全部成功构建，Migration/packages-init 正常退出，PostgreSQL/Redis/OTel/API/Worker/Scheduler 健康，三个服务 `/health` 均返回 `healthy`。验证后隔离 Compose、网络、容器、临时目录和 worktree 已清理，VM 原工作树改动保持不变。
- 回归与门禁：本机 `git diff --check` PASS；本机未安装 Docker CLI，未把本机 Compose 配置检查误记为通过。候选 `26e5d82` 的 [CI 33447522462](https://github.com/nekohands/InkFlow/actions/runs/33447522462)、[Docker 33447522530](https://github.com/nekohands/InkFlow/actions/runs/33447522530)、[Security 33447522397](https://github.com/nekohands/InkFlow/actions/runs/33447522397) 均 GREEN 且 head SHA 一致；CI 的全量 Test、Compose、前端、业务 Runtime、SLO、Redis、备份恢复和诊断步骤均通过。
- 结论与边界：源码构建的 NuGet cache reliability gap 已关闭，最新 VM Runtime 健康证据已恢复；整体仍为 `1.0 Release Candidate`，不标记 `Accepted/Completed`。阅读 3.0/MuMu、真实来源/追更/第二来源、真实凭据/PWA 跨设备、人工验收及生产治理继续按第 6 节待定。

### 5.22 Acceptance fixture NuGet 缓存与重复运行复验（本轮，2026-09-01）

- 缺口：源码 Compose 的 `acceptance-fixtures` 每次以 SDK 容器执行 `dotnet run`，原先把 NuGet 包和 HTTP cache 放在 `/tmp` 临时文件系统；同一轮多个 fixture 会重复下载，放大 VM/CI 外网抖动。
- 实现：仅在 `docker-compose.build.yml` 的 acceptance profile 增加 `inkflow-acceptance-nuget` 与 `inkflow-acceptance-nuget-http` 两个命名卷，继续使用既有 `NUGET_PACKAGES` / `NUGET_HTTP_CACHE_PATH` 路径；不改变生产服务、最终镜像或数据库事实卷。
- VM 证据：候选 `e7f4414` 的源码 Compose 健康启动后，使用独立非交互 `run -T` 连续执行两次 `ensure-reader-catalog`，两次均退出 `0` 并返回同一 fixture；第二次未重新创建 acceptance NuGet 卷，两个卷的 Compose project/volume label 与预期一致。
- 清理与门禁：已显式删除 acceptance profile 未被 `down --volumes` 自动回收的两个隔离缓存卷，并移除临时 worktree；原 VM 工作树 5 项用户改动保留。`e7f4414` 的 [CI 33449460834](https://github.com/nekohands/InkFlow/actions/runs/33449460834)、[Docker 33449460843](https://github.com/nekohands/InkFlow/actions/runs/33449460843)、[Security 33449460854](https://github.com/nekohands/InkFlow/actions/runs/33449460854) 均 GREEN。
- 结论与边界：Acceptance fixture 的重复 NuGet 下载稳定性缺口已关闭；本轮仍不启动 ADB、阅读 3.0 或第三方 live source，整体保持 `1.0 Release Candidate`，未标记 `Accepted/Completed`。

### 5.23 书籍打包租约丢失后的已发布文件清理回归覆盖（本轮，2026-09-01）

- 缺口：代码审计发现 `BookPackageService.ProcessAsync` 在文件已经发布、最终租约保存被拒绝时会进入 `PackageLeaseLostException` 清理分支，但原有单元测试只覆盖了发布前丢失租约，未证明已发布 artifact 和临时文件都不会残留。
- TDD 与实现：新增 `Process_Removes_Published_Artifact_When_Lease_Is_Lost_Before_Completion`，让测试替身在“设置进度→发布文件→最终保存”这条边界拒绝租约保存；测试验证三次租约保存调用、Builder 已执行、临时路径和最终 EPUB artifact 均被清理。未改变生产代码、API、Migration 或控制语义。
- 本机证据：`dotnet build InkFlow.sln -c Release --no-restore` 通过（0 warnings / 0 errors）；Unit `538/538`、Architecture `1/1`、Contract `10/10` 和 `git diff --check` 通过。
- 远端门禁：候选 `e96bd2f` 的 [CI 33451781181](https://github.com/nekohands/InkFlow/actions/runs/33451781181)、[Docker 33451781556](https://github.com/nekohands/InkFlow/actions/runs/33451781556)、[Security 33451781201](https://github.com/nekohands/InkFlow/actions/runs/33451781201) 均 GREEN 且 head SHA 一致；CI 全量 Test、Compose、前端/业务 Runtime、SLO、Redis、备份恢复和诊断步骤均通过。
- 结论与边界：书籍打包租约丢失后的 artifact 清理回归证据已补齐；本轮不启动 ADB、阅读 3.0、真实来源或真实凭据验收，整体仍为 `1.0 Release Candidate`，不标记 `Accepted/Completed`。

### 5.24 书籍包下载缺失 artifact 根目录的错误映射修复（本轮，2026-09-01）

- 缺口：`BookPackageService.OpenCompletedAsync` 原先只把 `FileNotFoundException` 映射为空结果；在 Linux/Ubuntu 上包根目录或挂载点缺失时，`FileStream` 会抛 `DirectoryNotFoundException`，导致下载端点返回 500，而不是既有契约中的 `package_artifact_not_found`/404。
- TDD 与实现：新增 `OpenCompleted_Returns_Null_When_Artifact_Root_Is_Missing` 回归，先复现目录缺失异常，再在下载服务增加受限异常过滤，将缺失文件和缺失目录统一映射为空结果；其他 I/O、权限和取消异常继续向上抛出，不吞掉未知故障。
- 本机证据：`dotnet restore InkFlow.sln` PASS；`dotnet build InkFlow.sln -c Release --no-restore` PASS（0 warnings / 0 errors）；定向 BookPackageServiceTests `5/5`、Unit `539/539`、Architecture `1/1`、Contract `10/10` 和 `git diff --check` PASS。
- 远端门禁：候选 `5157924` 的 [CI 33454092316](https://github.com/nekohands/InkFlow/actions/runs/33454092316)、[Docker 33454092239](https://github.com/nekohands/InkFlow/actions/runs/33454092239)、[Security 33454092192](https://github.com/nekohands/InkFlow/actions/runs/33454092192) 均 GREEN 且 head SHA 一致；CI 全量 Test、Compose、前端/业务 Runtime、SLO、Redis、备份恢复和诊断步骤均通过。
- 结论与边界：本轮关闭了包下载缺失目录的自动化错误映射缺口；不启动 ADB、阅读 3.0、真实来源或真实凭据验收，整体仍为 `1.0 Release Candidate`，不标记 `Accepted/Completed`。

### 5.25 采集运行成功进度与失败/取消计数语义修复（本轮，2026-09-01）

- 缺口：`CollectionRunView.ProgressPercent` 原先将 `FailedTaskCount`、`CancelledTaskCount` 与 `CompletedTaskCount` 相加，会把失败/取消任务显示为成功进度；例如 1 个成功、2 个失败、1 个取消会显示 `100%`，违反“失败数单独展示、不能伪装成功进度”的需求。
- TDD 与实现：新增 `Progress_Percent_Does_Not_Count_Failed_Or_Cancelled_Tasks_As_Completed`，先以旧实现得到 `25/100` 红态，再改为仅以 `CompletedTaskCount / TotalTaskCount` 计算百分比；失败/取消仍由独立字段和终态表达。
- 本机证据：`dotnet restore InkFlow.sln` PASS；Release Build `0 warnings / 0 errors`；Unit `540/540`、Architecture `1/1`、Contract `10/10`、定向 `CollectionRunEndpointTests 4/4`、`git diff --check` PASS。完整本机 Integration `106` 项中 `8` 通过、`3` 跳过、`95` 项因 Windows `npipe://./pipe/docker_engine` 不可用而 BLOCKED。
- Ubuntu VM 证据：候选 `bc119e5` 使用隔离 worktree 源码构建 Compose；Migration/packages-init 与 PostgreSQL/Redis/OTel/API/Worker/Scheduler 健康；Linux SDK 测试 Unit `540/540`、Architecture `1/1`、Contract `10/10`、Integration `103 passed / 3 skipped / 0 failed`，11 个 migration contexts PASS；Legado/Admin/Operations/collection-package runtime smoke PASS，后者覆盖直接地址、暂停/恢复/停止/取消幂等、ZIP/EPUB/TXT、完整性和审计。验证后隔离资源已清理，VM 原工作树用户改动未触碰。
- 远端门禁：候选 `bc119e5` 的 [CI 33456258013](https://github.com/nekohands/InkFlow/actions/runs/33456258013)、[Docker 33456258124](https://github.com/nekohands/InkFlow/actions/runs/33456258124)、[Security 33456257931](https://github.com/nekohands/InkFlow/actions/runs/33456257931) 均 GREEN 且 head SHA 一致。
- 结论与边界：关闭自动化进度语义缺口；不启动 ADB、阅读 3.0、真实来源或真实凭据验收，整体仍为 `1.0 Release Candidate`，不标记 `Accepted/Completed`。

### 5.26 GPT 内置浏览器 VM 本地转发真实交互复验（本轮，2026-09-01）

- 目标：在不启动 MuMu/阅读 3.0、不给真实账户输入凭据、也不触发第三方 live source 的前提下，补强 1.0 前端自动化证据，验证源码构建 Compose 栈能被 GPT 内置浏览器实际操作。
- VM 证据：以 `bc119e5` 建立隔离 worktree，按日常策略使用 `docker-compose.build.yml` 源码构建；Migration/packages-init 正常退出，PostgreSQL、Redis、OTel Collector、API、Worker、Scheduler 均健康。通过 SSH 本地端口转发将 VM API 映射到本机临时地址，未修改 VM 原工作树。
- 浏览器交互：实际打开 `/reader` 并通过搜索框/按钮完成空结果搜索；打开 `/reader/account` 验证登录/注册表单和空输入原生校验；打开未登录 `/reader/shelf`、`/reader/history` 验证保护提示和登录入口；打开 `/reader/offline` 验证离线回退文案；打开 `/admin/operations` 验证匿名提示和禁用刷新态。未提交账号、密码或其他敏感数据。
- 响应式证据：在 `375×812` 和 `1440×900` 视口读取 DOM/布局指标，`documentElement.scrollWidth == innerWidth`，搜索区和导航均存在；浏览器默认视口已恢复。随后执行 `reader-frontend-runtime-smoke.sh`，结果为 `PASS (Reader/PWA/Operations frontend contracts)`。
- 清理与边界：浏览器临时页、SSH 转发、Compose 容器/网络/卷和隔离 worktree 均已清理；VM 原工作树中的用户改动保持不变。本轮只新增浏览器自动化证据，不替代人工视觉、真实账户/PWA 安装与跨设备、阅读 3.0、真实来源和生产环境验收。
- 远端门禁：文档提交 `198dd61` 的 [CI 33459414177](https://github.com/nekohands/InkFlow/actions/runs/33459414177)、[Docker 33459414175](https://github.com/nekohands/InkFlow/actions/runs/33459414175) 和 [Security 33459414095](https://github.com/nekohands/InkFlow/actions/runs/33459414095) 均 GREEN 且指向同一 head SHA；CI 的全量测试、源码 Compose、前端/业务 Runtime、SLO、Redis、备份恢复和诊断步骤均通过，Security 保留既有 Node.js 20 弃用提示但未影响门禁。
- 当前状态：前端自动化证据进一步取得实际页面交互和 VM 源码构建支撑；整体仍为 `1.0 Release Candidate`，不标记 `Accepted/Completed`。

### 5.27 GPT 内置浏览器匿名夹具书目实际阅读链路复验（本轮，2026-09-01）

- 目标：在不启动 ADB、MuMu/阅读 3.0、不输入真实凭据且不触发第三方 live source 的前提下，补齐“有夹具数据的 Web Reader 搜索 → 详情 → 目录 → 正文”实际页面交互证据。
- VM：以 `2162ac1` 建立独立 worktree，按日常策略使用 `docker-compose.build.yml` 源码构建；Migration/packages-init 退出码为 0，API、Worker、Scheduler、PostgreSQL、Redis 健康。AcceptanceFixtures 的 `ensure-reader-catalog` 成功返回稳定 fixture `bookId=3a9c9f4b-4975-4b64-949a-63c56bc5df19`、`chapterId=20503455-be9e-4aa9-aaab-2e057b14757b`。
- 浏览器：GPT 内置浏览器经临时 SSH 本地转发访问 `http://127.0.0.1:18080/reader`，实际输入 `InkFlow Runtime Acceptance Fixture` 并点击搜索；页面报告找到 1 本书，随后点击结果进入书籍详情，验证作者、1 章目录和“开始阅读”，再进入章节页读取 `Automated Acceptance Chapter` 及已发布 Canonical Content 正文。章节页的 `阅读进度` progressbar 为 `aria-valuenow=100`，阅读设置对话框可打开并提供主题选择、字号和行高控件。
- Runtime：在 VM 上执行 `bash scripts/reader-frontend-runtime-smoke.sh http://127.0.0.1:8080` 与 `bash scripts/reader-content-runtime-smoke.sh http://127.0.0.1:8080 20503455-be9e-4aa9-aaab-2e057b14757b`，分别返回 `PASS (Reader/PWA/Operations frontend contracts)` 和 `PASS (published content, reader progress contract)`。
- 清理与边界：浏览器临时页、SSH 转发、隔离 Compose 容器/网络/卷和 worktree 均已清理；VM 原工作树的用户改动保持不变。未读取 Cookie/Storage/密码材料，未输入或提交账户凭据，未创建真实账户；本轮证据不替代人工视觉/触控、真实账户/PWA 安装与跨设备、真实来源、生产环境和 MuMu/阅读 3.0 验收。
- 远端门禁：`2162ac1` 的 CI `33460167644`、Docker `33460167700`、Security `33460167715` 均 GREEN；本轮无产品代码变更，整体仍为 `1.0 Release Candidate`，不标记 `Accepted/Completed`。

### 5.28 Web Reader 上一章/下一章自动化验收闭环（本轮，2026-09-01）

- 缺口：5.27 的实际浏览器证据只包含单章夹具，尚不能证明 Web Reader 章节连续阅读、首章/末章边界和稳定 ChapterId 导航。
- 实现：`InkFlow.AcceptanceFixtures ensure-reader-catalog` 现在幂等准备同一本书的首章与下一章，并通过正式 `ContentPublishingService` 发布两份 Canonical Content；新增 `scripts/reader-navigation-runtime-smoke.sh`，检查两章正文、进度元素、`rel="next"`/`rel="prev"` 和首末边界；新增 fixture 回归并接入 CI。
- TDD/本机证据：新增脚本测试先红后绿；脚本 `bash -n`、fixture 回归、相关 Reader smoke 和 `git diff --check` 通过；Release Build 0 warnings / 0 errors；Windows Docker Engine 不可用，本机全量 `dotnet test` 为 Unit/Architecture/Contract 通过、IntegrationTests 因 `npipe://./pipe/docker_engine` 不可用而 BLOCKED，不记为本机集成通过。
- Ubuntu VM 证据：候选 `9a0b7df` 使用 `docker-compose.build.yml` 源码构建，Migration/packages-init、API/Worker/Scheduler/PostgreSQL/Redis/OTel 健康；Linux SDK 容器完整测试为 Unit 540/540、Architecture 1/1、Contract 10/10、Integration 103 passed / 3 skipped / 0 failed。`reader-frontend-runtime-smoke`、`reader-content-runtime-smoke`、`reader-navigation-runtime-smoke` 均 PASS；GPT 内置浏览器经临时 SSH 转发实际完成搜索→详情→目录→首章→下一章→上一章，确认正文、进度 100、两端边界和稳定链接。验证后隔离 Compose、卷、转发和 worktree 已清理，VM 原工作树用户改动保持不变。
- 远端门禁：`9a0b7df` 的 [CI 33464240828](https://github.com/nekohands/InkFlow/actions/runs/33464240828)、[Docker 33464240871](https://github.com/nekohands/InkFlow/actions/runs/33464240871)、[Security 33464240909](https://github.com/nekohands/InkFlow/actions/runs/33464240909) 均 GREEN 且 head SHA 一致。
- 边界：本轮仍不启动 ADB、MuMu/阅读 3.0，不使用真实账户/凭据或第三方 live source；人工视觉、触控、长时间阅读、PWA 安装/跨设备、真实来源和生产环境事项继续按第 6 节待定，整体保持 `1.0 Release Candidate`，不标记 `Accepted/Completed`。

### 5.29 1.0 非延期范围缺口审计（本轮，2026-09-01）

- 审计范围：重新对照 `roadmap.md`、`phase-1-acceptance.md`、架构/不变量/前端规范与第 6 节待定清单，使用 CodeGraph 核对采集控制、书籍打包、Web Reader/PWA、Operations、Content Policy、Source Authorization、CredentialReference、Admin Audit 和三类 Official Source 的实现入口与自动化证据。
- 审计结论：当前未发现新的、未实现且不属于延期范围的 1.0 功能缺口。采集任务进度与暂停/恢复/停止/取消、直接地址采集、EPUB/TXT/ZIP 打包、Reader 搜索→详情→目录→阅读→上一章/下一章、PWA 离线壳、管理 API、权限/审计、确定性追更与双来源切源均已有测试、Runtime smoke 或 VM 源码 Compose 证据；三类 Official Source 也已进入宿主种子与适配器组合根。
- 回归证据：本机 Release Build、Unit `540/540`、Architecture `1/1`、Contract `10/10` 和相关 shell 语法/diff 检查通过；本机 Docker Engine named pipe 不可用导致完整 Testcontainers 仍 BLOCKED。上一轮候选 `9a0b7df` 的 Ubuntu VM 源码 Compose、Linux SDK 全量测试和 GPT 内置浏览器双章节实际交互证据见 5.28，远端三类门禁均 GREEN。
- 明确边界：阅读 3.0/MuMu 真机、真实账户/PWA 安装跨设备、真实 Official Source/追更/第二来源故障切换、真实 Provider/生产凭据、受保护页面登录后浏览器输入、生产 OTLP/SLO/告警/备份治理仍是第 6 节待定；本轮不启动 ADB、不输入账户密码、不访问第三方 live source，整体继续保持 `1.0 Release Candidate`，不标记 `Accepted/Completed`。

### 5.30 Web Reader 长元数据边界自动化收口（本轮，2026-09-01）

- 缺口：原有 Reader 详情页回归覆盖了普通书目和安全转义，但没有用最大长度标题/作者、特殊字符和无封面书目锁定移动端长文本不撑破布局的边界。
- 实现：详情/书目卡片/目录/面包屑相关元素补充 `overflow-wrap: anywhere`，网格卡片和链接补充 `min-width: 0`；新增 `ReaderHtml` 长元数据转义与样式回归；`InkFlow.AcceptanceFixtures ensure-reader-edge-catalog` 生成 512 字符标题、256 字符作者、特殊字符和 `coverUrl=null` 的无封面 fixture；新增 `scripts/reader-edge-metadata-runtime-smoke.sh` 及 fixture 回归并接入 CI。
- 本机证据：AcceptanceFixtures Release Build 0 warnings / 0 errors；ReaderHtml 定向测试 `22/22`；新增 smoke `bash -n` 与 fixture 回归通过，`git diff --check` 通过。
- Ubuntu VM 证据：候选 `5dc59ab` 在隔离 worktree 使用 `docker-compose.build.yml` 源码构建；Linux SDK 完整测试为 Build 0 warnings / 0 errors、Unit/Architecture/Contract 通过、Integration `103 passed / 3 skipped / 0 failed`，11 个 migration context 无漂移；Migration/packages-init 正常退出，PostgreSQL/Redis/OTel/API/Worker/Scheduler 健康。`reader-frontend-runtime-smoke`、`reader-navigation-runtime-smoke`、`reader-edge-metadata-runtime-smoke` 均 PASS。
- GPT 内置浏览器证据：经临时 SSH 转发实际打开边界详情页，标题/作者可见且按行折返；在当前可用 `1280×720` 视口下 `document/body` 宽度均为 `1265px`，无横向溢出，计算样式确认标题/作者/目录使用 `overflow-wrap:anywhere`，无封面时没有详情图片，开始阅读入口存在。没有输入或读取账户、Token、密码或浏览器存储。
- 远端门禁：文档同步提交 `067b21d` 的 [CI 33471120031](https://github.com/nekohands/InkFlow/actions/runs/33471120031)、[Docker 33471120041](https://github.com/nekohands/InkFlow/actions/runs/33471120041)、[Security 33471120007](https://github.com/nekohands/InkFlow/actions/runs/33471120007) 均 GREEN 且 head SHA 一致；CI 已包含新增 Reader edge metadata 脚本回归和源码 Compose Runtime smoke。
- 清理与边界：Compose、网络、转发和隔离 worktree 已清理，VM 原工作树用户改动保持不变；本轮不启动 ADB、MuMu/阅读 3.0，不访问第三方 live source。375×812 等移动端人工视觉/触控、真实账户/PWA 安装跨设备、真实来源和生产环境事项仍按第 6 节待定，整体保持 `1.0 Release Candidate`，不标记 `Accepted/Completed`。

### 5.31 Operations Center 内容政策管理 UI 与权限自动化闭环（本轮，2026-09-01）

- 工作包：将 Content Policy 纳入 Operations Center 前端，提供管理员下架/恢复、当前下架列表和追加式理由确认；Operator 可访问运维中心但不能使用内容政策管理控件。
- 实现：前端只读取受保护的 `GET /api/v1/admin/content/takedowns?limit=50`，列表和结果使用安全 DOM 构建；下架/恢复复用已有理由校验、确认对话框和审计动作壳，不删除历史。新增 `ReaderHtml` 回归断言、前端 runtime smoke 标记和 curl fixture 标记。
- 本机证据：Release Build 0 warnings / 0 errors；Unit `541/541`、Architecture `1/1`、Contract `10/10`；前端 smoke PASS。整套本机 Testcontainers 因 Windows Docker Engine `npipe://./pipe/docker_engine` 不可用而 BLOCKED，不记为本机集成通过。
- Ubuntu VM 证据：候选 `5bdb4ea` 在隔离 worktree 使用 `docker-compose.build.yml` 源码构建；Migration/packages-init、PostgreSQL、Redis、OTel、API、Worker、Scheduler 健康；Linux SDK `Restore → Release Build → Test` 为 Build 0 warnings / 0 errors、Unit `541/541`、Architecture `1/1`、Contract `10/10`、Integration `103 passed / 3 skipped / 0 failed`；`verify-migrations` 为 11 contexts PASS；`admin-runtime-smoke` 覆盖权限、审计、来源权限/健康、凭据绑定和 Content Policy，结果 PASS；前端 smoke PASS。
- GPT 内置浏览器证据：临时管理员实际完成下架→列表回显→公开书目隐藏→恢复→公开书目恢复；新签发 Operator 会话可进入运维中心，内容政策输入框和按钮均 disabled，并显示“仅管理员可用”。临时账号随后禁用，隔离 Compose 资源、卷、转发和 worktree 已清理，VM 原工作树的既有用户改动保持不变。
- 远端门禁：文档提交 `7e7f242` 的 [CI 33477390879](https://github.com/nekohands/InkFlow/actions/runs/33477390879)、[Docker 33477390849](https://github.com/nekohands/InkFlow/actions/runs/33477390849)、[Security 33477390880](https://github.com/nekohands/InkFlow/actions/runs/33477390880) 均 success 且 head SHA 一致。
- 边界：不启动 ADB、MuMu/阅读 3.0；不使用真实账户/生产凭据，不访问第三方 live source。Content Policy 真实凭据/人工视觉操作、PWA 安装跨设备、真实来源/追更/切源和生产治理仍按第 6 节待定；整体保持 `1.0 Release Candidate`，不标记 `Accepted/Completed`。

### 5.32 CollectionRun 取消终态领域回归补强（本轮，2026-09-01）

- 缺口：采集运行的取消行为已有运行时 Smoke 覆盖，但领域层缺少对“运行中取消、重复取消幂等、取消后不可继续”的专门回归断言。
- 实现：在 `CollectionRunTests` 增加 `Cancelled_Run_Is_Terminal_And_Idempotent`；只补测试，不改变 CollectionRun、API、Migration 或运行时行为。
- 本机证据：Release Build 0 warnings / 0 errors；Unit `542/542`、Architecture `1/1`、Contract `10/10`；采集领域定向测试 `9/9`，`git diff --check` PASS。
- 运行边界：本轮无产品行为变更，不重复启动 VM Compose、浏览器或真实来源；5.31 的 VM 源码 Compose、业务 Smoke 和浏览器证据继续有效。本机 Testcontainers 仍受 Windows Docker Engine `npipe://./pipe/docker_engine` 不可用限制。
- 边界：不启动 ADB、MuMu/阅读 3.0，不使用真实账户/生产凭据，不访问第三方 live source；整体仍为 `1.0 Release Candidate`，不标记 `Accepted/Completed`。

## 5. Phase 1A 核心验收链路

```text
Official Source
→ Search
→ SourceBook
→ Canonical Book
→ SourceChapter / TOC
→ Canonical Chapter
→ Chapter Content
→ ContentVersion / Selected Version
→ Public API
→ Web Reader
→ Legado bookSource
→ Legado Search / TOC / Content
→ 自动追更
```

不得依赖人工直接修改数据库或手工拼生产数据。

## 5.5 Phase 1A 验收清单核对（2026-08-27）

对照 `phase-1-acceptance.md` 的 Required flow 逐项核对：

| # | 验收项 | 状态 | 说明 |
| --- | --- | --- | --- |
| 1 | 从来源搜索书籍 | ⚙️ 机制就绪 | Search 能力规则执行链路可用;实际数据待接入真实 Official Source |
| 2 | 导入 SourceBook | ✅ | `SourceCatalogService.ImportBookInfoAsync`(upsert) |
| 3 | 创建/关联 CanonicalBook | ✅ | `CanonicalBookMatchingService`(Confirmed 候选 + 稳定 BookId) |
| 4 | 抓取 TOC / SourceChapter | ✅ | `SyncChaptersAsync` 幂等落库 |
| 5 | CanonicalChapter 记录/映射 | ✅ | `CanonicalChapterMappingService` + chapter_mappings |
| 6 | 抓取章节正文 | ✅ | Content 能力规则执行链路 |
| 7 | FetchArtifact 元数据 + RawHash | ✅ | SHA-256,哈希幂等去重 |
| 8 | 规范化为 Content AST | ✅ | `ContentNormalizer` → `ContentDocument`(等价标记同形态) |
| 9 | CanonicalHash + Quality v1 | ✅ | SHA-256 + 可解释启发式评分 |
| 10 | 持久化 ContentVersion | ✅ | content.versions 表((chapter, hash) 唯一) |
| 11 | 选定当前版本 | ✅ | 质量分高者胜、平分取新;IsCurrent 原子切换 |
| 12 | Minimal Web Reader 阅读 | ✅ | `/reader` 三页面流(CI 容器验证渲染) |
| 13 | 生成 book-source.json | ✅ | 程序化生成,CI smoke 断言 |
| 14–16 | Legado 导入与搜索/阅读 | ⏳ 契约就绪 | 端点已过容器 smoke;真机导入验证需阅读 3.0 客户端 |
| 17 | Scheduler 自动检测更新 | ✅ 机制就绪 | 扫描入队 + Worker 消费闭环;真实数据验证依赖真实源接入 |
| 18 | CI/Docker baseline green | ✅ | 全部工作包 CI GREEN |

结论：**机制层验收通过**。kanunu8 真实 Official Source 已完成；当前外部验收依赖为 Legado 真机导入/阅读与真实追更验证。在此之前 Phase 1A 状态为 **Ready for Real-Device Acceptance**,不标记 Completed。

## 5.6 Phase 1B 双来源验收核对（2026-08-27）

| 验收项 | 状态 | 证据 |
| --- | --- | --- |
| 一个 CanonicalBook 代表两个 SourceBook | ✅ 自动化 | `DualSourceCanonicalValidationTests.Two_SourceBooks_Reuse_CanonicalBook_And_CanonicalChapter_Identities` |
| 同逻辑章节复用稳定 CanonicalChapter | ✅ 自动化 | 章节序号 + 标题归一化对齐；4 条映射归并到 2 个正典章节 |
| 一个 CanonicalChapter 至少 2 个 SourceChapter 候选 | ✅ 自动化 | 每个正典章节均有 `official-a` / `official-b` 映射 |
| 一个 CanonicalChapter 至少 2 个 ContentVersion 候选 | ✅ 自动化 | 同章节 2 个不同 CanonicalHash 版本 |
| Quality Selection 有版本与证据 | ✅ 自动化 | `quality-v1` + 段落/字符/平均段长证据；低质量候选未替换当前版本 |
| 真实来源故障切换 / Legado 端到端 | ⏳ 待真实来源 | 4.99 已以源码 Compose 夹具完成 Web/Legado A→B→A 运行时验证；真实 Official Source pair 与阅读 3.0 真机仍待验收 |
| Capability Health 感知的自动切源 | ✅ 自动化 | `SourceCapabilityHealth` + `ContentSelectionService`；确定性测试覆盖禁用、切换、恢复和审计证据 |

结论：Phase 1B 已建立可回归的双来源自动化切源基线，尚未标记 Completed；真实故障切源和运行时/真机证据仍是 Release Gate。

## 5.7 第三个 Official Source 接入（本轮，2026-08-28）

- 缺口：1.0 要求至少 3 个稳定 Official Source；此前只有一个真实 CodeAdapter 和一个规则型来源，第三来源尚未进入宿主组合根。
- 实现：新增 `InkFlow.Sources.Adapters.SeventeenK` 17K CodeAdapter，覆盖 Search、BookInfo、TOC、Content；外部书籍 ID 约束为纯数字，章节 ID 固定为 `bookId/chapterId`，避免把可变 URL 当业务主键。API、目录和正文使用固定 allowlist 主机，所有请求先经 `SsrfGuard`，生产宿主再经 `SsrfSafeHttpMessageHandler`，适配器超时 20 秒。
- 访问边界：上游未购买 VIP 章节返回 null，不读取或执行订阅/自动购买地址；非 2xx、空响应和非法 JSON 不产生伪造内容。Worker 启动种子现在幂等登记 linovelib、kanunu8 和 17K 三个 Official Source，已有 Source 记录不会被覆盖。
- Fixture 回归：新增 17K JSON Fixture 覆盖搜索结果去重、书籍/目录/正文解析、稳定章节 ID、非法 ID 零触网和未购买 VIP 不绕过；三宿主均注册同一 CodeAdapter，并继续复用连接级 SSRF 防护。
- 自动化证据：本机 `dotnet restore InkFlow.sln` PASS；Release Build 0 warnings / 0 errors PASS；Unit 258/258、Architecture 1/1、Contract 2/2 PASS。Integration 48 项实际运行结果为 6 通过、41 项因本机 `npipe://./pipe/docker_engine` 不可用而 BLOCKED、1 项跳过，不记为本机集成通过；未执行真实 17K/其他来源请求。提交 `258e3c3` 的远端 CI `33127440930` 与 Docker `33127440917` 均 GREEN，包含 Restore/Build/Test、Compose、Runtime smoke/Diagnostics 和四镜像构建。
- 验收边界：本轮只完成第三来源的代码/种子/Fixture 机制闭环，不能据此宣称 17K 已稳定实测或 1.0 完成；真实 Search → BookInfo → TOC → Content、付费/免费边界和多源故障切换继续列入第 6 节待定事项。

## 6. 待定事项（人工/真实环境，后续处理）

> 以下事项本轮明确不执行，后续按清单逐项验收；自动化测试和 CI 绿灯不能替代这些证据。

### 6.1 需要人工或真实业务环境验收

- [x] **1.0 前端自动化验收（GPT 内置浏览器）**：Web Reader、Reader/PWA 和 Operations Center 的可自动化页面/交互/响应式/可访问性检查已在 4.75 完成；真实账户、PWA 安装/断网和长时间体验保留为补充验收。
- [ ] **阅读 3.0 真机导入与阅读**：在 MuMu 中导入 `/legado/book-source.json`，验证 Search → BookInfo → TOC → Content；记录截图、请求结果和异常。
- [ ] **Personal Legado Token 人工验收**：在阅读 3.0 导入签发响应中的 Personal 书源，验证个人 Search → BookInfo → TOC → Content、令牌 header 传递，以及撤销后请求失效；本轮按用户决定不执行。
- [x] **Web Reader 浏览器自动验收（1.0 必选）**：已在移动端、平板、桌面端、宽屏检查页面路由、空/错状态、搜索点击、正文壳宽度、焦点和无横向溢出；5.30 新增最大长度标题/作者、特殊字符、无封面详情的自动化与 VM 实际页面证据，375×812 等人工视觉/触控与长时间阅读仍未执行。
- [x] **Reader/PWA Service Worker 与离线壳非阅读 App 自动化验收（1.0 必选）**：4.82 在 localhost 安全上下文中自动验证 Manifest、激活/接管、壳缓存、API 不可用时的离线回退、恢复后在线页面及浏览器日志；VM IP 明文 HTTP 的 Service Worker 不可用也已记录为部署边界。
- [ ] **Reader/PWA 真实账户与安装/跨设备补充验收（1.0 必选）**：真实账户会话、安装提示/独立窗口启动、生产 HTTPS、跨设备同步和长期体验仍需可用测试账户与部署环境；按本轮范围不执行阅读 3.0。
- [x] **Reader/PWA 账户与阅读状态 API 非阅读 App 自动化运行验收**：4.84 已在 Ubuntu VM 源码构建 Compose 中验证注册/登录/刷新/登出、偏好、书架、进度、历史及非法请求边界；PWA 页面内真实凭据输入仍待人工或真实环境。
- [x] **Reader/PWA 页面临时账户内置浏览器自动化验收**：4.85 已在 Ubuntu VM 源码构建 Compose 中自动验证注册/刷新会话、Catalog fixture 加入书架、书架列表、章节未发布空状态、登出和匿名书架/历史保护提示；4.86 又验证了已发布章节正文页面；临时账户已禁用，未使用真实凭据。
- [x] **Private Library 非阅读 App 自动化运行验收**：源码构建 Compose 已由 4.78 的 runtime smoke 覆盖认证、所有权隔离、CRUD、TXT 导入/章节/正文/导出、私有缓存头、公共 API/Legado 直接路径 404，以及公共 Catalog/Reading Shelf 不泄漏。
- [x] **Private Library 非阅读 App 自动化文件/一致性验收**：源码构建 Compose 已覆盖 TXT/EPUB 导入/导出、章节/正文、重复导入不覆盖原书、失败导入无半本书、私有缓存头、所有权及公共路径隔离。
- [ ] **Private Library 真实账户/人工体验补充验收**：如需发布前补充，使用专用真实测试账户和真实 TXT/EPUB 验证浏览体验、导出文件可读性及长期使用；不作为阅读 App 以外自动化门禁的替代。
- [x] **Developer API / 商业基础非阅读 App 自动化运行验收**：源码构建 Compose 已自动验证 Free Entitlement、应用/密钥创建与列表脱敏、目录读取、`X-InkFlow-Api-Key` 专用 Header、Free 配额消耗后的 `429/Retry-After`、跨账户独立配额、停用用户拒绝、轮换和撤销；真实 Web 账户、真实套餐/Provider、生产 Redis 和人工审计核对仍需真实环境补充。
- [ ] **真实追更验收**：4.87 已用真实 Kanunu8 当前快照自动验证 Scheduler 扫描、Worker 消费、目录同步、任务去重与正文发布；5.10 又用确定性来源响应验证新增章节后的增量映射、正文发布和重复扫描幂等；仍需真实 Official Source 上游新增章节/修订事件，验证下一周期扫描确实产生增量并发布新版本。
- [ ] **真实第二来源与故障切换**：4.99 已用确定性双来源夹具完成源码 Compose 下 Web/Legado 的 A→B→A、稳定 BookId/ChapterId 和恢复验证；仍需从已接入 Official Source 中选择可稳定访问的真实第二来源，确认真实来源故障、真实响应和恢复不产生重复正典身份。
- [ ] **Content Policy 管理人工验收**：使用 Administrator 凭证验证下架/恢复与理由校验；确认 Operator/匿名不能执行管理命令，并逐一确认目录、详情、正文、Web Reader、公共搜索和 Legado 在下架期间不可见、恢复后可读，同时核对命令审计记录。
- [x] **Content Policy 非阅读 App 自动化验收**：4.83 已用临时管理员和 CanonicalBook fixture 验证下架/恢复、公共详情可见性、权限拒绝和审计过滤。
- [x] **Content Policy Operations UI 非阅读 App 自动化验收**：5.31 使用临时管理员/Operator 夹具和 GPT 内置浏览器验证下架/恢复 UI、公开书目隐藏/恢复、列表回显及 Operator 禁用边界；真实凭据和人工视觉验收仍待定。
- [x] **Operations Center 浏览器自动验收（1.0 必选）**：匿名角色拒绝、页面结构、状态提示、刷新按钮禁用态、桌面/移动布局、焦点/无横向溢出和浏览器错误日志已由 4.75 自动化；受保护命令的 API/集成基线已自动化。
- [ ] **Operations Center 真实凭据补充验收**：Operator/Administrator 真实登录后的命令执行、告警/来源/死信操作和生产截图仍需可用测试账户与部署环境。
- [x] **Operations Center 受保护 API 自动化验收**：4.83 已验证概览、告警和告警历史响应结构及管理员/Operator 运行时路径；真实凭据和生产通知仍待补充。
- [x] **Source Authorization 非阅读 App 自动化验收**：4.83 已验证授予/列出/撤销、重复授予幂等、`source.manage` 隐含读取、健康/停用/恢复、授权前后 403 和审计。
- [ ] **Source Authorization 人工/真实账号补充验收**：使用真实 Administrator/Operator 账户复核完整页面操作、来源过滤和生产权限配置；自动化基线已完成但未使用真实凭据。
- [x] **Source 默认 CredentialReference 非阅读 App 自动化验收**：4.83 已验证 Administrator set/clear、非 secret 引用、权限拒绝和 set/clear 审计。
- [ ] **Source 默认 CredentialReference 人工/真实 Provider 补充验收**：使用真实账户和可用 Provider 验证 Platform/User/Organization Owner Scope、显式引用优先与生产 secret 管理；自动化基线已完成但未使用真实凭据。
- [x] **Admin Audit Read 非阅读 App 自动化验收**：4.83 已验证管理员审计查询、命令过滤和不暴露 secret/body 的 API 响应。
- [ ] **Admin Audit Read 人工/真实环境补充验收**：使用真实 Operator/Administrator 复核时间范围、游标翻页、空结果、服务不可用和截图证据；自动化基线已完成。
- [x] **Developer API / 商业基础管理员套餐自动化验收**：4.83 已验证 Administrator 为临时 Operator 授予 Pro 后的 Entitlement、quota 和审计路径。
- [ ] **Developer API / 商业基础人工/真实账户补充验收**：使用真实 Web 账户创建/撤销应用与 API Key，确认原文只出现一次；补充真实套餐/Provider、跨应用用户级配额、超额 `429/Retry-After`、密钥/应用/用户停用后的拒绝和审计；5.13 已完成同范围临时账户自动化，但本轮未使用真实凭据。

### 6.2 需要可用环境复验

- [x] **PostgreSQL 集成测试（Ubuntu VM 可用 Docker 环境）**：已在 Ubuntu VM 的源码构建 Compose 环境中完成完整 Testcontainers 集成测试；Unit 530/530、Architecture 1/1、Contract 10/10，Integration 104 项为 102 passed / 2 skipped / 0 failed，覆盖 Private Library、Developers/Billing、Operations 告警历史、Messaging Outbox/Inbox、Sources Capability Health、ContentVersion 当前选择边界和确定性 Scheduler 追更链路等持久化场景。Windows 开发机的 `npipe://./pipe/docker_engine` 仍不可用，但不影响本次 VM 本地容器证据。
- [x] **Kanunu8 真实只读适配器验证**：BookInfo、TOC、章节正文 3/3 通过；Search 能力当前按适配器契约返回空结果，未计为完整 Phase 1A Search 链路。
- [x] **linovelib 真实公开站点只读链路**：已用 GPT 内置浏览器自动完成 Search（`恶魔高校`）→ BookInfo → 482 章 TOC → 首章正文读取；该证据不涉及登录、账号或站点写入，详见 4.77。
- [ ] **linovelib RuleAdapter 后端直连链路**：站点搜索表单为 `/S6/` + `searchkey`，规则与离线回归已覆盖；当前普通 HTTP POST 返回 200 但空响应体，尚不能把浏览器页面证据等同于服务端适配器通过。待网络/站点挑战可稳定处理后，再验证服务端 Search → BookInfo → TOC → Content，并纳入真实第二来源/切源候选。
- [ ] **17K 真实验证**：已在 Ubuntu VM 只读探测官方 API/Web，但当前 API 证书链校验失败或返回“请升级版本/图书信息不存在”，未形成稳定 Search → BookInfo → TOC → 免费 Content；待可用网络环境继续验证非购买 VIP、超时/非 2xx/重定向安全边界。
- [x] **PostgreSQL 备份恢复演练（Ubuntu VM）**：本轮源码 Compose 执行 `scripts/backup-restore-drill.sh`，custom-format 归档恢复到隔离数据库，所有非系统表行数签名与 `audit.events` 数量一致，最新结果为 `archive=108510 bytes, audit_events=271`；隔离库已清理，Compose 持久卷保留。此前 GHCR 发布镜像复验也已通过；生产异地/加密/保留/RPO-RTO 治理仍待部署环境验收。
- [ ] **生产 OTLP 后端与 SLO 窗口验收**：在部署环境把 Collector 接入受治理的持久化后端，确认 API/Worker/Scheduler/Reader 观测到达，基于合成探针与真实业务窗口完成聚合，验证错误预算告警、访问控制和保留策略；当前 CI 合成探针与 Compose debug exporter 仅是短窗口接收基线，不替代生产证据。

### 6.3 后续工程事项（非本轮人工验收）

- [x] **Inbox 业务消费闭环（`crawler.task.created` v1）**：已明确稳定 `IntegrationMessage` 类型、注册幂等 Handler，补充按任务 ID 原子租约、共享任务处理器、任务级重试/死信策略和 Outbox→Inbox→Handler→任务完成验证；其他 Integration Event 仍需各自接入和取得端到端证据。
- [x] **Inbox 死信 Operations 观测 v1**：终态 Inbox 死信以有界数量/截断标记进入平台级告警；读取失败 fail-closed 为 partial，来源过滤视图不泄漏平台级消息状态（ADR 0020）。
- Source Health / Capability Health、v1 健康感知切源、半开自适应恢复与探针冷却参数配置化（ADR 0005）已落地；Crawler 死信受控重放、受保护 Repair/replay 入口、跨模块 Consistency Check v1、Operations Center Read Model v1 与 Center UI v1 自动化基线已落地，自动修复与更强运维治理仍属于后续工程工作。
- API 限流已接入 Redis 原子 fixed-window 分布式计数，并保留同配额的本地有界故障降级；Developer API v1 已接入生产 API Key、固定版本套餐/Entitlement、PostgreSQL 用户级 UTC 月度加权配额和不可变 Usage Ledger，Redis 仅作快照加速。Operations 已提供 Redis/来源健康/死信/一致性告警快照、配置化阈值、PostgreSQL 告警 incident 去重/恢复历史、保留清理、管理员历史查询和 Operations Center 历史展示；来源级授权 v1 已落地并接入来源查询/控制及授权审计。组织/租户、支付、外部告警路由和生产告警治理仍待后续 Operations/Identity/商业化工作包；审计已具备有界 retention 代码基线，但生产法律/合同保留、归档和删除授权治理仍待部署环境确定。
- CI Security Scan v1 已接入依赖漏洞、Secret/Misconfiguration、CodeQL SAST、源码 SBOM 和 Docker 发布前扫描；Code Scanning API 未启用，当前以工作流产物提供证据。生产扫描策略、报告保留、Secret 轮换和动作版本治理仍待后续安全治理工作。
- PostgreSQL 备份恢复已有 CI 级 custom-format dump/restore 演练和全表行数签名证据；生产异地备份、加密、保留/删除治理、恢复授权、RPO/RTO 和告警仍待后续 Operations 工作包。
- Source 出网已具备 `SsrfGuard` 字面量/DNS 检查与连接级 `SsrfSafeHttpMessageHandler`；RuleAdapter 在前置请求、主请求和分页请求的成功响应进入结果提取前统一校验最终 `ResponseUri` 的同源、userinfo 和 fragment 约束；仍待真实生产网络、重定向链路和策略扫描演练的独立证据。
- Source Rule DSL v1 已具备严格 JSON Schema/codec、Fixture 和 `RuleTransform` 持久化往返基线；受控 XPath/JSONPath
  选择器运行时已在 4.51 接入，受控 next-link Pagination 已在 4.52 接入，page-number/cursor 与跨页
  Rule execution budgets 已在 4.53 接入，受控 response-cookie Session 已在 4.54 接入，有界请求模板变量已在
  4.55 接入，任务级 CredentialReference typed 初始认证已在 4.56 接入，有界响应派生变量已在 4.57 接入，
  4.58 已接入来源级默认 CredentialReference 回退，4.59 已接入 Administrator-only 设置/清除和命令审计入口，4.60 已将 Provider 解析上下文收敛为带 Platform/User/Organization Owner Scope 的契约；
  5.1 已接入最多 8 步的同源串行 PreRequests 与临时响应变量，复用请求/字节/结果/时间/Session 预算，5.6 已补齐无 Session 主请求最终响应的同源门禁；
  secret 材料 Owner/Admin 管理、真实 SecretProvider、持久会话、动态多请求/分支/递归预算仍待后续工程工作包。
- Worker 任务已具备过期租约恢复、跨进程原子领取、持久化退避调度、单任务异常重试和失败结构化观测基线；TOC 联动正文抓取的事件触发闭环、抓取→发布桥与上游修订重扫已落地（见 4.x 各工作包）。告警快照、阈值、历史/去重、恢复状态、内部保留清理和历史页展示已落地，外部告警路由、生产通知治理和完整运维闭环仍待后续 Operations/Crawling 工作包。
- 用户身份的基础认证/授权与受保护 Repair 入口已落地；Reading State v1 后端、Reader/PWA 用户状态 v1（账户/书架/历史/进度/偏好接入、公开安装壳）、Personal Legado Token v1、Web Reader v1 和 Private Library v1/v2 自动化基础已落地。PWA Service Worker/离线壳已在 4.82 通过 localhost 安全上下文自动验收；真实安装、账户/跨设备体验、私有内容真实账户/文件端到端验收和公共路径隔离验收仍未完成。
- Developer API / Plan / Entitlement / Billing v1 已实现候选基线；Organization、支付、OAuth、sandbox、Community Marketplace 和管理型 Developer API 尚未实现。

## 7. 当前阻塞

最新状态（2026-09-01）：行为候选 `5bdb4ea` 已推送，完成 Operations Center Content Policy 管理 UI、管理员下架/恢复和 Operator 禁用边界；随后 `3aab3e8` 补齐 CollectionRun 取消终态/幂等领域回归，`7d60235` 同步本进度与交接文档，当前 `dev` HEAD 为 `7d60235`。本机 Release Build 0 warnings / 0 errors、Unit `542/542`、Architecture `1/1`、Contract `10/10`、CollectionRun 定向测试 `9/9` 和前端 smoke 通过。Windows Docker Engine named pipe 不可用导致本机 Testcontainers BLOCKED；Ubuntu VM 的源码 Compose、Linux SDK 全量测试、11 contexts migration 检查、业务 smoke 和 GPT 内置浏览器实际下架/恢复链路均通过。当前 HEAD 的 [CI 33479935777](https://github.com/nekohands/InkFlow/actions/runs/33479935777)、[Docker 33479935816](https://github.com/nekohands/InkFlow/actions/runs/33479935816)、[Security 33479935776](https://github.com/nekohands/InkFlow/actions/runs/33479935776) 均 success 且 SHA 一致。

当前仍有以下验收级限制：Windows 开发机 Docker Engine 不可用，受影响的本机 Testcontainers 仍为 BLOCKED；阅读 3.0/MuMu、Personal Legado Token、真实账户/PWA 安装与跨设备、真实追更与真实第二来源、真实凭据/Provider、受保护 Operations/Content Policy/Source Authorization/Admin Audit 的人工操作、linovelib/17K 真实链路，以及生产 OTLP/SLO/告警/备份治理均按用户决定或环境边界保留在第 6 节。当前审计没有发现新的、未实现且不属于上述延期范围的 1.0 功能缺口；整体保持 `1.0 Release Candidate`，不标记 `Accepted/Completed`。

以下为历史复验记录，仅用于追溯，不代表当前最新测试数字：

历史复验记录（4.64）：此前本机 Restore、Release Build、Unit 472/472、Architecture 1/1、Contract 10/10 和迁移模型检查通过；完整 Integration 因本机 Docker Engine 不可用而部分 BLOCKED。代码候选 `acbbd10dd67e350f2bf6b2ae1080c54f7b725d91` 的远端 [CI 33290137667](https://github.com/nekohands/InkFlow/actions/runs/33290137667)、[Docker 33290137676](https://github.com/nekohands/InkFlow/actions/runs/33290137676)、[Security 33290137668](https://github.com/nekohands/InkFlow/actions/runs/33290137668) 均 GREEN。

历史复验记录（早期 Ubuntu VM）：Linux SDK 容器曾执行完整 `Restore → Build → Test`，源码构建 Compose、Core SLO 和备份恢复通过；验证后停止容器并保留 PostgreSQL/Redis 卷。

历史复验记录（GHCR 发布镜像）：默认 GHCR Compose 曾在 Ubuntu VM 拉取镜像并通过 Migration、服务健康、Core SLO、脚本回归和备份恢复；验证后停止容器并保留数据卷。

历史复验记录（ContentVersion）：Ubuntu VM 曾以 PostgreSQL Testcontainers 验证跨章节目标拒绝、原当前版本保留和同章节唯一当前版本；测试容器已清理。

## 8. dev 分支骨架重建记录（2026-08-25）

`dev` 是当前唯一开发主线（root commit `c5f2048`）；业务代码在本骨架上按路线图重新实现。

### 8.1 本轮完成

- CI 触发扩展：`push` / `pull_request` 覆盖 `main` + `dev`，保留 `workflow_dispatch`。
- 重建 solution 全部 22 个项目骨架（4 Apps / 6 BuildingBlocks / 8 Modules / 4 Tests），目录与 `InkFlow.sln` 引用一致，项目引用关系遵循 `architecture/architecture.md` 的模块依赖矩阵。
- 全部源码为 `dev` 上重新编写（宿主 `/health` 探针、Observability 扩展 `ObservabilitySetup`、四个测试项目的守卫用例），**未从 `main` 复制任何代码**。
- 新增仓库级 `nuget.config`：CPM 下固定单一 nuget.org 源，解决本机多源导致的 `NU1507`，同时消除对开发者全局源配置的依赖。
- 移除 `src/`、`tests/` 旧代码；业务实现待按路线图重建。

### 8.2 验证证据

```text
dotnet restore InkFlow.sln            PASS
dotnet build -c Release               PASS (0 warnings / 0 errors)
dotnet test -c Release                PASS (Unit/Architecture/Integration/Contract 各 1 用例)
```

远端 CI（Run `32821162412`）：**GREEN**，Restore → Release Build → Tests → Compose Validation → Runtime Smoke 全部通过，API / Worker / Scheduler `/health` 验证 OK。

### 8.3 剩余风险

无已知阻塞；后续工作包按第 4 节顺序在 `dev` 上推进，每个工作包维持完整 Gate 闭环。

## 9. 强制维护规则

每完成一个工作包至少记录：Build、Tests、Runtime、CI、验收结果、发现/修复的 Bug、剩余风险、Commit/PR。

禁止把 `Implemented` 当作 `Completed`；CI Pending/Red 时不得声称已验收；修改代码后必须重新执行适用 Gate。

完整流程见 `../engineering/development-workflow.md`。
