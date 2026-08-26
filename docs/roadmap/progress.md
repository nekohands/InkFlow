# InkFlow 项目进度

> 持续进度账本。状态只以真实代码、测试、Runtime 和 CI 结果为准。

- 产品：墨流 / InkFlow
- 当前阶段：Phase 1A — Single Source Vertical Slice
- 当前工作分支：`dev`（2026-08-25 起）
- 最后更新日期：2026-08-25

## 1. 总体状态

| 阶段 | 状态 | 说明 |
| --- | --- | --- |
| Grill Me / 产品与架构对齐 | ✅ Completed | 产品定位、核心领域、Legado、Source Runtime、安全、商业化和路线已文档化 |
| Repository Bootstrap | ✅ Completed | .NET 10 基础仓库与最初 CI 已建立 |
| Phase 0 — Foundation | ✅ Completed | 模块边界、Persistence、Migration、Outbox/Inbox、OTel、测试与 Runtime CI Gate 已验收 |
| Phase 1A — Single Source Vertical Slice | 🚧 In Progress | Source DSL/Adapter → Crawl → Canonical → Content → API/Legado/Web。已完成的 DSL/Crawler 工作包实现暂存于 `main` 历史，`dev` 上按原顺序重建 |
| Phase 1B — Dual Source Validation | ⏳ Not Started | 双来源匹配、内容版本、质量选优、故障切源 |
| Phase 2 — Multi-Source Production | ⏳ Not Started | 多源生产化、健康评分、自适应追更、规则 Canary |
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

## 6. 已知未完成项

- CSS/XPath/JSONPath 具体 selector 引擎尚未实现。
- Safe HTTP / SSRF 基础防线尚未实现。
- SourceBook / SourceChapter 持久化尚未实现。
- Canonical Matching / Chapter Alignment 尚未实现。
- Content AST / ContentVersion / Quality Engine 尚未实现。
- 正式 Legado API Contract 与 Rule Generator 尚未实现。
- Web Reader 尚未实现。
- 用户身份、书架、阅读历史尚未进入产品实现阶段。
- Developer API / Plan / Entitlement / Billing / Organization 尚未实现。

## 7. 当前阻塞

当前无已知产品级阻塞。

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
