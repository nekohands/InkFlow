# InkFlow 优化与重构方案

日期：2026-10-22。状态：`Planned`，文档先行，尚未实施本方案中的代码工作包。

本轮需求：优化重构项目，先出文档。本文提供现状审查、推荐方向、实施顺序和验收计划。Agent 合同与证据入口为 [RepoWiki 工作包](../../repowiki/work-packages/2026-10-22-optimization-refactoring-plan.md)；本文是对应的中文工程方案。

## 1. 目标与结论

本轮优化目标是降低维护和验证成本、减少职责交叉，并为已有规模风险建立测量依据。继续使用模块化单体，优先完成以下工作：

1. 补齐真正约束依赖方向的 Architecture Tests，建立重构保护网。
2. 分离一致性检查的规则、编排和数据库适配；测量全量快照成本后再实施有界查询。
3. 将 Reader、账户/PWA、Operations 页面归入 API 展示层，隔离内容业务与浏览器实现。
4. 按认证和使用场景整理 Developer API，保持配额、审计和错误行为。

Sources 执行器、Crawling 调度协调、Reading 用例和内容包已经有较多职责拆分，先保留并验证，不重复执行旧计划。消息持久化和复杂解析器只有在明确收益与回归条件满足后才进入后续工作包。

本次只修改方案、索引和交付状态文档；不修改业务代码、测试、依赖、数据库、CI 或部署配置，不执行代码重构、提交、推送或发布。后续实施应另行启动相应工作包。

## 2. 已核实的基线

### 2.1 仓库与证据

| 项目 | 2026-10-22 核查结果 |
| --- | --- |
| 分支 / HEAD | `dev` / `ca15cadbe1df97adf99150df0c9d3d7bc95e32af` |
| 工作树 | 开始审查时干净；本次输出为未提交文档 |
| 本地远端跟踪引用 | `origin/dev` 指向同一 SHA；这不代替远端分支实时查询 |
| 生产代码范围 | `src/` 下 23 个项目：4 个宿主、11 个模块、6 个 BuildingBlocks、2 个适配器；399 个非 Migration C# 文件 |
| CodeGraph | 本次检查 `.codegraph/` 不存在，使用定向文本与源码检查；10 月文档中的“存在”是历史观察 |
| 性能测量 | 本轮未执行负载、内存、SQL 执行计划或浏览器性能基准；下文区分结构事实和性能假设 |
| 本机 Docker | 本轮未重新验证；旧记录中的环境阻塞不作为当前实测结论 |

通过 `gh run list --commit <SHA>` 与 `gh run view <run-id> --json headSha,status,conclusion,jobs` 核对了以下 **基线提交** 的运行及所有 Job，均为 `completed/success`：

| Workflow | 运行证据 | 本次用途 |
| --- | --- | --- |
| CI | [39240297876](https://github.com/nekohands/InkFlow/actions/runs/39240297876) | 确认基线已有 Restore、Build、Test、Migration、Compose 和业务 smoke 等 CI 证据 |
| Docker | [39240297898](https://github.com/nekohands/InkFlow/actions/runs/39240297898) | 5 个镜像构建/扫描/发布 Job 及发布镜像拉取验证通过，含 OTLP Collector |
| Security | [39240298102](https://github.com/nekohands/InkFlow/actions/runs/39240298102) | CodeQL、文件系统扫描、SBOM、NuGet 审计 Job 通过 |

这些结果属于 2026-10-19 运行的 `ca15cad`，于 2026-10-22 重新查询确认。它们不表示本方案已实施、当前文档已经过 CI，或真实设备/生产环境验收已完成；也不推断扫描范围之外不存在风险。

### 2.2 与上一轮重构的关系

[10 月 15 日工作包](../../repowiki/work-packages/2026-10-15-module-refactor.md) 记录停在 Batch 2。当前源码和后续 Git 提交表明：

- [RuleAdapter](../../src/Modules/InkFlow.Modules.Sources/Application/RuleAdapter.cs) 已将 preflight、凭据解析、HTTP、前置请求、分页、字段抽取和请求预算交给内部实现；当前入口为 189 行。旧计划的“下一步拆执行器”不能直接沿用。
- [CrawlerTaskProcessor](../../src/Modules/InkFlow.Modules.Crawling/Application/CrawlerTaskProcessor.cs) 已区分运行协调、执行、失败和结果处理；发现、入队与采集运行也已有内部职责单元。
- [ReadingStateService](../../src/Modules/InkFlow.Modules.Reading/Application/ReadingStateService.cs) 已组合书架查询/命令、历史、进度、偏好和可见性；[CatalogQueryService](../../src/Modules/InkFlow.Modules.Content/Application/CatalogQueryService.cs) 与 [BookPackageService](../../src/Modules/InkFlow.Modules.Content/Application/BookPackageService.cs) 也已拆分部分内部职责。
- 当前 Docker 门禁已通过。旧 SHA 的 OTLP 扫描失败仍是历史事实，但不能继续作为当前 HEAD 的阻塞项，也不能反向改写旧运行的失败结果。

本方案接续当前源码。旧批次的验收账本仍需逐项对照；本轮不会仅凭当前 CI 绿色就宣称整轮历史重构已 Accepted。

## 3. 审查发现与优先级

行数仅用于定位，均对应上述基线；不设“每文件必须小于多少行”的重构指标。

| 编号 | 依据与发现 | 影响与优先级 | 推荐处理 |
| --- | --- | --- | --- |
| F-01 | [ModuleBoundarySmokeTests](../../tests/InkFlow.ArchitectureTests/ModuleBoundarySmokeTests.cs) 只有程序集加载测试，注释中的真实分层约束尚未实现 | P0：现有 Architecture GREEN 不能证明 Domain、Host 和跨模块持久化边界受到自动保护 | RF-01 增加可检出非法依赖的测试 |
| F-02 | [ReaderHtml.cs](../../src/Modules/InkFlow.Modules.Content/Application/ReaderHtml.cs)、[UserPages](../../src/Modules/InkFlow.Modules.Content/Application/ReaderHtml.UserPages.cs)、[Operations](../../src/Modules/InkFlow.Modules.Content/Application/ReaderHtml.Operations.cs) 分别 1,135 / 1,197 / 1,936 行，共 4,268 行；HTML、CSS、JS、会话和 PWA 行为放在 Content.Application | P1：展示职责与内容业务同模块，修改及测试定位成本高；不是已测得的浏览器性能故障 | RF-04 明确展示所有权，保持输出行为 |
| F-03 | [ConsistencyCheck.cs](../../src/Apps/InkFlow.Api/ConsistencyCheck.cs) 共 881 行，含快照 DTO、服务、纯规则和 EF 适配器；读取 13 组完整元数据集合，再生成全部问题，最后 `Take(1000)` | P1：响应上限没有约束输入快照及问题中间集合；规模风险已定位，生产延迟/内存影响尚未测量 | RF-02 分离职责并建立基准，RF-03 按证据优化 |
| F-04 | [DeveloperEndpoints.cs](../../src/Apps/InkFlow.Api/DeveloperEndpoints.cs) 共 904 行，混合自助管理、管理员套餐操作、外部只读目录、DTO、配额结果和审计 | P1：三类调用者的策略容易在修改时交叉；当前已存在配额公共 helper | RF-05 按协议/使用场景整理，复用既有 helper |
| F-05 | [MessagingStore.cs](../../src/BuildingBlocks/InkFlow.BuildingBlocks.Persistence/MessagingStore.cs) 共 826 行，同一实现承载 Outbox、Inbox、传输、死信查询和 retention 五个接口 | P2 候选：职责集中，但事务/租约风险高；行数不能证明需要拆分 | RF-06 先验证改动收益和真实 PostgreSQL 回归能力 |
| F-06 | [SourceRuleDslValidator](../../src/Modules/InkFlow.Modules.Sources/Domain/SourceRuleDslValidator.cs) 883 行；[HtmlXPathSelectorEvaluator](../../src/Modules/InkFlow.Modules.Sources/Infrastructure/HtmlXPathSelectorEvaluator.cs) 870 行 | P2 候选：复杂度部分来自真实安全约束；DSL 已委托分页校验，选择器语言已经分离 | 保留安全边界；只有重复规则或定位成本得到证据支持才继续整理 |
| F-07 | Wiki、Progress、Handoff 和 profile 的当前指针仍描述 10 月 15 日 Batch 2 和旧 Docker RED | P0 文档：可能导致重复实现和错误接续 | 本轮同步当前入口，保留历史证据 |

审查不是全仓缺陷证明：尚未完成全类型依赖扫描、全调用链审计、生产 SQL 采样或浏览器执行验证。未发现证据的区域不推断为“没有问题”。

## 4. 必须保留的行为与设计边界

- 保持模块化单体和现有宿主；Domain 不引入 EF Core、ASP.NET、Redis，业务模块不依赖宿主或其他模块的持久化实现。
- 对外 `BookId` / `ChapterId` 稳定；Source 与 Canonical 身份分离；阅读只消费已入库内容；内容版本追加；选择、匹配、对齐及切源可解释、可追溯、可撤销。
- 保持公开 API、Developer、Personal Legado 各自的路由、DTO、状态码、认证、Scope、配额、缓存和错误语义。私有数据继续绑定认证 UserId，Redis 只保存可重建数据。
- 保持 Source DSL schema、已发布规则和 Parser/Normalizer 版本语义，以及 SSRF、同源、请求/字节/时间/正则/列表预算、凭据范围、取消和 fail-closed 行为。
- 保持 Outbox 同事务、Inbox 幂等、租约所有者、重试/死信/清理规则；生产 Migration 继续独立运行。
- 展示整理保留 HTML 转义、sessionStorage 的现有受限例外、令牌只走同源授权 Header、`no-store` 和 PWA 仅缓存公共壳的边界。
- 审计行为须逐入口保留：[AuditedResult](../../src/Apps/InkFlow.Api/AuditedResult.cs) 在审计成功后才执行响应，而 `DeveloperEndpointAudit` 对已完成命令采用独立的尽力记录策略，使用 `CancellationToken.None` 并捕获非取消异常。不能为了统一代码而改变两者的失败/取消语义。

本方案不引入微服务、通用插件框架、Mediator/CQRS 框架、通用仓储、前端框架迁移或批量依赖升级，也不调整 UI 设计、Schema/Migration、源协议和业务功能。

推荐方向用于落实既有架构，因此本轮不新增 ADR。若实施需要改变模块依赖方向、查询一致性/对外报告语义、前端会话策略、技术栈、公共协议或数据模型，必须先记录 Proposed ADR 和合同影响，再进入相应实现。

## 5. 建议的职责归属

| 职责 | 目标位置 | 调用关系与边界 |
| --- | --- | --- |
| Reader、Account/PWA、Operations 页面与浏览器资源 | `InkFlow.Api` 内 `Presentation/` 的按功能目录 | Host 组合业务查询；模板只接收展示所需数据，Content 不反向引用展示代码 |
| Canonical 内容、质量、选优、公开策略与内容包 | 保留现有 Content / Library / Sources 职责 | 保留已经建立的查询、选优、发布和格式实现，不新增无实际变体的接口 |
| 一致性检查 DTO、规则与报告编排 | Operations.Application 的纯数据/规则实现 | 只消费元数据/问题结果端口；不引用其他模块 EF 实体或 DbContext |
| 跨 schema 一致性查询适配 | API 组合层的明确适配器 | 保留现有 Host 跨模块只读组合角色；不把其他模块 DbContext 依赖搬入 Operations |
| Developer 路由 | API 内自助管理、管理员套餐、外部 Catalog 三组映射 | 保留各自 policy；配额、响应映射、命令审计在明确的协议 helper 中复用 |
| Outbox/Inbox 持久化 | 现有 Persistence BuildingBlock | 是否分离实现由 RF-06 决定，不用泛型 SQL 框架抹平语义差异 |

目录名称是实施建议，不是已经存在的新结构。类/接口只有在减少调用者知识、集中真实重复或支持现存变体时才增加；仅改名、搬文件或新增转发层不构成验收收益。

## 6. 分阶段工作包

| 工作包 | 范围 / 产物 | 前置条件 | 风险 / 验收重点 |
| --- | --- | --- | --- |
| RF-00 文档与基线 | 本方案、Wiki、当前状态和索引 | 本次需求 | 只交付文档；代码工作均为 Planned |
| RF-01 架构保护网 | namespace/type 依赖检查、合法依赖清单、规则负例 | 后续启动实施；基线复核 | 中；能真实检出禁止依赖，而非仅加载程序集 |
| RF-02 一致性检查职责与测量 | 纯规则/报告归属、独立查询适配、行为夹具、规模基准 | RF-01 全部适用门禁通过 | 中；保持完整报告语义，先建立测量 |
| RF-03 一致性查询优化 | 基于测量选择问题聚合/流式处理/按规则查询 | RF-02；量化目标在修改前固定 | 高；完整问题计数、代表项、关系判定、取消、并发读取语义 |
| RF-04 展示层归属 | Reader / Account-PWA / Operations 三个可独立验收切片 | RF-01；页面行为与视觉基线齐全 | 高；会话、转义、缓存、移动端及 PWA 回归 |
| RF-05 Developer API 整理 | 管理与外部读取职责、协议 helper、策略矩阵 | RF-01；认证/配额/审计特征测试 | 高；401/403/429/503、用量计账、一次性密钥、审计差异 |
| RF-06 条件性后续项 | 消息存储或 DSL/解析器中一个有证据的问题 | 核心批次完成；收益明确；真实回归环境可用 | 高；无收益则记录暂缓原因，不为完成表格而改代码 |
| RF-07 总体验收 | 跨链路回归、当前 SHA 门禁和文档收口 | 本次选定实施包均验证通过 | 不关闭尚未执行的真实设备/账户/生产验收 |

推荐顺序为 RF-01 → RF-02 → RF-03 的测量决策 → RF-04 → RF-05 → RF-07。RF-03 若证据不足则显式暂缓；RF-06 不自动纳入首轮。RF-04/05 不依赖 RF-03 的性能收益，但都依赖 RF-01，不能跨过正在修改批次的失败门禁。

### RF-01：建立能够失败的架构测试

- 当前每个业务模块将 Domain、Application、Infrastructure 放在同一个项目内；只检查 assembly reference 会误报或漏报。测试必须区分 namespace/type 依赖，覆盖方法体等实际引用，不能仅搜索 `using` 文本。
- 自动约束 Domain → EF/ASP.NET/Redis、模块 → Host、模块 → 其他模块 Infrastructure/Persistence，以及既定模块依赖图中的禁止边。
- 盘点当前合法依赖；发现已有违规时独立记录并先处理，不用通配豁免把当前所有依赖永久放行。必要例外必须具名、说明原因、明确退出条件。
- 每条关键规则有最小非法依赖夹具与合法对照，证明检测器能够识别破坏；保留程序集可加载 smoke，但不再以其代表架构约束。
- 先利用当前测试工程和适合的依赖元数据手段；是否增加测试库由实现前检查决定，不为建立守卫拆出整套新项目。

### RF-02 / RF-03：分开维护性与性能工作

RF-02 先固定健康快照、孤儿关系、重复关系、错配、空集合、取消和超过 1,000 个问题的行为。保留 `CheckedAt`、`Status`、`TotalIssueCount`、`ReturnedIssueCount`、`Truncated`、问题代码和证据字段。

将纯规则/报告职责从 API 的组合文件移入 Operations，EF 适配器留在 Host 组合侧。规则按 Library、Sources、Content、Crawling 的实际检查分组，不引入可动态注册的规则引擎。已有 [Unit](../../tests/InkFlow.UnitTests/ConsistencyCheckTests.cs) 和 [PostgreSQL Integration](../../tests/InkFlow.IntegrationTests/ConsistencyCheckIntegrationTests.cs) 是回归入口。

RF-03 的技术路径由基准决定：可先让报告收集器只保留最多 1,000 个返回项，同时完整累计问题数；只有需要降低输入快照成本时，再为具体规则做数据库计数/投影或流式聚合。前一种优化不解决全量快照问题，报告必须明确其局限。

不能给每张表直接加 `Take(1000)`、找到 1,000 个问题就停止扫描，或把不同页之间存在的关系判为孤儿。必须在固定数据上核对完整计数、规则语义及现有定义的顺序；没有稳定排序合同的部分先明确取样规则，若对外语义发生变化先走 ADR/合同变更。并发写入时原有多次查询的观察语义也要记录，不能将其称为原子快照；如改为事务快照，须单独评审锁/事务成本。

### RF-04：展示迁移分三个切片

1. Reader 书库、详情和章节渲染。
2. 登录/账户、书架、历史、偏好、会话以及 PWA 壳。
3. Operations 页面及其浏览器交互。

每个切片同时调整真实调用者、测试和导航，移除已经迁出的旧实现。优先保持现有 SSR、URL、HTML/DOM 约定与响应头；可以把大段脚本/样式保存为宿主内嵌资源后按原方式输出，以改善编辑/检查能力。外链静态资源、缓存策略、压缩和资源版本策略属于另一个需要测量的变化，不夹带在文件迁移中。

内容 AST/纯内容渲染保留其业务归属。所有用户输入继续在正确上下文转义，不通过字符串模板改造扩大可执行脚本范围。资源必须由发布产物携带，不能依赖开发机的源码路径。

实施前按 [前端规范](frontend-design.md) 检查至少 3 个当前同类产品并记录适用 Benchmark Note；本方案不据此重设计页面。保留现有页面截图/行为基线，验收 Mobile、Tablet、Desktop、Wide Desktop、键盘/焦点、长标题、空/错状态、200% 缩放、上下章和会话刷新。HTML 字符串单测不能替代真实浏览器验证；真机和长期阅读体验仍单独记录。

### RF-05：保留协议差异的 API 整理

- 自助应用/密钥管理、Administrator 套餐管理、`/api/developer/v1` 外部只读查询各自拥有清晰的路由映射入口；Host 负责组合。
- 复用现有 `DeveloperEndpointResults.ReserveAsync` 和 DTO 映射，不新建另一套额度算法，不将所有入口套入一个改变执行顺序的通用中间件。
- 锁定“认证 → 请求校验 → 配额准入 → 业务读取”的各路由实际顺序和失败分支，保留 operation/unit、TraceId、UTC 配额周期、`Retry-After` 与 `private, no-store`。
- 为签发/轮换/撤销、非所有者、已停用用户、Scope 不足、额度不足、额度存储故障、取消、审计故障补齐或复核特征测试。不得把 Developer 的审计失败行为自动替换成 `AuditedResult`。

### RF-06：高风险区域的进入条件

消息存储只有在重复修改、测试隔离或维护成本得到证据支持时，才按现有 Outbox/Inbox/Retention 端口分离实现；共享事务、DbContext 生命周期、SQL 锁和身份核对须保持。Parser/DSL 仅提取有实际共用语义的纯规则；不放宽错误返回、预算、DTD/外部实体、Regex、同源和凭据约束。

没有真实 PostgreSQL 并发/租约/回滚证据时不得验收消息改造；没有固定 Fixture 安全回归时不得验收 Parser 改造。真实第三方站点验证仍是独立 opt-in，不作为普通 PR CI 的必备网络依赖。

## 7. 测量与验收方法

### 7.1 性能基准计划（均未执行）

| 场景 | 固定数据 / 负载 | 必须记录的指标 |
| --- | --- | --- |
| 一致性检查 | 1k / 10k / 100k Canonical chapters；每章 2 个来源章节、2 个内容版本；另设空库、固定比例孤儿/重复、超过 1,000 个问题的夹具 | SQL 次数/返回行数、查询计划、总耗时、p50/p95、分配字节、峰值内存、完整报告一致性 |
| Reader 展示 | 短章/长章，100 / 1,000 / 10,000 章目录；同一内容和设备/视口 | 响应字节、渲染耗时、浏览器可交互/切章延迟、DOM 数量；纯迁移先要求行为等价 |
| Developer Catalog | 相同已入库目录、固定有效/无效密钥与额度夹具，固定并发 | 响应状态/头/正文、Usage Ledger 条数与单位、SQL 次数、延迟；不调用第三方 |

在同一运行环境、Release 构建、数据库版本、固定随机种子和冷/热缓存条件下比较基线与候选；每组先预热 3 次、测量至少 30 次并重复 3 轮，保留原始样本和运行参数。先记录基线噪声范围，再在实现前固定改善目标与回归容差。性能包至少应有一个目标指标跨轮次改善，其他关键指标不超出约定容差；未完成测量不报告加速百分比。

既有 Core SLO smoke 阈值继续执行；短窗口 smoke 只能证明该环境的门禁，不代替真实业务负载或生产 SLO。查询优化也必须保持不读取不必要正文的既有边界。

### 7.2 验证矩阵

每个代码批次先审查 diff、Secret、依赖、合同和文档，再运行默认门禁：

```powershell
dotnet restore InkFlow.sln
dotnet build InkFlow.sln -c Release
dotnet test InkFlow.sln -c Release
```

| 改动 | 必须增加/执行的针对性验证 |
| --- | --- |
| RF-01 | Architecture 正/负例；确认检查覆盖生产模块类型 |
| RF-02/03 | Consistency Unit + PostgreSQL Integration；报告等价、规模/取消回归；Admin/Operations runtime |
| RF-04 | ReaderHtml/Reading 等受影响 Unit；Reader frontend/edge/account/content/navigation smoke、Operations/Admin smoke；浏览器多视口/PWA/安全检查；相关 Legado 回归 |
| RF-05 | Developer Unit、Contract、Developer/Billing PostgreSQL Integration；Developer API、Admin runtime；配额/权限/审计错误路径 |
| RF-06 | Messaging Unit + PostgreSQL 事务/幂等/租约/retention Integration，或 Sources/DSL/Selector/凭据/SSRF Fixture 回归；相应 Worker/Source runtime |
| 每个候选代码提交 | Release 0 warnings/errors、适用迁移模型检查（11 contexts）、CI/Docker/Security 全部强制 Job 与候选 SHA 一致；文档同步 |

运行时使用 `docker-compose.build.yml`，先 `config --quiet`，再 `up -d --build --wait api worker scheduler otel-collector`，执行对应 `scripts/*-runtime-smoke.sh`。运行前重新检查 Docker/数据库环境；不可用时写 `BLOCKED`，使用符合要求的 CI 环境取得证据，不能假设旧阻塞仍然存在或已解决。

## 8. 回退、交付与停止条件

- 每批只有一个明确责任范围，保持可独立审查、构建和回退；不把纯结构整理与 Schema、公开协议或依赖升级混为一批。
- 代码实施阶段每批使用独立 candidate commit，目标 SHA 门禁失败或 Pending 时停止下一依赖批次，读取失败日志并修复。文档阶段不创建候选代码提交。
- 本方案默认无数据迁移，回退以回退该批代码/资源/DI 的原子提交为主；若发现数据或不可逆副作用，先重新设计迁移/补偿和回退条件。
- 合并前比较保留接口、权限/审计顺序、缓存、取消、查询计数、租约与运行时结果；不以文件变短、测试数量增加或“已实现”代替验收。
- 用户目录 `.workbuddy-ai/` 不纳入修改。Release Candidate 的真机、真实来源、真实账户、生产 OTLP/SLO/备份治理验收保持未完成，见 [Progress](../roadmap/progress.md) 与 [Handoff](../handoff/handoff.md)。

## 9. 本轮文档交付状态

| 项目 | 结果 |
| --- | --- |
| 工作包 / 实现 | RF-00：现状审查、方案和验证/回退计划已写入；RF-01–RF-07 为 Planned 或条件性候选 |
| 验收 | 供评审的文档产物；不标记代码重构 Accepted/Completed |
| Restore / Build / Tests | NOT RUN：本轮只有文档变更；基线 CI 结果独立列于第 2 节 |
| Runtime / 性能基准 | NOT RUN：本轮未启动服务或执行测量 |
| 本轮 CI | NOT TRIGGERED：未提交/推送；不得沿用基线 GREEN 作为文档候选的门禁 |
| 文档校验 | PASS：34 页 Wiki catalog 检查、修改页本地链接/格式和元数据 JSON 检查、`git diff --check`、仅文档范围检查及敏感内容自检；源码抽查确认行数、13 组一致性物化和单个 Architecture 测试；详情见 Wiki 工作包 |
| 已处理问题 | 当前状态入口与代码基线不一致的问题；F-01–F-06 的代码问题/候选项尚未实施 |
| Commit | 未新增；代码基线 `ca15cadbe1df97adf99150df0c9d3d7bc95e32af` |
| 下一步 | 评审本方案；后续启动 RF-01，以可失败的架构约束和合法依赖清单作为第一批交付 |
