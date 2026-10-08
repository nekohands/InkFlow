# InkFlow 项目进度

> 持续进度账本。状态只以真实代码、测试、Runtime 和 CI 结果为准。

- 产品：墨流 / InkFlow
- 当前阶段：1.0 Release Candidate（本轮 Reader 顶部采集/下载/来源状态入口、书籍详情下载入口及来源只读权限已完成本机/VM/浏览器自动化验收，CI/Docker/Security 已通过；人工及其他真实环境验收待定）
- 当前工作分支：`dev`（2026-08-25 起）
- 文档状态：5.76 Matching source-book metadata projection Accepted；5.75 Content-fetch chapter ID projection、5.74 Reading history chapter metadata point lookup、5.73 Source content chapter metadata point lookup、5.72 Reader chapter metadata point lookup、5.71 Source registry page fencing、5.70 Health-probe candidate batching、5.69 Health-probe sample lookup fencing、5.68 Scheduled update-scan page/fan-out fencing、5.67 Source list-result budget fencing 及前序包均已 Accepted；当前无活动工作包，历史记录见 `progress-history.md`。
- 最后更新日期：2026-10-08

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

> 历史归档：Phase 0 验收记录、4.1–5.49 工作包明细与 dev 骨架重建记录见 [progress-history.md](progress-history.md)。

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

### 5.76 Matching source-book metadata projection（本轮，2026-10-08，Accepted）

- 缺口：`CanonicalBookMatchingService.CreateOrMatchAsync` 只需要来源书的 `Title`/`Author` 作为匹配键，但当前调用 `ISourceBookRepository.GetAsync` 并物化全部章节；章节规模增长会放大匹配临界区前的数据库行数和内存。
- Intake：新增按来源书身份读取书名/作者的标量投影，保留缺书分支；匹配路径只消费投影，完整聚合继续留给写入、目录同步、章节映射等调用方。
- 范围边界：不改确认候选快路径、匹配锁、标题/作者归一化、候选创建、目录同步、章节映射、正文抓取、公共 API/Legado、Schema/Migration、缓存、HTTP/重试预算或 `.workbuddy-ai/`。
- 验收：匹配结果、缺书错误、幂等、同名同作者复用、确认候选和取消语义不变；生产路径不调用完整 `GetAsync`，EF 只读取 `source_books` 的标题和作者，并传播取消。
- 实现：新增 `SourceBookMetadata`、兼容旧 test double 的默认回退和 EF `source_books` 标量投影；`CanonicalBookMatchingService.CreateOrMatchAsync` 只读取匹配所需的 Title/Author，完整 `GetAsync` 保留给确认候选及其他明确需要聚合的调用方；新增 Unit 读取计数和 PostgreSQL 投影/缺失/取消回归。
- 本地验证：TDD 红态先证明匹配路径仍调用完整书读取（`expected 0, actual 1`），修复后 focused `CanonicalBookMatchingServiceTests` `3/3`、Unit `623/623`、Architecture `1/1`、Contract `12/12`；Restore/tool restore、Release Build `0 warnings / 0 errors`、迁移模型 `11/11`、`git diff --check` 和 secret audit `0` hits PASS。完整本机 Integration 为 `8 passed / 3 skipped / 125 blocked`，新增 PostgreSQL 回归已编译但执行受 Windows Docker Engine `npipe://./pipe/docker_engine` 不可用阻塞。
- 门禁：实现 SHA `835e0914b84469581f1d2a21a33fa9a65fac09f6` 的 [CI 37753946308](https://github.com/nekohands/InkFlow/actions/runs/37753946308)、[Docker 37753946459](https://github.com/nekohands/InkFlow/actions/runs/37753946459)、[Security 37753946140](https://github.com/nekohands/InkFlow/actions/runs/37753946140) 均与 head SHA 一致并 GREEN；CI 迁移、全量测试、Compose/runtime smoke、SLO、Redis、PostgreSQL backup/restore 和 diagnostics 全部通过，Docker 与 Security 完整门禁通过。
- 状态：Accepted；无公共 Contract、Schema/Migration、缓存或 `.workbuddy-ai/` 变化；当前无活动工作包，下一项重新 intake。工作包明细见 [RepoWiki](../../repowiki/work-packages/2026-10-08-matching-source-book-metadata-projection.md)。

### 5.75 Content-fetch chapter ID projection（本轮，2026-10-08，Accepted）

- 缺口：`ContentFetchChainService.EnqueuePendingContentFetchesAsync` 只需要来源书按目录顺序排列的 `ExternalChapterId`，但当前调用 `ISourceBookRepository.GetAsync` 并物化完整 `SourceBook` 及所有章节标题；章节规模增长会放大追更联动前的数据库行数和内存。
- Intake：新增按来源书身份读取外部章节 ID 的投影查询，保留目录顺序与缺书/空目录返回 `0` 的语义；正文任务链只消费该投影，完整聚合继续留给写入、匹配、映射等调用方。
- 范围边界：不改 SourceCatalog 目录同步、章节映射、SourceContent 正文抓取、任务载荷、健康门控、FetchArtifact 新鲜度、去重/run gate、公共 API/Legado、Schema/Migration、缓存、HTTP/重试预算或 `.workbuddy-ai/`。
- 验收：内容入队的 new/stale/force-refresh、顺序、健康/缺书/空书、去重和 collection-run 语义不变；生产路径不调用完整 `GetAsync`，EF 只投影按 `ChapterIndex` 排序的外部章节 ID；取消向下传播。
- 实现：新增 `ISourceBookRepository.ListChapterIdsAsync` 及 EF `AsNoTracking` 身份 join/标量投影，按持久化 `ChapterIndex` 排序；`ContentFetchChainService` 只消费章节 ID，完整 `GetAsync` 保留给其他聚合调用方；取消令牌向下传递，任务链语义不变。
- 本地验证：TDD 红态先证明内容入队仍调用完整书读取，修复后 focused counter `1/1`、`ContentFetchChainServiceTests` `10/10`；Unit `623/623`、Architecture `1/1`、Contract `12/12`；Restore/tool restore、Release Build `0 warnings / 0 errors`、迁移模型 `11/11`、`git diff --check` 与 added-line secret audit PASS。完整本机 Integration 为 `8 passed / 3 skipped / 124 blocked`，阻塞原因为 Windows Docker Engine `npipe://./pipe/docker_engine` 不可用；新增 PostgreSQL 回归已编译，远端 CI 通过。
- 门禁：候选 SHA `523a305f735fda252dbfcb27dc560f39c930b604` 的 CI `37748826161`、Docker `37748826364`、Security `37748826225` 均与 head SHA 一致并 GREEN；CI 的迁移、全量测试、Compose/runtime smoke、SLO、Redis、PostgreSQL backup/restore 和 diagnostics 通过，Docker 与 Security 完整门禁通过。
- 状态：Accepted；无公共 Contract、Schema/Migration、缓存或 `.workbuddy-ai/` 变化；当前无活动工作包。工作包明细见 [RepoWiki](../../repowiki/work-packages/2026-10-08-content-fetch-chapter-id-projection.md)。

### 5.74 Reading history chapter metadata point lookup（本轮，2026-10-08，Accepted）

- 缺口：`ReadingStateService.ListHistoryAsync` 已将历史记录限制为有界条目，但每条记录仍通过 `ICanonicalBookRepository.GetAsync` 物化整本 `CanonicalBook`，只为取得书名、作者和一个章节元数据；章节规模增长会放大历史列表的数据库行数和内存。
- Intake：复用现有 `CanonicalBookSummary` 和 `GetChapterAsync`，新增按书 ID 的有界摘要点读；历史路径先保持现有可见性/撤下策略，再点读目标章节，保留缺失项跳过、顺序和响应字段。
- 范围边界：不改公共 Reading/Legado 响应、历史上限、撤下策略、写路径、Shelf/Progress 路径、完整聚合调用方、Schema/Migration、缓存、正文选择或 `.workbuddy-ai/`。
- 验收：历史列表不再调用完整 `GetAsync`；生产 EF 使用书籍摘要和目标章节的有界投影；缺书、缺章、跨书和撤下语义不变；取消正确传播。
- 验证计划：先补 ReadingState focused 红绿回归和 PostgreSQL 点查询/身份回归，再执行 Unit、Architecture、Contract、Restore/Release Build、迁移模型、脚本语法、Integration、diff/secret audit 与精确 SHA CI/Docker/Security。
- 实现：`ICanonicalBookRepository.GetSummaryAsync` 复用 `CanonicalBookSummary`；EF 先按书 ID 过滤再投影摘要与相关章节计数；`ReadingStateService.ListHistoryAsync` 改用摘要和既有 `GetChapterAsync`，保留缺失、撤下、顺序、元数据和取消语义，完整 `GetAsync` 继续服务有意需要聚合的调用方。
- 本地验证：TDD 红态先以完整书读取计数器失败；focused 回归 `1/1`、全部 `ReadingStateTests` `8/8`；Unit `623/623`、Architecture `1/1`、Contract `12/12`；Restore/tool restore、Release Build `0 warnings / 0 errors`、迁移模型 `11/11`、`wsl.exe bash -n scripts/verify-migrations.sh`、`git diff --check` 和 added-line secret audit 均 PASS。完整本机 Integration 为 `8 passed / 3 skipped / 123 blocked`，原因是 Windows Docker Engine `npipe://./pipe/docker_engine` 不可用；新增 PostgreSQL 回归已编译，远端 CI 通过，本机聚焦运行同样受该 named pipe 阻塞。
- 门禁：实现候选 SHA `4e918c1df43367a8ed52d9e52d2170c6782266b3` 的 [CI 37744554098](https://github.com/nekohands/InkFlow/actions/runs/37744554098)、[Docker 37744554120](https://github.com/nekohands/InkFlow/actions/runs/37744554120)、[Security 37744554105](https://github.com/nekohands/InkFlow/actions/runs/37744554105) 均 success 且 head SHA 一致。CI 的迁移、全量测试、Compose/runtime smoke、SLO、Redis、PostgreSQL backup/restore 和 diagnostics 通过；Docker 四业务镜像构建/扫描/发布及 Compose 镜像验证通过；Security 的 SBOM/Filesystem/NuGet/CodeQL 通过。CI 首次尝试仅因外部 registry 鉴权瞬态失败，原运行同 SHA 重跑后 GREEN。
- 状态：Accepted；无公共 Contract、Schema/Migration、缓存或 `.workbuddy-ai/` 变化；本机 Docker named pipe 是环境限制，远端容器证据已补足。工作包明细见 [RepoWiki](../../repowiki/work-packages/2026-10-08-reading-history-chapter-metadata-point-lookup.md)。

### 5.73 Source content chapter metadata point lookup（本轮，2026-10-08，Accepted）

- 缺口：`SourceContentService.FetchChapterContentAsync` 在触网前调用 `ISourceBookRepository.GetAsync`，EF 会为定位一个外部章节 ID 物化整本来源书的章节集合；章节规模增长会放大正文抓取前的数据库行数和内存。
- Intake：新增同时表达书存在状态与目标章节的来源章节点查找，正文路径只读取目标章节元数据；保留 `GetAsync` 给目录、同步和其他完整聚合调用方。
- 范围边界：不改公共 API/Legado、Source Adapter、正文/FetchArtifact 语义、缺书/缺章错误文本、Schema/Migration、缓存、durable cursor、HTTP/重试预算或 `.workbuddy-ai/`。
- 验收：正文抓取不再为章节定位调用完整书聚合；缺书和缺章继续返回各自稳定错误；EF 查询按来源书身份和外部章节 ID 有界定位一行并传播取消。
- 验证计划：先补 SourceContentService focused 红绿回归和 PostgreSQL 点查询/跨书回归，再执行 Unit、Architecture、Contract、Restore/Release Build、迁移模型、脚本语法、Integration、diff/secret audit 与精确 SHA CI/Docker/Security。
- 实现：`ISourceBookRepository.GetChapterAsync` 返回保留书存在状态的 `SourceChapterLookup`；EF 以来源书身份和外部章节 ID 做 `AsNoTracking` 左连接点查询；`SourceContentService.FetchChapterContentAsync` 改用点查，缺书/缺章错误与触网前置顺序保持不变。
- 本地验证：TDD 红态为缺少 `SourceChapterLookup` 的预期编译失败；focused `SourceContentServiceTests` `8/8`；Unit `622/622`、Architecture `1/1`、Contract `12/12`；Restore/tool restore、Release Build `0 warnings / 0 errors`、PowerShell 迁移模型 `11/11`、`bash -n scripts/verify-migrations.sh`、`git diff --check` 与 added-line secret audit PASS。完整 Solution Test 的 Integration 为 `8 passed / 3 skipped / 122 blocked`，均在类初始化因 Windows Docker Engine `npipe://./pipe/docker_engine` 不可用；新增 PostgreSQL 点查询回归已编译但未取得本机容器证据。
- 门禁：候选 SHA `5892eca6d754ba53d3a6d496b4d37cb69e387643` 的 [CI 37739646911](https://github.com/nekohands/InkFlow/actions/runs/37739646911)、[Docker 37739647011](https://github.com/nekohands/InkFlow/actions/runs/37739647011)、[Security 37739647021](https://github.com/nekohands/InkFlow/actions/runs/37739647021) 均 success 且 head SHA 一致；CI 的迁移、全量测试、Compose、Reader/Legado/Source smoke、SLO、Redis、PostgreSQL backup/restore 和 diagnostics 通过，Docker 四镜像与发布 Compose 镜像验证通过，Security 的 SBOM/Filesystem/NuGet/CodeQL 通过。
- 状态：Accepted；本机完整 Integration 仍有 122 项因 Windows Docker named pipe 不可用而 blocked，远端门禁补足 PostgreSQL/runtime 证据；无公共 Contract、Schema/Migration、缓存或 durable cursor 变化；`.workbuddy-ai/` 保持未跟踪且未触碰。下一工作包为 5.74 Reading history chapter metadata point lookup。

### 5.72 Reader chapter metadata point lookup（本轮，2026-10-08，Accepted）

- 缺口：`CatalogQueryService.GetChapterContentAsync` 在已完成策略门控和正文版本选择后，为读取一个章节的标题/序号调用 `ICanonicalBookRepository.GetAsync`，EF 会连同该书全部章节一起物化；章节规模增长会放大单章阅读请求的数据库行数和内存。
- Intake：新增按 `canonicalBookId + chapterId` 的章节元数据点查询，阅读路径只读取目标章节；保留 `GetAsync` 给书籍详情/TOC 聚合读取。
- 范围边界：不改公共 API/Legado JSON、TOC/详情契约、ContentVersion 选优、策略门控顺序、Schema/Migration、缓存、durable cursor 或 `.workbuddy-ai/`。
- 验收：正文读取不再为元数据调用完整书聚合；跨书/缺失章节返回 null 元数据并保持既有正文输出；EF 单行查询按书与章节约束并传播取消。
- 实现：`ICanonicalBookRepository.GetChapterAsync` 提供兼容回退；EF 以 `AsNoTracking`、`BookId + ChapterId` 和 `FirstOrDefaultAsync` 单行读取并传播取消；`CatalogQueryService.GetChapterContentAsync` 改走点查询，保留既有策略门控和正文版本选择。
- 本地验证：TDD 红态为点查询计数仍为 `0` 的预期失败，focused `CatalogQueryServiceTests` `12/12`；Restore/tool restore PASS；Release Build `0 warnings / 0 errors`；Unit `620/620`、Architecture `1/1`、Contract `12/12`；PowerShell 迁移模型 `11/11`、`bash -n scripts/verify-migrations.sh`、`git diff --check`、added-line secret audit PASS。完整 Solution Test 的 Integration 为 `8 passed / 3 skipped / 121 blocked`，均在类初始化因 Windows Docker Engine `npipe://./pipe/docker_engine` 不可用；新增 PostgreSQL 点查询回归已编译但未取得本机容器证据。
- 门禁：候选 SHA `bae00c78ac627daef2467bbd3e34c3778dbc4ba8` 的 [CI 37736706191](https://github.com/nekohands/InkFlow/actions/runs/37736706191)、[Docker 37736706174](https://github.com/nekohands/InkFlow/actions/runs/37736706174)、[Security 37736706156](https://github.com/nekohands/InkFlow/actions/runs/37736706156) 均 success 且 head SHA 一致；CI 的迁移、Unit/Architecture/Contract、Compose、reader/runtime smoke、Redis、PostgreSQL backup/restore 与 diagnostics 通过，Docker 四镜像构建/扫描/发布及 Compose 镜像验证通过。
- 状态：Accepted；本机完整 Integration 仍有 121 项因 Windows Docker named pipe 不可用而 blocked，远端门禁补足 PostgreSQL/runtime 证据；不改变公共 API/Legado、Schema/Migration、缓存或 durable cursor；`.workbuddy-ai/` 保持未跟踪且未触碰。当前无活动工作包，下一项重新 intake。

### 5.71 Source registry page fencing（本轮，2026-10-08，Accepted）

- 缺口：`EfSourceRepository.ListAsync` 会一次性物化全部来源及规则文档；`BookDiscoveryService` 和 `SourceBookUrlResolver` 的用户请求会直接枚举完整注册表，来源数量增长时读取内存和请求前置开销无上界。
- Intake：新增按 `Source.Id` 稳定 keyset 分页的来源仓储页查询；搜索发现与直链解析逐页消费，单次只持有固定上限的来源，保留既有来源顺序、禁用过滤、适配器解析、warning、取消和结果语义。
- 范围边界：保留 `ListAsync` 给 Operations Center 等显式全量快照调用；不改变公共 API/Legado、Search/URL 规则、来源健康/适配器预算、Schema/Migration、durable cursor 或 `.workbuddy-ai/`。
- 验收：两个生产调用方不再调用 `ListAsync`；EF 查询按 `Id` 排序、keyset 续页并读取 `limit + 1`；页边界不跳过来源且取消正确传播；其他全量快照调用方行为不变。
- 验证计划：先补多页搜索/直链解析红绿回归和 PostgreSQL 分页/排序/look-ahead/取消回归，再执行 Unit/Architecture/Contract、Release Restore/Build、迁移模型、diff/secret audit、适用 Integration/Runtime 与精确 SHA 的 CI/Docker/Security。
- 实现：`ISourceRepository` 增加 `Source.Id` keyset `SourcePage`；EF 读取固定页加一条 look-ahead；`BookDiscoveryService` 与 `SourceBookUrlResolver` 逐页消费，`OperationsCenter` 保留显式 `ListAsync` 全量快照。
- 本地验证：TDD 红态为缺失分页契约导致的预期编译失败；focused discovery/URL `18/18`、Unit `619/619`、Architecture `1/1`、Contract `12/12`、Restore/tool restore、Release Build `0 warnings / 0 errors`、迁移模型 `11/11`、脚本语法、`git diff --check` 与 changed-file secret audit PASS。完整 Integration `8 passed / 3 skipped / 120 blocked`，新 PostgreSQL 回归已编译但因 Windows Docker Engine `npipe://./pipe/docker_engine` 不可用而未运行。
- 门禁：实现 SHA `fe78305eda8055802bbc65cf95a1e3089a49c4d1` 的 [CI 37733255748](https://github.com/nekohands/InkFlow/actions/runs/37733255748)、[Docker 37733255786](https://github.com/nekohands/InkFlow/actions/runs/37733255786)、[Security 37733255787](https://github.com/nekohands/InkFlow/actions/runs/37733255787) 均 success 且 head SHA 一致；CI 的 PostgreSQL/Redis、备份恢复、runtime smoke 与 diagnostics 均通过。
- 状态：Accepted；分页为请求内固定页读取，不引入来源轮换、缓存或 durable cursor；本机 Integration 仍有 120 项因 Windows Docker named pipe 不可用而 blocked，远端容器门禁补足该证据；真实来源、生产凭据和其他 Release Candidate 人工验收不属于本包；`.workbuddy-ai/` 保持未跟踪且未触碰。当前无活动工作包，下一项重新 intake。

### 5.70 Health-probe candidate batching（本轮，2026-10-08，Accepted）

- 缺口：`HealthProbeService.ProbeDueAsync` 通过 `ListUnhealthyAsync` 全量物化所有 Unhealthy 能力行，来源与能力规模增长后每 10 分钟巡检的读取和探针 fan-out 无上界。
- Intake：本包改为按 `(SourceId, Capability)` 稳定 keyset 读取有限候选页，每批最多 100 行；Scheduler 在进程内保留游标，末页重置，保留既有冷却判断与结果语义。
- 范围边界：不持久化游标，不新增 Schema/Migration，不改变公共 API/Legado、健康策略/冷却算法、due SQL 表达式、探针并发/限流、来源轮换或其他仓储调用方；`.workbuddy-ai/` 保持未跟踪且未触碰。
- 验收：生产查询只读取最多 100 条候选加一条 look-ahead，过滤 Unhealthy、按 `(SourceId, Capability)` 排序并支持 keyset continuation；批次成功才推进游标，末页重置；due/skip/probe/recording/cancellation 语义不变。
- 验证计划：HealthProbeService 红绿批次/游标回归、PostgreSQL 分页/排序/取消回归、Unit/Architecture/Contract、Release Restore/Build、迁移模型、diff/secret audit、适用 Integration/Runtime 与精确 SHA 的 CI/Docker/Security。
- 实现：`ISourceHealthRepository` 增加 `SourceHealthPage` 与 `(SourceId, Capability)` keyset 游标，EF 查询按 `Unhealthy` 过滤、稳定排序并 `Take(limit + 1)`；`HealthProbeService.ProbeDueBatchAsync` 固定每批最多 100 条，保留旧 `ProbeDueAsync` 全量调用语义；Scheduler 成功批次后推进进程内游标，末页重置。同步更新 Source Runtime 与 RepoWiki 架构约束。
- 本地验证：红灯为新批次 API 缺失导致的预期编译失败，随后 focused HealthProbeService `7/7`；Restore PASS；Release Build `0 warnings / 0 errors`；Unit `617/617`、Architecture `1/1`、Contract `12/12`；迁移模型 `11/11`、迁移脚本 `bash -n`、`git diff --check`、changed-file secret scan PASS。完整 Integration `130` 项为 `8 passed / 3 skipped / 119 blocked`，均因 Windows Docker Engine `npipe://./pipe/docker_engine` 不可用；新增 PostgreSQL 回归已完整编译但未取得本机容器证据。
- 门禁：初始候选 `16b34e94bb5fb0214c2765fa2bad79604c5bc603` 暴露共享容器测试夹具假设，已由 `6acb91910db8eb90fb3ea06cfc80031bd4cfbaaf` 隔离起始游标并修复。最终 SHA 的 [CI 37730186762](https://github.com/nekohands/InkFlow/actions/runs/37730186762)、[Docker 37730186799](https://github.com/nekohands/InkFlow/actions/runs/37730186799)、[Security 37730186829](https://github.com/nekohands/InkFlow/actions/runs/37730186829) 均 success 且 head SHA 一致；CI PostgreSQL Integration `127 passed / 3 skipped`，含 Compose/runtime、Redis、备份恢复与 diagnostics。
- 状态：Accepted。游标仍只保存在 Scheduler 进程内，重启重扫与页面内未到期行是明确边界；未新增 Schema/Migration、公共 Contract、due SQL 表达式或探针并发/限流策略；真实来源和其他 Release Candidate 人工验收不属于本包；`.workbuddy-ai/` 保持未跟踪且未触碰。下一工作包重新 intake。

### 5.69 Health-probe sample lookup fencing（本轮，2026-10-08，Accepted）

- 缺口：`HealthProbeService.ProbeTocAsync` 为寻找某来源的一本样本书调用 `ListAllAsync`，主动巡检每 10 分钟会全量物化来源书目。
- Intake：本包改为按 `SourceId` 过滤、按 `(CreatedAt, Id)` 稳定排序并只取一条 chapter-free `SourceBook`；保留既有无样本静默跳过、空目录失败、健康上报、取消和稳定失败原因。
- 范围边界：不分页 unhealthy 健康候选，不新增 Schema/Migration，不改变公共 API/Legado、Source Adapter/HTTP budget、Scheduler interval、重试策略或其他仓储调用方；`.workbuddy-ai/` 保持未跟踪且未触碰。
- 验收：Toc 探针不再调用 `ListAllAsync`；生产 SQL 只返回目标来源的一条确定性样本；无样本、空/非空目录、健康状态、取消和错误分类语义不变。
- 验证计划：HealthProbeService 红绿回归、PostgreSQL first-sample/filter 回归、Unit/Architecture/Contract、Release Restore/Build、迁移模型、diff/secret audit、适用 Integration/Runtime 与精确 SHA 的 CI/Docker/Security。
- 实现：新增 `ISourceBookRepository.FindFirstForSourceAsync`，EF 按 `SourceId` 过滤、按 `(CreatedAt, Id)` 排序并 `FirstOrDefaultAsync`，返回无章节的 `SourceBook`；Toc 健康探针不再调用 `ListAllAsync`；同步确定性 EndToEnd fixture 与相关文档。
- 验证：HealthProbeService focused `6/6`、受影响 EndToEnd `1/1`、Unit `616/616`、Architecture `1/1`、Contract `12/12`、Release Build `0 warnings / 0 errors`、迁移模型 `11/11`、迁移脚本语法、diff/secret audit 均 PASS。Integration 本机为 `8 passed / 3 skipped / 118 blocked`，均因 Windows Docker Engine named pipe 不可用；远端 CI 负责 PostgreSQL/runtime 证据。
- 远端：精确 SHA `62331b47f1e1efd2d4080477b45e15661567e79f` 的 [CI 37724656744](https://github.com/nekohands/InkFlow/actions/runs/37724656744)、[Docker 37724656752](https://github.com/nekohands/InkFlow/actions/runs/37724656752)、[Security 37724656807](https://github.com/nekohands/InkFlow/actions/runs/37724656807) 均 success 且 head SHA 一致。
- 边界：仍使用一本已导入书作为 Toc 样本；本包未加入样本轮换或 unhealthy health 候选分页，`ListAllAsync` 保留给无关边界；`.workbuddy-ai/` 保持未跟踪且未触碰。下一工作包重新 intake。


> 4.1–5.49 历史工作包明细已归档至 [progress-history.md](progress-history.md)，近期记录如下。
### 5.68 Scheduled update-scan page/fan-out fencing（本轮，2026-10-08，Accepted）

- 缺口：`UpdateScanService` 通过 `ListAllAsync` 一次性物化全部 `SourceBook`，随后逐本创建追更任务；书库规模增长会同时放大内存峰值和单轮调度 fan-out。
- Intake：本包将 scheduled update-scan 改为 `(CreatedAt, Id)` 有序 keyset 分页，每轮最多扫描 100 本；调度器在进程内保留游标，扫完后重置，并保留既有健康检查、任务去重和取消语义。
- 范围边界：不持久化游标、不新增 Schema/Migration、不改变 `ISourceAdapter`、公共 API/Legado、Source registry、Worker 队列、重试策略或其他 `ListAllAsync` 调用方；`.workbuddy-ai/` 保持未跟踪且未触碰。
- 验收：单轮不超过 100 本；跨轮游标确定性推进且无跳过/重复，末页后重置；健康不可用、冲突任务和取消语义保持；生产查询不再为 scheduled scan 物化完整书库。
- 验证计划：先补 UpdateScanService 红绿回归和 PostgreSQL repository keyset 回归，再执行 Unit/Architecture/Contract、Release Restore/Build、迁移模型、diff/secret audit、适用 Integration/Runtime 与精确 SHA 的 CI/Docker/Security。
- 实现：`ISourceBookRepository.ListPageAsync` 按 `(CreatedAt, Id)` 提供稳定 keyset 页；`UpdateScanService`/Scheduler 每轮最多处理 100 本，成功批次后推进进程内游标，末页重置；保留健康门控、原子去重和取消语义。无公共契约、Schema 或 Migration 变化。
- 验证：focused UpdateScanService 4/4、受影响 EndToEnd scheduler 1/1、Unit 616/616、Architecture 1/1、Contract 12/12、Release Restore/Build 0 warnings / 0 errors、PowerShell 迁移模型 11/11、diff/secret audit PASS；本机 Integration 8 passed / 3 skipped / 117 blocked（Windows Docker named pipe 不可用）；远端完整测试通过 PostgreSQL keyset 回归及 runtime/Compose 门禁。
- 门禁：初始实现 SHA `a9686fd114c2704360588b81eb64d9c6f35f9df3` 的 CI 暴露旧测试替身未实现分页契约，已由 `83f12e8cdc30837ed6a8309e0288829966065eaf` 修复；最终 SHA 的 [CI 37721216436](https://github.com/nekohands/InkFlow/actions/runs/37721216436)、[Docker 37721216460](https://github.com/nekohands/InkFlow/actions/runs/37721216460)、[Security 37721216368](https://github.com/nekohands/InkFlow/actions/runs/37721216368) 均 success 且 head SHA 一致。
- 状态：Accepted；游标刻意只保存在 Scheduler 进程内，重启重扫与多 Scheduler 协调仍是独立边界；真实来源、生产凭据和 Release Candidate 人工验收不属于本包；当前无活动工作包。

### 5.67 Source list-result budget fencing（本轮，2026-10-08，Accepted）

- 缺口：`SourceRuleExecutionLimits` 已限制请求、响应字节、时间、选择器和结果字节，但 Rule/Code Source 的 Search/TOC 投影仍没有条目数量上限；`SourceCatalogService` 也会接受任意 adapter 返回的完整 TOC 后再同步。
- Intake：本包复用既有执行预算加入有限 `MaxResultItems`，覆盖 RuleBased、Kanunu8、17K 和通用 TOC 同步防线；不改 `ISourceAdapter`、公共 API/Legado JSON、分页语义、Source registry、Schema/Migration、权限或重试策略。
- 验收：正常列表、稳定 ID/顺序和取消语义不变；超过经验证条目预算时 fail-closed，不返回部分结果；超限 TOC 在持久化前被拒绝并返回稳定非敏感错误。
- 验证计划：RuleBased/Kanunu8/17K/SourceCatalogService 红绿回归、完整 Unit/Architecture/Contract、Release Restore/Build、迁移模型检查、diff/secret audit、适用 Integration/Runtime 与精确 SHA 的 CI/Docker/Security。
- 实现：在既有 `SourceRuleExecutionLimits` 中加入经验证的 `MaxResultItems`（默认 10,000）；RuleBased、Kanunu8、17K 的 Search/TOC 超限整体 fail-closed；`SourceCatalogService` 在 health success 和持久化前拒绝超限 TOC。
- 验证：focused adapter/catalog 25/25、Unit 615/615、Architecture 1/1、Contract 12/12、Release Build 0 warnings/0 errors、迁移模型检查 11/11、diff/secret audit PASS；本机 Integration 为 8 passed / 3 skipped / 116 blocked（Windows Docker named pipe 不可用）。
- 门禁：实现 SHA `2853c5e71cb321284113637042a7a91b224b069f` 的 [CI 37717734392](https://github.com/nekohands/InkFlow/actions/runs/37717734392)、[Docker 37717734423](https://github.com/nekohands/InkFlow/actions/runs/37717734423)、[Security 37717734362](https://github.com/nekohands/InkFlow/actions/runs/37717734362) 均 success，含远端 PostgreSQL/runtime/Compose smoke。
- 状态：Accepted；默认上限沿用既有 10,000 章节/包上限，Source registry cardinality、真实来源与 Release Candidate 人工验收仍为独立边界；当前无活动工作包。

### 5.66 Discovery search budget fencing（本轮，2026-10-08，Accepted）

- 缺口：`BookDiscoveryService` 只处理空查询，不限制查询长度、逐源命中处理量或总正典发现结果；用户触发搜索会把适配器返回的全部命中逐条导入与匹配。
- Intake：本包限定在 Crawling 发现编排边界加入查询/工作量上限，并通过既有 `warnings` 报告截断；不改 `ISourceAdapter`、公共 API/Legado JSON、目录分页、Source Runtime parser/HTTP 预算、Schema/Migration、权限或重试策略。
- 验收：超长查询在列出来源/调用适配器前返回稳定 warning；逐源命中和总正典结果均有界；空查询、正常归并、幂等与调用方取消语义保持不变。
- 验证计划：BookDiscoveryService 红绿回归、完整 Unit/Architecture/Contract、Release Restore/Build、迁移模型检查、diff/secret audit、适用 Integration/Runtime 与精确 SHA 的 CI/Docker/Security。
- 实现：查询上限 256，逐源命中上限 100，总正典发现结果上限 100；超限通过既有 warnings 稳定报告，未改变公共响应、适配器、Schema/Migration。
- 验证：Unit 609/609、Architecture 1/1、Contract 12/12、迁移模型检查 11/11、Release Build 0 warnings/0 errors；本机 Integration 为 8 passed / 3 skipped / 116 blocked（Windows Docker named pipe 不可用）。
- 门禁：候选 SHA `193722cffb726c7338128be1f66111369b48a8a9` 的 [CI 37715153756](https://github.com/nekohands/InkFlow/actions/runs/37715153756)、[Docker 37715153801](https://github.com/nekohands/InkFlow/actions/runs/37715153801)、[Security 37715153778](https://github.com/nekohands/InkFlow/actions/runs/37715153778) 均 success 且 head SHA 一致，含远端 PostgreSQL/runtime/Compose smoke。
- 状态：Accepted；Source registry cardinality 与真实来源/Release Candidate 人工验收继续作为独立边界；当前无活动工作包。

### 5.65 Rule selector execution deadline fencing（本轮，2026-10-08，Accepted）

- 缺口：`RuleAdapter` 的 `MaxExecutionTime` 取消令牌已约束 HTTP 与 Credential Provider，但字段、分页和响应变量选择器，以及 `RuleBasedSourceAdapter` 在分页响应收集后的 Search/TOC 列表绑定边界没有检查该令牌；慢选择器可能在预算过期后仍返回成功结果。
- Intake：本包只在既有选择器/提取边界加入 deadline fencing；不增加选择器语法、动态多请求/分支/递归、`MaxDepth`、公共契约、Schema/Migration、凭据存储或重试策略。
- 验收：内部预算过期返回稳定 `execution: time budget exceeded.` 且不暴露 values/bodies；字段、分页、派生变量和 Search/TOC 列表绑定均受保护；调用方取消继续传播；正常 Source fixture 语义不变。
- 实现：`RuleAdapter` 与 `RuleBasedSourceAdapter` 在 selector/extraction/list-binding 边界观察同一 `MaxExecutionTime`，内部超时 fail-closed，调用方取消继续传播；字段、分页、响应变量、Search/TOC 列表回归已补齐。
- 状态：本地 Release Build 0 warnings/0 errors、Unit 605/605、Architecture 1/1、Contract 12/12、11-context migration model check 和 diff/secret audit 通过。Integration 为 8 passed / 3 skipped / 116 blocked（Windows Docker named pipe 不可用）；远端 CI/Docker/Security 与 PostgreSQL/runtime smoke 全部通过。真实来源和 Release Candidate 人工验收不属于本包。
- CI：精确 SHA `8ec53bdbbf5092c70ff83c2b2ba3b22b22796eac` 的 CI `37710808321`、Docker `37710808308`、Security `37710808245` 均 success。

### 5.50 已停止任务重试/取消与已取消任务清理（本轮，2026-09-03）

- 实现：已停止采集运行增加“重试”和“取消”操作；“重试”沿用原有地址回填入口创建新运行，“取消”将 `Stopped` 归类为可清理的 `Cancelled`，不恢复原运行，也不删除书籍或正文。
- 清理：已取消页签增加带理由确认的一键清理；受保护 API 原子删除全部已取消运行及其采集子任务/死信，保留书籍、正文、审计和 Outbox 事实，并记录 `collection.run.cancelled.cleanup` 审计事件。
- 测试：先以缺失接口/类型验证红态，再补充 Domain、Service、Endpoint、PostgreSQL 和前端合同回归；VM Linux SDK 全量 Restore → Build → Test 为 Release Build 0 warnings / 0 errors、Unit `570/570`、Architecture `1/1`、Contract `12/12`、Integration `110 passed / 3 skipped / 0 failed`。
- VM/运行时：隔离 Compose 从源码重建并健康启动；`verify-migrations.sh` 通过 11 个 contexts；`collection-package-runtime-smoke` PASS，实际覆盖停止后取消、已取消批量清理、直接地址采集、ZIP/EPUB/TXT 生成下载、完整性和审计；`reader-frontend-runtime-smoke` PASS。为适配 VM 缓存，隔离验证临时使用 Collector `0.159.0`，生产 Compose/CI 仍为 `0.160.0`。
- 浏览器：内置浏览器在候选 `18080` 端口确认已停止页签出现“取消/重试”、展开状态跨 4.5 秒轮询保持；取消后进入已取消页签并出现“清理已取消任务”及不可恢复确认说明。清理确认未在浏览器中提交，避免误删现场；删除行为已由隔离 HTTP smoke 实际验证。
- CI：`5d64dd2` 的首次 CI 因前端 smoke 夹具漏记新合同字符串失败，已在 `4b896fb` 补齐；`4b896fb` 的 [CI 33734431190](https://github.com/nekohands/InkFlow/actions/runs/33734431190)、[Docker 33734431170](https://github.com/nekohands/InkFlow/actions/runs/33734431170)、[Security 33734431235](https://github.com/nekohands/InkFlow/actions/runs/33734431235) 均已 success。
- 验收边界：Windows Integration 仍因本机 Docker Engine named pipe 不可用而 BLOCKED；未使用真实上游账号、生产令牌、Cookie，阅读 3.0/MuMu 真机及其他明确人工验收继续待定。整体仍为 `1.0 Release Candidate`，不标记 `Accepted/Completed`。

### 5.51 运维操作理由候选与自定义输入（本轮，2026-09-03）

- 实现：复用现有操作确认弹窗，按重放、暂停/恢复/停止/取消、失败/已取消清理、来源、能力和内容政策操作展示 3 个常用理由；默认填入第一项，点击候选可替换理由，文本框仍可直接编辑或自行填写。
- 安全与可访问性：候选项使用键盘可操作的按钮，位于带“常用理由”图例的 fieldset 中；原有 1–512 字符校验、二次确认、权限和审计链路不变，不把理由改为不可编辑的枚举。
- 测试：ReaderHtml 单元回归先红后绿（24/24），前端 smoke 合同回归 PASS；完整本地 Build PASS、Unit `570/570`、Architecture `1/1`、Contract `12/12`，Windows Integration 因 Docker named pipe 不可用 BLOCKED。
- VM/运行时：Ubuntu VM 隔离栈已用 `4600d2b` 重建，API/Worker/Scheduler/PostgreSQL/Redis healthy；`reader-frontend-runtime-smoke` PASS。浏览器已实际打开“取消”理由弹窗，确认 3 个候选、默认理由、候选替换和自定义文本输入，未提交破坏性操作。
- CI：文档提交 `23757d0` 的 [CI 33738529085](https://github.com/nekohands/InkFlow/actions/runs/33738529085)、[Docker 33738530154](https://github.com/nekohands/InkFlow/actions/runs/33738530154)、[Security 33738530652](https://github.com/nekohands/InkFlow/actions/runs/33738530652) 均已 success。
- 验收边界：真实上游、真实凭据、阅读 3.0/MuMu 真机及其他明确人工项目继续待定；整体仍为 `1.0 Release Candidate`，不标记 `Accepted/Completed`。

### 5.52 Reader 采集与书籍包使用权限（本轮，2026-09-03）

- 实现：新增 `OperationsSnapshotRead`、`CollectionUse` 与 `BookPackageUse` 权限；Reader 可创建/查看采集运行、创建/查看/下载 EPUB/TXT/ZIP 书籍包，并读取来源状态快照。来源启停、能力操作、死信、失败/取消清理、内容治理、告警和一致性检查仍由 Operator/Administrator 保护。
- 前端：Reader 进入采集与下载工作面，来源页仅呈现状态和健康信息，不展示来源操作按钮；运营/管理员继续看到完整运维页签和控制入口。Reader 的账号中心链接同步指向采集与下载入口。
- 回归：Release Build 0 warnings / 0 errors；Unit `572/572`、Architecture `1/1`、Contract `12/12` PASS；受影响 shell 合同回归与 `bash -n` PASS。Windows Integration 因 Docker Engine `npipe://./pipe/docker_engine` 不可用 BLOCKED（环境阻塞，不记为代码失败）。
- Ubuntu VM：隔离工作树 `0f55282` 使用源码 Compose 构建并运行，Migration、API、Worker、Scheduler、PostgreSQL、Redis healthy；Reader 账户 runtime smoke 的 Reader 允许/拒绝矩阵 PASS；临时 Reader 实际创建、完成、下载并校验单文件 TXT 书籍包哈希 PASS；`reader-frontend-runtime-smoke` 使用隔离 18080 端口 PASS。手工栈未修改。
- 权限边界：当前采集运行/书籍包仍是平台级任务，没有 `UserId` 归属字段；本轮不把全局暂停/恢复/停止/取消或清理权限下放给 Reader。若后续要求普通用户只能控制“自己发起的任务”，需先增加任务所有权和按用户隔离列表的迁移与回归。
- 状态：代码提交 `0f55282`、文档提交 `a08d9a2` 已推送 `dev`；[CI 33750052745](https://github.com/nekohands/InkFlow/actions/runs/33750052745)、[Docker 33750052746](https://github.com/nekohands/InkFlow/actions/runs/33750052746)、[Security 33750052726](https://github.com/nekohands/InkFlow/actions/runs/33750052726) 均 success 且 head SHA 一致。阅读 3.0/MuMu 真机、真实上游/真实凭据和其他第 6 节人工项目继续待定，整体保持 `1.0 Release Candidate`，不标记 `Accepted/Completed`。

### 5.53 Reader 顶部任务导航、来源状态与书籍详情下载入口（本轮，2026-09-04）

- 实现：Reader 顶部新增独立“采集”“下载”“来源状态”页签；书籍详情新增 EPUB、单文件 TXT、ZIP 格式选择和下载入口。已完成书籍包使用当前登录会话直接下载，排队/运行中跳转下载页签，否则创建下载任务。
- 权限：普通 Reader 可读取来源状态快照但不能调用来源编辑接口；Administrator 可编辑来源状态。既有 Operator 来源级授权模型保持不变，未将普通用户与运维角色混淆。
- 验证：本机 focused ReaderHtml `27/27`、完整 Unit `574/574`、Architecture `1/1`、Contract `12/12`、Release Build `0 warnings / 0 errors`、前端 smoke 与 shell 语法均 PASS；Windows Integration `8 passed / 3 skipped / 102 failed`，全部失败发生于本机 Docker Engine named pipe 不可用，记为环境 BLOCKED。
- Ubuntu VM：隔离工作树 `9233b82` 使用源码 Compose 构建，API/Worker/Scheduler/PostgreSQL/Redis healthy；`reader-frontend-runtime-smoke` PASS；真实 HTTP 角色 smoke 验证首个注册账号为 Administrator、第二个为 Reader、Reader 来源总览 `200`、编辑 `403`、管理员禁用/恢复 `200`；内置浏览器确认三项顶部入口。隔离栈已停止并清理，既有手工栈保留。
- 状态：代码提交 `9233b82`、文档提交 `94ce713` 已推送 `dev`；[CI 33782382242](https://github.com/nekohands/InkFlow/actions/runs/33782382242)、[Docker 33782382191](https://github.com/nekohands/InkFlow/actions/runs/33782382191)、[Security 33782382185](https://github.com/nekohands/InkFlow/actions/runs/33782382185) 均 success 且 head SHA 为 `94ce713`。保持 `1.0 Release Candidate`，不标记 `Accepted/Completed`；阅读 3.0/MuMu 真机、真实上游/真实凭据及其他第 6 节人工项目继续待定。

### 5.54 Identity 令牌重放检测、族吊销与会话保留清理（本轮，2026-10-01）

- 重放检测：已轮换的 refresh token 再次出现判定为重放（含并发轮换竞态分支），事务内吊销整条轮换链（`RevokeSessionFamilyAsync`，visited 防环），对外返回独立错误码 `refresh_token_replay_detected`（401）；`AuditIdentitySecurityEventSink` 将脱敏事件（userId、吊销会话数）写入审计，不含 token、摘要或会话秘密。
- 访问令牌会话耦合：`ValidateAccessTokenAsync` 联查所属会话并要求会话活跃，登出/轮换/族吊销后旧访问令牌立即失效；`RotateRefreshSessionAsync` 轮换时同步吊销旧会话的全部访问令牌行，消除最长 15 分钟的幽灵凭证窗口。
- 保留清理（I-6）：`IdentityRetentionService`（宽限期/批大小/单轮批次上限，配置节 `Identity:Retention`，非法值启动快速失败）由 Worker `IdentityRetentionBackgroundService` 每小时执行；`EfIdentityRetentionStore` 以 `SKIP LOCKED` 取候选、逐会话事务“子表优先”删除；会话删除带 `NOT EXISTS` 护栏，防止外键 CASCADE 把未终态令牌一并带走，且删除谓词与该护栏共同保证“只删终态事实”。
- 数据回填：`AddIdentityRotatedTokenBackfill` Migration 把被吊销/轮换会话下仍为未吊销的访问令牌回填为随会话吊销（幂等、单向、无模型变更），使存量数据与新验证语义一致，也让保留清理可安全判定令牌终态。
- 测试：单元新增重放族吊销、安全事件上报、会话吊销后访问令牌失效、轮换吊销旧令牌等回归；集成（真实 PostgreSQL，远端 CI 容器执行）新增轮换吊销旧令牌行、令牌族全链吊销和保留终态矩阵（含 CASCADE 护栏场景）。
- 文档：`docs/roadmap/progress.md` 与 `docs/handoff/handoff.md` 拆分为“当前记录 + `*-history.md` 历史归档”，`phase-1-acceptance.md` 与 `handoff.md` 中的历史小节引用同步改指归档文件。
- 验证：本机 Restore PASS；Release Build 0 warnings / 0 errors；Unit 583/583、Architecture 1/1、Contract 12/12 PASS；`scripts/verify-migrations.sh` PASS（11 contexts 无漂移）。本机无 Docker，本机 Integration NOT RUN（环境 BLOCKED，与历轮同因）。
- CI 迭代与修复：候选 `a661293` 的远端 CI RED 暴露保留存储实现缺口（删除候选会话之外的终态令牌未被清理，与其声明的按行终态契约不符），以 `ctid` 有界终态清扫修复（`3c67dba`）；同提交 CI RED 再暴露 `reader-account-runtime-smoke` 沿用“旧令牌重用仅普通失效”的旧语义（现重放会吊销整族），脚本改为断言 `refresh_token_replay_detected`、验证族吊销并重新登录后通过。Docker 门禁另发现 ubuntu 基础镜像快照 `libssl3t64` CVE-2026-84782（HIGH），四个发布镜像在最终阶段显式升级修复。最终提交 `beb3e56` 的 [CI 36891052979](https://github.com/nekohands/InkFlow/actions/runs/36891052979)、[Docker 36891052985](https://github.com/nekohands/InkFlow/actions/runs/36891052985)、[Security 36891052989](https://github.com/nekohands/InkFlow/actions/runs/36891052989) 均 success 且 head SHA 一致。

### 5.55 API 宿主全局异常处理中间件（本轮，2026-10-02）

- 缺口（审查项 H1，2026-10-02 在当前代码复核成立）：未捕获异常走 ASP.NET 默认错误页，Development 环境泄露堆栈；宿主无任何全局兜底。
- 实现：`ApiErrorHandlingExtensions` 提供 `AddInkFlowProblemDetails`（写出前剥除 `exceptionDetails` 扩展）与 `UseInkFlowExceptionHandler`（最外层 `UseExceptionHandler`）；Program.cs 接线为首个中间件，任何环境统一返回 `application/problem+json`，不含异常类型、消息、堆栈或路径。审计与 SLO 中间件在内层仍观察原始异常（`unhandled-exception` 审计语义不变）；既有端点稳定错误体（auth 错误码、429、4xx）不重写。
- 测试：新增 TestServer 级回归 2 例——Development 环境注入异常断言 500 + `application/problem+json` 且无 `secret-exception-detail`/`InvalidOperationException`/`exceptionDetails`/`StackTrace` 泄露；正常端点与 404 不被兜底重写。测试依赖新增 `Microsoft.AspNetCore.TestHost`（CPM 固定 10.0.4，仅测试工程引用）。
- 验证：本机 Restore/Release Build 0 warnings / 0 errors；Unit 585/585、Architecture 1/1、Contract 12/12 PASS；Migration 未触及（N/A）。远端提交 `d2cbbca` 的 [CI 37427977446](https://github.com/nekohands/InkFlow/actions/runs/37427977446)、[Docker 37427977438](https://github.com/nekohands/InkFlow/actions/runs/37427977438)、[Security 37427977441](https://github.com/nekohands/InkFlow/actions/runs/37427977441) 均 success（含真实 PostgreSQL Integration 与 Compose/Runtime smoke）。
- 交付结构：同日完成 project-delivery adoption——建立 `repowiki/`（AI 权威参考）与 `docs/delivery/`（profile 迁移 + 索引 + adoption 记录），提交 `507e473` 三 workflow 全绿。

### 5.56 Canonical 匹配入口原子化（本轮，2026-10-06）

- 缺口（审查中危项）：`CanonicalBookMatchingService.CreateOrMatchAsync` 为非原子 check-then-act——并发匹配同一归一化书名/作者时，两路同时看到"无既有正典书"而各自创建，产生重复正典身份，直接威胁"对外 BookId 稳定"不变量；同源书并发路径则依赖 `match_candidates` 唯一索引兜底报 500。
- 实现：`ICanonicalBookRepository` 新增 `BeginTitleAuthorScopeAsync`（匹配互斥作用域，默认无互斥回退供测试替身，沿 ICrawlerTaskRepository 先例）；EF 实现在单个 ReadCommitted 事务内以归一化 (title, author) 的 SHA-256 稳定前缀取 `pg_advisory_xact_lock`（进程间确定性，不依赖 PG 哈希），作用域内同 DbContext 的书/候选写入共享事务。匹配服务重构为临界区：快路径（既有 Confirmed 候选）无需互斥；进入作用域后锁内复查候选（双检）再创建/挂接，书与候选原子提交；异常整体回滚。
- 测试：新增真实 PostgreSQL 并发回归 `CanonicalMatchConcurrencyTests`——8 路并发匹配两个空白变体来源书（"同一本书/烽火戏诸侯"），断言恰好 1 个正典书、全部结果同一 BookId、2 个候选、至多 1 次真正创建。候选提交 `7b9553a` CI RED 暴露测试夹具缺陷（并行任务逐上下文 `Migrate()` 竞争 `__EFMigrationsHistory`），改为类初始化迁移一次后修复（`8eb9162`）。
- 验证：本机 Restore/Release Build 0 warnings / 0 errors；Unit 585/585、Architecture 1/1、Contract 12/12 PASS；无 Schema/Migration 变更。远端提交 `8eb9162` 的 [CI 37437471071](https://github.com/nekohands/InkFlow/actions/runs/37437471071)、[Docker 37437470761](https://github.com/nekohands/InkFlow/actions/runs/37437470761)、[Security 37437470720](https://github.com/nekohands/InkFlow/actions/runs/37437470720) 均 success（含真实 PostgreSQL Integration 与 Runtime smoke）。
- 边界：`FindByTitleAuthorAsync` 的全表内存加载（审查另列的性能项）本轮未动，保持精确 C# 归一化语义；后续以持久化归一化键 + 索引单独立项。

### 5.57 死信与任务状态同事务（本轮，2026-10-06）

- 缺口（审查中危项）：`CrawlerTaskProcessor.FailTaskAsync` 把死信行与 DeadLettered 任务终态分两次 SaveChanges 提交——中间崩溃/写失败会留下"有死信无终态"（修复视图与任务状态漂移）或"有终态无死信"（无法重放）的半一致状态。
- 实现：`ICrawlerTaskRepository` 新增 `AddDeadLetterWithTaskAsync`（默认顺序两写回退供测试替身，生产必须覆写，沿仓库既有先例）；EF 实现以单个 ReadCommitted 事务同时提交死信行与任务终态，任一写失败整体回滚；`CrawlerTaskProcessor` 死信路径改用原子方法。
- 测试：新增真实 PostgreSQL 回归两条——死信+终态单事务同时可见；任务行缺失时死信写入整体回滚（旧两段式会先提交死信行留下漂移，单事务实现断言死信行不存在）。
- 验证：本机 Release Build 0 warnings / 0 errors；Unit 585/585、Architecture 1/1、Contract 12/12 PASS；无 Schema/Migration 变更。远端提交 `8a8fcd1` 的 [CI 37469389070](https://github.com/nekohands/InkFlow/actions/runs/37469389070) 与 [Security 37469389126](https://github.com/nekohands/InkFlow/actions/runs/37469389126) GREEN；Docker 首跑在镜像构建/扫描（0 漏洞）成功后遇 GHCR 推送瞬时 `unknown blob`，定位为 registry 侧抖动并重跑该 job 后 [Docker 37469389018](https://github.com/nekohands/InkFlow/actions/runs/37469389018) GREEN。

### 5.58 Inbox/Outbox 批次租约续约（本轮，2026-10-06）

- 缺口（审查中危项）：Outbox 投递与 Inbox 消费都以短租约整批领取后串行处理，批次耗时超过租约时长时，后半段消息在处理途中租约过期，会被其他实例重复领取/投递（仅靠幂等消费兜底，浪费且放大竞态窗口）。
- 实现：`IOutboxStore`/`IInboxStore` 新增 `ExtendLeaseBatchAsync`（默认无操作回退供测试替身，生产必须覆写）；`EfMessagingMessageStore` 以单语句 UPDATE 只续约仍由同一 owner 持有且未达终态的行（Outbox 要求 ProcessedAt 为空；Inbox 还要求 DeadLetteredAt 为空），owner 不匹配的行不会被续约；`OutboxDispatcher` 与 `InboxConsumerPump` 在处理每条消息前续约整批剩余租约。
- 测试：单元新增 dispatcher/pump 续约接线断言（每条消息前一次续约、ids/owner/时长正确）；集成（真实 PostgreSQL）新增两条——Inbox/Outbox 各验证"原租约过期后经续约不可被其他 owner 领取 + 终态行不参与续约"。候选 `e2b4e6a` CI RED 暴露 Outbox 领取无类型过滤会捞到共享容器中此前用例的遗留行，测试断言收窄到本用例消息后修复（`1658b87`）。
- 验证：本机 Release Build 0 warnings / 0 errors；Unit 587/587、Architecture 1/1、Contract 12/12 PASS；无 Schema/Migration 变更。远端提交 `1658b87` 的 [CI 37478901744](https://github.com/nekohands/InkFlow/actions/runs/37478901744)、[Docker 37478902070](https://github.com/nekohands/InkFlow/actions/runs/37478902070)、[Security 37478901915](https://github.com/nekohands/InkFlow/actions/runs/37478901915) 均 success（含真实 PostgreSQL Integration 与 Runtime smoke）。
- 边界：单条 Handler 执行超过完整租约时长的中途续约本轮未实现（仍由幂等消费兜底），已记录为后续可选加固。

### 5.59 Catalog 查询分页与 N+1 收敛（本轮，2026-10-07，Accepted）

- 缺口：`CatalogQueryService.ListBooksAsync` 原先先全量读取书目，再逐本读取 Content Policy 和完整章节聚合，Developer API 又在全部加载后才 `Take(limit)`，形成 `3N+1` 读取和无效放大。
- 实现：Library 增加有界 `CanonicalBookSummary` 投影，EF 在单个查询中返回书名、作者和章节数；Content Policy 增加按给定 BookId 批量读取最新决策，EF 单查询完成当前状态派生；Catalog 列表/搜索默认有界，Developer `limit` 下推到服务/仓储边界，响应字段和下架语义不变。
- 测试：Unit 新增有界列表、无完整聚合读取、批量策略读取与搜索候选顺序回归，聚焦 `CatalogQueryServiceTests` `11/11`、全量 Unit `590/590`；Architecture `1/1`、Contract `12/12`、Release Build 0 warnings / 0 errors；本机 Testcontainers 因 `npipe://./pipe/docker_engine` 不可用，但远端 CI `37621536608` 完成 PostgreSQL/Runtime 验证。
- 代码候选：`a700977`（有界摘要读取）、`771f027`（批量策略与 limit 下推）、`c0d79df`（搜索先匹配后限量）；无 Schema/Migration 变更。
- 结论：远端 CI `37621536608`、Docker `37621536753`、Security `37621536604` 均对 `9783b7d` 成功；5.59 已 Accepted。下一工作包重新 intake，游标续页与全文检索仍是明确非目标。

### 5.60 Entitlement actor validation（本轮，2026-10-07，Accepted）

- 缺口：`EntitlementService.AssignAsync` 原先只拒绝空 `actorId`；虽然 API 路由已有 Administrator policy，直接调用应用服务仍可传入任意非空操作者。
- 实现：Billing 新增 `IsActiveAdministratorAsync` 端口；服务在目标用户/计划仓储读取和赋值持久化前检查操作者；API 组合适配器从 Identity 用户记录派生 active Administrator；不符合条件统一返回 `403 entitlement_management_forbidden`。既有路由、载荷、reason、命令审计和数据模型不变。
- 测试：先建立红态回归，再实现并验证；聚焦 Commercial/Identity `18/18`、全量 Unit `592/592`、Architecture `1/1`、Contract `12/12`、Release Build `0 warnings / 0 errors`；Windows 迁移模型检查 11/11，脚本语法 PASS。
- 远端：`d0413f2` 的 [CI 37638597477](https://github.com/nekohands/InkFlow/actions/runs/37638597477)、[Docker 37638597479](https://github.com/nekohands/InkFlow/actions/runs/37638597479)、[Security 37638597492](https://github.com/nekohands/InkFlow/actions/runs/37638597492) 均 success，含 PostgreSQL/runtime 验证。
- 边界：本机 Billing Testcontainers 因 `npipe://./pipe/docker_engine` 不可用而 BLOCKED；真实账户、真实来源和其他 Release Candidate 人工验收不属于本包。下一工作包需重新 intake，候选为乐观并发和适配器正则/读取边界。

### 5.64 Crawler handler lease renewal（本轮，2026-10-08，Accepted）

- 缺口（5.58 已明确记录）：Crawler Task 创建事件/轮询领取使用两分钟租约，但单条 Handler 执行超过完整租约时长没有中途续约；仅靠 Inbox 幂等会让另一个 Worker 在执行中回收同一任务，放大重复抓取竞态。
- Intake：本包限定为 `CrawlerTask` active lease 的 owner/status/expiry 条件续约、独立 DI scope 的 Processor heartbeat 与 lease-loss cancellation；不改变任务重试预算、公共契约、Schema/Migration 或真实来源策略。
- 验收：未过期的当前 owner 可获得新 expiry；过期、终态或 owner 不匹配不续约；长 Handler 在半租约前续约并正常完成；续约丢失时取消 executor 且不以旧 owner 写入成功/失败终态。
- 实现：`CrawlerTask.RenewLease` 保持 active lease 不变量；`ICrawlerTaskRepository.TryRenewLeaseAsync` 保留测试替身回退；EF 以任务 ID、owner、活动状态和数据库 expiry 条件 UPDATE 原子续约；Processor 使用独立 DI scope 的 `PeriodicTimer` heartbeat，续约失败取消 executor 且不写旧 owner 终态。
- 测试：先红后绿；聚焦 CrawlerTask `21/21`、全量 Unit `600/600`、Architecture `1/1`、Contract `12/12`；新增真实 PostgreSQL owner/status/expiry 回归，但本机因 `npipe://./pipe/docker_engine` 不可用而 BLOCKED。
- 本地验证：`dotnet restore InkFlow.sln` PASS；Release Build `0 warnings / 0 errors`；PowerShell 等价 11 contexts migration model check PASS；`git diff --check`、security/architecture review PASS。WSL shell wrapper 未执行成功，原因是 WSL 内无 `dotnet`。
- 远端：精确 SHA `11a493a2da72dba38072cad1cbc142711654fc0d` 的 [CI 37707275909](https://github.com/nekohands/InkFlow/actions/runs/37707275909)、[Docker 37707275881](https://github.com/nekohands/InkFlow/actions/runs/37707275881)、[Security 37707275873](https://github.com/nekohands/InkFlow/actions/runs/37707275873) 均 success，含 PostgreSQL/runtime、Redis、backup/restore 和 diagnostics。
- 边界：本机 Docker/Testcontainers 与本地 Docker Compose runtime 仍 BLOCKED；真实账户、真实来源和其他 Release Candidate 人工验收不属于本包。下一工作包重新 intake。

### 5.63 Canonical match query bounding（本轮，2026-10-08，Accepted）

- Intake：5.56 已明确 `FindByTitleAuthorAsync` 会把 `library.books` 全表物化后再执行 Book Matcher v1 的归一化比较；本包只收敛该 PostgreSQL 读路径。
- 范围：使用参数化 PostgreSQL 归一化谓词、确定性首条排序和 `LIMIT 1`；保持去空白/忽略大小写语义、稳定 BookId、测试替身和所有公开契约不变。
- 非目标：不增加 Schema/Migration，不改变匹配政策，不清理重复正典，不引入全文检索、分页 API 或 UI。
- 验收：匹配/未命中回归；SQL 不再全表物化且有 `LIMIT 1`；参数化谓词、确定性排序和原有去空白/忽略大小写语义保持不变。
- 实现：Npgsql 读路径改为参数化 PostgreSQL `translate` 归一化谓词，按 `CreatedAt, Id` 确定性排序并 `LIMIT 1`；增加真实 PostgreSQL SQL/行为回归，未改 Schema、Migration 或公开契约。
- 本地验证：Restore/Release Build 0 warnings / 0 errors；Unit `597/597`、Architecture `1/1`、Contract `12/12`；Windows 迁移模型检查 `11/11`。聚焦 PostgreSQL Integration 本机因 Docker named pipe 不可用而 BLOCKED。
- 远端：精确 SHA `2f20125806b1bc3464fc5d2e299d75f5623ad7a0` 的 [CI 37661014830](https://github.com/nekohands/InkFlow/actions/runs/37661014830)、[Docker 37661014858](https://github.com/nekohands/InkFlow/actions/runs/37661014858)、[Security 37661014826](https://github.com/nekohands/InkFlow/actions/runs/37661014826) 均 success，含 PostgreSQL/runtime smoke；首个候选的测试夹具碰撞已由 `2f20125` 隔离修复。

### 5.62 Private book optimistic concurrency（本轮，2026-10-08，Accepted）

- Intake：将“乐观并发”收窄为 Private Library 私有书目元数据更新；`PrivateBook` 增加从 1 开始的单调 `Version`，PUT 提交当前版本，过期版本返回 409；不扩展到 Canonical/Source/Identity/Reading 等其他聚合。
- 实现：`PrivateBook.Version`、版本化 View/PUT contract、稳定 `private_book_version_conflict` 409、owner-scoped `UserId+BookId+Version` 原子更新、existing rows default 1 的 Migration，以及 Unit/Contract/Integration 回归。
- 验收：create/get/list/import 的初始版本为 1；当前版本更新只成功一次并递增；缺失/非正版本拒绝；过期版本不改 title/author；同版本并发写仅一个成功。
- 本机验证：Release Build 0 warnings / 0 errors；Unit 597/597、Architecture 1/1、Contract 12/12、聚焦 Library/API 13/13；迁移模型检查 11/11。Integration 实际执行 125 项（8 passed / 3 skipped / 114 failed），失败均为 Windows Docker named pipe 不可用，记录为环境 BLOCKED。
- 远端验证：精确 SHA `9610b76fa14b572da4a3203c9047ec0a9ae2d8e0` 的 [CI 37655196433](https://github.com/nekohands/InkFlow/actions/runs/37655196433)、[Docker 37655726362](https://github.com/nekohands/InkFlow/actions/runs/37655726362)、[Security 37655196520](https://github.com/nekohands/InkFlow/actions/runs/37655196520) 均 success，包含 PostgreSQL/runtime/Compose smoke 与 CodeQL。
- 边界：本机 Docker/Testcontainers 仍 BLOCKED；真实账户、真实外部来源和其他 Release Candidate 人工验收不属于本包；下一工作包需重新 intake。

### 5.61 Code adapter response and regex bounds（本轮，2026-10-08，Accepted）

- 缺口：可信 `Kanunu8` CodeAdapter 使用 `GetByteArrayAsync`，静态 HTML 提取正则没有有限超时；`17K` CodeAdapter 使用 `ReadAsStringAsync`，未复用 RuleAdapter 的响应体字节上限，存在解码/解析前的无界内存与 CPU 风险。
- 实现：Sources Application 新增共享 `SourceResponseReader`，保留 `Content-Length` 预检并对未知长度流式 fail-closed；ProductionSafeSourceHttpClient、Kanunu8、17K 均在解码/解析前复用 `MaxBytes`。Kanunu8 正则使用同一注册策略的 `MaxRegexTime`，超时返回失败/空结果，不泄漏部分结果；保留 SSRF、allowed-host、identifier、VIP 和既有 `ISourceAdapter` 合约。
- 测试：先建立红态构造器/边界回归，再实现；聚焦 adapter/HTTP bounds `13/13`、全量 Unit `595/595`、Architecture `1/1`、Contract `12/12`、Release Build `0 warnings / 0 errors`；Windows 迁移模型检查 `11/11`，`wsl.exe bash -n scripts/verify-migrations.sh` PASS。
- 本机 Integration：实际执行 `124` 项，`8 passed / 3 skipped / 113 failed`；失败均由 Docker Engine `npipe://./pipe/docker_engine` 不可用触发，记录为环境 BLOCKED，不作为代码失败结论。
- 安全/文档：staged diff/敏感模式扫描 PASS；同步 `docs/architecture/source-runtime.md`、`repowiki/architecture.md`、工作包、工作流档案和本页/交接页；无 Schema/Migration、公共适配器契约或阶段退出变化。
- 远端：代码候选 `0d7d5ce` 已推送 `dev`；[CI 37650394342](https://github.com/nekohands/InkFlow/actions/runs/37650394342)、[Docker 37650394297](https://github.com/nekohands/InkFlow/actions/runs/37650394297)、[Security 37650394257](https://github.com/nekohands/InkFlow/actions/runs/37650394257) 均 success 且 head SHA 为 `0d7d5ce426b0b42840505168bedf07aa2367df2a`，含 PostgreSQL/runtime/Compose smoke。
- 边界：本机 Docker runtime/Testcontainers 仍 BLOCKED；未运行真实外部来源，未使用真实账户、生产令牌或 Cookie；下一工作包需重新 intake，候选为乐观并发。

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
- 用户身份的基础认证/授权与受保护 Repair 入口已落地；Reading State v1 后端、Reader/PWA 用户状态 v1（账户/书架/历史/进度/偏好接入、公开安装壳）、Personal Legado Token v1、Web Reader v1 和 Private Library v1/v2 自动化基础已落地。PWA Service Worker/离线壳已在 4.82 通过 localhost 安全上下文自动验收；真实安装、账户/跨设备体验、私有内容真实账户/文件端到端验收和公共路径隔离验收仍未完成。Identity 令牌重放检测、族吊销与会话/令牌保留清理已接线（5.54）；API 宿主全局异常处理中间件已在 5.55 完成，响应不泄露异常细节。
- Developer API / Plan / Entitlement / Billing v1 已实现候选基线；Organization、支付、OAuth、sandbox、Community Marketplace 和管理型 Developer API 尚未实现。

## 7. 当前阻塞

最新状态（2026-10-08）：审查高危项全部关闭（H2→5.54 重放/族吊销，H1→5.55 全局异常处理，H3→5.52/ADR 0028 既定边界）；5.56–5.62 已分别关闭 Canonical 匹配入口原子化、死信与任务状态同事务、Inbox/Outbox 批次租约续约、适配器边界、Entitlement actor 校验和 PrivateBook 乐观并发缺口。本机无 Docker，Integration 继续由远端 CI 的 PostgreSQL 容器执行。`progress.md`/`handoff.md` 已拆分为当前记录与历史归档，交付工作流结构已 adoption（`repowiki/` + `docs/delivery/`）。下一工作包需重新 intake，候选应重新按风险和证据缺口排序。

当前仍有以下验收级限制：Windows 开发机 Docker Engine 不可用，受影响的本机 Testcontainers 仍为 BLOCKED；内置浏览器直接读取部分 VM JSON API、Manifest、Service Worker 资源仍可能被 `ERR_BLOCKED_BY_CLIENT` 拦截；本轮只使用一次性 `.invalid` Web Reader 测试账号和临时令牌，未使用真实外部账户、生产密码或 Personal Legado Token。阅读 3.0/MuMu、真实账户/PWA 安装与跨设备、真实追更与真实第二来源、真实凭据/Provider、受保护 Operations/Content Policy/Source Authorization/Admin Audit 的人工操作、linovelib/17K 真实链路，以及生产 OTLP/SLO/告警/备份治理继续按第 6 节处理。令牌浏览器撤销按钮未在未确认情况下点击；整体仍保持 `1.0 Release Candidate`，不标记 `Accepted/Completed`。

以下为历史复验记录，仅用于追溯，不代表当前最新测试数字：

历史复验记录（4.64）：此前本机 Restore、Release Build、Unit 472/472、Architecture 1/1、Contract 10/10 和迁移模型检查通过；完整 Integration 因本机 Docker Engine 不可用而部分 BLOCKED。代码候选 `acbbd10dd67e350f2bf6b2ae1080c54f7b725d91` 的远端 [CI 33290137667](https://github.com/nekohands/InkFlow/actions/runs/33290137667)、[Docker 33290137676](https://github.com/nekohands/InkFlow/actions/runs/33290137676)、[Security 33290137668](https://github.com/nekohands/InkFlow/actions/runs/33290137668) 均 GREEN。

历史复验记录（早期 Ubuntu VM）：Linux SDK 容器曾执行完整 `Restore → Build → Test`，源码构建 Compose、Core SLO 和备份恢复通过；验证后停止容器并保留 PostgreSQL/Redis 卷。

历史复验记录（GHCR 发布镜像）：默认 GHCR Compose 曾在 Ubuntu VM 拉取镜像并通过 Migration、服务健康、Core SLO、脚本回归和备份恢复；验证后停止容器并保留数据卷。

历史复验记录（ContentVersion）：Ubuntu VM 曾以 PostgreSQL Testcontainers 验证跨章节目标拒绝、原当前版本保留和同章节唯一当前版本；测试容器已清理。

## 9. 强制维护规则

每完成一个工作包至少记录：Build、Tests、Runtime、CI、验收结果、发现/修复的 Bug、剩余风险、Commit/PR。

禁止把 `Implemented` 当作 `Completed`；CI Pending/Red 时不得声称已验收；修改代码后必须重新执行适用 Gate。

完整流程见 `../engineering/development-workflow.md`。
