# InkFlow 项目进度

> 持续进度账本。状态只以真实代码、测试、Runtime 和 CI 结果为准。

- 产品：墨流 / InkFlow
- 当前阶段：Phase 1B — Dual Source Validation（自动化切源基线进行中）
- 当前工作分支：`dev`（2026-08-25 起）
- 最后更新日期：2026-08-28

## 1. 总体状态

| 阶段 | 状态 | 说明 |
| --- | --- | --- |
| Grill Me / 产品与架构对齐 | ✅ Completed | 产品定位、核心领域、Legado、Source Runtime、安全、商业化和路线已文档化 |
| Repository Bootstrap | ✅ Completed | .NET 10 基础仓库与最初 CI 已建立 |
| Phase 0 — Foundation | ✅ Completed | 模块边界、Persistence、Migration、Outbox/Inbox、OTel、测试与 Runtime CI Gate 已验收 |
| Phase 1A — Single Source Vertical Slice | 🚧 Ready for Real-Device Acceptance | 自动化链路与 kanunu8 真实源验证已完成；阅读 3.0 真机导入/阅读及真实追更仍待人工验收 |
| Phase 1B — Dual Source Validation | 🚧 In Progress | 确定性双 Official Source 夹具已覆盖正典身份、章节对齐、质量选优与健康感知切源；真实故障切源仍待后续验收 |
| Phase 2 — Multi-Source Production | 🚧 In Progress | Capability Health v1 与 Worker 任务可靠性基础已落地；自适应追更、健康评分、规则 Canary 仍待推进 |
| Phase 3 — User Product | ⏳ Not Started | Web Reader、账号、书架、历史、私人书库 |
| Phase 4 — Commercial Platform | ⏳ Not Started | Entitlement、Developer API、Billing、Organization、Community Source |

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

## 4. 当前正在做 — Phase 1A

> **分支说明（2026-08-25）**：项目已切换到 `dev` 分支重新起步，`dev` 为唯一开发主线，完成后经 PR 合入 `main`。`dev` 目前仅包含基础设施骨架（见第 8 节）；Phase 1A 各工作包按原顺序与设计文档在 `dev` 上重新推进。

按以下顺序推进，每一项继续执行完整工程闭环：

1. ✅ Source DSL v1 与校验模型。（已实现，本地验证通过）
2. ✅ `RuleAdapter` 与 Fixture 驱动执行器。（已实现，本地验证通过）
3. 🚧 Safe HTTP / SSRF 基础防线、请求预算与错误分类。（当前）
4. ✅ Crawler Task / Lease / Retry / DeadLetter。（已实现）
5. ⏳ SourceBook / SourceChapter 持久化。
6. ⏳ Canonical Book 创建与 Match Candidate 基础。
7. ⏳ Canonical Chapter / Chapter Mapping。
8. ⏳ Content AST / ContentVersion / ContentHash。
9. ⏳ 最小 Quality Engine 与 Selected Version。
10. ⏳ Public API：Search / Book / TOC / Chapter。
11. ⏳ Legado v1 API Contract。
12. ⏳ `ILegadoRuleGenerator` 与 `/legado/book-source.json`。
13. ⏳ Web Reader 最小纵向体验。
14. ⏳ 单来源自动追更链路。
15. ⏳ Phase 1A E2E / Contract / Runtime 验收。

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

**追更正文闭环：目录联动正文抓取（本轮，2026-08-28）**：

- 补齐"事件触发"缺口：`TocSyncTaskHandler` 在目录同步 + 正典映射成功后调用 `ContentFetchChainService`，为**该来源从未抓取过正文**的章节自动入队 Content 抓取任务——"检测新章 → 抓取 → 发布"不再依赖人工种子或额外扫描周期。
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

## 6. 待定事项（人工/真实环境，后续处理）

> 以下事项本轮明确不执行，后续按清单逐项验收；自动化测试和 CI 绿灯不能替代这些证据。

### 6.1 需要人工或真实业务环境验收

- [ ] **阅读 3.0 真机导入与阅读**：在 MuMu 中导入 `/legado/book-source.json`，验证 Search → BookInfo → TOC → Content；记录截图、请求结果和异常。
- [ ] **Web Reader 人工体验验收**：移动端、桌面端、宽屏正文宽度、长标题/缺封面/长作者、Loading/Empty/Error、键盘焦点、触控目标和阅读导航。
- [ ] **真实追更验收**：使用真实来源数据验证 Scheduler 扫描、新章检测、Worker 消费、目录增量与正文发布。
- [ ] **真实第二来源与故障切换**：补充第二个真实 Official Source；禁用 Source A 后验证 Web/Legado 仍可读，BookId/ChapterId 不变，恢复后不产生重复正典身份。

### 6.2 需要可用环境复验

- [ ] **本机 PostgreSQL 集成测试**：Docker 可用后重新执行完整 Testcontainers 集成测试；当前 23 个用例因 `docker_engine` 不可用而 BLOCKED（本轮总计 30 个，6 个通过、1 个跳过）。

### 6.3 后续工程事项（非本轮人工验收）

- Source Health / Capability Health 与 v1 健康感知切源已落地；自适应探测/自动恢复、跨源一致性与更强 Repair/Replay 仍属于后续工程工作。
- API 限流当前为单实例 fixed-window 基线；Redis 分布式配额、认证/授权、审计持久化与高风险命令审计仍待后续 Operations/Identity 工作包。
- Worker 任务已具备过期租约恢复、跨进程原子领取、持久化退避调度和单任务异常重试基线；TOC 联动正文抓取的事件触发闭环已落地（见 4.x 追更正文闭环），上游修订重扫与可观测告警仍待后续 Operations/Crawling 工作包。
- 用户身份、书架、阅读历史、导入/导出尚未进入产品实现阶段。
- Developer API / Plan / Entitlement / Billing / Organization / Community Marketplace 尚未实现。

## 7. 当前阻塞

当前有两项验收级限制：本机未安装/运行 Docker，完整 PostgreSQL 集成测试无法执行；阅读 3.0 真机流程按用户决定延后。两项均不改变已通过的内存自动化证据，但在 CI/人工证据补齐前不得标记工作包 Completed。

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
