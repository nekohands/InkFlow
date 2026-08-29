# InkFlow 工程交接文档

> 用于开发者、AI Agent 或未来会话快速、安全接手 InkFlow。真实状态以仓库与 CI 为准。

- 产品：墨流 / InkFlow
- 当前阶段：1.0 Release Candidate（Phase 1B/商业基础自动化门禁已通过，外部验收待定）
- 当前工作分支：`dev`（2026-08-25 起）
- `dev` 骨架 root commit：`c5f2048`
- 交接日期：2026-08-29；dev 骨架重建更新：2026-08-25

## 1. 接手顺序

1. `../product/product-vision.md`
2. `../engineering/development-workflow.md`
3. `../architecture/invariants.md`
4. `../architecture/architecture.md`
5. `../architecture/domain-model.md`
6. `../architecture/source-runtime.md`
7. `../architecture/legado-contract.md`
8. `../architecture/security-model.md`
9. `../roadmap/progress.md`
10. `../roadmap/phase-1-acceptance.md`
11. `../roadmap/risk-register.md`

`development-workflow.md` 是强制规范。

## 2. 产品定位

InkFlow 是以 Canonical Content 为核心、以 Legado 与 Web Reader 为主要消费端、支持多来源采集、自动追更、内容选优和开放 API 的小说内容平台。

固定产品优先级：

1. Legado
2. Web 阅读
3. 自动追更
4. 多源容灾
5. 多站点采集
6. 统一书库
7. 搜索
8. 书架/阅读历史

## 3. 当前真实仓库状态

**分支模型（2026-08-25 起）**：

- `dev`：当前唯一开发主线。仅包含基础设施骨架，业务代码按路线图重新实现，完成后经 PR 合入 `main`。
- 历史实现不迁移到 `dev`；已完成工作包的设计记录以 `../roadmap/progress.md` 第 4.1 节为准，落地时在 `dev` 上重新编写。

`dev` 骨架（root commit `c5f2048`）已重建并通过本地验证：

- `src/Apps`：API / Worker / Scheduler / Migrations（`/health` 探针骨架）。
- `src/BuildingBlocks`：Domain / Application / Persistence / Messaging / Security / Observability。
- `src/Modules`：Identity / Library / Sources / Crawling / Content / Reading / Search / Legado / Developers / Billing。
- Unit / Architecture / Integration / Contract 四个测试项目各含守卫用例。
- Central Package Management + 仓库级 `nuget.config`（单一 nuget.org 源）。
- Docker Compose 与 `deploy/docker/*.Dockerfile` 原样保留。
- CI 触发覆盖 `main` + `dev`。

`dev` 本地验证证据：

```text
Restore: PASS
Release Build: PASS (0 warnings / 0 errors)
Unit: PASS (338/338)
Architecture: PASS (1/1)
Integration: LOCAL BLOCKED (76 total: 6 passed / 2 skipped / 68 Docker-blocked); PASS (CI 33255354693: 74 passed / 2 skipped, including 12/12 Messaging persistence/execution/retention tests)
Contract: PASS (10/10)
Compose validation: PASS
Runtime smoke: PASS
CI: GREEN (CI 33255354693; Docker 33255354699; Security 33255354684)
```

## 4. 下一工作包

**当前状态（2026-08-29 更新）**：Phase 1A 的自动化链路与 kanunu8 真实源验证已通过；Legado 真机导入/阅读和真实追更仍待人工验收。Phase 1B 已完成确定性双来源自动化切源基线（含 Capability Health v1），但尚未宣称完成真实故障切源验收。Worker 已具备过期租约恢复、跨进程原子领取和持久化重试退避调度；Crawler 死信受控重放基线已补齐，Identity 基础认证/授权与受保护 Repair/replay 入口也已落地，Reading State v1 用户状态后端、Personal Legado Token v1、Web Reader v1、Reader/PWA 用户状态 v1 和 Private Library v1/v2（书目、私有章节、TXT/EPUB 导入导出）自动化基础已接入，真实账户/文件验收仍待推进，公开修复中心仍待后续安全/运维工作。CI Security Scan 基线 v1 已落地并通过远端 CI、四镜像发布前扫描和报告归档；来源级资源授权 v1 已落地并通过自动化/远端验证，生产安全治理、更广泛资源/组织权限、外部告警路由和备份治理仍待后续工作。Developer API / Commercial Foundation v1 已完成候选实现，远端 CI、Docker、Security 门禁已通过；真实凭据、真实 PostgreSQL/Redis 和人工验收仍待后续。Operations 告警历史、incident 去重/恢复、保留清理和 Administrator-only 历史读端已补齐，候选提交 `4ef206f` 已通过远端 CI `33244304809`、Docker `33244304814` 和 Security `33244304804`；外部通知渠道不在本轮实现。Personal 令牌的阅读 3.0 导入、四步阅读和撤销后失效，以及 Web Reader/PWA 浏览器视觉、安装和账户链路验收保留为人工验收。

本轮另完成 API 安全基线与三宿主可观测性接线：公共 API/Legado API 已有可配置限流，拒绝返回 `429/Retry-After`；API 请求审计已覆盖业务 API 且不记录 query string，`CompositeAuditEventSink` 同时写入 PostgreSQL `audit.events` 与结构化日志；API、Worker、Scheduler 均接入统一 OpenTelemetry 注册入口。Identity 基础认证/授权、会话轮换和死信重放命令审计已补齐；随后补齐 Redis 分布式计数、受保护的 Operations 告警快照与阈值基线，以及来源级资源授权 v1。授权管理、来源过滤和撤销审计已接入；告警内部历史/去重/恢复状态已由 Operations PostgreSQL 事实表承载，外部通知路由和更完整的组织/资源权限治理仍待后续工作包。

随后补齐 Worker 任务可靠性基础：过期 `Leased`/`Running` 任务会回收后重新领取，数据库领取查询覆盖过期 `Running`，`CompositeTaskExecutor` 已注册到 DI，单个执行异常进入失败/重试/死信路径；本轮进一步加入基于 PostgreSQL 事务与 `FOR UPDATE SKIP LOCKED` 的跨进程原子领取，以及基于 `ScheduledAt` 的持久化重试退避。追更写侧已完整闭环：目录联动入队 + 抓取→发布桥 + 上游修订重扫，Content 任务真正产出正典 `ContentVersion` 并保持版本追加不覆盖（详见 4.6 / 4.7）。本轮进一步打通冷启动主路径:`BookDiscoveryService` 让 `/api/v1/search` 与 Legado `/search` 能发现未入库书目,幂等导入并自动匹配正典身份(详见 4.8)。健康侧完成半开自动恢复与主动巡检探针(4.9);Web Reader 搜索也已接入发现流,三端(API/Legado/Reader)共用同一落库过滤语义(详见 4.10)。冷却曲线参数已配置化(ADR 0005,详见 4.11):运营经 `SourceHealth` 配置节调整失败阈值与重探节奏,无 Schema 变更。

本轮补齐 linovelib 的 Search 种子规则：`POST /S6/` + `searchkey={key}` + 列表抽取，统一修正 `/novel/` 外部 ID 归一化，并修复中文表单占位符的重复编码；离线回归与远端 CI/Docker 已通过（提交 `52c36a4`，CI `33090147713`，Docker `33090147561`）。真实来源访问、阅读 3.0 真机流程和其他人工验收仍按第 4.2 节待定，不在本轮执行。

随后补齐 Worker 失败观测基线：`CrawlerFailureObservation` 将失败原因归类为低基数 `FailureKind`，`CrawlerFailureReporter` 通过 `ICrawlerFailureSink` 向结构化日志和 OpenTelemetry counters 扇出；失败路径明确记录 retry/dead-letter/not-running disposition，sink 异常与任务状态隔离。远端 CI `33091872440`、Docker `33091872458` 均 GREEN；本机 Docker 集成仍因环境不可用 BLOCKED。外部告警路由、阈值治理与持久化运维闭环留待后续 Operations/Crawling 工作包。

本轮随后补齐 Crawler 死信受控重放：`ICrawlerTaskRepairRepository` 通过 PostgreSQL 事务与 `FOR UPDATE` 锁定死信/原任务，幂等创建新的 `Pending` 任务，并在原死信上追加操作者、理由、时间和重放任务 ID；重复/并发请求不会重复创建，已解决死信不再永久阻塞后续入队。实现提交 `20f75fb`、测试隔离修复 `c2d4aeb`；远端 CI `33094754193`、Docker `33094754210` GREEN，含 Runtime smoke。该历史工作包当时未实现公开 Admin/Operations 入口、认证授权与命令级审计，当前基础入口见下方 4.15。

随后补齐安全审计持久化基线：API/Legado 请求由 `CompositeAuditEventSink` 同时写入结构化日志和 PostgreSQL `audit.events`，Migration 安装数据库追加式触发器拒绝更新/删除；远端 CI `33096635143`、Docker `33096635237` GREEN，新增审计集成用例通过并在 Runtime diagnostics 观察到审计事件。该历史工作包未覆盖认证授权、命令级 before/after 审计、查询授权、保留策略与告警；当前基础认证、Repair 命令审计见下方 4.15。

随后补齐 SSRF / SafeHttpClient 连接级约束：`SsrfSafeHttpMessageHandler` 在每次新连接时使用同一批经过校验的 DNS 地址建立 TCP，关闭环境代理，限制端口与重定向次数；API、Worker、Scheduler 及 Kanunu8 生产接线均已更新。远端 CI `33099136084`、Docker `33099135992` GREEN，新增 5 个连接回调回归用例通过；本机三宿主 `/health` 均 200，但完整 Testcontainers 因 `docker_engine` 不可用 BLOCKED。真实来源/真机验收仍待后续人工执行。

本轮补齐 Identity 认证/授权与受保护 Repair 基线：新增 `User`、`RefreshSession`、`AccessToken` 聚合，注册/登录/refresh 轮换/登出/当前用户 API，PBKDF2-SHA256 密码哈希和仅保存摘要的 opaque token 会话；新增 `identity` schema 与 `AddIdentityFoundation` Migration。`Operator` / `Administrator` 角色保护死信列表和 replay 入口，操作者从认证主体取得，理由和死信/重放任务 reference 写入 `crawler.dead_letter.replay` 命令审计；原死信继续保持 `DeadLettered`。

本轮证据：本机 Release Build 0 warnings / 0 errors、Unit 209/209、Architecture 1/1、Contract 1/1；API `/health` 200，未认证身份/Repair 入口均返回 401。全量 Integration 42 项中 6 通过、1 跳过、35 项因本机 `npipe://./pipe/docker_engine` 不可用而 BLOCKED；远端 CI `33102831333` GREEN（含 refresh 轮换与登出 Runtime smoke），Docker `33102831388` GREEN（四镜像）。首次 Runtime 发现 `refresh_token` 字段绑定问题，已由提交 `9f9d5c7` 修复并复验；实现提交为 `09ea265`。

### 4.15 跨模块一致性检查 v1（本轮，2026-08-28）

- 缺口：跨源映射、正文版本、选择审计和抓取死信此前只有模块局部约束，缺少面向 Repair/Operations 的一次性一致性扫描。
- 实现：`IConsistencyCheckService` 提供单次报告接口，`IConsistencySnapshotReader` 提供可替换 Adapter seam；EF Adapter 从 Library、Sources、Content、Crawling 四个 schema 读取最小关系快照，正文只读取长度。纯检查器覆盖 Source/SourceBook/SourceChapter、FetchArtifact、MatchCandidate、ChapterMapping、ContentVersion、SelectionDecision、Crawler Task 与 DeadLetter 的孤儿、错配、重复、当前版本和可解释性问题。
- 入口：新增受 `Operator` / `Administrator` policy 保护的 `GET /api/v1/admin/consistency`；报告稳定返回 `healthy` / `issues_found`、稳定错误码、资源 ID、解释消息和最多 1000 条 issue。扫描只读，不自动修复，不新增 Migration；请求继续由现有审计中间件记录。
- 测试：Unit 212/212 覆盖健康快照、跨模块孤儿/错配和报告截断；新增 PostgreSQL Testcontainers 集成用例验证四 schema 快照与孤儿正文版本检测。
- 本机证据：Restore PASS；Release Build 0 warnings / 0 errors；Unit 212/212、Architecture 1/1、Contract 1/1 PASS；API `/health` 200，未认证身份和一致性入口均返回 401。完整 Integration 43 项中 6 通过、1 跳过、36 项因 `npipe://./pipe/docker_engine` 不可用而 BLOCKED，不记为通过。远端首跑 CI `33105564941` 仅暴露新增集成测试把 11 字符夹具误断言为 12 的测试问题；修复提交 `7dac6ce` 后 CI `33106044634` GREEN（43 项：42 通过、1 跳过，含 Compose 与 Runtime smoke），Docker `33106044677` GREEN（四镜像）。
- 边界：本轮按用户决定不执行 MuMu/阅读 3.0 真机、真实来源、真实追更和真实第二来源故障切换；自动修复、完整 Repair Center UI、查询授权、告警、备份恢复和安全扫描仍待后续。

### 4.16 Content Policy / Takedown v1（本轮，2026-08-28）

- 缺口：公开目录、详情、章节正文和 Legado 输出没有统一的下架门控，也没有受保护的可追溯下架/恢复入口。
- 实现：Content 增加书级不可变 `ContentPolicyDecision` 历史；最新决策派生公开状态，重复同状态命令幂等，操作者和理由经过领域校验。`CatalogQueryService` 在目录/详情/正文查询中统一门控，正文先取书籍 ID 再加载正文；API 搜索发现结果也过滤下架书，Legado/Web Reader 复用该语义。
- 持久化与权限：新增 `content.policy_decisions` / `AddContentPolicyDecisions` Migration 和数据库追加式触发器；新增 Administrator-only `ContentModeration` policy、`GET /api/v1/admin/content/takedowns`、下架/恢复命令，并写入命令级审计。
- 当前证据：本机 Restore PASS；Release Build 0 warnings / 0 errors；Unit 219/219、Architecture 1/1、Contract 1/1 PASS；API `/health` 200，未认证 Content Policy 管理入口 401。本机完整 Integration 45 项中 6 通过、1 跳过、38 项因 `npipe://./pipe/docker_engine` 不可用而在 Testcontainers 初始化阶段 BLOCKED，不记为通过；远端 CI `33109068649` GREEN（45 项：44 通过、1 跳过，含 Restore/Build/Compose/Runtime smoke/Diagnostics），Docker `33109068630` GREEN（API、Migrations、Scheduler、Worker 四镜像）。
- 边界：按用户决定不执行 MuMu/阅读 3.0 真机、真实来源、真实追更和真实第二来源故障切换；Content Policy 管理命令的 Administrator 人工验收已加入下方待定事项。

### 4.17 Source Health Operator Controls v1（本轮，2026-08-28）

- 缺口：Capability Health 已有自动状态机和持久化事实，但缺少受保护的运维查询与单能力人工停用/恢复入口。
- 实现：新增 `ISourceHealthOperations` 深接口和独立 `SourceOperations` policy；`GET /api/v1/admin/sources/{sourceId}/health` 查看来源能力状态，POST disable/enable 控制单个能力。命令要求 `Operator` / `Administrator`、认证主体和非空理由；恢复回到 `Unknown` 等待真实探针，不直接伪造 `Healthy`。
- 审计：命令写入 `source.health.disable` / `source.health.enable`，包含认证操作者、理由、来源/能力和状态 reference；不修改 Source 身份、Rule 或 Canonical Content。
- 当前证据：本机 Release Build 0 warnings / 0 errors；Unit 221/221、Architecture 1/1、Contract 1/1 PASS；API `/health` 200，未认证 Source Operations 入口 401。完整 Integration 45 项中 6 通过、1 跳过、38 项因本机 Docker Engine 不可用而 BLOCKED；远端 CI `33110684551` GREEN（Unit 221、Integration 45 项 44 通过/1 跳过，含 Compose/Runtime smoke/Diagnostics），Docker `33110684410` GREEN（四镜像）。
- 边界：按用户决定不执行 MuMu/阅读 3.0 真机、真实来源、真实追更和真实第二来源故障切换；Source Health 管理命令的人工实际操作、完整 Repair/Operations Center、告警和备份治理仍待后续。

### 4.18 Operations/Repair Center Read Model v1（本轮，2026-08-28）

- 缺口：死信、一致性和来源健康原本是分散的只读入口，缺少统一的运维快照；查询授权与命令授权也没有明确拆开。
- 实现：新增 `IOperationsCenterReader` 与 `GET /api/v1/admin/operations/overview`，聚合来源健康、有限死信和一致性报告；死信多取一条判断 `HasMore`，读模型不携带任务 Variables、CredentialReferenceId 或正文。
- 授权/韧性：新增 `OperationsRead` policy（`Operator` / `Administrator`），用于 overview、死信列表、一致性和 Source Health 查询；replay/disable/enable 继续使用独立命令 policy。每个区块隔离异常并返回稳定 `partial` / `unavailable` 状态，不泄漏内部异常细节。
- 当前证据：本机 Release Build 0 warnings / 0 errors、Unit 223/223、Architecture 1/1、Contract 1/1；API `/health` 200，Operations overview、Consistency、Source Health 未认证请求均 401。远端 CI `33112741068` GREEN（含 Restore/Build/Test/Compose/Runtime smoke/Diagnostics），Docker `33112741039` GREEN（四镜像）。真实设备/来源测试按用户决定跳过，本机 Docker 集成仍待环境恢复。
- 边界：未实现 Center UI、自动修复、告警、备份治理和真实业务验收；人工 Operations Center 操作加入待定事项。

### 4.19 Reading State v1（本轮，2026-08-28）

- 缺口：认证主体已有基础能力，但书架、阅读进度、最近阅读历史和阅读器偏好缺少用户范围的数据模型、迁移和 API。
- 实现：新增 `InkFlow.Modules.Reading` 领域/应用/EF 持久化；`reading` schema 下建立 shelf/progress/history/preferences 四张表；API 接入 `/api/v1/me/reading/*`，覆盖书架、历史、进度和偏好。
- 关键约束：所有 Reading 查询/写入显式携带认证 `sub` 对应的 `UserId`；书籍和章节使用稳定 Canonical ID；写入前通过 Content Policy 可见性检查；进度与历史在一个事务内保存，PostgreSQL upsert 按时间戳拒绝旧请求回写。
- 当前证据：本机 Release Build 0 warnings / 0 errors、Unit 230/230、Architecture 1/1、Contract 1/1；API `/health` 200，未认证 Reading 入口 401。远端 CI `33115433510` GREEN（Unit 230、Integration 48 项 47 通过/1 跳过，含 Reading migration/upsert、Compose、Runtime smoke/diagnostics），Docker `33115433490` GREEN（四镜像）。
- 边界：没有实现 Web/PWA Reader UI、私人书库、TXT/EPUB 导入导出或真实设备/真实来源验收；Personal Legado Token v1 的自动化实现已完成，但阅读 3.0 真机导入、四步阅读和撤销后失效与真实追更/真实切源一起保留在待定事项。

### 4.20 Personal Legado Token v1（本轮，2026-08-28）

- 缺口：公共书源契约没有用户隔离的 Legado 访问边界，长期秘密也没有独立的签发、撤销、过期和 Scope 模型。
- 实现：新增 `LegadoAccessToken` 聚合、`identity.legado_tokens` 表、签发/列表/撤销 API；原始 `lf_lgd_` 令牌只在签发成功响应中出现一次，数据库只保存 Prefix + SHA-256 Hash 与状态元数据。
- Legado 接入：Personal 书源返回 `header` JSON，通过 `X-InkFlow-Legado-Token` 访问 `/api/legado/v1/personal/*`；公共 `/api/legado/v1/*` 和公共书源保持兼容，令牌不进入 URL。
- 授权与审计：独立 `InkFlowLegadoToken` scheme 和 `LegadoRead` policy 校验用户状态、令牌过期/撤销与 `read` Scope；签发/撤销命令审计只记录脱敏 reference，不记录原始令牌。
- 当前证据：本机 Restore PASS；Release Build 0 warnings / 0 errors；Unit 245/245、Architecture 1/1、Contract 2/2 PASS。本机 Identity PostgreSQL Testcontainers 3 个目标用例因 `npipe://./pipe/docker_engine` 不可用而 BLOCKED；远端 CI `33118314796` GREEN，Docker `33118314789` GREEN（四镜像）。
- 边界：按用户决定不执行 MuMu/阅读 3.0 真机、真实来源、真实追更和真实第二来源切换；Personal 书源导入、Search → BookInfo → TOC → Content 与撤销后失效加入下方人工验收。

### 4.21 Web Reader v1（本轮，2026-08-28）

- 缺口：既有 `/reader` 三页面流只有极简 HTML，缺少长文阅读所需的设置入口、主题/字号/行高、响应式布局、可访问语义和状态反馈。
- 实现：重做服务端 `ReaderHtml` 的书目列表、书籍详情和章节页；保留搜索→详情→开始阅读、目录、上一章/下一章路径，增加统一视觉 token、语义 landmark、skip link、滚动进度条、空正文状态和移动端触控布局。
- 阅读设置：章节页提供 `dialog`，支持 System/Light/Sepia/Dark、字号和行高；匿名偏好以受限数值写入当前设备 `localStorage`，脚本不可用时正文和链接导航仍可用。
- 安全/验收：所有业务文本 HTML 转义，不输出 SourceId 或上游 HTML；加入 reduced-motion、焦点和设置控件结构回归。Benchmark Note 记录了 Royal Road、Kobo Web Reader、Wuxiaworld 的官方阅读模式取舍。
- 当前证据：本机 Restore、Unit 247/247、Architecture 1/1、Contract 2/2、Release Build 0 warnings / 0 errors PASS；候选提交 `a8d1c23` 的远端 CI `33120844695` 与 Docker `33120844685` 均 GREEN，包含 `/reader` 语义结构 smoke、Compose/Runtime smoke、Diagnostics 和四镜像构建。浏览器截图、移动/平板/桌面/宽屏视觉检查和长时间阅读仍未执行，按用户决定加入人工验收。
- 边界：未实现 PWA 安装/离线缓存、服务端 Reading State 同步、评论/书签、分页阅读或真实设备验收。

### 4.22 Reader/PWA 用户状态 v1（本轮，2026-08-28）

- 缺口：Web Reader 只有匿名阅读和本地偏好，缺少账户入口、个人书架/历史页面、服务端进度同步和可安装 PWA 基础壳。
- 实现：新增 `/reader/account`、`/reader/shelf`、`/reader/history`、`/reader/offline`，共享导航、Manifest、SVG 图标和 `/reader/sw.js`；登录/注册复用既有 Identity API，书架/历史复用既有 Reading State API，详情页支持加入书架，章节页保存进度/历史并同步阅读偏好。
- 安全与边界：当前标签页只在 `sessionStorage` 保留短期 Web Access/Refresh Token；同源 `Authorization` Header、401 一次 refresh、失败清理和 `cache: no-store` 均由客户端边界集中处理。Service Worker 只缓存公开壳与离线提示，不缓存 `/api/v1/me/*`、认证响应、令牌或私人正文；ADR 0006 与 `security-model.md` 已同步。
- 自动化证据：本机 Restore PASS；Release Build 0 warnings / 0 errors；Unit 252/252、Architecture 1/1、Contract 2/2 PASS。全量解决方案测试中 IntegrationTests 48 项为 6 通过、41 项因 `npipe://./pipe/docker_engine` 不可用而 BLOCKED、1 项跳过，进程 FAIL，不记为本机集成通过。候选提交 `b3561a2` 的远端 CI `33123325151`、Docker `33123325184` 均 GREEN；CI Runtime smoke 已覆盖 Manifest、Service Worker、账户、书架和历史公开路由。
- 未含：真实 PWA 安装/离线、登录注册和用户状态浏览器链路、跨尺寸视觉/长时间阅读、跨标签页/跨设备同步、离线私人内容、私人书库和 TXT/EPUB 导入导出；按用户决定全部列入待定事项。

### 4.23 Operations/Repair Center UI v1（本轮，2026-08-28）

- 缺口：Operations Read Model v1 已提供统一快照，但运维人员仍需直接调用多个 API，缺少按来源健康、采集死信和跨模块一致性分组的操作界面。
- 实现：新增 /admin/operations 静态管理壳；浏览器先用当前 tab 会话验证 Operator / Administrator，再通过受保护的 overview API 读取有限快照。页面按区块呈现 ready / partial / unavailable、合法空状态、生成时间和可解释问题。
- 受控操作：来源能力停用/恢复与死信重放均通过确认对话框提交非空理由，成功/冲突结果展示服务端状态；UI 不绕过既有 policy、审计、状态机和重放幂等约束。
- 安全与可访问性：动态字段只进入 textContent，不缓存运维 API，不显示凭据引用、任务 Variables 或正文；页面具备语义标题/表格、aria-live、键盘焦点、文字状态、窄屏布局和 reduced-motion 基线。基准记录为 docs/engineering/benchmarks/operations-center-v1.md。
- 自动化证据：本机 Restore PASS；Release Build 0 warnings / 0 errors；Unit 254/254、Architecture 1/1、Contract 2/2 PASS。API 静态页面 200、/health 200、未认证 overview 401；全量 Integration 48 项为 6 通过、41 项因本机 Docker Engine 不可用而 BLOCKED、1 项跳过，不记为本机集成通过。提交 ed0ff8c 的远端 CI 33125476460 GREEN（Restore/Build/Test/Compose/Runtime smoke/Diagnostics），Docker 33125476441 GREEN（四镜像）。
- 边界：未执行 Operator/Administrator 实际浏览器操作、跨尺寸视觉/对比度/键盘截图和真实修复命令；自动修复、告警、备份治理、私人书库和真实来源验收仍未完成。

### 4.24 第三个 Official Source：17K CodeAdapter（本轮，2026-08-28）

- 缺口：1.0 要求至少 3 个稳定 Official Source；此前第三来源只有路线和候选，没有进入三个宿主的适配器工厂与 Source 种子。
- 实现：新增 `SeventeenKSourceAdapter` 独立插件，基于 17K API/Web JSON 覆盖 Search、BookInfo、TOC、Content；固定 allowlist 主机并在请求前执行 `SsrfGuard`，三宿主均使用 `SsrfSafeHttpMessageHandler` 和 20 秒超时。书籍 ID 只接受纯数字，章节 ID 采用 `bookId/chapterId` 自包含格式。
- 版权/访问边界：上游标记为未购买的 VIP 章节返回 null，不绕过登录、订阅或自动购买地址；非 2xx、空响应和非法 JSON 不伪造正文。Worker 启动种子幂等登记 linovelib、kanunu8、17K，已有 Source 不覆盖。
- Fixture 回归：覆盖 Search 去重、书目/目录/正文解析、稳定章节 ID、非法 ID 零触网和 VIP 不绕过。自动化证据为本机 Restore PASS、Release Build 0 warnings / 0 errors、Unit 258/258、Architecture 1/1、Contract 2/2 PASS；Integration 48 项实际为 6 通过、41 项因本机 `npipe://./pipe/docker_engine` 不可用而 BLOCKED、1 项跳过。提交 `258e3c3` 的远端 CI `33127440930` 与 Docker `33127440917` 均 GREEN，包含 Restore/Build/Test、Compose、Runtime smoke/Diagnostics 和四镜像构建。
- 边界：本轮没有触发真实 17K 或其他来源网络请求，不能宣称 17K 已稳定实测，也不能关闭真实第二来源故障切换 Release Gate；真实链路和多源切换继续待后续人工/可用环境验收。

### 4.25 Admin Audit Read v1（本轮，2026-08-28）

- 缺口：`audit.events` 已是追加式持久化事实，但此前没有受保护的查询读端，无法在 Operations/Security 场景按条件复核事件。
- 实现：新增 `IAuditEventReader` / `EfAuditEventReader` 和 `GET /api/v1/admin/audit/events`；端点使用独立 `AuditRead` policy（`Operator` / `Administrator`），支持 `from`、`to`、`action`、`outcome`、`actorId` 精确过滤，默认 50、最多 100 条，并以时间戳+事件 ID 不透明游标分页。查询只读、无跟踪、无 CRUD 更新/删除入口，异常返回稳定 `audit_unavailable`。
- 安全：游标重新校验时间戳和非空 Guid；过滤器拒绝控制字符和超长值；读端不改变既有数据库追加式触发器、请求审计和命令审计边界。
- 当前证据：本机 Restore PASS；Release Build 0 warnings / 0 errors；Unit 263/263、Architecture 1/1、Contract 2/2 PASS；API `/health` 200、未认证审计查询 401；本机 Integration 49 项为 6 通过、42 项因 Docker Engine 不可用而 BLOCKED、1 项跳过。候选提交 `29a723c` 的远端 CI `33128764947` GREEN（48 通过、1 跳过，含 Restore/Build/Test/Compose/Runtime smoke/Diagnostics），Docker `33128764869` GREEN（四镜像）。
- 边界：本轮未执行带 Operator/Administrator 凭证的人工查询和来源授权实际操作、保留/清理策略、告警和真实运维演练；来源级授权机制已完成，但人工验收继续作为后续 Release Gate 留在待定清单。

### 4.26 PostgreSQL Backup/Restore Drill v1（本轮，2026-08-28）

- 缺口：现有 Compose/Migrations 能初始化 PostgreSQL，但此前没有在真实运行数据上形成可复核的备份恢复证据。
- 实现：新增 `scripts/backup-restore-drill.sh` 并接入 CI Runtime smoke 之后；脚本使用 custom-format `pg_dump`，在隔离数据库执行 `pg_restore`，比较所有非系统基础表的行数签名和 `audit.events` 数量，最后清理隔离库与临时文件。数据库标识符严格校验，源库不被修改。
- 当前证据：本机 `bash -n` PASS；本机 Docker/Compose 不可用，实际演练 BLOCKED。候选提交 `29c2c5f` 的远端 CI `33129734525` GREEN（48 通过、1 跳过，Restore/Build/Test/Compose/Runtime smoke/备份恢复/Diagnostics 全部通过），演练日志为 `archive=49125 bytes, audit_events=22`；Docker `33129734604` GREEN（四镜像）。
- 边界：该证据只覆盖 CI 环境中的恢复可用性，不等同于生产异地备份、加密、保留策略、恢复授权、RPO/RTO 和告警治理；这些继续保留为后续 Operations Gate。

### 4.27 Redis Distributed Rate Limit v1（本轮，2026-08-28）

- 缺口：原有 API/Legado fixed-window 只在单个 API 进程内计数，多实例部署会放大同一客户端的有效配额。
- 实现：API 新增 `RedisRateLimitCounter` / `RedisFixedWindowRateLimiter`，通过 Redis Lua 原子执行读取、判断、递增和过期；公共/Legado policy 与客户端身份分桶保持不变，Redis key 只含策略名和客户端短哈希。Redis 故障时使用相同配额/窗口的本地有界 limiter，并记录不可用/恢复转折，不无界放行。
- 配置：Compose API 注入 `ConnectionStrings__Redis=redis:6379,abortConnect=false`；生产环境需提供认证/TLS 连接配置，并监控 Redis 可用性。动态用户/组织配额、加权成本和外部告警不在本轮范围。
- 自动化：Unit 267/267、Architecture 1/1、Contract 2/2 与 Release Build 0 warnings / 0 errors 本机通过；本机 Docker/Redis 不可用，完整本机 Integration 仍有 42 项 BLOCKED、真实 Redis 用例本地跳过。提交 `2bace7d` 的 CI `33131258779` GREEN：完整 Test 50 项中 48 通过、2 跳过，独立 Redis Integration 真实两连接 1/1 通过，Runtime smoke、备份恢复与 Diagnostics 通过；Docker `33131258754` GREEN（四镜像）。
- 边界：本轮不执行 MuMu/阅读 3.0、真实来源、真实故障切源或人工验收；Redis 故障降级期间不宣称跨实例全局一致性。

### 4.28 Operations Alert Snapshot v1（本轮，2026-08-28）

- 缺口：来源健康、Crawler 死信、一致性检查和 Redis 限流故障此前各自可观测，但没有统一的当前告警快照和配置化阈值入口。
- 实现：新增 `OperationsAlertOptions`、`OperationsAlertEvaluator`、`OperationsAlertReader` 和受 `OperationsRead` policy 保护的 `GET /api/v1/admin/operations/alerts`；返回有界告警列表、稳定 code/severity/resource、总数和截断状态。Redis 限流计数器现在同步记录进程内可重建健康快照。
- 安全/边界：告警只使用稳定错误描述，不带异常原文、Token、IP、连接串或来源失败原因；只读快照不修复、不持久化历史、不去重、不发送通知。Reader/匿名请求分别应为 403/401；Operator/Administrator 的实际浏览器/接口验收仍按待定事项执行。
- 当前验证：本机 Restore PASS；Release Build 0 warnings / 0 errors；Unit 272/272、Architecture 1/1、Contract 3/3 PASS。完整 Integration 50 项中 6 通过、42 项因本机 Docker Engine 不可用而 BLOCKED、2 项按环境跳过。提交 `7e03def` 的远端 CI `33132755108` GREEN（Unit 272/272、Architecture 1/1、Contract 3/3、Integration 48 通过/2 跳过，Runtime smoke、Redis 1/1、备份恢复和 Diagnostics 均通过）；Docker `33132755124` GREEN（四镜像）。
- 边界：外部通知路由、告警历史/去重、保留策略、生产渠道与 RPO/RTO 关联仍未实现；本轮不执行 MuMu/阅读 3.0、真实来源、真实切源和人工验收。

### 4.29 CI Security Scan 基线 v1（本轮，2026-08-28）

- 缺口：1.0 要求依赖漏洞、Secret、SAST、容器扫描和 SBOM 证据，但此前没有独立安全扫描工作流，也没有发布前镜像阻断。
- 实现：新增 `.github/workflows/security.yml`，执行 NuGet 传递依赖漏洞审计、Trivy 源码/配置/依赖的 HIGH/CRITICAL 漏洞、Secret 与 Misconfiguration 扫描、C# CodeQL SAST 和 CycloneDX 源码 SBOM；审计、Trivy、CodeQL 和 SBOM 结果作为工作流产物保留。由于仓库未启用 GitHub Code Scanning API，结果不上传到代码扫描面板。
- 发布保护：`.github/workflows/docker.yml` 先构建并加载 API、Migrations、Scheduler、Worker 四个镜像，逐一执行 Trivy HIGH/CRITICAL 漏洞扫描，全部通过后才推送所有镜像标签。
- 远端证据：`f58599b` 的 CI `33134804300`、Security `33134804292`、Docker `33134804238` 均完成且通过；CodeQL、NuGet、Trivy、SBOM、Runtime smoke、Redis 限流、备份恢复、Diagnostics 和四镜像发布前扫描均有成功作业证据。后续文档提交会再次触发同一组 CI/Security/Docker 验证。
- 边界：`ignore-unfixed` 使不可修复漏洞不阻塞当前基线；生产镜像准入、扫描报告长期保留、Secret 轮换、动作版本治理、更广泛资源/组织权限治理和外部告警仍未完成。来源级资源授权 v1 已落地，但真实来源、阅读 3.0、MuMu、真机和带凭据人工验收继续按待定清单执行。

### 4.30 Resource-level Source Authorization v1（本轮，2026-08-28）

- 缺口：既有 `OperationsRead` / `SourceOperations` 只表达 Operator/Administrator 的平台级角色边界，无法限制 Operator 仅访问被明确授权的来源，也没有授权授予、查询和撤销的管理入口。
- 实现：Identity 新增 `PermissionGrant` 聚合、`permission_grants` 表与官方 EF Migration；新增 Administrator-only 的来源授权列表、授予和撤销 API。授权目标限定为 active Operator，权限为 `source.read` / `source.manage`，active `source.manage` 隐含 `source.read`；撤销保留历史，active grant 通过部分唯一索引保证幂等。
- 接入与边界：来源健康查询、来源能力停用/恢复，以及 Operations overview/alerts 的来源健康区块执行来源级授权；Administrator 绕过授权，Reader、匿名和无 grant 的 Operator 被拒绝。Crawler 与 consistency 区块在 v1 继续沿用平台级 `OperationsRead`，不伪装成来源级过滤；组织/租户/计费和通用私有资源权限不在范围内。
- 安全与审计：授予、撤销和拒绝结果记录认证操作者、来源、理由、结果及资源 reference；理由有界并拒绝控制字符，响应不返回凭据、Token 或正文。
- 当前证据：本机 Restore PASS；Release Build 0 warnings / 0 errors；Unit 279/279、Architecture 1/1、Contract 3/3 PASS。完整 Integration 51 项中 6 通过、43 项因 `npipe://./pipe/docker_engine` 不可用而 BLOCKED、2 项跳过；本轮未执行带凭据的本地 Runtime/人工验收。
- 远端证据：修复后的提交 `a663cef` 的 CI `33137358470`、Docker `33137358485`、Security `33137358428` 均 GREEN；CI 的 Runtime smoke、Redis 分布式限流、PostgreSQL 备份恢复和 Diagnostics，Docker 四镜像发布前扫描，以及 Security 的 NuGet/SBOM/Trivy/CodeQL 均通过。
- 边界：本轮不宣称完成 MuMu/阅读 3.0、真实来源/故障切换或带真实凭据的来源授权/Operations 人工验收；更广泛资源、组织/租户权限治理以及审计生产法律/合同保留、归档和删除授权仍待后续。

### 4.31 Legado Contract Release Gate v1（本轮，2026-08-28）

- 缺口：已有 Legado DTO/端点和书源生成逻辑，但 ContractTests 之前只验证程序集加载与 Personal header，未独立锁定 `Generate Rule → JSON Validate → Search → BookInfo → TOC → Content` 发布门禁。
- 实现：新增 `LegadoCompatibilityProfile` 与 `ILegadoRuleGenerator`/`LegadoRuleGenerator`；API 公共书源清单和 Personal Token 签发统一经过生成器 seam，静态 `LegadoBookSourceManifest.Generate` 保留为兼容入口。
- 验证：`LegadoContractReleaseGateTests` 使用已落库正典内存夹具，检查生成规则/JSONPath、Web JSON 字段、稳定 ID 和 Search → BookInfo → TOC → Content 连续读取；Contract 5/5、Unit 279/279、Architecture 1/1、Release Build 0 warnings / 0 errors PASS。
- 远端证据：提交 `aae5295` 的 CI `33138900850`、Docker `33138900845`、Security `33138900869` 均 GREEN；Runtime smoke、Redis 分布式限流、PostgreSQL 备份恢复、Diagnostics、四镜像扫描以及 NuGet/SBOM/Trivy/CodeQL 均通过。Security 仅有 Node 20 弃用和未启用 Code Scanning 的非阻断告警。
- 边界：完整 Integration 本机仍为 51 项中 6 通过、43 项因 Docker Engine `npipe://./pipe/docker_engine` 不可用而 BLOCKED、2 项跳过；本轮不执行真实来源、阅读 3.0 真机、HTTP 客户端导入或人工验收。该门禁自动化通过不替代外部 Release Gate。

### 4.32 Private Library v1 后端基础（本轮，2026-08-28）

- 缺口：用户身份和 Reading State 已有基础能力，但没有与公共 Canonical Library 隔离的私人书目实体、迁移和用户范围 API。
- 实现：Library 新增独立 `PrivateBook` 聚合与 `private_books` 表；PrivateBookId 与公共 BookId 分离，复合主键包含 UserId；仓储所有读取/更新/删除显式按认证主体范围执行，非所有者与不存在记录统一为 NotFound。领域词汇见根目录 `CONTEXT.md`，边界决策见 ADR 0007。
- API：新增受保护的 `GET/POST/GET/{id}/PUT/{id}/DELETE/{id} /api/v1/me/private-library/books`，覆盖书名与可选作者元数据 CRUD；输入有界，删除为当前无正文阶段的所有者直接删除。
- 边界：本轮不进入 Canonical、公共搜索、Legado、Content Policy 或公共 Reading Shelf；TXT/EPUB 导入、私有正文/章节、导出恢复、浏览器 UI 和人工验收另行处理。
- 当前证据：Restore PASS；Release Build 0 warnings / 0 errors；Unit 289/289、Architecture 1/1、Contract 5/5 PASS。全量 Integration 54 项中 6 通过、46 项因 `npipe://./pipe/docker_engine` 不可用而 BLOCKED、2 项跳过；新增 PostgreSQL 集成用例已编译并实际尝试但未取得容器证据。API `/health` 200；受限私有路由命中匿名认证门控，但本机 Redis/PostgreSQL 不可用，未宣称完整端到端 Runtime 通过。
- 远端证据：提交 `204c651` 的 CI `33150804876`、Docker `33150804885`、Security `33150804900` 均 GREEN；CI 的 Restore/Build/Test、Compose、Runtime smoke、Redis 限流、PostgreSQL 备份恢复和 Runtime diagnostics，Docker 的四镜像构建/扫描，以及 Security 的 NuGet、Trivy、CodeQL 和 SBOM 均通过。
- 验收边界：按用户决定不执行 MuMu/阅读 3.0、真实来源/切源、真实追更和人工操作；Private Library 的真实账户、跨用户隔离、公共路径不泄漏和删除语义仍列入待定事项。

### 4.33 Private Library v2 私有正文与 TXT/EPUB 导入导出（本轮，2026-08-28）

- 缺口：PrivateBook 元数据基础已经落地，但缺少独立私有章节、正文持久化、文件导入和可回收的导出闭环。
- 实现：新增独立 `PrivateChapter` / `PrivateContentDocument` 与 `private_chapters` Migration；正文保存为规范化段落和 SHA-256 校验，不复用公共 `ChapterId` / `ContentVersion`。TXT 支持 UTF-8/GB18030、章节标题和导出元数据；EPUB 读取 container/OPF/spine/XHTML，并拒绝路径穿越、DTD/外部实体和超出归档预算的输入。
- 导入语义：每次导入创建新的 PrivateBook 快照，章节与正文在一个持久化事务中落库；解析或校验失败不产生半本书，重复导入不覆盖既有书籍。
- API：新增受保护的 `POST /api/v1/me/private-library/import`、章节列表/正文读取和 `GET /api/v1/me/private-library/books/{id}/export?format=txt|epub`；所有读取显式按 UserId 限定，私有正文和导出响应使用 `Cache-Control: private, no-store`。
- 边界：私有正文不进入 Canonical Content、公共搜索、Legado、Content Policy、公共 Reading Shelf、共享缓存或 CDN；本轮不做私有正文编辑、版本恢复、发布为公共内容、浏览器 UI 或真实设备验收。决策见 `docs/adr/0008-private-content-import-snapshot.md`，词汇见根目录 `CONTEXT.md`。
- 当前证据：本机 Restore PASS；Release Build 0 warnings / 0 errors；Unit 299/299、Architecture 1/1、Contract 7/7 PASS。全量 Integration 55 项中 6 通过、2 跳过、47 项因 `npipe://./pipe/docker_engine` 不可用而 BLOCKED；新增 PrivateBook/PrivateChapter PostgreSQL 集成 4 项均实际尝试但未取得容器证据。`git diff --check` PASS。
- Runtime：本机 API `/health` 200；私有章节路由已注册，匿名请求最终返回 401。由于本机 Redis/PostgreSQL 未运行，限流和审计触发等待/降级，未宣称完整认证账户端到端通过。
- 远端验收：提交 `f83476a` 的 CI `33163145132`、Docker `33163145104`、Security `33163144984` 均 **GREEN**；CI 的 Restore/Build/Test、Compose、Runtime smoke、Redis 限流、PostgreSQL 备份恢复和 Diagnostics，Docker 的四镜像构建/扫描，Security 的 CodeQL/NuGet/Trivy/SBOM 均通过。Security 仅保留既有 Actions Node 20 弃用提示。
- 验收边界：按用户决定跳过 MuMu/阅读 3.0、真实来源、真实追更和人工操作；真实账户导入 TXT/EPUB、跨用户正文隔离、导出文件可读性和公共路径不泄漏继续列入待定事项。

### 4.34 Developer API / Commercial Foundation v1（本轮，2026-08-29）

- 缺口：1.0 Release Candidate 还缺少 Developer Application、生产 API Key、套餐 Entitlement、用户级月度配额和只读外部目录 API 的可审查基础。
- 决策：新增 ADR 0009。只提供 production opaque API Key 与 `catalog.read`；Free/Pro/Developer 为内置版本化套餐；活跃用户默认 Free；PostgreSQL 保存用户+UTC 月度 Usage Period 与不可变 Usage Ledger，Redis 只做快照加速；不接支付、OAuth、组织、sandbox、Community Marketplace 或管理型 Developer API。
- 实现：新增 Developers/Billing 模块与独立 schema/migrations；完成应用/密钥自助创建、列表、撤销、轮换，Administrator 套餐授予，Developer API `/api/developer/v1` 的 Search/Books/Chapters/Content 只读契约，`429/Retry-After` 配额超限和 `503` 配额故障闭合；应用撤销、用户停用、密钥撤销/过期均使认证失败。
- 安全与边界：密钥原文只在签发/轮换响应出现一次，持久化与审计不保存原文；Developer API 不触发来源抓取，不读取私人书库，不返回 SourceId/凭据；命令写入带资源引用的审计事件。公共/Legado/Developer 限流独立，Developer 专用认证先校验密钥，再按 API Key 短哈希分桶；缺失/无效密钥按 IP 分桶，Redis 操作超时配置化且有界。
- 自动化：新增 Developers/Billing 领域/服务单测、Developer API 契约门禁、认证 Handler 安全测试、模块加载边界和 PostgreSQL Testcontainers 迁移/密钥撤销/跨密钥用户级配额测试；本机新增集成用例已实际尝试，但 Docker Engine `npipe://./pipe/docker_engine` 不可用而 BLOCKED，不能记为通过。
- 最终本地证据：`dotnet restore InkFlow.sln` PASS；完整 Release Build 0 warnings / 0 errors PASS；Unit 311/311、Architecture 1/1、Contract 9/9 PASS；完整 Integration 58 项中 6 通过、2 跳过、50 项因 `npipe://./pipe/docker_engine` 不可用而 BLOCKED。API `/health` 返回 200，匿名 Developer 管理/目录接口返回 401；本机 Redis/PostgreSQL 未运行，未宣称完整 Runtime 或 PostgreSQL/Redis 端到端通过；`git diff --check` PASS。
- 远端验收：候选提交 `a0cc247` 的 CI `33241178943`、Docker `33241178942`、Security `33241178945` 均 **GREEN**。CI 的 Restore/Build/Test、Compose、Runtime smoke、Redis 限流、PostgreSQL 备份恢复和 Diagnostics，Docker 的四镜像构建/扫描，Security 的 CodeQL/NuGet/Trivy/SBOM 均通过；Security 仅保留既有 Actions Node 20 弃用提示。
- 当前状态：此工作包为 `1.0 Release Candidate`，自动化 Release Gate 已通过；仍是 `Implemented`，不是 `Accepted/Completed`。人工/真实环境验收按待定事项执行。
- 边界：按用户决定不执行 MuMu/阅读 3.0、真实来源、真实追更和人工验收；Developer API 生产凭据创建/轮换/撤销、套餐管理、配额超限、跨账户隔离、真实 PostgreSQL/Redis/Compose 运行验收仍需后续环境/人工门禁。

### 4.35 Developer 生命周期上限并发加固（本轮，2026-08-29）

- 缺口：应用创建和 API Key 签发原先采用“先查询再写入”，多 API 实例并发请求可能突破每用户 10 个应用、每应用 5 个活跃 Key 的既定上限；过期 Key 轮换也可能额外制造活跃 Key。
- 实现：Developer PostgreSQL Repository 在创建应用时按 UserId、创建/签发 Key 时按 ApplicationId 获取事务级 advisory lock，在同一事务内检查活跃数量后写入；Key 轮换复用同一 ApplicationId 锁，并拒绝在活跃 Key 已满时把过期 Key 轮换为新活跃 Key。服务层将持久化边界返回的拒绝映射为 `LimitReached`，不暴露生成中的原文密钥。
- 自动化：新增服务层上限拒绝回归 2 项、PostgreSQL 跨连接并发应用/Key 上限测试和过期 Key 轮换上限测试；本机 Release Build 0 warnings / 0 errors，Unit 313/313、Architecture 1/1、Contract 9/9 PASS。DeveloperBillingPersistenceTests 5 项实际尝试但因 `npipe://./pipe/docker_engine` 不可用而 BLOCKED；完整 Integration 60 项中 6 通过、2 跳过、52 项因 Docker Engine 不可用而 BLOCKED。
- 远端首轮 CI（`33242551277`）已实际启动真实 PostgreSQL：60 项中 57 通过、2 跳过，1 项失败原因为测试夹具生成的 seed 前缀不足 16 位并触发 `ArgumentOutOfRangeException`；过期 Key 轮换回归已在首轮远端通过。
- 修正提交 `638a18e` 的远端验证已通过：CI `33242669065` 中 60 项 58 通过、2 跳过，应用/Key 并发上限和过期 Key 轮换两个新增测试均通过；Docker `33242669053`、Security `33242669075` 均 GREEN。
- 当前状态：代码实现和远端真实 PostgreSQL 并发验证已完成，自动化 Release Gate 保持通过；不改变 `1.0 Release Candidate` 状态，也不替代待定事项中的人工/真实环境验收。

### 4.36 Operations 告警历史、去重与恢复状态（本轮，2026-08-29）

- 缺口：告警快照此前没有可追溯的 opened/resolved 历史、重复快照去重、并发协调、恢复状态、保留清理或受保护历史查询。
- 实现：新增 `InkFlow.Modules.Operations` 与 `operations` schema；`alert_incidents` 保存当前状态/last-seen/occurrence，`alert_history` 只保存稳定告警身份的 opened/resolved 转折。PostgreSQL 事务级 advisory lock 协调多 API 实例，完整快照才能恢复缺失 incident，partial/unavailable 快照不会误恢复；Migration 以触发器拒绝历史 UPDATE，按 `HistoryRetentionDays` 清理旧历史和过期 resolved 状态。
- API/权限：未过滤的 Administrator 告警快照接入持久化；新增 `GET /api/v1/admin/operations/alerts/history`，默认 50、最多 100 条，时间戳+事件 ID 不透明游标分页。平台级历史只对 Administrator 开放，Operator 仍只能读取来源过滤快照；查询故障返回稳定 `operations_alert_history_unavailable`。
- 安全边界：历史不写入动态 message、异常原文、Token、IP、连接串或正文；外部通知渠道、生产路由/治理不在本轮实现。
- 本地证据：Release Build 0 warnings / 0 errors；Unit 317/317、Architecture 1/1、Contract 10/10 PASS；完整 Integration 64 项中 6 通过、2 跳过、56 项因本机 `npipe://./pipe/docker_engine` 不可用而 BLOCKED，其中新增 4 项 Operations PostgreSQL Testcontainers 均已实际尝试；EF model pending-check、`git diff --check` PASS；API `/health` 200，匿名历史入口 401。
- 远端证据：候选提交 `4ef206f` 的 CI `33244304809` GREEN（64 项集成测试 62 通过、2 跳过，含 Restore/Build/Test/Compose/Runtime smoke/Redis 限流/备份恢复/Diagnostics），Docker `33244304814` GREEN，Security `33244304804` GREEN（NuGet、SBOM、Trivy 和 CodeQL）。
- 当前状态：保持 `1.0 Release Candidate`，不标记 `Accepted/Completed`；Operations Center 历史 API/UI 的自动化已补齐，真实 PostgreSQL/Redis、真实来源、阅读 3.0 和管理员/Operator 人工验收继续按待定清单执行。

### 4.37 Operations Center 告警历史 UI 增量（本轮，2026-08-29）

- 目标：把 4.36 的管理员告警历史 API 接入已有 Operations Center 页面，形成当前快照、历史转折与恢复状态的连续排查路径。
- 实现：管理员可刷新最新历史并加载更早页；页面使用不透明游标，展示稳定告警代码、资源坐标、发生时间、出现次数及“已触发/已恢复”。Operator 不发起平台级历史请求，只显示权限提示，服务端 Administrator-only 约束保持不变。
- UX/安全：沿用既有 Operations Center token、响应式表格和可访问状态提示；历史数据全部通过安全 DOM 节点写入，不展示动态 message、异常原文、Token、任务变量或正文，也不缓存认证响应。
- 本地证据：Release Build 0 warnings / 0 errors；Unit 317/317、Architecture 1/1、Contract 10/10 PASS；页面结构包含历史 API、分页控件和恢复文案，匿名历史 API 401，脚本 Node syntax check PASS；完整 Integration 64 项仍有 56 项因本机 Docker Engine 不可用而 BLOCKED。
- 远端验收：候选提交 `734c626` 的 CI `33245390370` GREEN（64 项集成测试 62 通过、2 跳过，含 Restore/Build/Test/Compose/Runtime smoke/Redis 限流/备份恢复/Diagnostics），Docker `33245390354` GREEN，Security `33245390350` GREEN（NuGet、SBOM、Trivy 和 CodeQL）。
- 边界：真实管理员/Operator 凭据、移动/桌面/宽屏视觉、键盘/对比度和截图验收继续保留在待定事项；本轮不标记 `Accepted/Completed`。

### 4.38 Core SLO 可观测性指标基线 v1（本轮，2026-08-29）

- 目标：为 1.0 Core SLO 建立可复核的服务面、可用性、延迟和 5xx 指标契约，补足通用 OpenTelemetry 自动 instrumentation 之外的业务边界。
- 实现：新增 `CoreSloPolicy` 与 `CoreSloMetricsMiddleware`，稳定映射 `public_api`、`legado_api`、`developer_api`、`reader` 四类服务面；记录 `inkflow.slo.requests`、`inkflow.slo.request.duration`（毫秒）和 `inkflow.slo.server.errors`，目标为 99.5% 可用性以及 public/developer 750ms、Legado/reader 1000ms 延迟 p95。预期 4xx 仍计入请求但不算服务端错误，5xx 计入 bad/error。
- 安全/配置：仅使用服务面和有限 outcome 标签，不携带路径参数、用户、IP、Token、异常原文或正文；`/health`、管理静态页、未知路径和来源内部请求不进入 Core SLO。OTLP exporter 仅在通用或对应 signal endpoint 显式配置时启用，应用不新增公开 `/metrics` 路由。
- 本地证据：Release Build 0 warnings / 0 errors；Unit 320/320、Architecture 1/1、Contract 10/10 PASS；完整 Integration 64 项中 6 通过、2 跳过、56 项因本机 `npipe://./pipe/docker_engine` 不可用而 BLOCKED。API `/health` 200、`/metrics` 404（按设计）；本机 PostgreSQL/Redis 未运行，`/reader` 数据链路未宣称端到端通过。
- 远端验收：候选提交 `a87c5ae` 的 CI `33246490603` GREEN（Unit 320/320、Architecture 1/1、Contract 10/10、Integration 64 项 62 通过/2 跳过，另有 Redis 限流集成 1/1；含 Restore/Build/Compose/Runtime smoke/备份恢复/Diagnostics），Docker `33246490571` GREEN，Security `33246490589` GREEN（NuGet、SBOM、Trivy 和 CodeQL）。
- 当前状态：真实 Collector、SLO 窗口/合成探针、错误预算告警/保留治理，以及 MuMu/阅读 3.0、真实来源和人工验收均按待定事项处理。本工作包保持 `1.0 Release Candidate`，自动化 Release Gate 已通过，不标记 `Accepted/Completed`。

### 4.39 Core SLO 窗口证据评估契约（本轮，2026-08-29）

- 缺口：Core SLO 指标已有记录出口，但没有统一窗口层判断；零流量、缺 p95、延迟样本不匹配和未知服务面不能默认当作达标。
- 实现：新增无状态 `CoreSloEvidenceEvaluator`。输入为明确窗口、证据来源和四个稳定服务面的请求/5xx/延迟聚合；输出 `Passed`、`Failed`、`InsufficientEvidence`、`InvalidEvidence`，并带稳定 reason code、可用性、99.5% 错误预算剩余和服务面 p95 目标。缺任一服务面、无正流量、p95 缺失或样本不完整时 `IsPassing=false`。
- 边界：不连接 Collector、不新增数据库/公开 API、不伪造生产窗口证据；结果不包含路径、用户、Token、异常原文或其他高基数数据。真实 OTLP/探针窗口与告警治理继续待部署环境验收，详见 ADR 0011。
- 本地证据：`dotnet restore InkFlow.sln` PASS；Release Build 0 warnings / 0 errors；Unit 324/324、Architecture 1/1、Contract 10/10 PASS；Integration 64 项中 6 通过、56 项因本机 Docker Engine 不可用而 BLOCKED、2 项跳过；API `/health` 200、`/metrics` 404（按设计）。
- 远端证据：提交 `71aa1a8` 的 CI `33247413751`、Docker `33247413755`、Security `33247413756` 均 **GREEN**；Security 仅有既有 `upload-artifact@v4` Node 20 弃用提示，不影响工作流结论。
- 当前状态：保持 `1.0 Release Candidate`，本工作包的自动化契约已完成，但不标记 `Accepted/Completed`。

### 4.40 Compose OTLP Collector 监控基线（本轮，2026-08-29）

- 缺口：应用 OTLP exporter 和 Core SLO 窗口评估契约已就绪，但 Compose 没有接收端，无法在 Runtime smoke 中确认观测出口的启动与健康边界。
- 实现：两份 Compose 加入固定版本官方 `otel/opentelemetry-collector:0.159.0`，配置位于 `deploy/observability/otel-collector-config.yaml`；OTLP gRPC/HTTP 只在内部网络监听，健康端口 `13133` 仅绑定 loopback。API、Worker、Scheduler 默认指向 `http://otel-collector:4317`，并保留 `OTEL_EXPORTER_OTLP_ENDPOINT` 覆盖入口。
- 安全/边界：Collector 配置只读挂载，服务启用 read-only、tmpfs、`no-new-privileges` 和 `cap_drop: ALL`；Docker 门禁先扫描固定版本 Collector，再扫描四个业务镜像。当前 debug exporter 只作本地/CI 接收诊断，不提供生产持久化、查询、告警或保留。健康 smoke 不等同于生产 SLO 月度达标，决策见 ADR 0012。
- 本地证据：Docker CLI 不存在，Compose config/Runtime smoke/Testcontainers 仍为 BLOCKED；候选提交 `3a891ef` 的远端 CI `33248301675`、Docker `33248301684`、Security `33248301664` 均 GREEN。CI 通过 Compose config、Collector loopback 健康 Runtime smoke、Restore/Build/Test、Redis 限流、备份恢复和 diagnostics；Docker 先扫描 Collector，再完成四个业务镜像构建/扫描/发布；Security 的 NuGet、Trivy、CodeQL、SBOM 均通过，仅保留既有 Actions Node 20 弃用提示。
- 当前状态：保持 `1.0 Release Candidate`。生产 OTLP 后端、四服务面到达、合成探针/窗口、错误预算告警和保留治理仍是 Release Gate。

### 4.41 Core SLO Runtime 合成探针基线（本轮，2026-08-29）

- 缺口：Collector 已能在 Compose 内部接收遥测，但 Runtime smoke 还没有对四个 Core SLO 服务面形成统一、可复核的请求与 p95 证据。
- 实现：新增 `scripts/core-slo-runtime-smoke.sh`，固定探测公共目录（200）、空查询 Legado（200）、未授权 Developer API（预期 401）和 Reader 页面（200）。每面默认 5 次请求，单请求超时 10 秒且上限 60 秒；失败、超时或非预期状态立即失败，不自动重试，也不保存响应正文。
- 证据：脚本生成包含 UTC 窗口、四面请求数/5xx 数/延迟样本数/p95 的 JSON，CI 上传 30 天构建产物。空 Legado 查询不触发真实来源，Developer 探针不使用真实凭据；证据可以映射到 `CoreSloWindowEvidence`，但不直接宣称生产 SLO 达标。远端 artifact 已解析确认 schemaVersion=1、四面各 5 requests/5 samples/0 server errors。
- 本地证据：Docker CLI 不存在，源码 Compose/真实 API Runtime 探针与 Testcontainers 仍为 BLOCKED；Bash 语法、fixture 回归和 `git diff --check` PASS，Release Build 0 warnings / 0 errors，Unit 324/324、Architecture 1/1、Contract 10/10 PASS；全量 Integration 64 项为 6 通过、2 跳过、56 项 BLOCKED。
- 远端证据：提交 `d5a8ef3` 的 CI `33249393448`、Docker `33249393438`、Security `33249393437` 均 GREEN；CI Runtime smoke、四面合成探针和 evidence artifact 上传均通过。
- 当前状态：自动化合成探针基线已进入 Release Gate，仍保持 `1.0 Release Candidate`；真实 OTLP 后端、长窗口聚合、错误预算告警、保留治理以及用户决定延后的人工/真实来源验收继续按待定清单执行。

### 4.42 Core SLO Collector metrics 到达验证（本轮，2026-08-29）

- 缺口：上一轮合成探针的四面 JSON 已通过，但短 Runtime smoke 未必等到 metrics 默认导出周期，Collector 日志未能证明 Core SLO metrics 到达。
- 实现：Compose 透传 `OTEL_METRIC_EXPORT_INTERVAL`；CI 使用 1000 毫秒，Collector metrics 使用独立 1 秒 batch。新增 signal-specific `debug/metrics`，默认 basic，CI 临时 detailed；receipt smoke 校验两个 Core SLO instrument 和四个稳定服务面标签。
- 安全/边界：详细 metrics 诊断只在 CI 期间启用，不新增公开 metrics API，不保存响应正文、身份、Token 或真实来源内容；生产仍需受治理 OTLP 后端、窗口聚合、告警和保留策略。
- 本地证据：配置/工作流 diff 检查、Restore、Release Build（0 warnings / 0 errors）、Unit 324/324、Architecture 1/1、Contract 10/10 和脚本回归 PASS；Docker CLI 不存在，Compose config/Collector receipt/源码 Compose 本地仍为 **BLOCKED**。
- 远端证据：候选提交 `0a1200e` 的 CI `33250749036`、Docker `33250749038`、Security `33250749023` 均 GREEN。CI receipt 实际匹配两个 Core SLO instrument 和 `public_api`、`legado_api`、`developer_api`、`reader` 四个标签；artifact 为 schemaVersion=1，四面各 5 requests/5 samples/0 server errors。
- 当前状态：Collector metrics 到达已纳入自动化门禁，继续保持 `1.0 Release Candidate`，不标记 `Accepted/Completed`；生产后端、长窗口 SLO、告警/保留治理和人工/真实来源验收仍待定。

### 4.43 Transactional Outbox / Inbox 基础恢复（本轮，2026-08-29）

- 缺口：`InkFlow.BuildingBlocks.Messaging` 只有空项目文件，Phase 0 文档中已宣称完成的 Outbox/Inbox 契约、表结构、租约投递和重复消费保护并未存在。
- 实现：新增有界 JSON `IntegrationMessage`、消息类型/载荷摘要校验、`messaging.outbox_messages` 与 `messaging.inbox_messages` Migration；Outbox 用 `FOR UPDATE SKIP LOCKED` + lease + attempt 支持 at-least-once，Inbox 用消息 ID 主键和处理成功标记实现幂等。
- 事务接线：`ITransactionalOutboxWriter` 要求业务 DbContext 已有活动事务；Crawler `AddAsync` 将任务行与最小 `crawler.task.created` 消息同事务提交，载荷不含 variables、章节 ID 或 credential reference。其他模块尚未自动接入，不能扩大宣称范围。
- 本地证据：Release Build 0 warnings / 0 errors、Unit 327/327、Architecture 1/1、Contract 10/10 PASS；新增 7 项真实 PostgreSQL 集成测试因本机 Docker `npipe://./pipe/docker_engine` 不可用 BLOCKED。
- 远端证据：提交 `dd80e2d` 的 CI `33252929657`、Docker `33252929642`、Security `33252929646` 均 GREEN；CI 真实 PostgreSQL 集成 71 项为 69 通过/2 跳过，新增 7 项 Messaging 用例全部通过，且 Compose、Runtime smoke、Core SLO receipt、Redis、备份恢复和 diagnostics 均通过；Docker 四镜像与 Collector 扫描通过，Security 的 NuGet/SBOM/Trivy/CodeQL 通过。保留既有 Actions Node 20 弃用提示。

### 4.44 Transactional Outbox / Inbox 执行层（本轮，2026-08-29）

- 缺口：基础表、租约和 Crawler 事务写入已经恢复，但此前没有可复用的 Dispatcher / Consumer 执行闭环来驱动成功确认、失败退避和 Inbox 幂等状态转换。
- 实现：新增 `OutboxDispatcher`、`IntegrationMessageConsumer`、Handler Registry、Publisher/Handler 接口、稳定失败码和有界指数退避。发布成功后才确认 Outbox；发布失败记录 `publish_failed` 并释放租约重试，确认异常不提前确认。Handler 成功后才确认 Inbox；未知类型和 Handler 异常记录稳定失败码，异常文本不写入持久化记录。
- 边界：本轮只提供可测试的执行层和传输/处理接口，不选择或接入未定义的 MQ，也不扩大宿主后台生命周期；实际适配器、宿主轮询和业务 Handler 接入仍需后续按模块推进。
- 本地证据：Release Build 0 warnings / 0 errors、Unit 334/334、Architecture 1/1、Contract 10/10 PASS；完整 Integration 74 项中 6 项通过、2 项跳过、66 项因本机 Docker `npipe://./pipe/docker_engine` 不可用 BLOCKED；Messaging Persistence/Execution 10 项已实际尝试但无法取得本机容器证据。
- 远端证据：提交 `fa81db7` 的 CI `33253938424`、Docker `33253938404`、Security `33253938443` 均 GREEN；CI 真实 PostgreSQL 集成 74 项为 72 通过/2 跳过，10/10 Messaging Persistence/Execution 用例通过，Unit 334/334、Compose、Runtime smoke、Core SLO receipt、Redis、备份恢复和 diagnostics 均通过；Docker 四镜像与 Collector 扫描通过，Security 的 NuGet/SBOM/Trivy/CodeQL 通过。保留既有 Actions Node 20 弃用提示。

### 4.45 Messaging Outbox/Inbox 保留清理与 Worker 周期接线（本轮，2026-08-29）

- 缺口：Outbox/Inbox 已具备成功确认和失败重试语义，但已处理历史记录没有有界保留清理；长期积压会增加事实表与索引维护成本。
- 实现：新增 `MessageRetentionOptions`、`MessageRetentionService` 和 `IMessageRetentionStore`。按 `BatchSize` 与 `MaxBatchesPerRun` 双重上限计算 Outbox/Inbox cutoff，只删除 `ProcessedAt` 已设置且早于 cutoff 的记录；失败、待重试、未处理和仍被锁定的消息保留。PostgreSQL 使用事务内 `FOR UPDATE SKIP LOCKED` 分批删除；Worker 注册 `MessagingDbContext`，按 `Messaging:Retention` 配置在启动延迟后每小时执行清理。
- 边界：本轮只接入消息事实表保留清理，不选择 MQ、Publisher 或业务 Handler；传输适配和业务事件宿主仍需后续按模块推进。
- 本地证据：Restore PASS；Release Build 0 warnings / 0 errors；Unit 338/338、Architecture 1/1、Contract 10/10 PASS；完整 Integration 76 项中 6 项通过、2 项跳过、68 项因本机 `npipe://./pipe/docker_engine` 不可用而 BLOCKED。新增 Retention 单测 4/4 通过；Messaging Persistence/Execution/Retention 12 项已实际尝试但未取得本机容器证据；`git diff --check` PASS。
- 远端证据：候选提交 `bf6eae1` 的 CI `33255354693`、Docker `33255354699`、Security `33255354684` 均 GREEN。CI 真实 PostgreSQL 集成 76 项为 74 通过/2 跳过，12/12 Messaging Persistence/Execution/Retention 用例通过，Unit 338/338、Compose、Runtime smoke、Core SLO receipt、Redis、备份恢复和 diagnostics 均通过；Docker 的 Collector 与四业务镜像构建/扫描/发布通过；Security 的 NuGet、Trivy、CodeQL、SBOM 全部通过，保留既有 `actions/upload-artifact@v4` Node 20 弃用提示。

### 4.46 Migration 漂移安全与完整性门禁（本轮，2026-08-29）

- 缺口：Migrations 入口会对 11 个上下文自动执行 `MigrateAsync`，此前没有在执行前拒绝模型快照漂移，CI 也没有逐一覆盖全部上下文。
- 实现：`InkFlow.Migrations` 在每个上下文的 `MigrateAsync` 前调用 `HasPendingModelChanges()`；检测到漂移时输出稳定错误并以退出码 1 停止。新增 `.config/dotnet-tools.json` 锁定 `dotnet-ef` 10.0.4，`scripts/verify-migrations.sh` 通过 API 启动项目、Release 产物和 `--no-build` 逐一检查 11 个上下文。
- 本地证据：`dotnet tool restore` PASS；Migrations Release Build 0 warnings / 0 errors PASS；11 个 `has-pending-model-changes` 检查全部 PASS；`bash -n scripts/verify-migrations.sh` 与 `git diff --check` PASS。完整 Solution Test 为 76 项 Integration 中 6 项通过、2 项跳过、68 项因本机 Docker/数据库环境不可用而 BLOCKED；Unit 338/338、Architecture 1/1、Contract 10/10 PASS。
- 边界：本轮建立模型漂移 fail-closed 与上下文覆盖门禁，不替代生产 Expand → Migrate → Contract 评审、真实数据库迁移演练或人工/真实来源验收。
- 远端证据：候选提交 `5878652` 的 CI `33256728058`、Docker `33256728051`、Security `33256728081` 均 GREEN；CI 新增 `Verify migrations` 实际逐一检查 11 个上下文并通过，Docker 的 Migrations/API/Worker/Scheduler 镜像与 Collector 检查通过，Security 的 NuGet、Filesystem、CodeQL、SBOM 全部通过。保留既有 `actions/upload-artifact@v4` Node 20 弃用提示。
- 当前状态：Migration 自动安全门禁已实现并取得三类远端门禁证据，整体仍保持 `1.0 Release Candidate`；真实 PostgreSQL Migrations/Compose、人工验收和真实来源验收仍按待定清单执行。

### 4.47 审计事实保留治理与 Worker 周期接线（本轮，2026-08-29）

- 缺口：`audit.events` 已具备追加式持久化和受保护查询，但此前没有可配置的过期清理；无限增长会增加审计表和索引维护成本，同时普通删除必须继续被数据库拒绝。
- 实现：新增 `AuditRetentionOptions`、`AuditRetentionService`、`IAuditRetentionStore` 和 PostgreSQL `EfAuditRetentionStore`。默认保留 365 天，按 `BatchSize` / `MaxBatchesPerRun` 双重上限，以 `(OccurredAt, Id)` 索引、事务和 `FOR UPDATE SKIP LOCKED` 分批删除 `OccurredAt < cutoff` 的事件；Worker 启动延迟后每小时执行。
- 安全/边界：新增 Migration 将追加式触发器调整为只对 retention transaction-local 标记放行删除，更新和普通直接删除仍失败；没有新增 API 或用户触发入口。生产法律保留、归档、恢复授权、删除审批和实际策略仍需部署治理，决策见 ADR 0014。
- 本地证据：`dotnet restore InkFlow.sln` PASS；Release Build 0 warnings / 0 errors；Unit 342/342、Architecture 1/1、Contract 10/10 PASS；`bash -n scripts/verify-migrations.sh` 与 PowerShell 等价的 11 个迁移模型检查 PASS；完整 Integration 78 项中 6 项通过、2 项跳过、70 项因本机 `npipe://./pipe/docker_engine` 不可用而 BLOCKED，新增 2 项审计保留集成测试已实际尝试但未取得本机容器证据；`git diff --check` PASS。
- 远端证据：候选提交 `b8046af` 的 CI `33257996992`、Docker `33257996951`、Security `33257996953` 均 GREEN。CI Test 为 78 项、76 通过/2 跳过，11 个迁移模型检查、Compose、Runtime smoke、Core SLO receipt、Redis、备份恢复和 diagnostics 全部通过；Docker 的 Collector 与 API/Migrations/Scheduler/Worker 四镜像构建、扫描和发布通过；Security 的 NuGet、Filesystem、CodeQL、SBOM 全部通过，保留既有 Actions Node 20 弃用提示。
- 当前状态：审计保留代码基线和 Worker 周期接线已完成并取得远端证据，整体继续保持 `1.0 Release Candidate`；生产法律/合同保留策略、归档与删除授权治理、本机 Docker 集成、真实来源、阅读 3.0 和人工验收仍按第 6 节待定。

### 4.48 Source Rule DSL v1 严格 JSON 契约与 Fixture 基线（本轮，2026-08-29）

- 缺口：Source DSL 已有 typed AST 与领域校验，但持久化仍使用默认 JSON 序列化；抽象 `RuleTransform` 没有稳定 wire shape，未知字段/转换类型、缺失必需字段和过大文档也没有统一的版本化 fail-closed 边界。
- 实现：新增 `SourceRuleDslJson` 版本化编解码器与 `docs/contracts/source-rule-dsl-v1.schema.json`。JSON 边界拒绝未知属性，要求构造参数对应的核心字段，限制文档大小、规则/字段/转换/映射及各类表达式长度；`trim` / `replace` 使用显式 `kind` AST，输出统一 camel-case 字符枚举，兼容读取既有数字枚举但不以数字写出。领域 Validator 同步空值、枚举、集合、长度、POST 表单和列表绑定约束。
- 持久化与回归：Sources EF 仓储统一经过该 codec；非法已存规则读取时 fail-closed，不静默执行。新增无第三方网络依赖的 `source-rule-dsl-v1.json` Fixture、内置 linovelib 定义往返测试、未知属性/未知转换/必需字段/超大文档测试，以及 PostgreSQL `RuleTransform` 往返集成测试；未新增 API 或 Migration。
- 执行边界：本工作包只建立最小可测试 schema/AST 与持久化契约，不宣称完整 DSL 引擎。Schema 保留 CSS/XPath/JSONPath 的 AST 枚举；当前 RuleAdapter 执行基线仍为 CSS，单请求的请求/响应字节、执行时间、正则时间和结果大小预算已在后续 4.49 接入；XPath/JSONPath、Cookie/Session、Pagination、通用变量扩展及多请求/递归的完整预算需单独回归，不能仅凭 JSON 解析通过标记为 Published 或真实来源可用。
- 本地证据：`dotnet build InkFlow.sln -c Release --no-restore` PASS（0 warnings / 0 errors）；Unit 353/353、Architecture 1/1、Contract 10/10 PASS；Schema JSON 语法检查与 `git diff --check` PASS。新增 Sources PostgreSQL 集成目标已编译，但本机 `npipe://./pipe/docker_engine` 不可用，实际容器执行 BLOCKED。
- 远端证据：`2451c72` 首次 CI 暴露既有 Search 仓储 Fixture 缺少列表绑定的问题，已在 `2966088` 修复并重新验证；最终 CI `33259952185`、Docker `33259952247`、Security `33259952205` 均 GREEN。CI Test 为 Unit 353/353、Architecture 1/1、Contract 10/10、Integration 79 项中 77 通过/2 跳过，新增 `Source_With_Transform_Rule_Dsl_Roundtrips` 通过；11 个 Migration 检查、Compose、Runtime/SLO telemetry、Redis、PostgreSQL 备份恢复和 diagnostics 全部通过。Security 保留既有 Actions Node 20 弃用提示，未影响门禁。
- 当前状态：Source DSL v1 最小 schema/AST、Fixture 和仓储边界已取得三类远端门禁证据，整体继续保持 `1.0 Release Candidate`；XPath/JSONPath 等执行能力、真实来源/故障切换、阅读 3.0 与人工验收和生产治理仍按待定清单，不等同于 `Accepted/Completed`。

### 4.49 Source Rule 单请求执行预算与响应体边界（本轮，2026-08-29）

- 缺口：Source Rule 执行此前没有统一的请求数、请求/响应体大小、执行时间、正则时间和结构化结果大小边界；生产 HTTP 客户端读取响应时会先完整载入再解码。
- 实现：新增不可变 `SourceRuleExecutionLimits`，默认 MaxRequests=1、MaxBytes=2 MiB、MaxExecutionTime=20 秒、MaxRegexTime=2 秒、MaxResultSize=512 KiB；API/Worker/Scheduler 注册同一默认快照。`RuleAdapter`、`RuleBasedSourceAdapter` 和 `ProductionSafeSourceHttpClient` 分别在请求、字段/列表结果和流式响应读取边界 fail-closed，预算超限不暴露部分结果，内部执行超时不泄漏异常文本。
- 非目标：当前只完成单请求执行预算；自动重定向仍为 SSRF Handler 固定 5 跳，XPath/JSONPath、Cookie/Session、Pagination 和递归 MaxDepth 仍需后续运行时工作包，真实来源和阅读 3.0 人工验收继续保留在待定事项。
- 本地证据：`dotnet restore InkFlow.sln` PASS；`dotnet build InkFlow.sln -c Release --no-restore` PASS（0 warnings / 0 errors）；Unit 363/363、Architecture 1/1、Contract 10/10 PASS；完整 Solution Test 的 Integration 为 79 项，其中 6 通过、2 跳过、71 项在类初始化时因本机 `npipe://./pipe/docker_engine` 不可用而 BLOCKED；`git diff --check` PASS。
- 远端证据：候选提交 `143685f` 的 CI `33261900485`、Docker `33261900470`、Security `33261900542` 均 **GREEN**。CI Test 为 79 项、77 通过/2 跳过，Restore/Build、11 个迁移模型检查、Compose、Runtime smoke、Core SLO probe/telemetry、Redis 分布式限流、PostgreSQL 备份恢复和 diagnostics 均通过；Docker 四业务镜像构建/扫描通过；Security 的 NuGet、Filesystem/Trivy、CodeQL 和 SBOM 均通过，仅保留既有 Actions Node 20 弃用提示。
- 下一步：继续处理真实第二 Official Source/故障切换与用户已延期的人工验收；本机 Docker 恢复后重跑 Integration 以取得本地容器证据。

### 4.50 Capability Health 并发变更串行化（本轮，2026-08-30）

- 缺口：健康服务原先采用先读后写，多个 API/Worker 实例并发上报失败时可能覆盖连续失败计数，延迟 Unhealthy 判定和自动切源。
- 实现：新增 `SourceHealthMutationKind` 和 `ISourceHealthRepository.MutateAsync`；健康服务的成功、失败、停用、恢复统一走原子入口。PostgreSQL 仓储在事务内以稳定 `(SourceId, Capability)` 摘要获取 advisory lock，再重新读取、应用 Domain 状态转移并提交；不新增 Migration。
- 自动化证据：本机 Restore/Release Build（0 warnings / 0 errors）、Unit 364/364、Architecture 1/1、Contract 10/10、11 个迁移模型检查均 PASS；完整 Integration 80 项中 6 通过、2 跳过、72 项因本机 `npipe://./pipe/docker_engine` 不可用而 BLOCKED。远端 CI `33263255422` 报告 Integration 80 项 78 通过/2 跳过，新增跨连接并发健康测试通过；Docker `33263255437`、Security `33263255420` 均 GREEN。
- 当前状态：本工作包保持 `1.0 Release Candidate`，远端已取得真实 PostgreSQL 并发证据；真实来源/故障切换、阅读 3.0、人工验收和生产治理仍是待定事项，不等同于 `Accepted/Completed`。

1. **Legado 真机验证（后续人工）**：在阅读 3.0 中导入 `/legado/book-source.json`，验证搜索/详情/目录/正文四步；本轮按用户决定不执行。
2. **Personal Legado Token 人工验收**：在阅读 3.0 导入签发响应中的 Personal 书源，验证 token header、Search → BookInfo → TOC → Content 和撤销后请求失效；本轮按用户决定不执行。
3. **Web Reader 人工视觉/功能验收**：在移动、平板、桌面和宽屏浏览器打开 `/reader` 三页面，检查正文宽度、设置面板、键盘焦点、触控目标、长文滚动和上下章导航；本轮只完成自动化 HTML/CI 基线。
4. **Reader/PWA 用户状态人工验收**：在支持的浏览器中验证账户登录/注册、刷新后会话、书架加入/移除、历史、章节进度/偏好同步、401 刷新、登出、安装提示、Service Worker 注册与网络不可用时离线提示；本轮按用户决定不执行。
5. **追更真实验证**：Scheduler 扫描 + Worker 消费已在容器环境运行，新章检测需真实源数据佐证。
6. **Phase 1B 真实切源验收**：从已接入来源中选择可稳定访问的真实第二 Official Source，验证 Source A 不可用时 Web/Legado 仍读取，且 BookId/ChapterId 不变。
7. **Content Policy 管理人工验收**：使用 Administrator 凭证验证下架/恢复、Operator/匿名拒绝、全公开读取路径隐藏/恢复和命令审计记录；本轮只完成自动化基线，未执行人工操作。
8. **Operations Center 人工验收**：使用 Operator/Administrator 凭证打开 /admin/operations，验证登录/角色拒绝、overview/告警快照读取、Administrator 告警历史分页与恢复转折、来源能力停用/恢复、死信理由确认与重放、HasMore 截断标记、区块部分失败展示和命令结果；检查移动/桌面布局、键盘焦点、对比度与截图证据。本轮只完成自动化基线，未执行人工操作。
9. **Admin Audit Read 人工验收**：使用 Operator/Administrator 凭证验证审计查询授权、时间范围/精确过滤/游标、空结果、稳定错误和响应脱敏；本轮只完成自动化基线，未执行人工操作。
10. **Source Authorization 人工验收**：使用 Administrator 授予/列出/撤销某个 Operator 的 `source.read` / `source.manage`，验证重复授予幂等、撤销后拒绝、`source.manage` 隐含读取、来源健康/停用/恢复及 Operations 来源健康区块过滤；验证 Reader/匿名和未授权 Operator 的 401/403、理由校验与授权审计。本轮只完成自动化基线，未执行真实凭据操作。
11. **生产备份恢复治理验收**：在目标部署环境配置加密/异地备份、保留与删除策略、恢复授权和 RPO/RTO；执行恢复演练并保留归档、校验和、行数签名、耗时及告警证据。本轮只完成 CI 级恢复演练。
12. **Private Library 人工验收**：使用两个真实账户验证私有书目创建、列表、详情、更新、删除和跨用户 404；上传真实 TXT/EPUB，验证章节/正文读取、导出文件可读性、重复导入不覆盖和失败导入无半本书；确认不进入公共 Catalog、搜索、Legado 或公共 Reading Shelf。本轮只完成自动化基线。
13. **Developer API / 商业基础人工验收**：使用真实 Web 账户创建/撤销应用与 API Key，确认原文只出现一次；由 Administrator 授予套餐，验证 Developer API 的目录读取、跨应用用户级配额、超额 `429/Retry-After`、密钥/应用/用户停用后的拒绝和审计；本轮只完成自动化基线，未使用真实凭据。
14. **生产 OTLP/SLO 窗口验收**：将 Collector 接入受治理持久化后端，确认 API/Worker/Scheduler/Reader 观测到达，基于合成探针与真实业务窗口完成聚合，并验收错误预算告警、访问控制和保留策略；当前 CI 探针仅为短窗口基线。
15. **继续推进 1.0**：在上述证据基础上完成第三来源真实验收、Private Library 真实账户/文件验收，并继续推进 Security/Operations、外部告警和组织/支付商业化能力。

当前推荐顺序：

```text
✅ kanunu8 真实源 + Source → Canonical → Content → Query E2E
✅ 双来源确定性夹具：CanonicalBook/Chapter 复用 + Quality Selection
✅ Capability Health v1：健康状态持久化 + 健康感知切源 + 选择审计
✅ CI/Docker 验证租约恢复与跨进程原子领取（`33060930049` / `33060930029`）
✅ CI/Docker 验证重试退避调度与 `ScheduledAt` Migration（`33062448255` / `33062448243`）
✅ 追更正文闭环：TOC 联动正文入队、死信不复活、Worker 短轮询（`33065212994` / `33065212936`）
✅ 抓取→发布桥 + 上游修订重扫：Content 任务产出 IsCurrent 版本、stale 复检保鲜（`33066966836` / `33066966966`）
✅ 搜索发现接入：/api/v1/search 与 Legado search 可发现未入库书目并自动建档（`33069358438` / `33069358437`）
✅ 自适应健康自动恢复：Unhealthy 冷却后半开重探、指数退避封顶一天（`33070869295` / `33070869320`）
✅ 主动巡检探针 + Reader 接入发现流（Progress 表对应记录）
✅ 冷却参数配置化：SourceHealth 配置节 → SourceHealthParameters，启动时装载（ADR 0005）
✅ linovelib Search 规则 + 中文表单编码/路径归一化离线回归（`33090147713` / `33090147561`）
✅ Crawler 失败结构化日志 + OTel counters（`2747e2b`，`33091872440` / `33091872458`）
✅ Crawler 死信受控重放：事务化、幂等、并发安全（`20f75fb` / `c2d4aeb`，`33094754193` / `33094754210`）
✅ 安全审计持久化：`audit.events` + 追加式触发器 + API/Legado 双写（`cc2a089`，`33096635143` / `33096635237`）
✅ SSRF 连接级约束：校验地址直连 + 端口/重定向限制 + 三宿主接线（`379cf79`，`33099136084` / `33099135992`）
✅ Source DSL v1 严格 JSON Schema/codec + Fixture + RuleTransform 持久化往返（`2966088`，CI `33259952185` / Docker `33259952247` / Security `33259952205`）
✅ Identity 基础认证/授权 + refresh 轮换 + 受保护死信 Repair/replay（`09ea265` / `9f9d5c7`，`33102831333` / `33102831388`）
✅ 跨模块 Consistency Check v1：只读四 schema 扫描 + 受保护 Admin 入口（`7dac6ce`，CI `33106044634` / Docker `33106044677` 均 GREEN）
✅ Content Policy / Takedown v1：公开读取门控 + Administrator 命令审计 + 追加式决策历史（`34c5c71`，CI `33109068649` / Docker `33109068630` 均 GREEN）
✅ Source Health Operator Controls v1：来源能力查询 + Operator/Administrator 停用/恢复 + 命令审计（`49e0fc1`，CI `33110684551` / Docker `33110684410` 均 GREEN）
✅ Operations/Repair Center Read Model v1：统一只读快照 + 独立查询 policy + 区块异常隔离（`ff02c23`，CI `33112741068` / Docker `33112741039` 均 GREEN）
✅ Personal Legado Token v1：一次性原文签发 + Hash 持久化 + 独立 header 认证 + Personal API + 撤销审计（`fbe0c62`，CI `33118314796` / Docker `33118314789` 均 GREEN）
✅ Web Reader v1：响应式书目/详情/章节页 + 主题/字号/行高本地设置 + 可访问章节导航（`a8d1c23`，CI `33120844695` / Docker `33120844685` 均 GREEN）
✅ Reader/PWA 用户状态 v1：账户/书架/历史/进度/偏好渐进增强 + 公开 PWA 壳（`b3561a2`，CI `33123325151` / Docker `33123325184` 均 GREEN）
✅ Admin Audit Read v1：独立 AuditRead policy + 有界精确过滤 + 稳定不透明游标（`29a723c`，CI `33128764947` / Docker `33128764869` 均 GREEN）
✅ PostgreSQL Backup/Restore Drill v1：custom-format dump/restore + 隔离库全表行数签名校验（`29c2c5f`，CI `33129734525` / Docker `33129734604` 均 GREEN）
✅ Redis Distributed Rate Limit v1：Redis Lua 原子 fixed-window + 独立连接集成验证 + 有界本地降级（`2bace7d`，CI `33131258779` / Docker `33131258754` 均 GREEN）
✅ Operations Alert Snapshot v1：来源健康/死信/一致性/Redis 告警快照 + 配置化阈值 + OperationsRead 保护（本工作包）
✅ Operations Alert History v1：PostgreSQL incident 去重/恢复 + opened/resolved 历史 + 保留清理 + Administrator-only 有界查询（`4ef206f`；CI `33244304809` / Docker `33244304814` / Security `33244304804` GREEN）
✅ Operations Center Alert History UI v1：管理员历史刷新/不透明游标分页 + opened/resolved 转折展示 + Operator 权限提示（`734c626`；CI `33245390370` / Docker `33245390354` / Security `33245390350` GREEN；真实与人工验收待定）
✅ Core SLO Observability v1：四类服务面 + 可用性/延迟/5xx 低基数指标 + OTLP 显式配置（`a87c5ae`；CI `33246490603` / Docker `33246490571` / Security `33246490589` GREEN；真实 SLO 窗口与人工验收待定）
✅ Core SLO 窗口证据评估契约：四面完整性 + p95/可用性 + 错误预算 + 缺证据 fail-closed（本轮；真实 Collector/合成探针窗口待定）
✅ Compose OTLP Collector 监控基线：固定版本 Collector + 内部 OTLP 接收 + loopback 健康 smoke + 三宿主默认接线（`3a891ef`；CI `33248301675` / Docker `33248301684` / Security `33248301664` GREEN；生产后端/窗口/告警/保留仍待定）
✅ Core SLO Runtime 合成探针基线：四服务面固定入口 + 有界状态/延迟采样 + UTC JSON artifact（`d5a8ef3`；CI `33249393448` / Docker `33249393438` / Security `33249393437` GREEN；生产 OTLP 后端/长窗口/告警/保留与人工验收待定）
  ✅ Core SLO Collector metrics 到达验证：1 秒 CI metrics 导出 + signal-specific receipt smoke + 四面 instrument/tag 校验（`0a1200e`；CI `33250749036` / Docker `33250749038` / Security `33250749023` GREEN；生产后端/长窗口/告警/保留与人工验收待定）
✅ Transactional Outbox / Inbox 基础恢复：消息契约 + PostgreSQL Migration + Crawler 任务同事务写入 + lease/幂等集成测试（`dd80e2d`；CI `33252929657` / Docker `33252929642` / Security `33252929646` GREEN；其他模块接入与人工/真实业务验收仍待定）
✅ Transactional Outbox / Inbox 执行层：Dispatcher/Consumer + 稳定失败码 + 有界重试（`fa81db7`；CI `33253938424` / Docker `33253938404` / Security `33253938443` GREEN；传输适配与宿主后台接线仍待选型）
✅ Messaging Outbox/Inbox 保留清理：过期已处理消息有界删除 + Worker 每小时周期接线（`bf6eae1`；CI `33255354693` / Docker `33255354699` / Security `33255354684` GREEN；本机 Docker 集成与真实/人工验收仍待定）
✅ Audit retention：过期审计事实有界删除 + 追加式触发器受控例外 + Worker 每小时周期接线（`b8046af`；CI `33257996992` / Docker `33257996951` / Security `33257996953` GREEN；生产法律/合同保留与归档治理仍待定）
✅ Capability Health 并发变更串行化：事务级 advisory lock + 服务原子变更契约 + 跨连接 PostgreSQL 并发回归（`3ba51a1`；CI `33263255422` / Docker `33263255437` / Security `33263255420` GREEN；本机 Docker、真实来源/切源和人工验收仍待定）
✅ CI Security Scan 基线 v1：NuGet/Trivy/CodeQL/SBOM + 四镜像发布前扫描（`f58599b`，CI `33134804300` / Security `33134804292` / Docker `33134804238`）
✅ Resource-level Source Authorization v1：来源授权授予/列表/撤销 + 来源查询/控制过滤 + 命令审计（`a663cef`，CI `33137358470` / Security `33137358428` / Docker `33137358485`）
✅ Legado Contract Release Gate v1：Compatibility Profile + Rule Generator seam + Generate/JSON/Search/BookInfo/TOC/Content 自动门禁（本轮；真实来源与真机验收待定）
✅ Private Library v1 后端基础：独立 PrivateBook/PrivateBookId + UserId 范围仓储 + 迁移 + 受保护元数据 CRUD（本轮；真实账户/公共路径隔离人工验收待定）
✅ Private Library v2：独立 PrivateChapter/私有正文 + TXT/EPUB 导入导出 + 用户范围读取 + ZIP/XML 输入边界（`f83476a`，CI `33163145132` / Docker `33163145104` / Security `33163144984` 均 GREEN；真实账户/文件和公共路径隔离人工验收待定）
✅ Operations/Repair Center UI v1：受保护快照展示 + 来源能力控制 + 死信理由确认重放（ed0ff8c，CI 33125476460 / Docker 33125476441 均 GREEN）
✅ 第三个 Official Source 机制接入：17K CodeAdapter + 三宿主 SSRF 接线 + 幂等 Source 种子 + JSON Fixture 回归（本轮；真实验收待定）
→ Reader/PWA 浏览器安装、离线和账户链路人工验收
→ Private Library 真实账户与公共路径隔离人工验收
→ Legado 真机导入/阅读（后续人工）
→ 17K 真实 Search/BookInfo/TOC/Content 验收
→ 真实追更与真实第二来源切源演练
→ Phase 1A / Phase 1B 分别完成外部验收
→ 继续推进 1.0 Release Gates
```

### 4.1 本轮 Phase 1B 自动化证据

- `official-a` / `official-b` 确定性夹具复用一个 `CanonicalBook`，等价章节复用两个稳定 `CanonicalChapter`；每个正典章节有两个来源章节映射。
- `ChapterMapping` 记录 `chapter-alignment-v1` 与对齐证据；`ContentVersion` 记录 `quality-v1` 与质量证据。
- 低质量第二来源保存为独立候选，不替换已选正文；健康不可用时排除对应来源，全部不可用时保留当前版本；查询路径只读已落库当前版本。
- Release Build：PASS（0 warnings / 0 errors）。Unit 126/126、Architecture 1/1、Contract 1/1、双来源健康感知切源 2/2：PASS。
- 完整集成测试：本机 Docker 不可用，20 个 Testcontainers 用例在初始化阶段 BLOCKED；不得将其记为通过。远端 CI `33055478173` 已全绿，包含 Test、Compose Validation 与三服务 Runtime Smoke；Docker `33055478099` 的四个镜像也已全绿。
- EF 新迁移已用官方生成流程补齐 Designer，并由 `dotnet ef migrations list` 发现。

### 4.3 API 安全与可观测性基线

- `ApiRateLimitOptions` / `ApiRateLimitPolicies`：公共 API 与 Legado 独立 fixed-window 策略，匿名按连接层 IP、认证主体按 `sub` / `client_id` 短哈希分桶；未配置可信代理前不信任 `X-Forwarded-For`。
- `RequestAuditMiddleware` / `IAuditEventSink`：业务 API 请求和 `429` 拒绝均记录结构化 `AuditEvent`，去除 query string；`CompositeAuditEventSink` 同时写入 PostgreSQL `audit.events` 与结构化日志，数据库触发器保证普通路径追加式写入。Operations Center 已补齐粗粒度查询 policy；高风险命令的 before/after、资源级查询授权和生产保留治理仍未完成，有界清理代码见 4.47。
- `SsrfGuard` / `SsrfSafeHttpMessageHandler`：来源请求先做字面量与 DNS 全结果检查，再由连接回调直接连接同一批已校验地址；环境代理关闭，80/443 之外端口和超过 5 跳的自动重定向被拒绝。真实网络策略扫描和 live 来源证据仍未完成。
- 自动化证据：新增安全测试使 Unit 达到 133/133；Architecture 1/1、Contract 1/1、Release Build 0 warnings / 0 errors。API 本地烟测实际验证 `429` 与 `Retry-After: 60`；首次业务请求受本机 PostgreSQL 不可用影响返回 500。
- 全量测试仍有 20 个 Testcontainers 用例因本机 Docker 不可用而 BLOCKED，1 个跳过；远端 CI `33057431574` 与 Docker `33057431610` 已 GREEN，具体以远端实际记录为准。

### 4.4 Worker 租约恢复、原子领取与任务可靠性基础

- `CrawlerTask.IsLeasable` 与 `CrawlerLeaseService` 支持过期 `Leased`/`Running` 任务回收；重新领取会增加 `AttemptCount`，保留重试耗尽进入死信的不变量。
- `EfCrawlerTaskRepository.TryLeaseAsync` 在事务内以 `FOR UPDATE SKIP LOCKED` 完成候选筛选与租约写入；`FindLeasableAsync` 仅用于候选发现。Worker 已注册 `CompositeTaskExecutor`，并对单任务异常执行 `Fail → Pending/DeadLettered`，避免异常逃逸到外层轮询后留下不可恢复状态。
- 自动化证据：租约恢复回归测试 11/11；新增跨进程原子领取/过期 Running 回收集成用例 2/2；Unit 136/136、Architecture 1/1、Contract 1/1、Release Build 0 warnings / 0 errors；Worker `/health` 本地返回 200。PostgreSQL Testcontainers 本机有 22 个用例因 Docker 不可用而 BLOCKED，1 个 live 用例跳过；候选提交 `445d0bc` 已通过远端 CI `33060930049` 与 Docker `33060930029`，均为 GREEN。本节记录的是该历史工作包，最新重试调度证据见 4.5。

### 4.5 Crawler Task 重试退避与持久化调度

- `CrawlerTask.ScheduledAt` 表示下一次可领取时间：新任务立即可领取，失败且未耗尽尝试次数时使用 `RetryPolicy` 写入全抖动指数退避时间；完成、死信和租约回收清除调度时间。
- `FindLeasableAsync` 与 `TryLeaseAsync` 都过滤未来调度的 Pending 任务；Worker 失败路径在 `SaveAsync` 前计算并保存下一次尝试时间。
- 官方 Migration `AddCrawlerTaskScheduling` 增加可空 `crawler.tasks.ScheduledAt` 与 `(Status, ScheduledAt)` 索引，旧记录 `NULL` 保持立即可领取兼容性。
- 自动化证据：Unit 137/137、Architecture 1/1、Contract 1/1；远端 PostgreSQL 集成测试 30 个中 29 通过、1 个 live 用例跳过；Release Build 0 warnings / 0 errors；Worker `/health` 本地返回 200。候选提交 `3372180` 的 CI `33062448255` 与 Docker `33062448243` 均 GREEN。

### 4.6 追更正文闭环（本轮，2026-08-28）

- 目录同步 + 正典映射成功后，`ContentFetchChainService` 为"该来源从未抓取过正文"的章节自动入队 Content 任务：判定 = 书目存在有章节 ∧ Content 能力健康 ∧ 无 FetchArtifact ∧ 无同 `(source, content, chapter)` 阻止性任务。
- 新增 `ICrawlerTaskRepository.HasConflictingTaskAsync`（Pending/Leased/Running/DeadLettered 阻止、Completed 放行）与 `IFetchArtifactRepository.ListFetchedExternalChapterIdsAsync` 批量存在性查询；无 Schema 变更、无新 Migration。死信任务不会被周期扫描反复复活；后续通过受控 Repair seam 重放，公开管理入口仍待实现。
- Worker 轮询节奏：有任务时 250ms 短轮询消化联动批次，空闲回退 15s。
- 自动化证据：Unit 147/147（新增链式服务 7 例 + Handler 编排 3 例）、Architecture 1/1、Contract 1/1；远端 Integration 33 中 32 通过 + 1 live 跳过（新增 EF 阻止态矩阵与批量存在性用例全过）；Release Build 0 warnings / 0 errors；本机 docker_engine 缺失导致 24 例集成 BLOCKED，不记为通过；Worker `/health` 本地返回 200。候选提交 `94c8be9` 的 CI `33065212994` 与 Docker `33065212936` 均 GREEN。
- 未含：已抓正文的修订重扫、死信人工重放工具、多 Worker 并发消费。

### 4.7 抓取→发布桥与上游修订重扫（本轮，2026-08-28）

- 发布桥（接口倒置）：`ContentFetchTaskHandler` 抓取成功后把原文交给 `IChainedContentPublisher`；Worker 宿主 `MappingContentPublisher` 经 `ChapterMapping` 定位正典身份后调 `ContentPublishingService`，CanonicalHash 判重幂等 + 自带选优。发布基础设施异常显式转 `CrawlOutcome.Fail` 走既有重试退避；未映射章节返回 false 静默完成。
- 修订重扫：链式入队扩展为"零产物(new) ∨ 最新产物过期(now - `DefaultStaleAfter`=7d)(refetch)"；上游变化产生新 ContentVersion(版本追加不覆盖)，未变化复检行续期锚点、Content 侧哈希幂等零新增。死信章节下一保鲜周期自然重入队(非无限复活)。
- 语义修正:`SourceContentService` Unchanged 复检同样落相同哈希的真实产物行(最新产物时间=最近一次核查),该行为变更先改回归测试再实现。
- 新增 `IFetchArtifactRepository.ListRecentlyFetchedExternalChapterIdsAsync(since)` 批量保鲜查询;无 Schema 变更、无新 Migration。
- 自动化证据:Unit 153/153、Architecture 1/1(接口倒置未破坏依赖矩阵)、Contract 1/1;远端 Integration 34 中 33 通过 + 1 live 跳过;Release Build 0 warnings / 0 errors;本机 docker_engine 缺失致 Integration 27 例 BLOCKED 不记为通过;Worker 进程烟测 `/health` 200。候选提交 `3edb3dc` 的 CI `33066966836` 与 Docker `33066966966` 均 GREEN。
- 未含:stale 任务错峰调度、多 Worker 并发消费、publishing 失败可观测告警。

### 4.8 搜索发现接入（本轮，2026-08-29）

- 冷启动缺口:Legado/公共 API 搜索原本只过滤已入库书目,新书永远搜不到;v1 自动匹配服务自实现以来无生产调用方。
- `BookDiscoveryService`(Crawling.Application):健康过滤 → 多源搜索(失败隔离为逐源 warning)→ 幂等导入 BookInfo → v1 匹配(Confirmed 幂等/同名同作者挂接/新建)→ 按正典身份归并。导入书目自动进入 Scheduler→Worker 追更链路。
- API:新增 `GET /api/v1/search`(归并结果+warnings);Legado `/search` 先发现后返回落库数据,DTO 形态不变。
- Api 宿主补引 Sources/Crawling/Kanunu8 并扩展组合根;**进程烟测实测抓到 ProductionSafeSourceHttpClient 缺 IIpAddressResolver 注册的必然 DI 失败**,修复后复测通过。
- 自动化证据:Unit 159/159(发现服务 6 例)、Architecture 1/1、Contract 1/1(Legado DTO 未变);远端 Integration 35 中 34 通过 + 1 live 跳过;首次 CI RED 暴露 List 用例误按空库断言总数(共享容器残留的既有教训),改为专属 ID 断言后复绿——候选提交 `66fc150` 修复提交 `42ac47e`,CI `33069358438` 与 Docker `33069358437` 均 GREEN。
- 未含:结果排序/分页与全文检索评分、Discovery 异步化、Reader 页接入发现流。

### 4.9 自适应健康自动恢复（本轮，2026-08-29）

- 缺口:Unhealthy 是死胡同——能力连续三次失败后,扫描/发现的健康门控永远跳过该来源,而没有任何流量能再把成功结果送进健康表,恢复只能靠人工 Enable。
- 半开恢复(无 Schema 变更、无 Migration):`SourceHealthPolicy` 新增由**持久化失败计数 + UpdatedAt 推导**的探针冷却期(30 分钟起步、随失败深度翻倍、封顶一天);`ConsecutiveFailures` 不再封顶在阈值(深度是退避依据而非被丢弃);`SourceHealthService.IsAvailableAsync` 对冷却期满的 Unhealthy 来源放行——周期扫描/搜索发现的真实抓取天然充当探针,成败经既有 Record* 上报:成功回 Healthy 重置失败链,失败刷新锚点并延长冷却。
- 现有调用方(追更扫描/搜索发现/发布桥)零改动即获得自动恢复;Disabled 仍为人工终态。
- 自动化证据:Unit 163/163(冷却阶梯与边界、失败深度增长不受阈值截断、服务级半开流程:冷却内不可用→到期放行→探针失败冷却翻倍→二次到期→成功恢复)、Architecture 1/1、Contract 1/1、Release Build 0 warnings / 0 errors。候选提交 `ac0de64`,远端 CI 结论见 Progress 表。
- 未含:主动巡检式探测(Unhealthy 源在无自然流量时不会主动发探针)、冷却参数配置化。

### 4.10 Reader 搜索接入发现流（本轮，2026-08-29）

- 缺口:`/reader` 搜索表单不过滤、不触发发现——Web 端「搜索→详情→阅读」主路径断裂,4.8 的遗留项。公共 API/Legado 已接发现流而 Reader 未接。
- 实现:`GET /reader?q=` 非空先经 `BookDiscoveryService` 幂等发现,再经新增 `CatalogQueryService.SearchBooksAsync`(书名/作者大小写不敏感过滤,空白=浏览全部)从落库正典数据返回;`LegadoContractService.SearchAsync` 委托同一方法,三端过滤语义统一。发现整体异常仅降级提示,页面不阻断;端点接入公共限流(可同步触网)。
- UX/frontend-design:双空态文案(空库引导 / 无结果建议换词)、命中计数、部分来源不可用的人话降级提示,SourceId 与异常细节零泄漏(单测断言),搜索词回显转义(单测断言)。
- 自动化证据:Unit 175/175(+6)、Architecture 1/1、Contract 1/1(Legado DTO 未变)、Release Build 0 warnings / 0 errors。候选提交 `48c05a2`,CI/Docker 结论见 Progress 表。
- 未含:排序/分页/全文检索(v2)、Discovery 异步化。

### 4.11 冷却参数配置化（本轮，2026-08-29）

- 缺口:探针冷却曲线(3 次/30 分钟/封顶一天)是编译期 const——调整失败容忍度或重探节奏必须改代码重发布(4.9/4.10 遗留项)。
- 实现(无 Schema 变更、无 Migration、健康调用方零改动):曲线算法唯一实现移入 Domain record `SourceHealthParameters.ProbeCooldown`,`SourceHealthPolicy` 变为只读视图,组合根启动时 `Configure()` 装载。配置链:`SourceHealthOptions.FromConfiguration`(节 `SourceHealth`,环境变量 `SourceHealth__ProbeCooldownBaseMinutes` 等;缺省回退 v1,非法值启动快速失败)→ `ToParameters()` 扩展 → Api/Scheduler/Worker 三宿主装载。ADR 0005。
- 细节:`Configure(null)` 恢复默认走编译期常量而非静态属性快照,规避静态初始化次序缺陷;`SourceHealthOptions` 是 BuildingBlocks.Application 的纯 POCO(仅依赖 Configuration.Abstractions),模块映射扩展在 Sources.Application,依赖方向不破坏。
- 自动化证据:Unit 180/180(+5)、Architecture 1/1、Contract 1/1、Release Build 0 warnings / 0 errors;本机 Integration 与基线一致(29 例 docker BLOCKED 不记为通过)。候选提交与 CI/Docker 结论见 Progress 表。
- 未含:运行时热更新(仅启动时装载)、per-source 冷却粒度。

### 4.12 Crawler 死信受控重放（本轮，2026-08-28）

- 缺口：死信只能记录，无法通过受控的 Repair/Replay 流程恢复；正常修复不得依赖手工 SQL。
- 实现：`DeadLetterReplayCommand` 校验操作者和理由；`ICrawlerTaskRepairRepository` 作为 Application seam；EF/Npgsql 在单事务内以 `FOR UPDATE` 锁定死信和原任务，创建新的 `Pending` 任务并复制原始 payload / `MaxAttempts`，原任务继续保持 `DeadLettered`。
- 轨迹与幂等：原死信保留失败原因/尝试次数，并追加重放任务 ID、时间、操作者和理由；重复请求返回 `AlreadyReplayed`，并发请求只创建一个重放任务；已解决死信不再阻塞同变量后续任务。
- 迁移/接线：官方生成 `AddDeadLetterReplay` Migration；Worker 将 `ICrawlerTaskRepository` 与 `ICrawlerTaskRepairRepository` 指向同一个 scoped EF 实现。当前没有公开 Admin API，因此认证授权、命令级审计和 Repair Center 尚未声称完成；请求审计持久化基线已在后续 4.13 完成。
- 证据：本机 Release Build 0 warnings / 0 errors、Unit 189/189、Architecture 1/1、Contract 1/1；本机 PostgreSQL Testcontainers 因 `docker_engine` 不可用 BLOCKED。远端 CI `33094754193`、Docker `33094754210` GREEN，包含 Test、Compose、Runtime smoke 和四镜像。

### 4.13 安全审计持久化基线（本轮，2026-08-28）

- `AuditDbContext` 在独立 `audit` schema 中持久化不可变 `AuditEvent` 行；`PersistentAuditEventSink` 负责追加写入，API 的 `CompositeAuditEventSink` 同时保留结构化日志可见性。
- `AddAuditEvents` Migration 创建 `audit.events`、时间索引和数据库追加式触发器；更新和普通删除被拒绝，避免普通应用路径静默改写审计历史；受控 retention 删除见 4.47。
- `RequestAuditMiddleware` 继续覆盖 `/api`、`/legado` 和 `429`，不记录 query string；持久化失败隔离于请求结果，并输出运维错误。
- 证据：本机 Release Build 0 warnings / 0 errors、Unit 189/189、Architecture 1/1、Contract 1/1、API `/health` 200；本机 PostgreSQL Testcontainers 因 `docker_engine` 不可用 BLOCKED。远端 CI `33096635143`、Docker `33096635237` GREEN，审计集成测试通过、Runtime diagnostics 记录审计事件。
- 未含：认证/授权、公开 Admin/Repair Center、命令级 before/after 审计、查询授权、保留策略、告警和 Redis 分布式限流。

### 4.14 SSRF / SafeHttpClient 连接级约束（本轮，2026-08-28）

- `SsrfSafeHttpMessageHandler` 是来源 HTTP 的连接级安全 Adapter：每次建立连接时重新解析 DNS，全部解析结果必须通过 `IpAddressClassification`，然后用已验证 IP 建立 `Socket`，避免“预检查后由默认 DNS 再解析”的 rebinding 窗口。
- 关闭环境代理，限制目标端口为 80/443，允许自动重定向但最多 5 跳；目标字面量与重定向目标的连接均经过同一连接回调校验。API、Worker、Scheduler 的 `ISourceHttpClient` 与 Kanunu8 typed client 已接入。
- 证据：本机 Release Build 0 warnings / 0 errors、Unit 194/194、Architecture 1/1、Contract 1/1、API/Worker/Scheduler `/health` 均 200；本机 Integration 因 `docker_engine` 不可用 BLOCKED（32 个类初始化失败、6 个通过、1 个跳过）。远端 CI `33099136084` 与 Docker `33099135992` GREEN，新增 5 个 Handler 回归用例通过，Runtime smoke 完成。
- 未含：真实来源网络验证、真实重定向服务演练、阅读 3.0 真机验收；这些继续保留在人工/真实环境待定事项。

### 4.2 待定事项（人工/真实环境，后续处理）

> 本轮按用户决定不执行；完成后补充可复核证据，未完成前不关闭 Phase 1A/1B Release Gate。

- [ ] **阅读 3.0 真机**：在 MuMu 中导入 `/legado/book-source.json`，验证 Search → BookInfo → TOC → Content，并记录结果。
- [ ] **Web Reader 人工 UX/视觉验收**：移动端、桌面端、宽屏、长标题/缺封面/长作者、加载/空/错、键盘焦点、触控和上下章导航。
- [ ] **Reader/PWA 用户状态人工验收**：验证账户登录/注册、刷新会话、书架/历史/进度/偏好同步、登出、PWA 安装提示、Service Worker 注册和离线提示；本轮按用户决定跳过。
- [ ] **真实追更**：用真实来源数据验证 Scheduler → Worker → 目录增量 → 正文发布闭环。
- [ ] **真实第二来源故障切换**：从已接入来源中选择可稳定访问的真实第二 Official Source；禁用 Source A 后验证 Web/Legado 可继续读取，BookId/ChapterId 不变；恢复后不得产生重复 Canonical 身份。
- [ ] **linovelib 真实 Search/阅读链路**：网络环境可用后验证 Search → BookInfo → TOC → Content，并把该来源纳入真实第二来源/故障切换演练；本轮仅完成离线规则回归，未触网。
- [ ] **17K 真实 Search/阅读链路**：网络环境可用后验证 Search → BookInfo → TOC → 免费 Content、VIP 访问边界和安全重定向；本轮仅完成 Fixture 回归，未触网。
- [ ] **本机 Docker 集成复验**：Docker 可用后重跑完整 Testcontainers 集成测试；当前全量 80 项中 72 项因 `docker_engine` 不可用而 BLOCKED、2 项跳过、6 项通过，其中包含 Sources Capability Health 并发变更测试；本机未取得真实容器证据。
- [ ] **生产 OTLP 后端与 SLO 窗口验收**：在部署环境将 Collector 接入受治理的持久化后端，验证 API/Worker/Scheduler/Reader 观测到达，执行合成探针和窗口聚合，并验收错误预算告警、访问控制与保留策略；Compose debug exporter/健康 smoke 仅为接收基线。

扩展新来源的方式(书源兼容层):
- 规则型站点:在 sources 表登记含 RuleDsl 的 Source 记录,零代码;
- 复杂站点(特殊编码/签名):实现 `ISourceAdapter`(参考 `KanunuSourceAdapter`)并在适配器工厂注册。

普通 PR CI 不依赖真实第三方小说站点；Crawler 使用固定 Fixture/Mock Server。真实 Source 进入独立 Live/Nightly 检查。

## 5. 关键架构不变量

未经 ADR 不得破坏：

1. 对外 BookId / ChapterId 稳定。
2. `SourceBook != CanonicalBook`，`SourceChapter != CanonicalChapter`。
3. 正常阅读路径不得依赖同步实时爬取。
4. 新正文创建新 ContentVersion，不覆盖旧正文。
5. Match / Alignment / Selection / Failover 必须可解释、可追踪、可撤销。
6. Legado 是一级协议，有独立 Contract 与测试。
7. 公共与私人内容授权严格隔离。
8. Redis 不是关键事实数据唯一存储。
9. Community Source 禁止无限制代码执行。
10. Modular Monolith 优先，不提前微服务化。
11. 每个工作包必须经过真实 Build/Test/Runtime/CI/Fix/Regression/Documentation Gate。

## 6. Source Runtime 约束

```text
ISourceAdapter
├── RuleAdapter   # DSL / 配置，大多数站点
└── CodeAdapter   # 仅可信官方复杂适配
```

抓取层级：HTTP → Session/签名 → Playwright → 人工辅助会话。

Community Source 必须受限 DSL，并通过 SafeHttpClient；禁止任意 Shell/C#/JS eval、文件、Socket 权限。

安全至少覆盖：

- SSRF，包括 DNS rebinding / redirect 再校验。
- IPv4/IPv6 私网、loopback、link-local、metadata endpoint 阻断。
- Request / Bytes / Time / Regex 预算。
- Credential 只传引用，不放入 Task Payload。

## 7. 领域所有权

```text
Library  → CanonicalBook / CanonicalChapter / matching / alignment
Sources  → Source / Rule / RuleVersion / Capability / Health Policy
Crawling → Task / Lease / Retry / DeadLetter / Fetch Artifact
Content  → AST / ContentBlob / ContentVersion / Quality / Selection
Reading  → Reader preference / progress / bookshelf-facing state
Legado   → Protocol DTO / Rule Generator / Compatibility Profile
Identity → User / Session / Token / Credential identity
```

Crawler 只执行抓取并产出结果，不拥有 Canonical Match 或最终 Content Selection。

## 8. Legado 主路径

```text
阅读 3.0
→ InkFlow 官方 bookSource
→ /api/legado/v1/*（公共）或 /api/legado/v1/personal/*（Personal Token）
→ Canonical Content
```

最小目标 API：

```text
GET /api/legado/v1/search?q=
GET /api/legado/v1/books/{bookId}
GET /api/legado/v1/books/{bookId}/chapters
GET /api/legado/v1/chapters/{chapterId}
GET /legado/book-source.json
```

规则由 `ILegadoRuleGenerator` 生成，不长期手改静态 JSON 作为唯一事实来源。

## 9. 数据与一致性

- PostgreSQL 是事实数据来源。
- Redis 仅承载可重建状态。
- Crawler Task Source of Truth 在 PostgreSQL。
- Outbox + At-Least-Once + Inbox/Idempotent Consumer。
- 生产 Migration 由独立 Migrations App 执行，API 不自动迁移。
- Schema 变更遵循 Expand → Migrate → Contract。

## 10. 当前未完成

Phase 1A / 1B 外部验收：

- 阅读 3.0 导入 `/legado/book-source.json`，Search → BookInfo → TOC → Content 真机验证（按用户决定后续人工执行）。
- Scheduler/Worker 使用真实更新数据的追更验证。
- 第二个真实 Official Source 与真实故障切源演练；当前只有确定性双来源夹具和 17K 离线 CodeAdapter 证据，不能替代真实来源验收。
- linovelib 已完成 Search 规则的离线定义与回归，真实网络验证仍受 DNS 污染影响，待可用环境复验。
- 本机 Docker 缺失导致 PostgreSQL Testcontainers 集成测试待本机可用容器环境复验；本轮一致性检查新增用例已在远端 CI PostgreSQL 容器中通过。

Phase 2 及以后：

- Source Health 的半开恢复、主动巡检探针与冷却参数配置化已完成；Crawler 死信受控重放、受保护 Repair/replay 入口、跨模块 Consistency Check v1、Operations Center Read Model v1 和 Center UI v1 自动化基线已完成，自动修复和更强运维治理仍待实现。
- Crawler 失败结构化日志与 OpenTelemetry counters、请求审计持久化、独立 `AuditRead` 有界查询、CI 级 PostgreSQL 备份恢复演练、告警快照/阈值/内部历史去重与恢复、来源级授权 v1 和已落地高风险命令审计基线已完成；审计有界 retention 代码基线已完成，但生产法律/合同保留、归档、删除授权和证据治理仍待部署环境确定。外部告警路由、生产异地备份/RPO-RTO、安全扫描治理、组织/更广泛资源权限仍待实现。限流已接入 Redis 原子分布式计数，并在 Redis 故障时保留同配额本地有界降级。
- 用户身份基础、Reading State v1、Reader/PWA 用户状态 v1（账户/书架/历史/进度/偏好接入、公开安装壳）、Personal Legado Token v1、Web Reader v1、Private Library 私有正文/TXT/EPUB 导入导出自动化基础和 Developer API / Entitlement / Billing v1 候选基线已完成；PWA 实际安装/离线/跨设备验收、Private Library 与 Developer API 真实账户/凭据验收、Organization、Community Marketplace 仍未完成。

更后阶段：Developer API / Commercial Foundation 的真实运营与产品化深化、Organization、Community Marketplace、Enterprise Deployment。

## 11. 每轮强制闭环

```text
明确目标/验收
→ 实现
→ Diff 自检
→ Restore/Build
→ Unit/Architecture/Integration/Contract Tests
→ Runtime/业务链路验收
→ Security/Architecture 检查
→ Candidate Commit
→ 实际 CI
→ 失败读取日志并修根因
→ 全量回归
→ Progress/Handoff/Contract 同步
→ Accepted / Completed
```

禁止通过删除测试、弱化断言、隐藏 warning 或反复重跑来伪造 Green。

## 12. 开始下一阶段前检查

- [x] `dev` 分支远端 CI（含 Runtime Smoke）首跑确认 GREEN（Run `32821162412`），骨架阶段 Completed。
- [x] Phase 1A 自动化链路与 kanunu8 真实源端到端验证已在 `dev` 上重建并通过相应证据。
- [ ] Legado 真机导入/阅读与真实追更仍待执行。
- [x] Personal Legado Token v1 的自动化签发、Hash 持久化、header 认证、Personal API 与撤销审计已完成；阅读 3.0 导入、四步阅读和撤销后失效仍待人工执行。
- [x] Web Reader v1 的服务端渲染、响应式结构、阅读设置与 HTML 安全回归已完成；浏览器四尺寸视觉、焦点、触控和长时间阅读仍待人工执行。
- [x] Reader/PWA 用户状态 v1 的账户/书架/历史/进度/偏好渐进增强、公开 PWA 壳与 CI Runtime smoke 已完成；PWA 安装、离线、账户真实链路和跨尺寸浏览器验收仍待人工执行。
- [x] 已阅读并按 `phase-1-acceptance.md` 建立 Phase 1B 双来源自动化基线。
- [x] Capability Health v1 与确定性健康感知故障切源已建立自动化基线。
- [ ] 第二个真实 Official Source / 真实故障切源尚未验收。
- [x] 当前租约恢复与跨进程原子领取候选改动已完成 Docker/CI 验证；真实设备、真实来源和本机 Docker 集成复验仍未完成。
- [x] Source DSL v1 已定义可测试的最小 schema/AST，不提前做万能脚本语言；完整 XPath/JSONPath 等执行引擎仍待后续工作包。
- [x] Fixture 驱动，无真实第三方 Source PR-CI 依赖。
- [ ] 新 Source 网络能力必须同步安全测试。
