# InkFlow 项目进度

> 持续进度账本。状态只以真实代码、测试、Runtime 和 CI 结果为准。

- 产品：墨流 / InkFlow
- 当前阶段：1.0 Release Candidate（自动化 Release Gate 已通过，人工/真实环境验收待定）
- 当前工作分支：`dev`（2026-08-25 起）
- 最后更新日期：2026-08-29

## 1. 总体状态

| 阶段 | 状态 | 说明 |
| --- | --- | --- |
| Grill Me / 产品与架构对齐 | ✅ Completed | 产品定位、核心领域、Legado、Source Runtime、安全、商业化和路线已文档化 |
| Repository Bootstrap | ✅ Completed | .NET 10 基础仓库与最初 CI 已建立 |
| Phase 0 — Foundation | ✅ Completed | 模块边界、Persistence、Migration、Outbox/Inbox、OTel、测试与 Runtime CI Gate 已验收 |
| Phase 1A — Single Source Vertical Slice | 🚧 Ready for Real-Device Acceptance | 自动化链路与 kanunu8 真实源验证已完成；阅读 3.0 真机导入/阅读及真实追更仍待人工验收 |
| Phase 1B — Dual Source Validation | 🚧 In Progress | 确定性双 Official Source 夹具已覆盖正典身份、章节对齐、质量选优与健康感知切源；真实故障切源仍待后续验收 |
| Phase 2 — Multi-Source Production | 🚧 In Progress | Capability Health v1 与 Worker 任务可靠性基础已落地；自适应追更、健康评分、规则 Canary 仍待推进 |
| Phase 3 — User Product | 🚧 In Progress | Reading State v1、Web Reader v1、Reader/PWA 用户状态 v1 与 Private Library 私有正文/导入导出自动化基础已落地；真实 PWA、账户/文件和私有路径验收仍待推进 |
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
- 边界：本轮完成来源级授权机制和自动化验证，但 MuMu/阅读 3.0、真实来源/故障切换、Operations/授权凭据人工验收仍按第 6 节待定；更广泛资源、组织/租户权限治理和审计保留策略仍未完成。

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
| 真实来源故障切换 / Legado 端到端 | ⏳ 待验收 | 真机与真实运行时按后续人工流程执行；本轮不宣称完成 |
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

- [ ] **阅读 3.0 真机导入与阅读**：在 MuMu 中导入 `/legado/book-source.json`，验证 Search → BookInfo → TOC → Content；记录截图、请求结果和异常。
- [ ] **Personal Legado Token 人工验收**：在阅读 3.0 导入签发响应中的 Personal 书源，验证个人 Search → BookInfo → TOC → Content、令牌 header 传递，以及撤销后请求失效；本轮按用户决定不执行。
- [ ] **Web Reader 人工体验验收**：在移动端、平板、桌面端、宽屏实际打开 `/reader` 三页面，检查正文宽度、长标题/长作者、Loading/Empty/Error、键盘焦点、触控目标、主题/字号/行高和上下章导航；自动化基线已完成，本轮未做浏览器截图/长时间阅读。
- [ ] **Reader/PWA 用户状态人工验收**：在支持的浏览器中验证账户登录/注册、刷新后会话、书架加入/移除、历史、章节进度/偏好同步、401 刷新和登出；验证安装提示、Service Worker 注册与网络不可用时离线提示。本轮按用户决定不执行。
- [ ] **Private Library 人工验收**：使用两个真实账户验证私有书目创建、列表、详情、更新、删除和跨用户 404；上传真实 TXT/EPUB，验证章节/正文读取、导出文件可读性、重复导入不覆盖和失败导入无半本书；确认私有内容不会出现在公共 Catalog、搜索、Legado 或公共 Reading Shelf。当前只有后端自动化基线，未执行人工操作。
- [ ] **真实追更验收**：使用真实来源数据验证 Scheduler 扫描、新章检测、Worker 消费、目录增量与正文发布。
- [ ] **真实第二来源与故障切换**：从已接入的 Official Source 中选择可稳定访问的真实第二来源；禁用 Source A 后验证 Web/Legado 仍可读，BookId/ChapterId 不变，恢复后不产生重复正典身份。
- [ ] **Content Policy 管理人工验收**：使用 Administrator 凭证验证下架/恢复与理由校验；确认 Operator/匿名不能执行管理命令，并逐一确认目录、详情、正文、Web Reader、公共搜索和 Legado 在下架期间不可见、恢复后可读，同时核对命令审计记录。
- [ ] **Operations Center 人工验收**：使用 Operator/Administrator 凭证打开 /admin/operations，验证登录/角色拒绝、overview/告警快照读取、管理员告警历史分页与恢复转折、来源能力停用/恢复、死信理由确认与重放、HasMore 截断标记、区块部分失败状态和命令结果；检查移动/桌面布局、键盘焦点、对比度与截图证据。本轮只完成自动化基线。
- [ ] **Source Authorization 人工验收**：使用 Administrator 授予/列出/撤销某个 Operator 的 `source.read` / `source.manage`，验证重复授予幂等、撤销后拒绝、`source.manage` 隐含读取、来源健康/停用/恢复及 Operations 来源健康区块按来源过滤；验证 Reader/匿名和未授权 Operator 的 401/403、理由校验与授权审计。本轮只完成自动化基线，未使用真实凭据操作。
- [ ] **Admin Audit Read 人工验收**：使用 Operator/Administrator 凭证验证审计查询 200、Reader/匿名请求 401/403、时间范围/精确过滤/游标翻页、空结果和服务不可用时的稳定错误；确认响应不暴露秘密或正文，并保留截图/请求证据。本轮只完成自动化基线。
- [ ] **Developer API / 商业基础人工验收**：使用真实 Web 账户创建/撤销应用与 API Key，确认原文只出现一次；由 Administrator 授予套餐，验证目录读取、跨应用用户级配额、超额 `429/Retry-After`、密钥/应用/用户停用后的拒绝和审计；本轮未使用真实凭据。

### 6.2 需要可用环境复验

- [ ] **本机 PostgreSQL 集成测试**：Docker 可用后重新执行完整 Testcontainers 集成测试（当前 64 项中 56 项因 `docker_engine` 不可用而 BLOCKED、2 项跳过）；Private Library、Developers/Billing 与 Operations 告警历史新增迁移、隔离、并发和保留清理用例也必须取得真实容器证据。
- [ ] **linovelib 真实验证**：站点可自本机间歇访问（UTF-8 静态 HTML、搜索表单为 `/S6/` + `searchkey`），但当前网络 DNS 解析被污染漂移（CNAME 链至嵌套 punycode 域、部分解析指向 127.0.0.1），无法稳定闭环；种子规则已补齐 Search（`POST /S6/`、`searchkey`、列表绑定）并修正 `/novel/` ID 归一化，离线回归已覆盖。待网络环境可用时按 live 流程验证 Search → BookInfo → TOC → Content，并作为真实第二来源/真实切源验收候选。
- [ ] **17K 真实验证**：待可用网络环境中验证官方 API/Web 的 Search → BookInfo → TOC → 免费 Content 链路、非购买 VIP 返回边界、超时/非 2xx/重定向安全行为；本轮仅完成离线 JSON Fixture 回归，未触网。
- [ ] **本机 PostgreSQL 备份恢复演练**：Docker 可用后启动源码 Compose，先产生运行数据，再执行 `scripts/backup-restore-drill.sh` 并保留归档大小、恢复库行数签名和清理结果；当前因 Docker 命令不可用而 BLOCKED。
- [ ] **生产 OTLP 后端与 SLO 窗口验收**：在部署环境把 Collector 接入受治理的持久化后端，确认 API/Worker/Scheduler/Reader 观测到达，执行合成探针与窗口聚合，验证错误预算告警、访问控制和保留策略；Compose 的 debug exporter 与健康 smoke 仅是接收基线，不替代生产证据。

### 6.3 后续工程事项（非本轮人工验收）

- Source Health / Capability Health、v1 健康感知切源、半开自适应恢复与探针冷却参数配置化（ADR 0005）已落地；Crawler 死信受控重放、受保护 Repair/replay 入口、跨模块 Consistency Check v1、Operations Center Read Model v1 与 Center UI v1 自动化基线已落地，自动修复与更强运维治理仍属于后续工程工作。
- API 限流已接入 Redis 原子 fixed-window 分布式计数，并保留同配额的本地有界故障降级；Developer API v1 已接入生产 API Key、固定版本套餐/Entitlement、PostgreSQL 用户级 UTC 月度加权配额和不可变 Usage Ledger，Redis 仅作快照加速。Operations 已提供 Redis/来源健康/死信/一致性告警快照、配置化阈值、PostgreSQL 告警 incident 去重/恢复历史、保留清理、管理员历史查询和 Operations Center 历史展示；来源级授权 v1 已落地并接入来源查询/控制及授权审计。组织/租户、支付、外部告警路由、生产告警治理和审计保留策略仍待后续 Operations/Identity/商业化工作包。
- CI Security Scan v1 已接入依赖漏洞、Secret/Misconfiguration、CodeQL SAST、源码 SBOM 和 Docker 发布前扫描；Code Scanning API 未启用，当前以工作流产物提供证据。生产扫描策略、报告保留、Secret 轮换和动作版本治理仍待后续安全治理工作。
- PostgreSQL 备份恢复已有 CI 级 custom-format dump/restore 演练和全表行数签名证据；生产异地备份、加密、保留/删除治理、恢复授权、RPO/RTO 和告警仍待后续 Operations 工作包。
- Source 出网已具备 `SsrfGuard` 字面量/DNS 检查与连接级 `SsrfSafeHttpMessageHandler`；仍待真实生产网络、重定向链路和策略扫描演练的独立证据。
- Worker 任务已具备过期租约恢复、跨进程原子领取、持久化退避调度、单任务异常重试和失败结构化观测基线；TOC 联动正文抓取的事件触发闭环、抓取→发布桥与上游修订重扫已落地（见 4.x 各工作包）。告警快照、阈值、历史/去重、恢复状态、内部保留清理和历史页展示已落地，外部告警路由、生产通知治理和完整运维闭环仍待后续 Operations/Crawling 工作包。
- 用户身份的基础认证/授权与受保护 Repair 入口已落地；Reading State v1 后端、Reader/PWA 用户状态 v1（账户/书架/历史/进度/偏好接入、公开安装壳）、Personal Legado Token v1、Web Reader v1 和 Private Library v1/v2 自动化基础已落地。PWA 实际安装/离线/跨设备验收、私有内容真实账户/文件端到端验收和公共路径隔离验收仍未完成。
- Developer API / Plan / Entitlement / Billing v1 已实现候选基线；Organization、支付、OAuth、sandbox、Community Marketplace 和管理型 Developer API 尚未实现。

## 7. 当前阻塞

当前仍有以下验收级限制：本机未安装/运行 Docker，完整 PostgreSQL 集成测试（含 Private Library 私有章节和 Operations 告警历史）无法在本机执行；阅读 3.0 真机流程按用户决定延后；Reader/PWA、Private Library、Operations Center、Source Authorization 和 Admin Audit Read 的实际安装/操作/跨尺寸浏览器验收尚未执行；真实来源与故障切换仍未执行。Compose 已补齐 OTLP Collector 的内部接收与 loopback 健康基线，但真实生产 OTLP 后端、四个服务面的到达、SLO 窗口/合成探针、错误预算告警和生产保留治理尚未验收。CI Security Scan 基线已在远端通过，但生产安全治理、镜像策略和报告保留尚未完成。此前提交 `f83476a` 的 Content Policy、Identity/Repair、Reader/PWA、Operations Center、Source Authorization、Admin Audit Read、Private Library v1/v2 自动化基线与一致性检查已有远端 CI、Compose、Runtime smoke 与 Docker 绿灯证据（CI `33163145132` / Docker `33163145104` / Security `33163144984`）；本轮 Operations 告警历史的候选提交 `4ef206f` 已通过远端 CI `33244304809`、Docker `33244304814` 和 Security `33244304804`；Core SLO 候选提交 `a87c5ae` 已通过远端 CI `33246490603`、Docker `33246490571` 和 Security `33246490589`。这些人工/环境限制属于整体 Release Gate，不改变已通过的本地自动化证据。

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
