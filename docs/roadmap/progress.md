# InkFlow 项目进度

> 持续进度账本。状态只以真实代码、测试、Runtime 和 CI 结果为准。

- 产品：墨流 / InkFlow
- 当前阶段：1.0 Release Candidate（历史自动化验收不替代当前提交门禁；真实设备、来源与账户验收仍待完成）
- 当前工作分支：`dev`（2026-08-25 起）
- 文档状态：RF-04 Reader 基线与展示层迁移已在 `4ea9dd0` 验收；RF-03 在 `0a6e641`、RF-02 在 `3a38ad1`、RF-01 在 `f90e6bd` 验收。RF-05 在 `51fa810`、RF-07 在 `096eb3e` 验收；选定首轮已收尾，RF-06 条件延期，历史与真实环境待验收项继续保留。
- 最后更新日期：2026-10-24

## RF-07 当前工作包

Accepted / Completed at `096eb3e`（2026-10-24）。RF-01 至 RF-05 的 10 个实现/收尾提交、30 条 workflow、124 个 Job 已核对通过；RF-02 c3347ca 原取消 CI 已在 原 SHA 的 attempt 2 补齐。综合回归 CI 40060934590（4/4）、Docker 40060934577（6/6）、Security 40060934575（4/4）全部 Job 成功；Unit 726、Architecture 53、Contract 12、迁移 11/11，PostgreSQL 136 passed / 3 skipped、Redis 1/1、Runtime/SLO/OTLP/备份 PASS。本地隔离与 PG 各 720 项预算、Developer 810 项、Reader 22 输出/四屏/40 张零像素差异截图通过。初次性能失败、历史取消与本地 Docker 阻塞均保留。选定首轮 RF-01–05/RF-07 已收尾，RF-06 条件延期；项目仍为 1.0 RC，人工/真实来源/生产验收继续开放。详见 [RF-07 记录](../engineering/optimization-refactoring-closeout-2026-10-24.md)，文档收尾 SHA 自身 CI 另行核对。

## RF-05 前序工作包

Accepted / Completed at `51fa810`。行为基线 `4804dc0` 与 PostgreSQL 观察冻结 `9d7c558` 先于生产拆分；自助管理、管理员套餐管理、外部读取已分离，公共 DTO/配额/审计语义不变。810 项响应/账本/SQL 完全等价。CI 39815455574（4/4）、Docker 39815455624（6/6）、Security 39815455651（4/4）全部 Job 通过；Unit 726、Architecture 53、Contract 12、迁移 11/11，真实 PostgreSQL 136 passed / 3 skipped、Redis 1/1、Runtime/SLO/OTLP/备份 PASS。本地 Docker 缺失仍记录为 BLOCKED，人工设备/账户/生产验收继续开放。详见 [RF-05 记录](../engineering/developer-api-refactoring.md)。下一步 RF-07 综合回归与收尾，RF-06 条件项不自动开展；文档收尾提交自身 CI 另行验证。

## RF-04 前序工作包

Accepted / Completed at `4ea9dd0`。先冻结 22 项完整输出与四屏宽浏览器基线，再将 Reader、账户/PWA、Operations 及 16 个嵌入资源迁入 API Presentation，Content 旧实现已删除。CI 39797024080（attempt 2，3/3）、Docker 39797024019（6/6）、Security 39797024110（4/4）全部 Job 成功；Unit 660、Architecture 53、Contract 12，PostgreSQL Integration 136 passed / 3 skipped、Redis 1/1、Runtime/SLO/OTLP/备份均通过。Linux 40 张截图零像素差异，前后各 450 次采样。首次截图时序/抗锯齿噪声问题已修复并保留证据，未改生产输出或一致性预算。本机 Docker 仍不可用；人工设备/账户/PWA/长时间阅读等验收继续开放。详情见 [Reader 基线与展示层迁移](../engineering/reader-presentation-migration.md)。文档收尾 SHA 另行验证，RF-05 已开始行为基线。

## RF-03 前序工作包

复测基线与量化目标已先于生产优化冻结。直接建字典与最多 1,000 条问题示例保留已实现，精确全量计数/排序/公共报告不变。已在 `0a6e641` Accepted / Completed；Unit 659、Architecture 51、Contract 12、真实 PostgreSQL/Redis、Runtime/SLO/备份及 CI/Docker/Security 全部通过。本地 Docker 缺失由远端真实证据补足；证据与限制见 [性能记录](../engineering/consistency-check-performance.md)。真实环境待验收清单继续保留。

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

> **分支说明（2026-08-29）**：项目已切换到 `dev` 分支重新起步，`dev` 为唯一开发主线，完成后经 PR 合入 `main`。Phase 1A/1B、用户产品和商业基础已在 `dev` 上按原设计文档重建；历史自动化 Release Gate 曾通过，本轮提交门禁以下方工作包为准。真实设备、真实来源和真实账户验收仍按第 6 节待定事项执行。

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

### 前序工作包：RF-02 一致性检查职责拆分与测量（2026-10-22，Accepted / Completed）

- 用户授权按既定方案实施 RF-02；[中文实施与测量](../engineering/consistency-check-refactoring.md)、[Wiki 合同与证据](../../repowiki/work-packages/2026-10-22-rf02-consistency.md)。
- 纯快照/报告、报告编排和四组规则迁入 Operations.Application；API 保留 EF 与枚举元数据转换，无新增跨模块依赖，46 处问题输出及查询实现保持不变。
- 本地 Restore/Release Build 零警告通过，Unit 656/656、Architecture 51/51、Contract 12/12、迁移模型 11/11 通过；Integration 8 passed / 128 DockerUnavailable / 3 skipped。本地 Runtime 仍 BLOCKED。
- 规则前后各 720 次采样完整报告等价；远端 PostgreSQL 720 次测量通过。100k 章节读取 500,005 行、13 条 SQL，健康/孤儿 p50 约 1.13/1.36 秒、分配约 448/530 MiB；原始数据与限制已归档，不宣称提速。
- 验收实现 `3a38ad1`：CI 39766807840（attempt 2）、Docker 39766808118、Security 39766807728 全部 Job 通过；Integration 136 passed / 3 skipped，Redis 独立 1/1；Admin/Operations、SLO/OTLP、备份恢复通过。首轮 Ubuntu 索引哈希不一致保留为历史失败，重跑未绕过完整性检查。收尾增加直接一致性报告字段断言，由收尾 CI 执行。
- RF-02 交付时 RF-03 尚未开始；现已复测、冻结目标并实施，当前状态见本文 RF-03 区块。既有真实设备、账户、来源与生产治理验收仍开放。

### 前序工作包：RF-01 架构保护网（2026-10-22，Accepted / Completed）

- 用户在文档方案后明确授权先实施 RF-01；RF-00 保存在 `df6c701`。中文说明：[架构保护网](../engineering/architecture-guards.md)；合同、源码与证据：[RF-01 工作包](../../repowiki/work-packages/2026-10-22-rf01-architecture-guards.md)。
- 已实现 Domain 基础设施、模块到宿主、跨模块 Infrastructure 和显式模块图四类编译依赖规则，覆盖 11 个模块，并检查项目引用/清单。51 项架构测试含正反例与嵌套类型漏检回归；临时新增模块探针证明清单守卫真实失败，清理后恢复通过。
- Restore/Release Build PASS（0 警告/0 错误）；Unit 648/648、Architecture 51/51、Contract 12/12、Migration 模型 11/11 PASS。
- 全量本地 Integration：8 通过、128 因 Docker 初始化不可用失败、3 跳过（两项可选真实源、一项 Redis 配置），本地 Runtime BLOCKED。修复候选 `f90e6bd658ecc657dc68e34b1936990a4ce99555` 的 [CI 39758029891](https://github.com/nekohands/InkFlow/actions/runs/39758029891)、[Docker 39758029869](https://github.com/nekohands/InkFlow/actions/runs/39758029869)、[Security 39758029837](https://github.com/nekohands/InkFlow/actions/runs/39758029837) 全部 Job 通过，head SHA 一致。
- CI Integration 136 通过、3 跳过，Redis 独立配置后 1/1；业务 Runtime、SLO/OTLP、备份恢复均通过。两项可选真实源未运行，不在本包验收范围。
- 四类约束未检出生产违规；已修复 internal 嵌套类型筛选漏检。Docker 原候选 `34cfa8f` 检出 Go 标准库 CVE-2026-111943，Collector Go 1.26.9 → 1.26.10 补丁已通过扫描/发布/拉取与 Runtime；ADR 0012 与安全/架构文档同步。业务、协议、Schema/Migration 不变。
- RF-02 已验收；RF-03 当前状态见本文顶部，RF-04 当前状态见本文顶部，RF-05 当前状态见本文顶部，RF-07 已验收，RF-06 条件延期。真实设备/账户/来源/生产验收仍按下方清单保持开放。

### 前序工作包快照：模块化重构（2026-10-15，Batch 2 Pushed / Remote Gates Incomplete）

以下是当时记录，当前基线以上方 2026-10-22 工作包为准。

- 已完成宿主组合与审计包装器归并；当前继续按模块收敛 Sources 职责。
- Batch 1 统一规则 Search/TOC 列表投影；Batch 2 将选择器分派、CSS、HTML/XML XPath、JSONPath 及共享安全上限分开，保留公共接口和有界解析行为。
- Restore / Release Build 通过；Batch 2 Unit 630/630、Architecture 1/1、Contract 12/12、Sources 相关回归 93/93 通过。Integration/Compose 仍受本机 Docker 不可用阻塞；提交 `49e3515` 的 Docker 在 OTLP collector 镜像 HIGH 漏洞扫描处失败，最终文档 SHA 的 CI/Security 仍运行，未提前视为通过。
- 公共协议、模块依赖、Schema/Migration、调度节奏及 UI 不变；工作包和模块导航已同步。
- 范围与证据：[模块化重构工作包](../../repowiki/work-packages/2026-10-15-module-refactor.md)。已完成历史明细见 [历史归档](progress-history.md)。

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

- 项目结构简化已推送；当前 OTLP Collector 镜像扫描失败阻止应用镜像发布，不能标记 Accepted / Completed。精确提交、工作流链接及日志读取限制见 [工作包](../../repowiki/work-packages/2026-10-15-project-simplification.md)。
- 本机 Docker Engine 不可用，Integration 与 Compose 本地仍 BLOCKED；远端容器验证与本地验证分开记录。
- 真实设备、来源、账户及生产 OTLP/SLO/备份治理仍按第 6 节处理，本轮未接触生产数据或真实账户。
- 旧阻塞状态与早期 VM/镜像复验证据已原样归档至 [进度历史](progress-history.md)，不用于证明当前提交通过。

## 9. 强制维护规则

每完成一个工作包至少记录：Build、Tests、Runtime、CI、验收结果、发现/修复的 Bug、剩余风险、Commit/PR。

禁止把 `Implemented` 当作 `Completed`；CI Pending/Red 时不得声称已验收；修改代码后必须重新执行适用 Gate。

完整流程见 `../engineering/development-workflow.md`。
