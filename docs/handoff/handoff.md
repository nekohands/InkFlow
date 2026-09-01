# InkFlow 工程交接文档

> 用于开发者、AI Agent 或未来会话快速、安全接手 InkFlow。真实状态以仓库与 CI 为准。

- 产品：墨流 / InkFlow
- 当前阶段：1.0 Release Candidate（Phase 1B 确定性运行时/商业基础/前端自动化门禁已通过，真实来源与外部验收待定）
- 当前工作分支：`dev`（2026-08-25 起）
- 文档状态：5.32 的 CollectionRun 取消终态/幂等领域回归补强已同步；行为候选为 `5bdb4ea`，测试提交为 `3aab3e8`，当前 HEAD/文档同步提交为 `7d60235`。当前 HEAD 的 CI `33479935777`、Docker `33479935816`、Security `33479935776` 均 GREEN 且 head SHA 一致。采集/打包 VM 与 Compose 证据见 5.18、5.21、5.22、5.25；最新取消终态回归交接见 5.32。
- `dev` 骨架 root commit：`c5f2048`
- 交接日期：2026-09-01；dev 骨架重建更新：2026-08-25

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

**当前状态（2026-09-01 更新）**：Phase 1A 的自动化链路与 kanunu8 真实源验证已通过；Legado 真机导入/阅读和真实追更仍待人工验收。Phase 1B 已完成确定性双来源自动化切源基线（含 Capability Health v1），但尚未宣称完成真实故障切源验收。Worker 已具备过期租约恢复、跨进程原子领取和持久化重试退避调度；Crawler 死信受控重放基线已补齐，Identity 基础认证/授权与受保护 Repair/replay 入口也已落地，Reading State v1 用户状态后端、Personal Legado Token v1、Web Reader v1、Reader/PWA 用户状态 v1 和 Private Library v1/v2（书目、私有章节、TXT/EPUB 导入导出）自动化基础已接入，真实账户/文件验收仍待推进，公开修复中心仍待后续安全/运维工作。CI Security Scan 基线 v1 已落地并通过远端 CI、四镜像发布前扫描和报告归档；来源级资源授权 v1 已落地并通过自动化/远端验证，生产安全治理、更广泛资源/组织权限、外部告警路由和备份治理仍待后续工作。Developer API / Commercial Foundation v1 已完成候选实现；5.13 又在源码构建 Compose 中通过 Free 配额超额 `429/Retry-After`、跨账户独立配额和停用用户拒绝自动化 smoke，远端 CI、Docker、Security 门禁均为 GREEN；真实凭据、真实套餐/Provider、生产 PostgreSQL/Redis 和人工验收仍待后续。Operations 告警历史、incident 去重/恢复、保留清理和 Administrator-only 历史读端已补齐；外部通知渠道不在本轮实现。Personal 令牌的阅读 3.0 导入、四步阅读和撤销后失效，以及 Web Reader/PWA 的真实账户、安装/独立窗口、生产 HTTPS、跨设备同步和长时间体验保留为人工验收；PWA Service Worker、壳缓存和 API 不可用时的离线回退已在 4.82 用 localhost 安全上下文自动验收。Source Credential Owner Scope 契约 v1 已接入 Provider、RuleAdapter 与 Worker：Platform/User/Organization 范围被显式区分，来源默认引用固定按 Platform 解析，真实 secret 管理与 Provider 仍待后续。

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

### 4.51 Source Rule 受控 XPath/JSONPath 运行时（本轮，2026-08-30）

- 缺口：Rule DSL 虽已声明 CSS/XPath/JSONPath，运行时此前只有 CSS；Search/TOC 列表绑定固定按 CSS 解释。
- 实现：`RuleSelectorEvaluator` 已接入 API、Worker、Scheduler。它分派 CSS、XML XPath、HTML 受限 XPath fallback 和受限 JSONPath；HTML 支持常见路径、属性/文本谓词与属性终端，JSONPath 支持 root/property/quoted property/index/wildcard/recursive-property。列表绑定增加可选 `itemsSelectorKind`、`textAttribute`，旧四参数构造与旧 JSON 继续按 CSS/文本工作。
- 安全/回归：XML 禁止 DTD/外部实体；选择器、文档、深度、遍历和匹配数量有界；不支持语法、非法 CSS 和超限输入 fail-closed。新增 9 项求值回归及 DSL JSON/Validator 回归，覆盖 JSON 列表、XML/HTML XPath、非法表达式、DTD 和列表元数据；三宿主统一注入该实现。
- 本地证据：Restore PASS；Release Build 0 warnings / 0 errors；Unit 376/376、Architecture 1/1、Contract 10/10 PASS；PowerShell 等价迁移检查 11/11 PASS；API `/health` 200。完整 Integration 80 项为 6 通过、2 跳过、72 项因本机 Docker Engine 命名管道不可用而 BLOCKED；真实来源、MuMu/阅读 3.0 和人工验收按用户决定未执行。
- 远端证据：提交 `2f16b6e` 的 CI `33265352562`、Docker `33265352563`、Security `33265352595` 均 GREEN；CI 报告 Unit 376/376、Architecture 1/1、Contract 10/10、Integration 80 项 78 通过/2 跳过，并通过 Runtime/SLO/Redis/备份恢复/diagnostics。
- 当前状态：受控选择器运行时完成并取得三类远端门禁，整体仍为 `1.0 Release Candidate`；完整选择器语法、Cookie/Session/Pagination/多请求递归、真实来源故障切换、阅读 3.0 和人工验收仍在待定清单，不等同于 `Accepted/Completed`。

### 4.52 Source Rule 受控 next-link Pagination / 多请求预算（本轮，2026-08-30）

- 缺口：RuleAdapter 之前固定只发起一次请求，Search/TOC 无法安全聚合 next-link 分页；页面循环也没有统一的请求数、累计响应字节和执行时间边界。
- 实现：`CapabilityRule` 增加可选 `RulePagination`，仅允许 Search/TOC 的 List 绑定；首请求沿用原 method/form，后续链接固定 GET。CSS next selector 必须提供链接属性，XPath/JSONPath 复用受控选择器求值；`RuleBasedSourceAdapter` 汇总所有通过校验的页面，旧规则保持兼容。
- 安全/失败关闭：后续 URL 必须与首请求保持相同 scheme/host/port，并重新通过 SSRF 字面量检查；拒绝 userinfo、fragment、控制字符、非法/过长链接、循环和跨源。`maxPages` 有界为 1..32，默认 8；所有页面共享 MaxRequests、累计响应字节和单一执行超时。任何边界或传输失败都整体失败，不返回部分页面/结果。
- 定向证据：RuleAdapter 分页 6/6、分页列表 2/2、Validator 类 20/20、JSON 往返 1/1、累计响应字节 1/1 PASS；完整本地与远端门禁已在下方补录。
- 本地证据：Restore PASS；Release Build 0 warnings / 0 errors；Unit 389/389、Architecture 1/1、Contract 10/10、11 个迁移模型检查均 PASS；Schema/Fixture JSON 语法和 API `/health` 200；完整 Integration 80 项中 6 通过、2 跳过、72 项因本机 `npipe://./pipe/docker_engine` 不可用而 BLOCKED；`git diff --check` PASS。
- 远端证据：首个候选提交 `83aa68d` 的 CI `33267442596` 因 Linux 将 `/search?page=2` 相对链接误解析为 `file:` 而失败 6 项；Docker `33267442625`、Security `33267442628` 为 GREEN。修复提交 `c4cddcd` 的 CI `33267729513`、Docker `33267729544`、Security `33267729548` 均 GREEN；CI Unit 389/389、Architecture 1/1、Contract 10/10、Integration 80 项 78 通过/2 跳过，迁移 11/11、Compose、Runtime/SLO、Redis、PostgreSQL 备份恢复和 diagnostics 均通过。Security 的 NuGet、Filesystem、CodeQL、SBOM 均通过，仅保留既有 Node 20 弃用与 CodeQL API 权限提示。
- 非目标：page-number/cursor、Cookie/Session、通用变量、next-link 之外的多请求/递归 MaxDepth、完整 XPath/JSONPath 语法和真实来源/阅读 3.0 人工验收仍未完成。
- 当前状态：受控 next-link Pagination 候选实现完成，整体保持 `1.0 Release Candidate`，尚不能标记 `Accepted/Completed`。

### 4.53 Source Rule page-number / cursor Pagination（本轮，2026-08-30）

- 缺口：4.52 只支持响应 next-link；API 型来源需要页码或游标续页，但续页参数、终止条件和多请求预算必须仍由 DSL/执行器统一控制。
- 实现：新增 `RulePaginationMode.PageNumber` 与 `RulePaginationMode.Cursor`。页码模式使用规则已声明且唯一的 query/form `parameterName`，按 `startPage`/`pageStep` 递增并由 `nextPageSelector` 判断继续；游标模式由 `cursorSelector` 读取下一游标并写回同一参数，保留原请求 method。省略 `mode` 的既有 JSON 仍为 next-link。
- 安全/失败关闭：GET 续页只允许 query；页码值限制 0..1,000,000，游标限制 2,048 字符并拒绝控制字符；所有模式共享 MaxRequests、MaxPages、累计响应字节和执行时间预算。重复游标、配置错误、来源不一致或预算超限时整体失败，不暴露部分页面。
- 定向证据：RuleAdapter/Validator/JSON 69/69 PASS；完整本地与远端门禁已在本节补录。
- 本地证据：Restore PASS；Release Build 0 warnings / 0 errors；Unit 399/399、Architecture 1/1、Contract 10/10 PASS；完整 Integration 80 项中 6 通过、2 跳过、72 项因本机 `npipe://./pipe/docker_engine` 不可用而 BLOCKED；PowerShell 等价迁移模型检查 11/11、Schema/Fixture JSON 语法、API `/health` 200 和 `git diff --check` PASS。Git Bash 迁移 wrapper 在 Windows 中因找不到 `dotnet` 未通过。
- 远端证据：提交 `0e9164b` 的 CI `33269606086`、Docker `33269606076`、Security `33269606147` 均 GREEN。CI 真实 PostgreSQL 集成共 80 项，78 通过、2 跳过；Unit 399/399、Architecture 1/1、Contract 10/10，11 个迁移检查、Compose、Runtime/SLO、Redis、备份恢复和 diagnostics 全部通过。Docker 的 Collector 与四个业务镜像构建/扫描/发布通过；Security 的 NuGet、Filesystem、CodeQL、SBOM 全部通过，保留既有 Actions Node 20 弃用提示。
- 非目标：Cookie/Session、通用变量、完整 XPath/JSONPath 语法、受控分页之外的多请求/递归 MaxDepth、真实来源和阅读 3.0 人工验收仍未完成。
- 当前状态：page-number/cursor 形成可执行候选基线，并取得本地与远端三类门禁证据；整体仍为 `1.0 Release Candidate`，尚不能标记 `Accepted/Completed`。

### 4.54 Source Rule 受控 response-cookie Session（本轮，2026-08-30）

- 缺口：来源分页链路可能要求首个响应设置 Cookie，后续同源请求才能继续；此前没有声明式 Session 策略，也不能安全地把响应 Cookie 传过 HTTP seam。
- 实现：新增可选 `CapabilityRule.Session` / `RuleSession`，只保存 `maxCookies`、`maxCookieBytes`、`maxCookieLifetimeSeconds` 策略。`RuleAdapter` 为一次执行创建内存 Cookie jar，消费成功响应 `Set-Cookie`，按同源最终响应和 Domain/Path/Secure/Max-Age/Expires 匹配后向后续受控分页请求发送；`SourceHttpRequest.CookieHeader` 为临时传递字段，`SourceHttpResponse` 提供 `SetCookieHeaders` 和 `ResponseUri`。
- 安全/边界：生产 `SsrfSafeHttpMessageHandler` 的 `SocketsHttpHandler` 关闭共享 CookieContainer；Rule DSL/Adapter 拒绝静态 `Cookie` / `Set-Cookie` 头。Cookie 状态最多 32 个、累计 4 KiB、最长 3600 秒，不能进入持久化 JSON、Task Payload、日志、结果或下一次执行。跨源最终响应和资源上限整体失败，非法/外域 Cookie 不被采用。
- 回归：新增 RuleAdapter 的传播、执行隔离、路径、过期删除、跨源响应和上限测试；新增生产 HTTP 头传递/响应 Cookie 测试、Validator 边界测试和 JSON 往返测试。当前本地 Unit 410/410、Architecture 1/1、Contract 10/10 通过。
- 非目标：不实现 CredentialReference/ISecretProvider 的初始账号/Token 注入、跨任务或跨来源持久会话、完整 RFC Cookie/公共后缀策略、自动重定向中间响应 Cookie 或带 Cookie 请求的自动重定向、通用变量或递归多请求。真实来源、故障切换、阅读 3.0 与人工验收继续保留在待定清单。
- 本地证据：`dotnet restore InkFlow.sln` PASS；Release Build 0 warnings / 0 errors；Unit 410/410、Architecture 1/1、Contract 10/10 PASS；Schema/Fixture JSON 语法、PowerShell 等价迁移模型检查 11/11、API `/health` 200 与 `git diff --check` PASS。Git Bash 迁移 wrapper 仅完成 `bash -n` 语法检查，完整 wrapper 在 Windows 因找不到 `dotnet` 未执行；本机完整 Integration 80 项为 6 通过、2 跳过、72 项因 `npipe://./pipe/docker_engine` 不可用而 BLOCKED。
- 远端证据：候选提交 `6f52719` 已推送；[CI 33271405103](https://github.com/nekohands/InkFlow/actions/runs/33271405103)、[Docker 33271405122](https://github.com/nekohands/InkFlow/actions/runs/33271405122)、[Security 33271405107](https://github.com/nekohands/InkFlow/actions/runs/33271405107) 均为 GREEN，包含 Restore/Build/Test/Compose/Runtime smoke/Diagnostics、四镜像构建和 SBOM/Filesystem/CodeQL/NuGet 检查。
- 当前状态：受控 response-cookie Session 为已通过候选门禁的 `Implemented` 基线，不等同 `Accepted/Completed`；CredentialReference 初始认证/持久会话、真实来源/切源、阅读 3.0 和人工验收仍待后续。

### 4.55 Source Rule 有界请求模板变量（本轮，2026-08-30）

- 缺口：此前 Header 值不支持执行期模板，路径之外的模板花括号校验不统一，调用方变量上下文也没有统一的资源边界。
- 实现：路径、Header、Query、Form 模板值统一支持 `{name}` 占位符；`RuleAdapter` 在 HTTP seam 前渲染 Header 值并保持 Header 原值语义；`SourceRuleExecutionLimits` 新增变量数量、名称长度、单值长度和累计 UTF-8 字节预算。
- 安全/失败关闭：变量名符合 `[A-Za-z_][A-Za-z0-9_]*`，默认最多 32 个变量、单名 128 字符、单值 2,048 字符、累计 16 KiB；变量值和渲染 Header 名/值拒绝控制字符，发布期/执行期拒绝残留花括号，错误不回显变量值，边界检查均早于 HTTP seam。
- 回归：新增 Header 渲染、控制字符、变量数量/名称/单值/总字节预算、发布校验及失败不出网测试；本地 Unit 418/418、Architecture 1/1、Contract 10/10 通过。
- 非目标：不实现响应派生变量、CredentialReference/ISecretProvider 初始认证或持久会话、递归/通用多请求、完整 XPath/JSONPath 语法；真实来源/切源、阅读 3.0 和人工验收仍按待定清单处理。
- 本地证据：Restore PASS；Release Build 0 warnings / 0 errors；Unit 418/418、Architecture 1/1、Contract 10/10 PASS；完整 Integration 80 项 6 通过、2 跳过、72 项因本机 `npipe://./pipe/docker_engine` 不可用而 BLOCKED；PowerShell 等价迁移模型检查 11/11、Schema/Fixture JSON 语法、API `/health` 200 和 `git diff --check` PASS。Git Bash 迁移 wrapper 在 Windows 中因找不到 `dotnet` 未完成执行。
- 远端证据：候选提交 `dd39396` 已推送；[CI 33272774115](https://github.com/nekohands/InkFlow/actions/runs/33272774115)、[Docker 33272774105](https://github.com/nekohands/InkFlow/actions/runs/33272774105)、[Security 33272774138](https://github.com/nekohands/InkFlow/actions/runs/33272774138) 均 GREEN。CI 真实测试为 Unit 418/418、Architecture 1/1、Contract 10/10、Integration 80 项 78 通过/2 跳过，并完成迁移、Compose、Runtime smoke、Core SLO、Redis、PostgreSQL 备份恢复和 diagnostics；Docker 四业务镜像/Collector 扫描发布及 Security NuGet/Filesystem/CodeQL/SBOM 均通过。
- 当前状态：有界请求模板变量为通过候选门禁的 `Implemented` 基线，不等同 `Accepted/Completed`；响应派生变量、Credential 初始认证、真实来源/切源、阅读 3.0 和人工验收仍待后续。

### 4.56 Source CredentialReference 有界初始认证（本轮，2026-08-30）

- 缺口：`CrawlPayload.CredentialReferenceId` 之前只作为预留字段，活动 Worker 的 TOC、联动正文和 RuleAdapter 链路没有统一的安全解析与请求头投影。
- 实现：新增 `ISourceCredentialProvider`、`ConfigurationSourceCredentialProvider` 和非敏感 `SourceExecutionContext`；任务级引用贯通 TOC → 联动 Content → `RuleBasedSourceAdapter` → `RuleAdapter`，仅允许 typed Bearer、Basic 或受限 API-Key Header。配置适配器读取 `SourceCredentials:<sourceId>:<referenceId>`；未实现凭据能力的 CodeAdapter 会显式拒绝带引用的执行上下文。
- 安全/边界：引用 ID 最长 256 字符并拒绝路径注入；secret 不进入 Task Payload、Variables、Rule JSON、日志、错误文本、结果或 `ToString()`。凭据只在 URL/SSRF/请求预算通过后解析，并受 `MaxExecutionTime` 约束；缺失提供器、解析异常、超时、非法材料或规则头冲突均在 HTTP seam 前失败关闭。自定义 Provider 仍必须执行 Owner Scope 与跨租户授权。
- 回归：新增凭据三种 typed 头、分页复用、配置解析、TOC/Content 任务传播、CodeAdapter 拒绝和失败关闭测试；本地 Unit 430/430、Architecture 1/1、Contract 10/10 通过。
- 非目标：来源级默认凭据绑定、Scheduler/Admin 凭据管理、真实 SecretManager SDK、跨任务/跨来源持久会话、响应派生变量、递归/通用多请求和完整 XPath/JSONPath 语法仍未实现；真实来源、故障切换、阅读 3.0 和人工验收保留在待定清单。
- 本地证据：Restore PASS；Release Build 0 warnings / 0 errors；Unit 430/430、Architecture 1/1、Contract 10/10 PASS；完整 Integration 80 项 6 通过、2 跳过、72 项因本机 `npipe://./pipe/docker_engine` 不可用而 BLOCKED；迁移模型检查 11/11、Schema/Fixture JSON、API `/health` 和 `git diff --check` PASS。Git Bash 迁移 wrapper 在 Windows 中因找不到 `dotnet` 未完成执行。
- 远端证据：最终提交 `47992d7`（代码实现提交 `c32dc80`，本提交仅补写验证证据）的 [CI 33275310547](https://github.com/nekohands/InkFlow/actions/runs/33275310547)、[Docker 33275266875](https://github.com/nekohands/InkFlow/actions/runs/33275266875)、[Security 33275266884](https://github.com/nekohands/InkFlow/actions/runs/33275266884) 均 GREEN，且三项 Run 的 headSha 均为 `47992d7`；CI Unit 430/430、Architecture 1/1、Contract 10/10、Integration 80 项 78 通过/2 跳过，并完成迁移、Compose、Runtime smoke、SLO、Redis、PostgreSQL 恢复和 diagnostics；Docker 四业务镜像/Collector 通过，Security 扫描通过并保留既有 Node 20/CodeQL 权限提示。
- 当前状态：任务级 CredentialReference 初始认证为通过候选门禁的 `Implemented` 基线；整体仍为 `1.0 Release Candidate`，不等同于 `Accepted/Completed`。

### 4.57 Source Rule 有界响应派生变量（本轮，2026-08-30）

- 缺口：调用方模板变量已经覆盖首请求，但 API 型来源的续页 token/cursor 辅助值仍无法从当前响应安全派生；本轮只沿既有有界分页 seam 增量实现，不打开通用多请求编排。
- 实现：`CapabilityRule.ResponseVariables` / `RuleResponseVariable` 仅服务 page-number/cursor 续页；每个实际续页前按受控 Selector 或带超时 Regex 从当前响应提取，应用 Trim/Replace，合并到执行期临时变量上下文，再渲染下一次 path/header/query/form。Selector/Regex 声明保持与输出字段一致的严格互斥形状。
- 安全/边界：只允许 page-number/cursor，名称唯一且受界；提取值复用变量数量、名称、单值、累计 UTF-8 字节和控制字符预算。缺失/非法/正则超时/超限在续页出网前整体失败，不把响应、派生值、部分结果写入结果、日志、Task Payload 或错误文本；最终页不要求派生变量，不跨执行持久化。
- 回归与证据：定向 RuleAdapter/Validator/JSON 91/91；本地 Restore、Release Build 0/0、Unit 437/437、Architecture 1/1、Contract 10/10、迁移模型 11/11、Schema/Fixture、API `/health` 200 通过；Integration 80 项中 6 通过、2 跳过、72 项因本机 Docker 管道不可用 BLOCKED。候选提交 `8977a42` 的 [CI 33276544113](https://github.com/nekohands/InkFlow/actions/runs/33276544113)、[Docker 33276544229](https://github.com/nekohands/InkFlow/actions/runs/33276544229)、[Security 33276544165](https://github.com/nekohands/InkFlow/actions/runs/33276544165) 均 GREEN，headSha 均为 `8977a425560f20bde38a162a598816a9cd56c1e7`。
- 非目标：不包含持久化/跨执行状态、next-link 派生模板、递归/MaxDepth、通用请求序列、完整 XPath/JSONPath 或真实来源/阅读 3.0 人工验收。
- 当前状态：有界响应派生变量为通过候选门禁的 `Implemented` 基线；整体仍为 `1.0 Release Candidate`，不等同于 `Accepted/Completed`。来源默认绑定、Owner/Admin 凭据管理、真实 SecretProvider、持久会话、真实来源/切源、阅读 3.0 和人工验收仍待处理。

### 4.58 Source 级默认 CredentialReference 绑定（本轮，2026-08-30）

- 缺口：任务显式 CredentialReference 已接入，但来源型规则/Worker 缺少来源级默认引用回退。
- 实现：Source 增加可选非敏感 DefaultCredentialReferenceId、Domain 校验、设置/清除和显式优先解析；RuleBasedSourceAdapter 与 RuleCrawlerTaskExecutor 未提供显式引用时回退；Sources 表新增可空 256 字符列和 Migration，任务载荷不复制默认值。
- 安全/边界：secret 仍只由 ISourceCredentialProvider 解析，默认绑定不保存或输出 secret；Provider 继续负责 Owner Scope/跨租户授权，CodeAdapter 不继承规则型默认值，Owner/Admin 管理、真实 SecretProvider、持久会话和人工验收不在本轮。
- 回归与证据：Unit 442/442、Architecture 1/1、Contract 10/10、迁移模型 11/11、API `/health` 200；Integration 81 项中 6 通过、2 跳过、73 项因本机 Docker 管道不可用 BLOCKED。候选提交 `6d9c2ec` 的 [CI 33277737624](https://github.com/nekohands/InkFlow/actions/runs/33277737624)、[Docker 33277737577](https://github.com/nekohands/InkFlow/actions/runs/33277737577)、[Security 33277737675](https://github.com/nekohands/InkFlow/actions/runs/33277737675) 均 GREEN。
- 当前状态：来源级默认 CredentialReference 为通过候选门禁的 Implemented 基线；整体仍为 `1.0 Release Candidate`，不等同于 `Accepted/Completed`。

### 4.59 Source 默认 CredentialReference Administrator 管理入口（本轮，2026-08-30）

- 缺口：来源默认引用已有执行侧回退，但没有统一的受保护设置/清除入口和命令审计。
- 实现：新增 `ISourceCredentialBindingService` 与 `PUT /api/v1/admin/sources/{sourceId}/credential-binding`；`null` 清除、合法非空引用设置，复用既有 `Source.DefaultCredentialReferenceId` 和 Sources Repository，无新 Migration。入口绑定独立 Administrator-only policy，不扩大 Operator 来源运维权限。
- 安全/边界：请求只接受非敏感 `CredentialReferenceId` 与有界理由；响应只返回引用 ID；不接收/返回 secret、Token、Cookie 或密码。set/clear 结果写入命令审计，实际 secret 仍由 Provider 按 Owner Scope/跨租户规则解析。
- 回归与证据：定向服务/接口测试 8/8；本地 Restore、Release Build 0/0、Unit 450/450、Architecture 1/1、Contract 10/10、API `/health` 200、匿名管理路由 401、`git diff --check` 通过；Integration 81 项中 6 通过、2 跳过、73 项因本机 Docker 管道不可用 BLOCKED。候选提交 `dee61d3` 的 [CI 33279039667](https://github.com/nekohands/InkFlow/actions/runs/33279039667)、[Docker 33279039645](https://github.com/nekohands/InkFlow/actions/runs/33279039645)、[Security 33279039666](https://github.com/nekohands/InkFlow/actions/runs/33279039666) 均 GREEN，headSha 均为 `dee61d31fdd9983c7cc30f57ea091cd016c5a6db`。
- 非目标/当前状态：不实现 secret 材料管理、真实 SecretProvider、持久会话、真实来源/切源或人工验收；本包为通过候选门禁的 `Implemented` 基线，整体仍为 `1.0 Release Candidate`，不等同于 `Accepted/Completed`。

### 4.60 Source Credential Owner Scope 契约（本轮，2026-08-30）

- 缺口：原 Provider 只接收 SourceId 与 CredentialReferenceId，无法在解析 seam 强制区分平台、用户和组织范围；同名引用存在跨所有者误取 secret 的风险。
- 实现：新增 `SourceCredentialOwnerKind`、`SourceCredentialOwnerScope` 与 `SourceCredentialResolutionContext`，并将 `ISourceCredentialProvider` 改为接收完整非敏感解析上下文。Worker/Crawler 显式使用 Platform；`RuleBasedSourceAdapter` 只有在显式提供引用时才透传用户/组织范围，来源默认引用固定使用 Platform。
- 安全/边界：Platform 不带 OwnerId，User/Organization 必须带稳定 Guid；Source/Reference/Scope 组合输入有界且非法时在 Provider/HTTP seam 前失败。配置 Provider 仅解析 Platform，不接收用户/组织范围；secret 不进入上下文、任务载荷、规则 JSON、日志、错误或结果。
- 回归与证据：定向凭据回归 24/24；本机 Restore PASS、Release Build 0 warnings / 0 errors、Unit 455/455、Architecture 1/1、Contract 10/10；API/Worker/Scheduler `/health` 均 200。完整 Integration 81 项中 6 项通过、2 项跳过、73 项因本机 Docker 管道 `npipe://./pipe/docker_engine` 不可用而 BLOCKED。
- 非目标：不实现用户/组织/租户实体、真实 Vault/Cloud SecretProvider、secret 材料管理、轮换、持久会话或用户/组织凭据管理；真实来源、切源、阅读 3.0 和人工验收仍待处理。
- 当前状态：本工作包已完成代码、本地自动化验证和候选提交后的三类远端门禁；[CI 33280448686](https://github.com/nekohands/InkFlow/actions/runs/33280448686)、[Docker 33280448680](https://github.com/nekohands/InkFlow/actions/runs/33280448680)、[Security 33280448687](https://github.com/nekohands/InkFlow/actions/runs/33280448687) 均 GREEN，且均指向 `ee20afef2f9247fdb774ca6dda35a0f81b7452fe`。仍未达到 `Accepted/Completed`，真实 Provider、真实来源、阅读 3.0 与人工验收继续按待定清单处理。

### 4.61 PostgreSQL Outbox Relay 与 Worker 宿主接线（本轮，2026-08-30）

- 缺口：Outbox/Inbox 事实和可测试执行层已存在，但此前没有实际 Publisher 或 Worker 后台循环，不能证明消息会从 Outbox 进入 Inbox。
- 实现：Worker 现在注册 `PostgreSqlInboxMessagePublisher`、`OutboxDispatcher` 与 `OutboxRelayBackgroundService`；relay 使用 `FOR UPDATE SKIP LOCKED`/lease 批量领取，先核对类型、PayloadHash、TraceId，再幂等写入 Inbox，写入成功后才确认 Outbox。考虑到 PostgreSQL `jsonb` 读回会规范化 JSON，Outbox/Inbox 追加受消息大小上限约束的 `RawPayload` 原文列；旧记录缺少原文时沿用已保存 hash，不误算规范化文本。Inbox 新增 TraceId 字段与独立 Migration；`Messaging:Relay` 的轮询、启动延迟、lease、批量和 owner 前缀均有界，日志不记录消息载荷/异常文本。
- 边界：v1 仅选择同一 PostgreSQL 事实库作为内部耐久 relay，不接入外部 MQ；Inbox 消费轮询和具体业务 Handler 等待接收模块明确后接入，当前不宣称全部消息已消费完成。
- 回归与证据：首次候选的 PostgreSQL Relay Integration 暴露 `jsonb` 规范化导致的 hash 误判，已新增 RawPayload 修复和旧记录回归。修复后本机 Restore、Release Build（0 warnings / 0 errors）、Unit 460/460、Architecture 1/1、Contract 10/10、迁移模型 11/11、三宿主 `/health` 均 PASS；定向 PostgreSQL Relay Integration 因本机 Docker `npipe://./pipe/docker_engine` 不可用而 BLOCKED。修复候选 `ed4a7a7abc70732df5310546c0af01909b54ac96` 的 [CI 33282208833](https://github.com/nekohands/InkFlow/actions/runs/33282208833)、[Docker 33282208841](https://github.com/nekohands/InkFlow/actions/runs/33282208841)、[Security 33282208838](https://github.com/nekohands/InkFlow/actions/runs/33282208838) 均 GREEN 且 headSha 一致；CI Integration 82 项为 80 passed / 2 skipped。
- 当前状态：代码为 `Implemented`，整体仍为 `1.0 Release Candidate`，不等同于 `Accepted/Completed`；本机 Docker、Inbox Handler/消费闭环、真实来源、阅读 3.0 和人工验收继续按第 6 节处理。

### 4.62 Inbox Consumer 轮询与 Worker 消费宿主（本轮，2026-08-30）

- 缺口：4.61 的 Outbox→Inbox relay 已具备耐久投递，但 Inbox 缺少按已注册类型领取、消费和确认的 Worker 宿主。
- 实现：新增 `IInboxStore.ClaimBatchAsync`、`InboxConsumerPump` 与 `InboxConsumerBackgroundService`。PostgreSQL 在事务内以 `FOR UPDATE SKIP LOCKED` + lease 批量领取，并以 Handler registry 的 MessageType allowlist 过滤；成功 Handler 才写 `ProcessedAt`，失败保留 `handler_failed` 等稳定失败码。`InboxMessageRecord` 恢复完整 Envelope；新增 nullable `OccurredAt` 和查询索引，旧行缺失 `OccurredAt` 时回退 `ReceivedAt`，无 `RawPayload` 时不重算已保存 hash。
- 宿主边界：Worker 通过 `Messaging:Inbox` 配置启用、启动延迟、轮询间隔、lease 和 batch 上限，每轮使用独立 DI scope。当前没有注册业务 Inbox Handler，空 registry 会安全 idle，不领取未知消息；未伪造 `crawler.task.created` 业务消费完成。
- 回归与证据：本机 Restore PASS、Release Build 0 warnings / 0 errors、Unit 464/464、Architecture 1/1、Contract 10/10、Windows .NET 等价迁移模型检查 11/11、三宿主 `/health` HTTP 200、漏洞审计和 `git diff --check` PASS。定向 Inbox PostgreSQL Integration 两项因本机 `npipe://./pipe/docker_engine` 不可用而 BLOCKED；WSL 包装脚本因缺少 dotnet 未执行成功。远端候选 `fa50c07b6eee042644ea72a331c75e9f61e0ba81` 的 [CI 33283884681](https://github.com/nekohands/InkFlow/actions/runs/33283884681)、[Docker 33283884682](https://github.com/nekohands/InkFlow/actions/runs/33283884682)、[Security 33283884688](https://github.com/nekohands/InkFlow/actions/runs/33283884688) 均 GREEN 且 headSha 一致；CI 为 Unit 464/464、Architecture 1/1、Contract 10/10、Integration 84（82 passed / 2 skipped），并包含迁移、Compose、Runtime/SLO、Redis、备份恢复、diagnostics 和 Security 扫描。
- 当前状态：本工作包为 `CI Green / Implemented`，整体仍为 `1.0 Release Candidate`，不等同于 `Accepted/Completed`。业务 Handler/消费闭环、本机 Docker、真实来源、阅读 3.0、人工验收和生产治理继续待定；架构决策记录见 [ADR 0017](../adr/0017-inbox-consumer-polling.md)。

### 4.63 Inbox 消费失败有界退避与终态死信（本轮，2026-08-30）

- 缺口：4.62 失败消息释放 lease 后会在下一轮立即重试，缺少统一退避和终态，持续失败可能形成热循环。
- 实现：`MaxAttempts` 默认 5、范围 1–100；失败复用有界指数退避写入 `AvailableAt`，达到上限写入 `DeadLetteredAt` 并清除 lease/重试时间。单条与批量 claim 都排除未到时间、已处理和终态死信；Worker 记录并告警 dead-lettered 计数。失败只写稳定码，不保存异常原文。
- Migration/边界：新增 `AddInboxFailurePolicy` Migration 和领取索引；旧 nullable 列兼容为立即可领取，普通 retention 不清理未处理/死信。当前 Worker 仍无业务 Handler，空 registry 安全 idle；本轮不新增 `crawler.task.created` 业务消费、不引入自动重放/API/MQ。决策见 [ADR 0018](../adr/0018-inbox-failure-policy.md)。
- 本地证据：Restore PASS；Release Build 0 warnings / 0 errors；Unit 466/466、Architecture 1/1、Contract 10/10、迁移模型 11/11、三宿主 `/health` HTTP 200、NuGet 漏洞审计、敏感信息模式检查和 `git diff --check` PASS。完整 Integration 85 项中 6 项通过、2 项跳过、77 项因本机 Docker `npipe://./pipe/docker_engine` 不可用而 BLOCKED；定向新 Inbox Integration 同样 BLOCKED，未将本机容器结果记为通过。
- 远端证据：提交 `622446264c9dbee09298e8001aef6c092d235211` 的 [CI 33285403134](https://github.com/nekohands/InkFlow/actions/runs/33285403134)、[Docker 33285403140](https://github.com/nekohands/InkFlow/actions/runs/33285403140)、[Security 33285403125](https://github.com/nekohands/InkFlow/actions/runs/33285403125) 均 GREEN 且 headSha 一致；CI Integration 85 项为 83 passed / 2 skipped，新 Inbox 死信集成用例实际通过，Docker 与 Security 全部通过。
- 当前状态：本工作包为 `CI Green / Implemented`，不等同于 `Accepted/Completed`；业务 Handler/完整消费闭环、本机 Docker、真实来源、阅读 3.0、人工验收和生产治理继续待定。

### 4.64 `crawler.task.created` Inbox 业务消费闭环（本轮，2026-08-30）

- 缺口：4.61–4.63 已完成 Outbox→Inbox relay、按类型领取和失败死信，但 Worker 没有具体业务 Handler；Crawler 任务创建事件不能触发完整的任务执行链路。
- 实现：Crawling Application 新增稳定载荷解析/校验和 `CrawlerTaskCreatedMessageHandler`；Handler 回读 `CrawlerTask` 权威事实，校验 Source/Capability/CreatedAt 后调用按任务 ID 的 PostgreSQL `FOR UPDATE SKIP LOCKED` 原子租约。新增 `CrawlerTaskProcessor` 统一周期轮询与 Inbox 触发的 Running、成功、任务级重试和死信状态机；Worker 注册 Handler，并补齐 Canonical Book 仓储依赖。决策见 [ADR 0019](../adr/0019-crawler-task-created-inbox-handler.md)。
- 可靠性与边界：任务表仍是执行权威事实；任务行与 Outbox 继续同事务写入，Inbox 确认与任务状态提交分离，重复投递由 Inbox 主键、任务终态和租约吸收。事件不携带 Variables、CredentialReference、secret 或正文；身份不匹配/任务缺失进入通用 Inbox 稳定失败、退避和死信。其他 Integration Event 不因本轮实现而宣称已消费。
- 回归与证据：Release Build 0 warnings / 0 errors；Unit 472/472、Architecture 1/1、Contract 10/10；Windows 直接迁移模型检查 11/11；Worker `/health` HTTP 200。完整 Integration 86 项中 6 项通过、2 项跳过、78 项因本机 `npipe://./pipe/docker_engine` 不可用而 BLOCKED；定向本轮端到端用例同样 BLOCKED。WSL 迁移包装脚本因找不到 `dotnet` 未执行成功，但不影响 Windows 等价检查；NuGet 漏洞审计无漏洞，`git diff --check` PASS。
- 远端证据：代码候选提交 `acbbd10dd67e350f2bf6b2ae1080c54f7b725d91` 的 [CI 33290137667](https://github.com/nekohands/InkFlow/actions/runs/33290137667)、[Docker 33290137676](https://github.com/nekohands/InkFlow/actions/runs/33290137676)、[Security 33290137668](https://github.com/nekohands/InkFlow/actions/runs/33290137668) 均 GREEN 且 headSha 一致；CI 为 Unit 472/472、Architecture 1/1、Contract 10/10、Integration 86（84 passed / 2 skipped），Docker 四业务镜像和 Collector 扫描通过，Security 的 CodeQL、Filesystem、SBOM、NuGet 审计通过。
- 当前状态：本工作包为 `CI Green / Implemented`，不等同于 `Accepted/Completed`；本机 Docker、真实来源、阅读 3.0 和人工验收继续待定，其他 Integration Event 仍需各自接入和取得端到端证据。

### 4.65 Inbox 终态死信纳入 Operations 告警观测（本轮，2026-08-30）

- 缺口：4.63 已将 Inbox Handler 失败收敛为 `DeadLetteredAt` 终态，但 Operations 告警快照只读取 Crawler 死信；消息消费持续失败无法进入统一运维告警与历史链路。
- 实现：新增 `IInboxDeadLetterReader` 与 PostgreSQL `EfMessagingMessageStore` 有界摘要读取，只统计 `DeadLetteredAt IS NOT NULL AND ProcessedAt IS NULL`，返回数量和 `HasMore`，不带载荷、失败文本或 TraceId；新增 `(ProcessedAt, DeadLetteredAt, Id)` 索引 Migration。Operations 增加 `InboxDeadLetterCountThreshold`，平台级快照产生 `inbox_dead_letters_present`，读取失败产生稳定 `inbox_dead_letter_snapshot_unavailable` 并将快照标为 partial；来源过滤的 Operator 视图不查询平台级 Inbox 状态。决策见 [ADR 0020](../adr/0020-inbox-dead-letter-operations-observation.md)。
- 回归与证据：新增告警阈值、稳定错误、partial 历史语义、来源过滤单元回归和 PostgreSQL 有界读取回归；Windows Release Build 0 warnings / 0 errors，Unit 475/475、Architecture 1/1、Contract 10/10 PASS；Windows 本机 Docker `npipe://./pipe/docker_engine` 不可用，完整 Integration 在本机保持 BLOCKED。随后在 Ubuntu VM 以源码构建 Compose 实际启动 PostgreSQL、Redis、OTel Collector 与四个应用镜像，Migration 退出码 0，API/Worker/Scheduler 健康检查和 Collector loopback 健康检查通过；Linux SDK 容器完整 `Restore → Build → Test` 为 Unit 475/475、Architecture 1/1、Contract 10/10、Integration 85 passed / 2 skipped / 0 failed，新增 Inbox 有界死信读取回归实际通过；Core SLO Runtime smoke 的 public、Legado、Developer 未授权和 Reader 四个服务面均通过。漏洞审计、敏感信息检查与 `git diff --check` PASS。
- 备份恢复证据：同一 Ubuntu VM 源码 Compose 先通过 Core SLO smoke 产生审计数据，再执行 `scripts/backup-restore-drill.sh`；custom-format 归档恢复到隔离数据库，全部非系统表行数签名与 `audit.events` 数量一致，结果为 `archive=78516 bytes, audit_events=31`，隔离库已清理，验证卷保留。
- 远端证据：代码候选提交 `72e49b30f36e78d0405b984580e1ce2a43381b32` 的 [CI 33291943661](https://github.com/nekohands/InkFlow/actions/runs/33291943661)、[Docker 33291943632](https://github.com/nekohands/InkFlow/actions/runs/33291943632)、[Security 33291943645](https://github.com/nekohands/InkFlow/actions/runs/33291943645) 均 GREEN 且 headSha 一致；CI Integration 87 项为 85 passed / 2 skipped，包含本轮 Inbox 回归，Docker 四业务镜像与 Collector 构建/扫描/发布通过，Security 的 NuGet、Filesystem、CodeQL、SBOM 全部通过。
- 当前状态：本工作包为 `Implemented`，整体继续保持 `1.0 Release Candidate`，不等同于 `Accepted/Completed`；外部通知、真实来源、阅读 3.0、人工验收和本机 Docker 仍按待定事项处理。

### 4.66 GHCR 发布 Compose 与 Core SLO 证据文件健壮性（本轮，2026-08-30）

- 缺口：Docker 发布工作流实际推送到 `ghcr.io/nekohands/inkflow/inkflow-*`，默认 Compose 少了 `/inkflow/` 路径；Core SLO 探针默认证据文件固定命名，跨用户重复执行可能因 `/tmp` 粘滞位无法覆盖。
- 实现：修正四个 GHCR 应用镜像引用；新增发布后 `docker compose -f docker-compose.yml pull` 门禁；默认证据路径改为随机后缀，显式 `INKFLOW_SLO_EVIDENCE_FILE` 行为保持不变，并补充不可写旧文件回归。
- 验证：Ubuntu VM 成功拉取并启动 GHCR 发布镜像，Migration 退出码 0，PostgreSQL/Redis/Collector/API/Worker/Scheduler 健康；Core SLO 四面、公开 API/Legado/Reader/PWA 入口、脚本回归通过；备份恢复为 `archive=80181 bytes, audit_events=63`，验证后已停止栈且保留数据卷。最终 [CI 33294167216](https://github.com/nekohands/InkFlow/actions/runs/33294167216)、[Docker 33294167310](https://github.com/nekohands/InkFlow/actions/runs/33294167310)、[Security 33294167234](https://github.com/nekohands/InkFlow/actions/runs/33294167234) 均 GREEN 且指向 `ff7ba52`；CI 为 Unit 475/475、Architecture 1/1、Contract 10/10、Integration 87（85 passed / 2 skipped）、Redis 1/1。
- 失败修复记录：`623077c` 的 CI 回归测试未隔离 Runner `RUNNER_TEMP` 而失败，已读取日志并由 `ff7ba52` 修复后全量复绿；VM 备份脚本首次无 sudo 仅为 Docker socket 权限问题，授权重跑通过。
- 当前状态：本工作包 `Implemented`，整体仍是 `1.0 Release Candidate`；真实来源/切源、阅读 3.0、浏览器/私有库人工验收和生产 OTLP/告警/备份治理继续待定。

### 4.67 ContentVersion 当前版本切换边界修复（本轮，2026-08-30）

- 缺口：`EfContentVersionRepository.SetCurrentAsync` 原先先清空章节当前标记、再按 `versionId` 无条件设置，未验证版本属于目标章节，且两条更新之间存在非原子中间状态。
- 实现：同一数据库事务内校验目标版本归属，并以单条按章节 UPDATE 同时清除其它当前标记和设置目标版本；无效或跨章节目标抛出稳定 `InvalidOperationException`，不会清除既有当前版本。
- 回归：新增 2 个 PostgreSQL Testcontainers 集成用例，覆盖跨章节拒绝/原当前版本保留和同章节切换唯一性。本机 Release Build 0 warnings / 0 errors；本机 Docker 不可用导致完整 Integration 仍 BLOCKED，Ubuntu VM 真实 Testcontainers 2/2 通过且测试容器已清理。
- 远端证据：候选 `74b0d536af9d37f282c64fb78f6041987841300d` 的 [CI 33294984996](https://github.com/nekohands/InkFlow/actions/runs/33294984996)、[Docker 33294984938](https://github.com/nekohands/InkFlow/actions/runs/33294984938)、[Security 33294984918](https://github.com/nekohands/InkFlow/actions/runs/33294984918) 均 GREEN 且 headSha 一致；CI Unit 475/475、Architecture 1/1、Contract 10/10、Integration 89（87 passed / 2 skipped）、Redis 1/1 全部通过。
- 当前状态：本工作包 `Implemented`，整体仍为 `1.0 Release Candidate`，不等同于 `Accepted/Completed`；真实来源/切源、阅读 3.0、浏览器/私有库人工验收和生产治理继续待定。

1. **Legado 真机验证（后续人工）**：在阅读 3.0 中导入 `/legado/book-source.json`，验证搜索/详情/目录/正文四步；本轮按用户决定不执行。
2. **Personal Legado Token 人工验收**：在阅读 3.0 导入签发响应中的 Personal 书源，验证 token header、Search → BookInfo → TOC → Content 和撤销后请求失效；本轮按用户决定不执行。
3. **Web Reader 人工视觉/功能验收（1.0 必选）**：在移动、平板、桌面和宽屏浏览器打开 `/reader` 三页面，检查正文宽度、设置面板、键盘焦点、触控目标、长文滚动和上下章导航；本轮只完成自动化 HTML/CI 基线。
4. **Reader/PWA 用户状态人工验收（1.0 必选）**：在支持的浏览器中验证账户登录/注册、刷新后会话、书架加入/移除、历史、章节进度/偏好同步、401 刷新、登出、安装提示、Service Worker 注册与网络不可用时离线提示；本轮按用户决定不执行。
5. **追更真实验证**：Scheduler 扫描 + Worker 消费已在容器环境运行，新章检测需真实源数据佐证。
6. **Phase 1B 真实切源验收**：从已接入来源中选择可稳定访问的真实第二 Official Source，验证 Source A 不可用时 Web/Legado 仍读取，且 BookId/ChapterId 不变。
7. **Content Policy 管理人工验收**：使用 Administrator 凭证验证下架/恢复、Operator/匿名拒绝、全公开读取路径隐藏/恢复和命令审计记录；本轮只完成自动化基线，未执行人工操作。
8. **Operations Center 人工验收（1.0 必选）**：使用 Operator/Administrator 凭证打开 /admin/operations，验证登录/角色拒绝、overview/告警快照读取、Administrator 告警历史分页与恢复转折、来源能力停用/恢复、死信理由确认与重放、HasMore 截断标记、区块部分失败展示和命令结果；检查移动/桌面布局、键盘焦点、对比度与截图证据。本轮只完成自动化基线，未执行人工操作。
9. **Admin Audit Read 人工验收**：使用 Operator/Administrator 凭证验证审计查询授权、时间范围/精确过滤/游标、空结果、稳定错误和响应脱敏；本轮只完成自动化基线，未执行人工操作。
10. **Source Authorization 人工验收**：使用 Administrator 授予/列出/撤销某个 Operator 的 `source.read` / `source.manage`，验证重复授予幂等、撤销后拒绝、`source.manage` 隐含读取、来源健康/停用/恢复及 Operations 来源健康区块过滤；验证 Reader/匿名和未授权 Operator 的 401/403、理由校验与授权审计。本轮只完成自动化基线，未执行真实凭据操作。
11. **Source 默认 CredentialReference 管理人工验收**：使用 Administrator 设置/清除来源默认引用，确认 Operator/Reader/匿名拒绝、理由与 set/clear 审计正确、响应不包含 secret，并在可用真实 Provider 后验证默认回退按 Platform Scope、显式用户/组织引用按对应 Owner Scope 且显式引用优先；本轮只完成自动化基线，未使用真实凭据操作。
12. **生产备份恢复治理验收**：在目标部署环境配置加密/异地备份、保留与删除策略、恢复授权和 RPO/RTO；执行恢复演练并保留归档、校验和、行数签名、耗时及告警证据。本轮只完成 CI 级恢复演练。
13. **Private Library 人工验收**：使用两个真实账户验证私有书目创建、列表、详情、更新、删除和跨用户 404；上传真实 TXT/EPUB，验证章节/正文读取、导出文件可读性、重复导入不覆盖和失败导入无半本书；确认不进入公共 Catalog、搜索、Legado 或公共 Reading Shelf。本轮只完成自动化基线。
14. **Developer API / 商业基础人工验收**：使用真实 Web 账户创建/撤销应用与 API Key，确认原文只出现一次；由 Administrator 授予真实套餐/Provider，验证 Developer API 的目录读取、跨应用用户级配额、超额 `429/Retry-After`、密钥/应用/用户停用后的拒绝和审计；5.13 已完成同范围临时账户自动化，但本轮仍未使用真实凭据。
15. **生产 OTLP/SLO 窗口验收**：将 Collector 接入受治理持久化后端，确认 API/Worker/Scheduler/Reader 观测到达，基于合成探针与真实业务窗口完成聚合，并验收错误预算告警、访问控制和保留策略；当前 CI 探针仅为短窗口基线。
16. **继续推进 1.0**：在上述证据基础上完成第三来源真实验收、Private Library 真实账户/文件验收，并继续推进 Security/Operations、外部告警和组织/支付商业化能力。

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
✅ Inbox 消费失败有界退避与终态死信：`MaxAttempts` + `AvailableAt` + `DeadLetteredAt` + Worker 计数告警（`6224462`；CI `33285403134` / Docker `33285403140` / Security `33285403125` GREEN；本机 Docker 与人工验收仍待定）
✅ Audit retention：过期审计事实有界删除 + 追加式触发器受控例外 + Worker 每小时周期接线（`b8046af`；CI `33257996992` / Docker `33257996951` / Security `33257996953` GREEN；生产法律/合同保留与归档治理仍待定）
✅ Capability Health 并发变更串行化：事务级 advisory lock + 服务原子变更契约 + 跨连接 PostgreSQL 并发回归（`3ba51a1`；CI `33263255422` / Docker `33263255437` / Security `33263255420` GREEN；本机 Docker、真实来源/切源和人工验收仍待定）
✅ CI Security Scan 基线 v1：NuGet/Trivy/CodeQL/SBOM + 四镜像发布前扫描（`f58599b`，CI `33134804300` / Security `33134804292` / Docker `33134804238`）
✅ Resource-level Source Authorization v1：来源授权授予/列表/撤销 + 来源查询/控制过滤 + 命令审计（`a663cef`，CI `33137358470` / Security `33137358428` / Docker `33137358485`）
✅ Legado Contract Release Gate v1：Compatibility Profile + Rule Generator seam + Generate/JSON/Search/BookInfo/TOC/Content 自动门禁（本轮；真实来源与真机验收待定）
✅ Private Library v1 后端基础：独立 PrivateBook/PrivateBookId + UserId 范围仓储 + 迁移 + 受保护元数据 CRUD（本轮；真实账户/公共路径隔离人工验收待定）
✅ Private Library v2：独立 PrivateChapter/私有正文 + TXT/EPUB 导入导出 + 用户范围读取 + ZIP/XML 输入边界（`f83476a`，CI `33163145132` / Docker `33163145104` / Security `33163144984` 均 GREEN；真实账户/文件和公共路径隔离人工验收待定）
✅ Operations/Repair Center UI v1：受保护快照展示 + 来源能力控制 + 死信理由确认重放（ed0ff8c，CI 33125476460 / Docker 33125476441 均 GREEN）
✅ 第三个 Official Source 机制接入：17K CodeAdapter + 三宿主 SSRF 接线 + 幂等 Source 种子 + JSON Fixture 回归（本轮；真实验收待定）
✅ Source Rule page-number/cursor Pagination：声明式 query/form 参数注入 + 有界执行/失败关闭（`0e9164b`；CI `33269606086` / Docker `33269606076` / Security `33269606147` GREEN；真实来源与人工验收待定）
✅ Source Rule 有界请求模板变量：路径/Header/Query/Form 占位符 + 变量上下文预算 + 控制字符/语法失败关闭（`dd39396`；CI `33272774115` / Docker `33272774105` / Security `33272774138` GREEN；真实来源与人工验收待定）
✅ Source 默认 CredentialReference 管理 API：Administrator-only 设置/清除 + 有界理由 + 命令审计（`dee61d3`；CI `33279039667` / Docker `33279039645` / Security `33279039666` GREEN；真实 Provider 与人工验收待定）
✅ Source Credential Owner Scope 契约：Provider 强制携带 Platform/User/Organization 范围；默认绑定固定 Platform，显式用户/组织引用透传范围（`ee20afe`；CI `33280448686` / Docker `33280448680` / Security `33280448687` GREEN，真实 Provider 与人工验收待定）
✅ PostgreSQL Outbox Relay 与 Worker 宿主接线 v1：Outbox→Inbox 耐久 relay + `RawPayload` hash 稳定性修复（`ed4a7a7`；CI `33282208833` / Docker `33282208841` / Security `33282208838` GREEN；Inbox Handler/真实与人工验收待定）
✅ Inbox Consumer 轮询与 Worker 消费宿主：按已注册类型批量 claim、lease 恢复、Envelope 兼容恢复和空 registry 安全 idle（`fa50c07`；CI `33283884681` / Docker `33283884682` / Security `33283884688` GREEN；业务 Handler/真实与人工验收待定）
✅ `crawler.task.created` 业务 Inbox Handler：稳定契约校验 + 按任务 ID 原子租约 + 共享任务处理器 + Outbox→Inbox→任务完成验证（本轮；其他 Integration Event 仍待各自接入）
✅ Inbox 死信 Operations 观测：有界摘要读取 + 平台级告警阈值 + partial fail-closed + 来源过滤隔离（本轮，ADR 0020；Ubuntu VM 真实 PostgreSQL/Redis 集成、Compose Runtime 与备份恢复已复验，Windows 本机 Docker 仍不可用）
✅ GHCR 发布 Compose 与 Core SLO 证据路径：镜像引用对齐、发布后拉取门禁、默认证据文件随机化与回归（`ff7ba52`；CI `33294167216` / Docker `33294167310` / Security `33294167234` GREEN；Ubuntu VM GHCR Runtime/备份复验通过）
→ 扩展其他业务 Integration Event 接收者并取得对应端到端证据
→ Reader/PWA 浏览器安装、离线和账户链路人工验收
→ Private Library 真实账户与公共路径隔离人工验收
→ Legado 真机导入/阅读（后续人工）
→ 17K 真实 Search/BookInfo/TOC/Content 验收
→ 真实追更与真实第二来源切源演练
→ Phase 1A / Phase 1B 分别完成外部验收
→ Source Credential Owner Scope / 默认 CredentialReference 管理人工验收
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
- [x] **Private Library 非阅读 App 自动化 runtime smoke**：源码构建 Compose 已覆盖认证、所有权隔离、书目 CRUD、TXT/EPUB 导入/导出、章节/正文、重复导入不覆盖原书、失败导入无半本书、私有缓存头、公共 API/Legado 直接路径 404 和公共 Catalog/Reading Shelf 不泄漏。
- [ ] **Private Library 真实账户/人工体验补充验收**：如需发布前补充，使用专用真实测试账户和真实 TXT/EPUB 验证浏览体验与长期使用；不替代自动化门禁。
- [x] **Developer API / 商业基础非阅读 App 自动化 runtime smoke**：源码构建 Compose 已覆盖 Free Entitlement、应用/密钥创建与列表脱敏、目录读取、Header-only 鉴权、轮换和撤销；真实账户、套餐管理、配额超额和用户停用仍待真实环境补充。
- [ ] **真实追更**：用真实来源数据验证 Scheduler → Worker → 目录增量 → 正文发布闭环。
- [ ] **真实第二来源故障切换**：4.99 已在源码 Compose 确定性 fixture 中验证 Web/Legado A→B→A、稳定 BookId/ChapterId 和恢复；仍需用可稳定访问的真实第二 Official Source 验证真实故障、响应和恢复，不得产生重复 Canonical 身份。
- [x] **linovelib 真实公开页面只读链路**：GPT 内置浏览器已完成 Search → BookInfo → TOC → Content 页面证据；不等同于服务端 RuleAdapter 直连通过。
- [ ] **linovelib RuleAdapter 后端直连链路**：当前普通 HTTP POST 搜索返回 200 但空响应体；已提供 `scripts/linovelib-live-acceptance.sh` 与 `INKFLOW_LIVE_TESTS=1` opt-in 测试入口，待网络/站点挑战可稳定处理后验证服务端 Search → BookInfo → TOC → Content，并纳入真实第二来源/故障切换演练。
- [ ] **17K 真实 Search/阅读链路**：已在 Ubuntu VM 只读探测，但当前 API 证书链校验失败或返回“请升级版本/图书信息不存在”，仍待可用网络环境验证 Search → BookInfo → TOC → 免费 Content、VIP 访问边界和安全重定向。
- [ ] **本机 Docker 集成复验**：Windows 本机 Docker Engine 仍不可用；Ubuntu VM 已在 5.11 使用源码构建 Compose 完成 Unit 530/530、Architecture 1/1、Contract 10/10、Integration 102 passed / 2 skipped / 0 failed 的完整容器证据。若需关闭本机复验项，仍待 Windows Docker 恢复后在本机重跑 Testcontainers。
- [ ] **生产 OTLP 后端与 SLO 窗口验收**：在部署环境将 Collector 接入受治理的持久化后端，验证 API/Worker/Scheduler/Reader 观测到达，执行合成探针和窗口聚合，并验收错误预算告警、访问控制与保留策略；Compose debug exporter/健康 smoke 仅为接收基线。

扩展新来源的方式(书源兼容层):
- 规则型站点:在 sources 表登记含 RuleDsl 的 Source 记录,零代码;
- 复杂站点(特殊编码/签名):实现 `ISourceAdapter`(参考 `KanunuSourceAdapter`)并在适配器工厂注册。

普通 PR CI 不依赖真实第三方小说站点；Crawler 使用固定 Fixture/Mock Server。真实 Source 进入独立 Live/Nightly 检查。

### 4.68 前端纳入 1.0 Release Gate 与源码构建验证策略（本轮，2026-08-30）

- 范围：Web Reader 的书库/详情/目录/正文、Reader/PWA 的账户/书架/历史/离线壳/Manifest/Service Worker，以及受保护 Operations Center UI，全部纳入 1.0 必选范围；不再把前端视为 1.0 之外的后续 polish。
- 自动化：新增 `scripts/reader-frontend-runtime-smoke.sh` 与 fixture 回归；CI 增加独立 `Frontend 1.0 runtime smoke`，在 `docker-compose.build.yml` 源码构建栈上验证页面壳、响应式/可访问性标记、PWA 资源和敏感数据排除。既有 `ReaderHtml` 单元回归继续保留。
- Docker：日常 Docker 验证使用源码构建 Compose；GHCR 默认 Compose 仅在发布镜像、镜像一致性或明确需要时复验。CI Runtime smoke 的日志与清理已固定使用源码构建编排，避免把发布镜像误当作日常验证依据。
- 验收边界：移动/平板/桌面/宽屏视觉与 UX、键盘焦点/对比度/触控、长时间阅读、PWA 安装/离线和真实账户操作仍按用户决定 `NOT RUN`，但现在明确是 1.0 Release Gate 的待定项。
- 远端证据：候选提交 `1b1149d4f1bdbb3369c3c3e84baea913ef275437` 的 [CI 33295992063](https://github.com/nekohands/InkFlow/actions/runs/33295992063)、[Docker 33295992049](https://github.com/nekohands/InkFlow/actions/runs/33295992049)、[Security 33295992045](https://github.com/nekohands/InkFlow/actions/runs/33295992045) 均 GREEN 且 headSha 一致。CI 的 Restore/Build/迁移校验、Unit 475/475、Architecture 1/1、Contract 10/10、Integration 89（87 passed / 2 skipped）、Redis 1/1、源码构建 Compose Runtime、`Frontend 1.0 runtime smoke`、SLO、备份恢复和 diagnostics 全部通过；Docker 的四业务镜像构建/扫描/发布与发布 Compose 拉取复验通过；Security 的 NuGet、Filesystem、SBOM、CodeQL 全部通过。
- 当前状态：本工作包自动化门禁已通过，整体保持 `1.0 Release Candidate`，不标记 `Accepted/Completed`；浏览器/真实设备、真实来源、真实账户和其他人工/生产验收仍按待定事项执行。

### 4.69 Ubuntu VM 源码构建 Runtime 复验（本轮，2026-08-30）

- 目标：在独立 Ubuntu 验证环境使用当前 `dev` 源码执行 `docker-compose.build.yml`，补足本机 Docker CLI 不可用时缺失的真实 Compose 证据；本轮不使用 GHCR 镜像替代源码验证。
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

- 按本轮要求，除 MuMu/阅读 3.0 外，非阅读 App 验收优先使用源码、fixture、运行时脚本和 GPT 内置浏览器完成；真实第三方来源、真实生产账户和阅读 App 不被自动化证据替代。
- GPT 内置浏览器已自动访问 Reader 书库、账户、书架、历史、离线壳、Operations Center 和未发布章节状态；主内容锚点、跳转入口、viewport、无横向溢出和匿名角色保护通过，Reader/Operations 浏览器错误日志为空。
- 四视口 375×812、768×1024、1280×720、1920×1080 检查通过；宽屏内容保持 1152px 最大宽度，移动搜索控件可用，跳过主要内容、语义区域、状态区和可见焦点通过。搜索点击提交、无结果提示和账户必填字段约束通过；Enter 合成按键未提交被记录为浏览器驱动限制。
- VM 源码构建 Compose 已完成构建、Migration、健康检查和 reader-frontend-runtime-smoke，随后停止容器并清理本轮隔离测试卷。Linux SDK 容器 Restore/Build/Test 通过：Build 0 warnings / 0 errors，Unit 482/482、Architecture 1/1、Contract 10/10、Integration 89（87 passed / 2 skipped / 0 failed）。
- PWA 页面 Manifest 链接存在，资源契约由 VM 前端冒烟通过；内置浏览器直接导航非 HTML 资源时被客户端拦截，因此实际安装、Service Worker 注册和断网切换仍需安全上下文浏览器证据。未创建真实账户。
- 代码候选 01d09ab 的 CI 33301352344、Docker 33301352352、Security 33301352365 均 GREEN。整体仍为 1.0 Release Candidate，不标记 Accepted/Completed；真实来源/追更/切源、真实账户/生产凭据、PWA 安装断网、阅读 3.0 真机和长时间阅读继续待定。

### 4.76 Kanunu8 真实来源只读自动验证（本轮，2026-08-30）

- Ubuntu VM 临时 .NET 10 SDK 容器启用现有 opt-in live 测试，源码当前候选的 Kanunu8 真实网络适配器 BookInfo、TOC、章节正文 3/3 通过；不创建账号、不写业务数据，测试后临时 NuGet 卷已删除。
- Kanunu8 Search 当前按适配器契约返回空结果，不能据此宣称完整 Search → BookInfo → TOC → Content 或真实追更；linovelib 搜索响应体为空，17K 搜索 API TLS 证书校验失败，真实第二来源/故障切换仍待定。
- 整体继续为 1.0 Release Candidate；阅读 3.0/MuMu、真实账户、PWA 安装/断网和生产环境验收边界不变。

### 4.77 linovelib 真实公开站点只读链路自动验证（本轮，2026-08-30）

- GPT 内置浏览器在 linovelib 公开站点提交 `恶魔高校`，返回 3 个搜索结果并定位 `novel/1`；详情页读取 `恶魔高校DxD` / `石踏一荣`，目录页读取 482 章，正文页读取 `Life.1 不当人类。` 的非空段落。
- 该轮未登录、未创建账号、未写入站点数据，完成真实公开页面层面的 Search → BookInfo → TOC → Content 只读证据。
- 边界：普通 HTTP POST 仍返回 200/空响应体，浏览器会话结果不等于 InkFlow `RuleAdapter` 后端直连通过；不读取 Cookie、不绕过反爬挑战。`RuleAdapter` 直连、真实第二来源故障切换和真实追更继续待定。

### 4.78 Private Library 非阅读 App 自动化运行验收（本轮，2026-08-30）

- 新增 `scripts/private-library-runtime-smoke.sh` 和脚本回归，并接入 CI 源码构建 Runtime；链路覆盖未认证 401、两个唯一临时用户、书目 CRUD、TXT 两章导入、章节顺序/正文、`private, no-store`、TXT 导出、跨用户 404。
- 公共路径隔离也纳入同一 smoke：私有书/章通过公共 API 和公共 Legado 详情/正文路径返回 404，公共 Catalog 与用户 Reading Shelf 不包含私有书名；不触发第三方来源搜索。
- VM 源码构建证据：候选 `8e69b46` 的 `docker-compose.build.yml` 四业务镜像构建和健康启动成功，临时 SDK 容器运行结果为 `private-library-runtime-smoke: PASS (auth, ownership, CRUD, TXT import/read/export)`；随后停止 Compose，持久数据卷保留。
- 首轮脚本实跑发现更新请求误用 POST，已修复为 PUT；修复后的代码候选 `8e69b469ee0a56c28d3ba24ec99817cdf1a1f86a` 的 [CI 33305236784](https://github.com/nekohands/InkFlow/actions/runs/33305236784)、[Docker 33305236750](https://github.com/nekohands/InkFlow/actions/runs/33305236750)、[Security 33305236817](https://github.com/nekohands/InkFlow/actions/runs/33305236817) 均 GREEN 且 head SHA 一致。
- 边界：本轮未使用真实账户、阅读 3.0 或第三方登录；两个临时测试账户因没有账号删除 API 保留在 VM 持久数据库，测试书目已清理。EPUB/重复导入/失败导入已由 4.79 自动化关闭，真实账户和阅读 App 流程仍在待定事项中，整体不标记 Accepted/Completed。

### 4.79 Private Library 文件边界与失败一致性自动化验收（本轮，2026-08-30）

- 扩展 `scripts/private-library-runtime-smoke.sh` 与脚本回归：TXT 导出 EPUB 后再导入，检查 EPUB 响应类型/文件非空、元数据、章节顺序和正文；重复导入检查独立 PrivateBook 身份及原书不被覆盖；损坏 EPUB 检查 HTTP 400/`invalid_file` 和导入前后书目数量不变。
- Ubuntu VM 使用 `docker-compose.build.yml` 源码构建四业务镜像并健康启动；临时 SDK 工具容器输出 `private-library-runtime-smoke: PASS (auth, ownership, CRUD, TXT/EPUB import/read/export, duplicate isolation, failed-import rollback)`，验证结束后已停止 Compose，持久卷保留。
- 代码候选 `5b13d8e` 与最终文档同步提交 `fd9f8b1` 均已推送；最终 [CI 33306505203](https://github.com/nekohands/InkFlow/actions/runs/33306505203)、[Docker 33306505188](https://github.com/nekohands/InkFlow/actions/runs/33306505188)、[Security 33306505230](https://github.com/nekohands/InkFlow/actions/runs/33306505230) 均 GREEN 且 head SHA 一致。真实账户、阅读 3.0 和第三方登录均未使用；测试书目已清理，临时账户因没有账号删除 API 保留。

### 4.80 17K 真实公开接口只读探测（本轮，2026-08-30）

- 按“除阅读 App 外尽量自动化”的要求，在 Ubuntu VM 对 17K 公开接口执行只读探测；未创建账户、未写入站点数据、未关闭 TLS 校验，也未把外部接口响应伪装成适配器通过。
- 结果：`api.ali.17k.com` Search 入口因当前 VM 无法用系统 CA 验证上游证书而失败；备用 `api.17k.com` Search 返回 HTTP 200 但状态为“请升级版本”，固定书目详情返回“图书信息不存在”；Web 章节地址可达但只得到压缩页面响应，不能形成稳定业务链路。
- 结论：17K 真实 Search/BookInfo/TOC/Content、免费/VIP 边界和安全重定向保持 `BLOCKED / 待定`；仅保留离线 Fixture 与网络阻塞证据。

### 4.81 Developer API / 商业基础非阅读 App 自动化运行验收（本轮，2026-08-30）

- 新增 `scripts/developer-api-runtime-smoke.sh` 和结构回归，并接入 CI 源码构建 Runtime；覆盖未认证拒绝、默认 Free Entitlement、应用/密钥生命周期、原始密钥只在签发响应出现、列表脱敏、目录读取、私有缓存头和 Header-only 鉴权。
- 轮换与撤销断言覆盖旧密钥立即失效、新密钥可用、撤销状态可见以及撤销后拒绝；查询参数和 Bearer 均不能替代 `X-InkFlow-Api-Key`。脚本不输出原始密钥，临时应用在退出时撤销。
- Ubuntu VM 已快进到 `f235c8e`，使用 `docker-compose.build.yml` 源码构建 API/Worker/Scheduler/Migrations；临时 SDK 工具容器输出 `developer-api-runtime-smoke: PASS (account, entitlement, app/key lifecycle, redaction, header-only auth, catalog quota path, rotation, revoke)`，随后自动停止 Compose，持久卷保留。
- 本轮未使用真实 Web 账户或生产凭据；因没有账户删除 API，临时账户可能保留在 VM 数据库，但应用和密钥已清理/撤销。真实账户、Administrator 套餐、超额 `429/Retry-After`、跨账户配额和用户停用仍是人工/真实环境事项。
- 代码候选 `f235c8e` 已推送；文档同步后必须确认最终 HEAD 的 CI、Docker、Security 全部 GREEN，才能关闭本工作包。

### 4.82 Reader/PWA Service Worker 与离线壳非阅读 App 自动化验收（本轮，2026-08-30）

- 按“除阅读 App 外尽量自动化”的要求，本轮继续使用 Ubuntu VM 的 `docker-compose.build.yml` 源码构建栈，并用 GPT 内置浏览器完成安全上下文、Service Worker、缓存和断网回退验收；未使用 MuMu/阅读 3.0、真实账户或生产凭据。
- 直接访问 VM 的 `http://172.19.31.153:8080/reader` 得到 `secureContext=false`、Service Worker 不可用；临时建立本地 SSH 转发访问同一 VM 的 `http://localhost:18080/reader/` 后，浏览器具备安全上下文。转发只用于验收，结束时已关闭。
- 浏览器证据通过：`secureContext=true`；Manifest 的 `start_url=/reader`、`scope=/reader/`、`display=standalone` 与两枚图标可读取；Service Worker `/reader/sw.js` 状态为 `activated`，刷新 `/reader/` 后已接管页面；`inkflow-reader-shell-v1` 缓存包含 `/reader/offline`、Manifest 和两枚图标。
- 真实断网回退通过：停止 VM API 容器后，浏览器访问 `/reader/account` 仍由 Service Worker 控制并显示“当前处于离线状态”；恢复 API 后回到正常账户表单且无离线提示。浏览器错误/警告日志为空。验证结束已停止全部 Compose 容器，持久卷保留。
- 边界：内置浏览器未执行安装提示/独立窗口启动、真实账户登录/状态同步和跨设备同步；VM IP 的明文 HTTP 不能作为生产 PWA 安全上下文证据。生产 HTTPS、安装体验、真实账户及跨设备验收继续列入待定事项；本项不替代阅读 3.0 真机验收。

### 4.83 管理端/运维/权限非阅读 App 自动化运行验收（本轮，2026-08-30）

- 缺口：Content Policy、Operations、Source Authorization、CredentialReference 管理、Admin Audit Read 和 Administrator 套餐授予此前已有 API/集成基线，但仍缺少一条可重复的受保护运行时验收链。
- 实现：新增 `InkFlow.AcceptanceFixtures`，通过现有 Identity/Library/Sources Domain 与 EF Repository 准备临时角色、来源和 CanonicalBook；新增 acceptance profile 的源码构建 fixture launcher，仓库只读挂载、临时构建目录、NuGet 缓存隔离、DLL 运行和非 secret 输出均有明确边界。
- 自动化覆盖：Admin plans、Operations overview/alerts/history、Audit Read；Operator 授权前后和撤销后的 403；`source.manage` 授权幂等、健康读取、能力 disable/enable；默认 CredentialReference 非 secret 引用 set/clear；Content Policy takedown/restore 与公共详情可见性；Administrator 授予 Pro 后的 Entitlement/quota；命令审计过滤。
- 本机证据：Release Build 0 warnings / 0 errors；Unit 482/482、Architecture 1/1、Contract 10/10；launcher 与 smoke 结构回归、`git diff --check` 均 PASS。Windows Docker Engine 不可用，因此本机 Testcontainers 仍不作为证据。
- Ubuntu VM 证据：候选 `b7019b7` 以 `docker-compose.build.yml` 源码构建 API/Worker/Scheduler/Migrations 并通过健康等待；`admin-runtime-smoke: PASS (admin/operations, audit, source permissions and health, credential binding, content policy, entitlement)`；临时用户清理为 disabled，Compose 已停止，持久卷保留，VM 工作区 CLEAN。
- 远端证据：候选 `b7019b7c6ef7f2999a800ec65b668372c9e7643d` 的 [CI 33311294258](https://github.com/nekohands/InkFlow/actions/runs/33311294258)、[Docker 33311294256](https://github.com/nekohands/InkFlow/actions/runs/33311294256)、[Security 33311294239](https://github.com/nekohands/InkFlow/actions/runs/33311294239) 均 GREEN；CI 实际执行新增 Admin runtime smoke，并通过前端 1.0、SLO、Redis、PostgreSQL 备份恢复和 runtime diagnostics。
- 边界：按用户决定不启动 MuMu/阅读 3.0；未使用真实 Web 账户、生产凭据或真实第三方来源。真实管理员/Operator 体验、Provider/生产通知、跨设备和阅读 App 验收继续列在第 10 节，不将本轮标记为整体 `Accepted/Completed`。

### 4.84 Reader/PWA 账户与阅读状态 API 非阅读 App 自动化运行验收（本轮，2026-08-30）

- 按“除阅读 App 外尽量自动化”的要求，新增 scripts/reader-account-runtime-smoke.sh 及结构回归，并接入 CI 源码构建 Runtime；账户页面不由脚本输入真实或人工凭据。
- 自动化覆盖匿名拒绝、临时 Reader 注册与 auth/me、注册会话登出、登录、Refresh Token 轮换及旧 refresh 失效；阅读偏好默认值/持久化/边界拒绝；空书架/历史/进度 404；CanonicalBook fixture 书架加入/查询/移除、进度保存/读取、当前章节回显、历史联动、非法章节拒绝，以及最终登出后的认证失效。
- 本机证据：Release Build 0 warnings / 0 errors；reader-account smoke 结构回归、bash -n 和 git diff --check PASS。Windows Docker Engine 仍不可用；ShellCheck 未安装，不作为证据。
- Ubuntu VM 证据：候选 0597332 使用 docker-compose.build.yml 源码构建 API/Worker/Scheduler/Migrations，健康等待通过；reader-account-runtime-smoke: PASS (register, login, refresh rotation, logout, preferences, shelf, progress, history)；临时账户由 fixture 清理为 disabled，验证结束 Compose 已停止，持久卷保留。
- 远端证据：候选 05973324870386e67bd3cf6e8c45479b3288f4cf 的 [CI 33312963081](https://github.com/nekohands/InkFlow/actions/runs/33312963081)、[Docker 33312963065](https://github.com/nekohands/InkFlow/actions/runs/33312963065)、[Security 33312963084](https://github.com/nekohands/InkFlow/actions/runs/33312963084) 均 GREEN；CI 的 Reader account smoke script regression 与 Reader account runtime smoke 均实际通过。
- 边界：本轮不启动 MuMu/阅读 3.0，不输入真实 Web 账户，不把 API smoke 等同于 PWA 页面内的真实登录、安装/独立窗口、跨设备同步或长期体验；整体 1.0 仍保留真实来源、真实账户/安装、阅读 3.0 与生产环境待定项。

### 4.85 Reader/PWA 页面临时账户内置浏览器自动化验收（本轮，2026-08-30）

- 在 Ubuntu VM 的 `docker-compose.build.yml` 源码构建栈上，使用 GPT 内置浏览器和一次性本地账户完成页面级自动验收；未使用真实账户、第三方凭据或阅读 3.0。
- 自动化证据：注册后回到书库；账户页刷新保持登录态；Catalog fixture 详情页加入书架；书架列表展示书目；有效章节入口显示正确的未发布内容空状态；退出后账户表单恢复；匿名书架/历史显示登录提示；离线页显示离线状态和返回书库入口。
- 账户生命周期：临时账户已由 AcceptanceFixtures 禁用；源码 Compose 已停止；本地 SSH 转发已关闭。浏览器操作未读取 Cookie、Storage 或密码材料。
- 边界：该证据覆盖 VM 本地源码栈的非阅读 App 页面交互，不替代真实生产 HTTPS、安装/独立窗口、跨设备同步、长期阅读、真实章节正文和阅读 3.0 真机验收；整体仍为 `1.0 Release Candidate`。

### 4.86 Reader/PWA 已发布正文自动化验收（本轮，2026-08-30）

- `InkFlow.AcceptanceFixtures` 新增独立 `ensure-reader-catalog`：通过正式 `ContentPublishingService` 幂等准备已发布 Canonical Content；原 `ensure-catalog` 无正文行为不变，避免破坏空状态测试。
- 新增 `scripts/reader-content-runtime-smoke.sh`、fixture 回归并接入 CI；脚本以稳定 ChapterId 读取 `/reader/read/{chapterId}`，断言已发布正文、进度同步脚本、章节结束标记和阅读进度元素，同时拒绝“未发布内容”提示。
- 本机：`dotnet restore`、Release Build 0 warnings / 0 errors；Unit 482/482、Architecture 1/1、Contract 10/10 和新脚本回归通过。全量测试的 Windows IntegrationTests 因本机 Docker Engine 缺失为 6 passed / 2 skipped / 81 blocked，不作为本机集成通过。
- VM：`593f093` 源码构建 Compose 健康启动；`ensure-reader-catalog` 两次返回同一稳定书目/章节 ID；内容烟测 PASS。内置浏览器经本地 SSH 转发实际读到 3 段已发布正文，页面含进度元素且无未发布提示。验证结束已停止 Compose/转发，持久卷保留。
- 边界：进度/历史的认证写入由 4.84 API smoke 覆盖，本轮新增正文页面证据；未启动 MuMu/阅读 3.0、未使用真实凭据。真实账户、PWA 安装/生产 HTTPS、跨设备和长时间体验仍待定，整体不标记 `Accepted/Completed`。

### 4.87 Kanunu8 真实来源 Scheduler/Worker 内容链自动验收（本轮，2026-08-30）

- 新增 opt-in 真实来源编排测试和 `scripts/kanunu-live-acceptance.sh`：Kanunu8 当前目录经 `UpdateScanService` → `TocSyncTaskHandler` → `ContentFetchChainService` 入队，再由 `CrawlerTaskProcessor` / `ContentFetchTaskHandler` 完成真实正文抓取、`FetchArtifact` 记录、`ContentVersion` 发布和公共查询。
- 测试覆盖周期重扫的 TOC 去重、在途正文任务冲突去重、稳定来源章节 ID 和发布后可读性；常规 CI 只执行脚本语法回归，避免把外部站点变成 PR-CI 依赖。
- Ubuntu VM 候选 `d819935` 的一次性 .NET 10 SDK 容器输出 `kanunu-live-acceptance: PASS`，测试 5/5 通过；源码只读复制，不创建账号、不写业务数据库。候选已推送，等待该提交的远端 CI/Docker/Security 三组门禁。
- 边界：当前真实快照与应用编排已自动化，但没有可控的真实新增章节事件，因此“上游新增章节后下一周期发现并增量发布”仍待定；真实第二来源与故障切换同样未关闭。整体保持 `1.0 Release Candidate`，不标记 `Accepted/Completed`。

### 4.88 采集工作台与书籍打包 1.0 交接（本轮，2026-08-31）

- 采集工作台已落地：`CollectionRun` 父运行关联 `CrawlerTask` 子任务，支持 direct URL、持久化进度、暂停/恢复/停止/取消、阶段状态和审计；书籍包支持独立 ZIP、EPUB 3、单文件 UTF-8 TXT。
- 本轮回归修复：API 阶段值改为稳定的 `bookInfo`/`toc`/`content` 契约；迁移集成断言纳入 `crawler.runs`；共享包卷新增成功完成后才允许 API/Worker 启动的 `packages-init` 权限初始化。
- 并发补强：`crawler.runs` 增加 `(SourceId, ExternalBookId)` 活跃状态部分唯一索引，`EfCollectionRunRepository.TryAddAsync` 捕获唯一键竞争并由 `CollectionRunService` 复用获胜运行；跨连接 PostgreSQL 测试 `Concurrent_Active_Collection_Runs_Allow_Only_One_Insert` 已通过。
- 本机证据：Release Build PASS；Unit 494/494 PASS；`git diff --check` PASS。Windows Docker Engine 缺失，不能把本机 IntegrationTests 当作通过。
- Ubuntu VM 证据：`docker-compose.build.yml` 源码构建 API/Worker/Scheduler/Migrations 成功，服务健康、迁移和包卷初始化成功；Linux SDK 容器中的 Crawling PostgreSQL 集成测试 12/12 通过；完整 `collection-package-runtime-smoke` PASS，覆盖 direct URL、安全拒绝、四类持久控制幂等、前置不确定进度、ZIP/EPUB/TXT 生成下载、哈希/长度完整性和审计。
- 远端门禁：提交 `c6c4d25` 的 [CI 33328060988](https://github.com/nekohands/InkFlow/actions/runs/33328060988)、[Docker 33328060997](https://github.com/nekohands/InkFlow/actions/runs/33328060997)、[Security 33328060984](https://github.com/nekohands/InkFlow/actions/runs/33328060984) 均 GREEN，且三者 head SHA 一致。当前浏览器仅完成未认证运维页状态检查；受保护页面登录后的输入/交互验收需用户在填写临时本地账号密码前明确确认。MuMu/阅读 3.0 不执行，保留人工待定。
- 下一步：若用户确认临时账号输入，再经本地 SSH 转发完成 `/admin/operations` 登录后页面验收，并禁用临时账户、关闭转发；不论是否执行该补充验收，整体均保持 `1.0 Release Candidate`，不得把阅读 3.0/MuMu 人工待定项标为完成。

### 4.89 书籍包一致性快照与租约崩溃恢复交接（本轮，2026-08-31）

- `BookPackageService` 已改为一次整书当前版本读取后建立固定快照，避免逐章读取造成的混合版本；`IContentVersionRepository.ListCurrentForBookAsync` 明确要求持久化实现使用整书一致性查询。Unit 回归覆盖该调用边界。
- `BookPackageJob`/`EfBookPackageJobRepository` 已修复过期 Running 租约不消耗重试预算的问题：每次过期重领递增尝试次数，预算耗尽立即持久化为 Failed；Unit 2/2 与 PostgreSQL 集成 2/2 覆盖重领和耗尽分支。
- 本机证据：Release Build 0 warnings / 0 errors；Unit 497/497、Architecture 1/1、Contract 10/10、迁移模型检查和 diff check 均通过。Windows Docker Engine 不可用，集成测试在 Ubuntu VM 验证。
- Ubuntu VM 证据：源码构建 Compose、健康检查、Migration 和包任务集成回归通过；完整 `collection-package-runtime-smoke` 通过 direct URL、安全边界、四类控制幂等、前置进度、ZIP/EPUB/TXT、哈希/长度和审计，临时账号已禁用，Compose 已停止且卷保留。
- 候选 `ecd8533` 已推送 `dev`；[CI 33329741035](https://github.com/nekohands/InkFlow/actions/runs/33329741035)、[Docker 33329741037](https://github.com/nekohands/InkFlow/actions/runs/33329741037)、[Security 33329741041](https://github.com/nekohands/InkFlow/actions/runs/33329741041) 均 GREEN。未执行受保护 Operations 页面登录后的浏览器输入验收，也未启动 MuMu/阅读 3.0；整体不得标记 `Accepted/Completed`。

### 4.90 多书籍增量目录扫描冲突隔离交接（本轮，2026-08-31）

- `UpdateScanService` 已修复来源级活动 TOC 任务误阻塞同源其他书籍的问题；新增回归测试先红后绿，现按 `source + capability + bookId` 判断冲突，保留 `CrawlerTask` 的 `bookId` 变量和既有幂等边界。
- 本机：Release Build 0 warnings / 0 errors；Unit 498/498、Architecture 1/1、Contract 10/10、diff check PASS。Windows Docker Engine 缺失，Testcontainers 集成不作为本机证据。
- Ubuntu VM：`98e3725` 经 Linux SDK 容器定向 Unit 2/2；源码构建 Compose、健康启动/迁移通过；`reader-frontend-runtime-smoke`、`admin-runtime-smoke`、`collection-package-runtime-smoke` 均 PASS，后者覆盖持久控制和 ZIP/EPUB/TXT 包的完整性/审计。临时账号已禁用，Compose 已停止，持久卷保留。
- 交接门槛：候选 `b791c69` 的 [CI 33333159334](https://github.com/nekohands/InkFlow/actions/runs/33333159334)、[Docker 33333159291](https://github.com/nekohands/InkFlow/actions/runs/33333159291)、[Security 33333159324](https://github.com/nekohands/InkFlow/actions/runs/33333159324) 均 GREEN，三者 head SHA 一致。Security 的 CodeQL 仅报告既有权限/Action annotation，不影响门禁；真实追更新增事件、真实第二来源故障切换、真实账户/Provider/生产通知、受保护 Operations 页面输入验收和 MuMu/阅读 3.0 仍是人工/真实环境待定项，整体保持 `1.0 Release Candidate`。

### 4.91 Scheduler TOC 任务去重原子化交接（本轮，2026-08-31）

- `UpdateScanService` 已从“先查后插”改为调用 `TryAddIfNoConflictingTaskAsync`；EF 实现用同一事务内的 PostgreSQL advisory lock 串行化同一来源书籍的 TOC 入队，并将任务与 `TaskCreated` Outbox 一起提交，避免多 Scheduler 并发重复入队。
- TDD/本机：新增单元回归先红后绿；Release Build 0 warnings / 0 errors；Unit 499/499、Architecture 1/1、Contract 10/10、diff check PASS。Windows Docker Engine 缺失，Testcontainers 本机定向集成为 BLOCKED。
- Ubuntu VM：候选 `8cb2211` 的 Crawler PostgreSQL 集成测试 13/13 通过；完整测试 Unit 499/499、Architecture 1/1、Contract 10/10、Integration 92 passed / 2 skipped；源码构建 Compose、迁移、健康检查通过；前端/正文/账号/私有库/开发者 API/管理员运维/采集打包/SLO/备份恢复 Runtime smoke 均 PASS。临时账号已禁用，Compose 已停止，持久卷保留。
- 交接门槛：代码候选 `8cb2211` 的 [CI 33334393155](https://github.com/nekohands/InkFlow/actions/runs/33334393155)、[Docker 33334393053](https://github.com/nekohands/InkFlow/actions/runs/33334393053)、[Security 33334393020](https://github.com/nekohands/InkFlow/actions/runs/33334393020) 均 GREEN，三者 head SHA 一致。真实追更新增事件、真实第二来源故障切换、真实账户/Provider/生产通知、受保护 Operations 页面输入验收和 MuMu/阅读 3.0 仍是待定项，整体保持 `1.0 Release Candidate`，不标记 `Accepted/Completed`。

### 4.92 正文联动 Content 任务去重原子化交接（本轮，2026-08-31）

- `ContentFetchChainService` 已从“先查后插”改为调用 `TryAddIfNoConflictingTaskAsync`；EF 实现复用同一事务内的 PostgreSQL advisory lock、冲突检查、任务插入和 `TaskCreated` Outbox，避免并发正文联动重复入队。`ignoreDeadLettered` 参数保持 CollectionRun 的原有死信语义。
- TDD/本机：新增 `Uses_Atomic_Dedupe_Gate_For_Content_Tasks` 单元回归先红后绿；Release Build 0 warnings / 0 errors；Unit 500/500、diff check PASS。Windows Docker Engine 缺失，Testcontainers 集成在本机为 BLOCKED。
- Ubuntu VM：候选 `6b4b256` 的 Crawler PostgreSQL 集成测试 14/14 通过；完整测试 Architecture 1/1、Integration 93 passed / 2 skipped、Contract 10/10、Unit 500/500；源码构建 Compose、迁移、健康检查通过；`reader-content-runtime-smoke` 和 `reader-frontend-runtime-smoke` 均 PASS。验证结束后临时运行环境已停止，持久卷保留。
- 交接门槛：代码候选 `6b4b256` 的 [CI 33336335560](https://github.com/nekohands/InkFlow/actions/runs/33336335560)、[Docker 33336335553](https://github.com/nekohands/InkFlow/actions/runs/33336335553)、[Security 33336335552](https://github.com/nekohands/InkFlow/actions/runs/33336335552) 均 GREEN，三者 head SHA 一致。真实追更新增事件、真实第二来源故障切换、真实账户/Provider/生产通知、受保护 Operations 页面输入验收和 MuMu/阅读 3.0 仍是待定项，整体保持 `1.0 Release Candidate`，不标记 `Accepted/Completed`。

### 4.93 采集运行 Reconcile 与控制状态原子化交接（本轮，2026-08-31）

- `CollectionRunService.ReconcileAsync` 已改为调用原子仓储 seam；EF 仓储在同一 PostgreSQL 事务中锁定运行行、汇总子任务进度、执行领域折叠并保存，避免陈旧 Reconcile 覆写并发暂停/停止/取消命令。默认接口回退保持内存替身兼容。
- TDD/本机：`Reconcile_Does_Not_Overwrite_Control_State_Changed_After_Read` 先红后绿；Release Build 0 warnings / 0 errors；Unit 501/501、diff check PASS。Windows Docker Engine 缺失，新增 Testcontainers 集成在本机为 BLOCKED。
- Ubuntu VM：候选 `0133775` 的 Crawler PostgreSQL 集成测试 15/15 通过；完整测试 Architecture 1/1、Integration 94 passed / 2 skipped、Contract 10/10、Unit 501/501；源码构建 Compose、迁移、健康检查通过；采集/打包、正文和前端运行时冒烟均 PASS。临时验收账号已禁用，Compose 已停止，持久卷保留。
- 交接门槛：代码候选 `0133775` 的 [CI 33337767070](https://github.com/nekohands/InkFlow/actions/runs/33337767070)、[Docker 33337767065](https://github.com/nekohands/InkFlow/actions/runs/33337767065)、[Security 33337767076](https://github.com/nekohands/InkFlow/actions/runs/33337767076) 均 GREEN，三者 head SHA 一致。真实追更新增事件、真实第二来源故障切换、真实账户/Provider/生产通知、受保护 Operations 页面输入验收和 MuMu/阅读 3.0 仍是待定项，整体保持 `1.0 Release Candidate`，不标记 `Accepted/Completed`。

### 4.94 采集运行聚合写入口原子化交接（本轮，2026-08-31）

- `CollectionRunService` 的 CanonicalBook、阶段推进和工作启动写入已统一改用 `ICollectionRunRepository.MutateAsync`；EF 实现事务内锁定 `crawler.runs` 行并重新装载聚合后执行领域变更，避免陈旧写入覆写并发暂停/停止/取消状态。默认接口回退保留内存仓储与既有测试替身兼容。
- TDD/本机：`Run_Mutation_Does_Not_Overwrite_Control_State_Changed_After_Read`、`Stage_And_Work_Mutations_Preserve_Concurrent_Control_State` 先红后绿；Release Build 0 warnings / 0 errors；Unit 503/503、diff check PASS。Windows Docker Engine 缺失，新增 Testcontainers 回归在本机为 BLOCKED。
- Ubuntu VM：候选 `f3be335` 的 Crawler PostgreSQL 集成测试 16/16 通过；完整测试 Architecture 1/1、Integration 95 passed / 2 skipped、Contract 10/10、Unit 503/503；源码 Compose 构建、Migration、健康检查通过；采集打包运行时冒烟覆盖直接 URL、持久控制、ZIP/EPUB/TXT、完整性与审计并通过。临时账号已禁用，Compose 已停止且无残留容器。
- 交接门槛：代码候选 `f3be335` 的 [CI 33339150508](https://github.com/nekohands/InkFlow/actions/runs/33339150508)、[Docker 33339150530](https://github.com/nekohands/InkFlow/actions/runs/33339150530)、[Security 33339150520](https://github.com/nekohands/InkFlow/actions/runs/33339150520) 均 GREEN，三者 head SHA 一致。真实追更新增事件、真实第二来源故障切换、真实账户/Provider/生产通知、受保护 Operations 页面输入验收和 MuMu/阅读 3.0 仍是待定项，整体保持 `1.0 Release Candidate`，不标记 `Accepted/Completed`。

### 4.95 PWA 安装增强自动化契约覆盖交接（本轮，2026-08-31）

- Reader/PWA 已补齐安装增强回归：`reader-install`、`beforeinstallprompt` 的默认行为阻止与安装提示、`appinstalled` 后入口隐藏均由 ReaderHtml 单测和前端 smoke fixture 覆盖；产品运行逻辑未改变。
- 本机：Release Build 0 warnings / 0 errors、Unit 504/504、Reader 前端 smoke 和 `git diff --check` 均通过。Ubuntu VM 已同步候选 `b01f32d`，源码 Compose 构建、Migration、服务健康检查和 `reader-frontend-runtime-smoke` 通过；验证后 Compose 已停止且无残留服务容器。
- 交接门槛：候选 `b01f32d` 的 [CI 33341287060](https://github.com/nekohands/InkFlow/actions/runs/33341287060)、[Docker 33341287043](https://github.com/nekohands/InkFlow/actions/runs/33341287043)、[Security 33341287049](https://github.com/nekohands/InkFlow/actions/runs/33341287049) 均 GREEN，三者 head SHA 一致。
- 当前状态：整体保持 `1.0 Release Candidate`，自动化 Release Gate 已通过，不标记 `Accepted/Completed`。受保护 Operations 页面登录后输入、真实账户/PWA 安装与跨设备、真实来源/追更/故障切换、真实凭据和 MuMu/阅读 3.0 继续按待定事项执行。

### 4.96 BookInfo 采集子任务编排回归覆盖交接（本轮，2026-08-31）

- `BookInfoSyncTaskHandlerTests` 新增 5 个回归用例，覆盖成功的来源导入/正典匹配/Toc 子任务编排、输入缺失、来源失败、悬空匹配和 Stopping 运行；成功用例验证子任务与父运行共享 `RunId`，凭据只以引用传递。
- 本机：Release Build 0 warnings / 0 errors，定向 5/5，Unit 509/509，`git diff --check` PASS。
- Ubuntu VM：候选 `3ffebf2` 已同步；首次 `docker-compose.build.yml` 源码重建在 API/Scheduler 发布阶段遇到外部 NuGet 包下载瞬时超时，NuGet 恢复后重试完成源码构建、Migration、PostgreSQL、Redis、OTel Collector、API、Worker、Scheduler 健康启动。当前提交栈上的 `reader-frontend-runtime-smoke`、Reader 账号/正文、Core SLO、Developer API、Private Library 和完整 `collection-package-runtime-smoke` 均 PASS；采集包 smoke 使用临时管理员/操作员和控制运行夹具，覆盖直接地址、四类持久控制、ZIP/EPUB/TXT、完整性与审计。之后 Compose 已停止，`ps --all` 无残留服务容器，持久卷保留。
- 远端门槛：候选 `3ffebf2` 的 [CI 33342649568](https://github.com/nekohands/InkFlow/actions/runs/33342649568)、[Docker 33342649537](https://github.com/nekohands/InkFlow/actions/runs/33342649537)、[Security 33342649534](https://github.com/nekohands/InkFlow/actions/runs/33342649534) 均 GREEN 且指向同一 head SHA；Docker Migrations 首次推送的 `unknown blob` 已通过失败 Job 重跑消除。
- 当前状态：本工作包为 `Implemented`，整体继续保持 `1.0 Release Candidate`，不等同 `Accepted/Completed`。真实来源/追更/切源、受保护 Operations 登录后操作、真实账户/Provider/PWA 安装跨设备和 MuMu/阅读 3.0 仍按第 6 节待定事项执行。

### 4.97 Operations 登录后控制请求与浏览器验收交接（本轮，2026-08-31）

- 缺口与修复：内置浏览器发现 Operations run-control 对话框只提交 `reason`，遗漏 API 所需的 `action`；新增回归后修复为 run-control 提交 `{ action, reason }`，来源能力和死信重放保持 `{ reason }`。
- 本机：Restore PASS；Release Build 0 warnings / 0 errors；缺陷回归红→绿；Unit 510/510；Reader 前端脚本回归和 `git diff --check` PASS。
- Ubuntu VM：候选 `d5e8322` 源码 Compose 重建、Migration、服务健康检查和 `reader-frontend-runtime-smoke` 通过。内置浏览器通过临时 SSH 转发和一次性 Operator 账户完成登录后 Operations 页面验收：直接地址创建运行、取消、暂停、恢复及 EPUB 打包完成/下载入口均通过；运行已取消、临时账户已禁用、Compose 已停止且无残留服务容器。
- 交接门槛：候选 `d5e8322` 的 [CI 33344939033](https://github.com/nekohands/InkFlow/actions/runs/33344939033)、[Docker 33344939099](https://github.com/nekohands/InkFlow/actions/runs/33344939099)、[Security 33344939034](https://github.com/nekohands/InkFlow/actions/runs/33344939034) 均 GREEN 且指向同一 head SHA。
- 边界：临时测试账户不等于真实生产凭据；真实来源/追更/切源、生产 Operations 账号与通知、真实 PWA 安装/跨设备及 MuMu/阅读 3.0 真机仍待定，整体不标记 `Accepted/Completed`。

### 4.98 Legado 四步运行时门禁交接（本轮，2026-08-31）

- 实现：新增 `scripts/legado-runtime-smoke.sh`，逐项校验公开 `book-source.json`、Search、BookInfo、TOC、Content 的响应契约、稳定 ID/URL 和正文纯文本；新增确定性 curl fixture、脚本回归，并接入 CI。
- 运行时：候选 `df35d5e` 在 Ubuntu VM 通过 `docker-compose.build.yml` 源码构建、Migration 和全套服务健康检查；确定性 Reader 夹具发布后，四步运行时 smoke 输出 PASS。默认空查询用于避免触发真实来源网络请求；关键字过滤和真实来源证据仍按既有单元/契约/真实来源工作包处理。
- 清理：验证后 Compose 已停止，`ps --all` 无 InkFlow 服务容器残留，持久卷保留。
- 远端门槛：文档提交 `e4a9ea5` 的 [CI 33349225217](https://github.com/nekohands/InkFlow/actions/runs/33349225217)、[Docker 33349225212](https://github.com/nekohands/InkFlow/actions/runs/33349225212)、[Security 33349225202](https://github.com/nekohands/InkFlow/actions/runs/33349225202) 均 GREEN 且指向同一 head SHA。
- 边界：本轮未执行阅读 3.0 / MuMu 真机、真实凭据、真实来源访问或真实第二来源故障切换；整体仍为 `1.0 Release Candidate`，不标记 `Accepted/Completed`。

### 4.99 双来源故障切换运行时门禁交接（本轮，2026-08-31）

- 缺口与修复：公共正文读取此前只加载已存当前版本，未在 Source A 被管理员停用后重新评估持久化候选；`CatalogQueryService` 现通过 `IContentSelectionService` 先按已存候选/能力健康重选，再复查 Policy 和稳定正典身份，不在阅读路径触发第三方网络。ADR 见 [0022](../adr/0022-canonical-content-read-reselection.md)。
- 自动化：新增 `ensure-failover-catalog` fixture、`source-failover-runtime-smoke.sh` 和 curl fixture 回归，覆盖 Web Book/Content、Legado Search/BookInfo/TOC/Content 以及 Manifest；状态序列为 Source A 正常 → A Content 停用、切到 B → A 恢复、切回 A，并断言 BookId/ChapterId 不变。
- 本机：Release Build 0 warnings / 0 errors；Unit 511/511、Architecture 1/1、Contract 10/10、脚本语法 PASS。Windows 缺少 `jq`，新脚本功能测试本机 `BLOCKED`，由 VM/CI 执行。
- Ubuntu VM：`80962fb` 以源码 Compose 健康启动；全量 `Restore → Build → Test` 为 Unit 511/511、Architecture 1/1、Contract 10/10、Integration 95 passed / 2 skipped / 0 failed；Legado/failover 脚本回归 PASS；实际 `source-failover-runtime-smoke` 输出 PASS。临时管理员已清理，Compose 已停止，`ps --all` 无残留 InkFlow 容器，卷保留。
- 远端门槛：[CI 33351257794](https://github.com/nekohands/InkFlow/actions/runs/33351257794)、[Docker 33351257775](https://github.com/nekohands/InkFlow/actions/runs/33351257775)、[Security 33351257773](https://github.com/nekohands/InkFlow/actions/runs/33351257773) 均 GREEN 且指向同一 head SHA `80962fb`。
- 边界：本轮关闭确定性 Web/Legado 运行时切源基线，但不关闭真实 Official Source pair、真实追更、真实凭据、受保护生产 Operations、PWA 安装/跨设备或阅读 3.0/MuMu 真机验收；整体仍为 `1.0 Release Candidate`，不标记 `Accepted/Completed`。

### 5.0 Core SLO p95 目标门禁与冷启动隔离交接（本轮，2026-08-31）

- 缺口与修复：原合成探针会输出 p95 但不校验服务面目标，且源码 Compose 冷启动首个请求可能把初始化耗时计入短窗口；`core-slo-runtime-smoke.sh` 现按服务面 fail-closed 校验 p95（public/developer ≤ 750ms，Legado/reader ≤ 1000ms），每面先执行一次不计入统计的预热请求。
- 回归：fixture 覆盖 750ms 边界通过和 751ms 超目标失败；预热请求仍校验传输与预期状态，不保存响应正文或身份信息。
- 本机：脚本语法、回归、`git diff --check` PASS；Release Build 0 warnings / 0 errors；Unit 511/511、Architecture 1/1、Contract 10/10 PASS。
- Ubuntu VM：`61f739e` 源码构建 Compose 健康启动后，重启 API/Worker/Scheduler 并执行真实四面门禁；p95 为 public 6.887ms、Legado 8.120ms、developer 4.848ms、reader 5.984ms，四面均 PASS，JSON 含四面各 5 个样本和 0 个服务端错误。脚本回归 PASS；验证后 Compose 已停止，`ps --all` 无残留服务容器，卷保留。
- 远端门槛：候选 `8818390` 的 [CI 33354062102](https://github.com/nekohands/InkFlow/actions/runs/33354062102)、[Docker 33354062087](https://github.com/nekohands/InkFlow/actions/runs/33354062087)、[Security 33354062065](https://github.com/nekohands/InkFlow/actions/runs/33354062065) 均 GREEN 且指向同一 head SHA；CI 的 Core SLO 脚本回归、合成探针、telemetry receipt、证据上传和其他自动化门禁均通过。
- 边界：短窗口合成证据不等同生产 SLO；生产 OTLP/长窗口/告警保留、真实来源与追更/切源、真实凭据、Operations 受保护操作、PWA 安装跨设备和阅读 3.0/MuMu 真机仍按待定事项处理，整体不标记 `Accepted/Completed`。

### 5.1 Source Rule 有界串行前置请求链交接（本轮，2026-08-31）

- 实现：新增 `CapabilityRule.PreRequests` / `RuleRequestStep`，允许最多 8 个声明式、按顺序执行的同源前置请求；步骤响应可提取临时变量供后续步骤和主请求模板使用。一次执行内复用 CredentialReference、Session Cookie 与 MaxRequests/响应字节/结果大小/超时预算，前置响应正文不进入结果或持久化状态。ADR 见 [0023](../adr/0023-source-rule-bounded-pre-requests.md)。
- 失败关闭：Schema/codec/Validator 限制步骤与变量边界；每个请求和最终响应均通过绝对 URL、userinfo/fragment、SSRF、同源和控制字符校验。变量缺失、跨源、传输、解析或共享预算失败时不发主请求且不返回部分结果；动态 URL、循环、分支、递归和跨任务持久会话仍明确不支持。
- 本机：Restore PASS；Release Build 0 warnings / 0 errors；Unit 522/522、Architecture 1/1、Contract 10/10；Schema、定向 RuleAdapter/Validator/JSON 回归和 `git diff --check` PASS。
- Ubuntu VM：候选 `bcf8889` 以 `docker-compose.build.yml` 源码构建并健康启动；Migration 退出 0。Linux SDK 容器完整测试为 Unit 522/522、Architecture 1/1、Contract 10/10、Integration 95 passed / 2 skipped / 0 failed；Reader/Legado、A→B→A failover、Private Library、Developer API、Admin、collection/package（含四类控制和 ZIP/EPUB/TXT）、Core SLO/OTel receipt、Redis 1/1 和备份恢复均 PASS。Core SLO p95 为 public 60.375ms、Legado 13.887ms、developer 11.865ms、reader 6.705ms；完成后 Compose 已停止，服务容器无残留，持久卷保留。
- 远端门槛：候选 `bcf8889` 的 [CI 33357094411](https://github.com/nekohands/InkFlow/actions/runs/33357094411)、[Docker 33357094410](https://github.com/nekohands/InkFlow/actions/runs/33357094410)、[Security 33357094388](https://github.com/nekohands/InkFlow/actions/runs/33357094388) 均 GREEN 且 head SHA 一致。
- 当前状态：本工作包为 `Implemented`，整体仍为 `1.0 Release Candidate`，不标记 `Accepted/Completed`。真实 Official Source/追更/动态递归多请求、真实 SecretProvider/生产凭据、生产 Operations、PWA 安装跨设备和阅读 3.0/MuMu 真机验收继续按第 6 节待定。

### 5.2 书籍包租约栅栏与尝试隔离交接（本轮，2026-08-31）

- 工作包：修复书籍包租约回收后的旧 Worker 写入竞态，并隔离不同租约尝试的包文件；不新增 Migration，不改变旧已完成包的下载兼容路径。
- 实现：`SaveLeasedAsync` 以任务 ID、Running 状态、租约所有者、`AttemptCount` 和未过期条件执行原子更新；Running 任务拒绝通用 `SaveAsync`。服务层在每次处理期间固定租约身份，进度/完成/失败均走栅栏写入；租约丢失时只清理本次尝试的文件。临时文件使用 `jobId + attempt`，最终文件使用 `jobId-attempt.ext`。
- 回归：新增跨 DbContext 的 PostgreSQL 回归，确认旧租约不能覆盖新尝试；新增服务层回归，确认丢失租约不会把任务标记失败，也不会留下旧尝试文件。
- 本机：Restore PASS；Release Build 0 warnings / 0 errors；Unit 523/523 PASS；Integration 项目 Release 编译 0 warnings / 0 errors；`git diff --check` PASS。Windows Docker Engine 不可用，本机 Testcontainers 运行未记为通过。
- Ubuntu VM：候选 `052d34e` 通过源码 Compose 构建、Migration 和服务健康检查；真实 PostgreSQL `BookPackageJobRepositoryTests` 3/3 PASS；采集/打包运行烟测覆盖 direct URL、暂停/恢复/停止/取消、ZIP/EPUB/TXT、完整性和审计并 PASS。临时验收账号已禁用，Compose 已停止，服务容器无残留，持久卷保留。
- 远端门槛：`052d34e` 的 [CI 33362042359](https://github.com/nekohands/InkFlow/actions/runs/33362042359)、[Docker 33362042406](https://github.com/nekohands/InkFlow/actions/runs/33362042406)、[Security 33362042372](https://github.com/nekohands/InkFlow/actions/runs/33362042372) 均 GREEN 且 head SHA 一致。
- 当前状态：本工作包为 `Implemented`，整体仍为 `1.0 Release Candidate`，不标记 `Accepted/Completed`。真实来源/追更/第二来源故障切换、真实凭据/生产运维、PWA 安装跨设备、Operations 受保护登录后操作和阅读 3.0/MuMu 真机验收继续按第 6 节待定。
- 下一步：后续若要提升到全量 `Accepted/Completed`，先处理第 6 节真实环境与人工验收；本轮不启动 MuMu/阅读 3.0 测试。

### 5.3 直接地址采集启动原子一致性交接（本轮，2026-08-31）

- 工作包：修复直接地址采集“先建运行、后建首任务”的半成品窗口；保持 Source/Canonical 边界、稳定 BookId/ChapterId 和既有 Outbox 语义，不新增 Migration。
- 实现：新增 `TryAddWithInitialTaskAsync` 原子仓储 seam；EF 在同一事务内提交 `CollectionRun`、首个 `BookInfo` `CrawlerTask` 与 `crawler.task.created` Outbox。仅活动运行唯一键冲突返回 false 供服务层复用并发运行，首任务/Outbox 的其他失败整体回滚并传播；服务层不再依赖独立 Task Repository 完成两步启动。
- 回归：新增首任务写入失败不留空运行、并发启动只保留一个运行/任务/Outbox 的 PostgreSQL 回归；Unit fake 与 AcceptanceFixtures 已同步新接口。
- 本机：红测已复现旧缺陷；Restore、Release Build 0 warnings / 0 errors、Unit 523/523、Architecture 1/1、Contract 10/10、Integration 项目 Release 编译和 `git diff --check` 通过；完整本机 Integration 受 Windows Docker named pipe 不可用影响，99 项中 7 passed / 2 skipped / 90 BLOCKED。
- Ubuntu VM：`ef2b8dd` 源码 Compose 构建、Migration、服务健康检查、真实 PostgreSQL `CrawlerTaskRepositoryTests` 17/17 和完整测试通过（Unit 523/523、Architecture 1/1、Contract 10/10、Integration 97 passed / 2 skipped / 0 failed）；第二轮 collection/package smoke 覆盖 direct URL、四类控制、ZIP/EPUB/TXT、完整性和审计并 PASS。临时账号已禁用，Compose 已停止，服务容器无残留，持久卷保留。
- 远端门槛：候选 `ef2b8dd` 的 [CI 33367713458](https://github.com/nekohands/InkFlow/actions/runs/33367713458)、[Docker 33367713401](https://github.com/nekohands/InkFlow/actions/runs/33367713401)、[Security 33367713423](https://github.com/nekohands/InkFlow/actions/runs/33367713423) 均 GREEN 且 head SHA 一致。
- 当前状态：自动化 Release Gate 已通过，整体继续为 `1.0 Release Candidate`，不标记 `Accepted/Completed`。真实来源/追更/第二来源故障切换、真实凭据/Provider/生产运维、PWA 安装跨设备、受保护 Operations 登录后人工验收和 MuMu/阅读 3.0 真机验收继续待定；本轮不启动 MuMu/阅读 3.0 测试。

### 5.4 采集任务租约与运行控制并发栅栏交接（本轮，2026-08-31）

- 工作包：保证暂停、停止或取消 CollectionRun 与 Worker 领取其子任务并发发生时，父运行的已提交控制状态优先于候选查询中的旧快照；不改变公开 API、状态机、Source/Canonical 边界或历史任务兼容性。
- 实现：`EfCrawlerTaskRepository.TryLeaseCoreAsync` 在锁定候选任务后，对 `RunId` 对应的 `crawler.runs` 执行 `FOR UPDATE` 并重新读取状态；父运行不存在或不是 `Pending/Running` 时提交空结果，不写入任务租约。无 `RunId` 的任务继续走既有领取路径。
- 回归：`Lease_Rechecks_Parent_Run_After_Control_Transaction_Commits` 使用独立 DbContext/事务和定向任务 ID，验证控制事务提交前等待、提交后不领取以及任务仍为 `Pending`；此前全量运行暴露了测试必须定向任务的隔离问题，已在 `da04e8e` 修正并复验。
- 本机：Release Build 0 warnings / 0 errors、Unit 523/523、Integration 项目 Release 编译和 `git diff --check` 通过；Windows Docker Engine 缺失，本机 Testcontainers 不作为通过证据。
- Ubuntu VM：候选 `da04e8e` 的定向真实 PostgreSQL 回归 1/1 PASS；同一 Linux SDK 容器完整 `Restore → Build → Test` 为 Unit 523/523、Architecture 1/1、Contract 10/10、Integration 98 passed / 2 skipped / 0 failed。当前 head 重新完成 `docker-compose.build.yml` 源码构建四镜像、Migration 退出 0、Compose 健康等待，API/Worker/Scheduler `/health` 均返回 200；验证后 Compose 已停止，服务容器和网络已清理，持久卷保留。
- 远端门槛：`da04e8e` 的 [CI 33372702168](https://github.com/nekohands/InkFlow/actions/runs/33372702168)、[Docker 33372702149](https://github.com/nekohands/InkFlow/actions/runs/33372702149)、[Security 33372702139](https://github.com/nekohands/InkFlow/actions/runs/33372702139) 均 GREEN 且 head SHA 一致。
- 当前状态：本工作包为 `Implemented`，自动化 Release Gate 已通过，整体仍为 `1.0 Release Candidate`，不标记 `Accepted/Completed`。阅读 3.0/MuMu 真机、真实来源/追更/第二来源故障切换、真实凭据/Provider/生产运维、PWA 安装跨设备和受保护 Operations 登录后操作继续按第 6 节待定；本轮不启动 MuMu/阅读 3.0 测试。

### 5.5 采集子任务入队与执行启动事务门禁交接（本轮，2026-08-31）

- 工作包：关闭 CollectionRun 控制提交与子任务追加、任务执行器调用之间的竞态窗口；不新增 Migration，不改变公开 API、状态机、Source/Canonical 边界或无 `RunId` 历史任务路径。
- 实现：`ContentFetchChainService`、`BookInfoSyncTaskHandler` 使用带父运行行锁的原子去重入队；`CrawlerTaskProcessor` 使用原子 `TryMarkRunningAsync`，生产 EF 按任务→父运行锁顺序重新检查状态，拒绝终态/缺失父运行并取消任务，避免调用执行器；Pending 父运行与任务启动在同一事务内推进为 Running。仓储接口保留默认兼容实现供既有测试替身使用，生产实现承担真实门禁。
- 回归：TDD 红→绿覆盖内容链、BookInfo 子任务和处理器；真实 PostgreSQL 定向控制竞态 3/3、任务启动 2/2 通过，包含控制事务提交后的入队拒绝、执行启动拒绝、Pending 父运行正向启动；Unit 处理器回归 5/5，并新增拒绝启动时父运行保持 Pending 的回归。
- Ubuntu VM：同一 Linux SDK 容器完整 `Restore → Build → Test` 为 Release Build 0 warnings / 0 errors、Unit 526/526、Architecture 1/1、Contract 10/10、Integration 101 passed / 2 skipped / 0 failed。`835ccd5` 通过 `docker-compose.build.yml` 源码构建四个业务镜像；Migration/packages-init 正常退出，API/Worker/Scheduler/PostgreSQL/Redis 健康，三个服务 `/health` 返回 200；验证后 Compose 已停止，`ps --all` 无服务容器残留，网络已清理，持久卷保留。
- 远端门槛：代码候选 `835ccd5` 的 [CI 33380404527](https://github.com/nekohands/InkFlow/actions/runs/33380404527)、[Docker 33380404455](https://github.com/nekohands/InkFlow/actions/runs/33380404455)、[Security 33380404474](https://github.com/nekohands/InkFlow/actions/runs/33380404474) 均 GREEN 且 head SHA 一致。
- 当前状态：本工作包为 `Implemented`，自动化 Release Gate 已通过，整体仍为 `1.0 Release Candidate`，不标记 `Accepted/Completed`。真实 Official Source/追更/第二来源故障切换、真实凭据/Provider/生产运维、PWA 安装跨设备、受保护 Operations 登录后操作和 MuMu/阅读 3.0 真机验收继续按第 6 节待定；本轮不启动 MuMu/阅读 3.0 测试。

### 5.6 Rule 主请求最终响应同源门禁交接（本轮，2026-08-31）

- 工作包：修复无 `Session` 的 Rule 主请求未检查最终 `ResponseUri` 的边界缺陷；保持 Safe HTTP 的连接级 SSRF 防护和现有重定向跳数策略，不改变公开 API、Schema、Migration 或 Source/Canonical 边界。
- 实现：新增跨源最终响应红→绿回归；`RuleAdapter` 在主请求成功响应进入正文/字段提取前，与前置请求一样校验绝对 URI、userinfo、fragment 和 source origin，并在失败时不返回部分结果。连接级 `SsrfSafeHttpMessageHandler` 继续负责每跳 DNS/地址/端口校验，当前自动重定向最多 5 跳边界不变。
- 回归：本机 RuleAdapter 52/52、Unit 527/527、Architecture 1/1、Contract 10/10 通过；`git diff --check` 通过。
- Ubuntu VM：候选 `c0ad1dc` 在 Linux SDK 容器完整 `Restore → Build → Test` 为 Release Build 0 warnings / 0 errors、Unit 527/527、Architecture 1/1、Contract 10/10、Integration 101 passed / 2 skipped / 0 failed；`verify-migrations.sh` 通过 11 个 contexts。随后用 `docker-compose.build.yml` 源码构建 Compose，Migration/packages-init 正常退出，API/Worker/Scheduler/PostgreSQL/Redis/OTel 健康，三个服务 `/health` 返回 200；验证后 Compose 已停止，服务容器和网络已清理，持久卷保留。
- 远端门槛：候选 `c0ad1dc` 的 [CI 33382784197](https://github.com/nekohands/InkFlow/actions/runs/33382784197)、[Docker 33382783508](https://github.com/nekohands/InkFlow/actions/runs/33382783508)、[Security 33382783564](https://github.com/nekohands/InkFlow/actions/runs/33382783564) 均 GREEN 且 head SHA 一致。
- 当前状态：本工作包为 `Implemented`，自动化 Release Gate 已通过，整体仍为 `1.0 Release Candidate`，不标记 `Accepted/Completed`。真实来源/追更/第二来源故障切换、真实凭据/Provider/生产运维、PWA 安装跨设备、受保护 Operations 登录后操作和 MuMu/阅读 3.0 真机验收继续按第 6 节待定；本轮不启动 MuMu/阅读 3.0 测试。

### 5.7 执行失败信息稳定化与敏感细节边界交接（本轮，2026-08-31）

- 工作包：安全复审发现部分执行失败路径会把 `Exception.Message` 传播到业务结果、持久化失败原因或宿主控制台；本轮收敛错误文本的稳定性和敏感细节边界，不改变重试、死信、状态机、审计时序、公开 API 形状或 Migration。
- 实现：RuleAdapter 的 transport/invalid-regex、Crawler executor、Content publisher、Health probe、Book Package builder、CollectionRun 控制结果以及 Worker/Scheduler/SourceSeed 宿主文本均使用稳定低基数失败文本；新增红→绿回归覆盖关键结果路径。异常对象仍可由既有结构化日志设施按访问控制记录，业务错误文本不回显原始异常细节。
- 本机：定向安全回归 6/6；Restore、Release Build 0 warnings / 0 errors、Unit 530/530、Architecture 1/1、Contract 10/10、`git diff --check` 均 PASS。Windows Docker Engine 不可用，Testcontainers 不计为本机通过。
- Ubuntu VM：`e167a1f` 在 Linux SDK 容器完整 `Restore → Build → Test` 为 Release Build 0 warnings / 0 errors、Unit 530/530、Architecture 1/1、Contract 10/10、Integration 101 passed / 2 skipped / 0 failed；`verify-migrations.sh` 通过 11 个 contexts。`docker-compose.build.yml` 低并发源码构建四业务镜像并健康启动，Migration/packages-init 正常退出，API/Worker/Scheduler/PostgreSQL/Redis/OTel 健康，三个服务 `/health` 返回 healthy；验证后 Compose 已停止，服务容器和网络清理，持久卷保留。
- 远端门槛：代码候选已推送；文档候选提交后必须重新查询 CI、Docker、Security，并确认三者指向同一最终 head SHA。
- 当前状态：本工作包为 `Implemented`，自动化 Release Gate 已通过，整体仍为 `1.0 Release Candidate`，不标记 `Accepted/Completed`。真实 Official Source/追更/第二来源故障切换、真实凭据/Provider/生产运维、PWA 安装跨设备、受保护 Operations 登录后操作和 MuMu/阅读 3.0 真机验收继续按第 6 节待定；本轮不启动 MuMu/阅读 3.0 测试。

### 5.8 Quality failure drill 运行时门禁交接（本轮，2026-08-31）

- 工作包：补齐 Phase 1B 的 Quality failure drill；同一来源先发布完整高质量章节，再发布故意截断的低质量重放，要求选择器保留高质量当前版本。
- 实现：AcceptanceFixtures 的 `ensure-quality-failure-catalog` 使用真实 `ContentPublishingService`、`QualityEngine` 与 `ContentSelectionService` 创建/重放候选，并输出分数、版本 ID、选择证据；`scripts/quality-failure-runtime-smoke.sh` 通过 Web `/api/v1/chapters/{chapterId}/content`、Legado `/api/legado/v1/chapters/{chapterId}` 和 `/reader/read/{chapterId}` 校验高质量标记存在且低质量标记不存在；脚本回归已加入 `.github/workflows/ci.yml`。
- 证据：本机 Release Build 0 warnings / 0 errors、Unit 530/530、Architecture 1/1、Contract 10/10、脚本语法和 diff 检查 PASS；Ubuntu VM 脚本回归 PASS，当前源码 acceptance fixture 输出 good `100` / low `30` 且 selected 为 good，源码 Compose API/Worker/Scheduler 健康，三个公共出口运行烟测 PASS。Windows 本机缺少 `jq`，因此功能脚本不在 Windows 直接执行。
- 代码提交：`f29256e`；质量门禁证据提交：`d5e78ed`。该证据提交的远端 [CI 33392373531](https://github.com/nekohands/InkFlow/actions/runs/33392373531)、[Docker 33392373476](https://github.com/nekohands/InkFlow/actions/runs/33392373476)、[Security 33392373377](https://github.com/nekohands/InkFlow/actions/runs/33392373377) 均 GREEN 且 SHA 一致；后续仅文档性修订不改变代码候选。
- 边界：这是确定性运行时演练，不替代真实 Official Source、真实追更、真实第二来源故障切换、阅读 3.0/MuMu 真机、真实凭据、PWA 安装/跨设备和人工视觉/可访问性验收；整体继续保持 `1.0 Release Candidate`，不标记 `Accepted/Completed`。

### 5.9 1.0 前端自动化证据矩阵对齐交接（本轮，2026-08-31）

- 发现：`docs/roadmap/phase-1-acceptance.md` 的旧 UX 清单没有区分自动化门禁和人工/真实环境验收，虽然 Progress 已记录浏览器自动化通过，但文件状态容易被误读为自动化未执行。
- 修复：Phase 1A 文档新增 Automated evidence 小节，列出 Web Reader 多视口、Reader/PWA shell/离线、Operations/采集打包、敏感信息排除和契约检查的自动化范围；原清单明确标为 Human / visual acceptance evidence，未把人工项错误勾选为完成。
- 对应入口：`scripts/reader-frontend-runtime-smoke.sh`、`scripts/reader-account-runtime-smoke.sh`、`scripts/collection-package-runtime-smoke.sh`，结构回归位于 `scripts/tests/`；运行证据见 Progress 4.75、4.82–4.86、4.97–4.99、5.8。
- 边界：真实 PWA 安装/跨设备、长时间阅读、真实账户、人工视觉/触控/对比度、真实 Official Source 与阅读 3.0/MuMu 仍未执行；整体保持 `1.0 Release Candidate`，不标记 `Accepted/Completed`。

### 5.10 Scheduler/Worker 新章节确定性追更验收交接（本轮，2026-08-31）

- 工作包：补齐“周期扫描遇到上游新增章节”这一默认自动化证据，不启动真实 Official Source、阅读 3.0 或 MuMu。
- 回归：`EndToEndDataFlowTests.Automated_Scheduler_Discovers_New_Chapter_And_Publishes_Content` 以可控来源适配器在两次扫描之间追加第三章，验证 `UpdateScanService` 入队、TOC 同步、稳定 CanonicalChapter 映射、增量 Content 任务、FetchArtifact、ContentVersion 发布和公共查询可读；重复扫描不重复创建已完成章节的 Content 任务。
- 本机/VM：Windows 定向测试 1/1 PASS；Ubuntu VM 的 .NET 10 SDK 容器隔离 worktree 定向测试 1/1 PASS。VM 原工作树质量演练未提交改动保留，临时 worktree 已清理。
- 代码提交：`5875479` 已推送；远端 [CI 33397704667](https://github.com/nekohands/InkFlow/actions/runs/33397704667)、[Docker 33397704675](https://github.com/nekohands/InkFlow/actions/runs/33397704675)、[Security 33397704619](https://github.com/nekohands/InkFlow/actions/runs/33397704619) 均 GREEN 且 head SHA 一致。
- 边界：确定性追更工程门禁已补齐，但真实上游新增/修订事件、真实第二来源故障切换、真实凭据/Provider、PWA 安装/跨设备、人工视觉验收和阅读 3.0/MuMu 仍按第 6 节待定；整体保持 `1.0 Release Candidate`，不标记 `Accepted/Completed`。

### 5.11 最新 dev 全量 VM 与源码 Compose Release Gate 复验交接（本轮，2026-08-31）

- 范围：在 Ubuntu VM 从最新 `dev` 代码候选 `5875479`（文档头 `361fe18`）建立隔离 worktree，按源码构建 Compose 复验适用 Release Gate；明确跳过 MuMu/阅读 3.0 和真实来源网络请求。
- 全量测试：Linux .NET 10 SDK 容器 Restore、Release Build 0 warnings / 0 errors、Unit `530/530`、Architecture `1/1`、Contract `10/10`、Integration `104` 项（`102 passed / 2 skipped / 0 failed`）；确定性 Scheduler 追更用例在全量测试中通过。
- 运行态：源码构建四业务镜像，Migration/packages-init 正常退出，PostgreSQL/Redis/OTel/API/Worker/Scheduler 健康；Reader/PWA、账号/正文、Legado、failover、Quality failure、Private Library TXT/EPUB、Developer API、Admin、collection/package（直接地址和四类控制、ZIP/EPUB/TXT、完整性/审计）均 PASS。
- 观测与恢复：Core SLO p95 为 public `28.058ms`、Legado `15.181ms`、developer `7.840ms`、reader `7.639ms`，四面 PASS；1 秒指标导出 + detailed debug 下两个 `inkflow.slo.*` 指标和四个 surface 的 Collector receipt PASS；backup/restore PASS，archive `108510 bytes`、`audit_events=271`。
- 过程与清理：初次运行中一次重复注册测试账户的编排错误产生 409，修正后全套业务 smoke 通过；首次 OTel 检查过早，调整导出周期后通过。临时账户、隔离 Compose 服务/网络/卷、fixture SDK 和 worktree 均已清理，原 VM 工作树保留。
- 交接边界：本轮仅确认最新代码栈的自动化 VM Release Gate，不关闭真实追更/第二来源/真实凭据与 Provider/生产 OTLP-SLO/生产运维/PWA 跨设备/人工视觉和 MuMu/阅读 3.0；整体仍为 `1.0 Release Candidate`，不标记 `Accepted/Completed`。

### 5.12 配额快照缓存身份与损坏值 fail-closed 加固交接（本轮，2026-08-31）

- 工作包：修复 Redis 配额快照损坏值导致的反序列化异常，并阻止错误用户或错误计费周期的缓存快照被当作当前用户结果；PostgreSQL 继续作为权威来源。
- 实现：`RedisQuotaSnapshotCache` 捕获 `JsonException` 并按 cache miss 处理；`QuotaService` 命中时增加 `UserId`、`PeriodStart` 校验，同时保留周期结束和套餐字段校验；新增 Developer API Key 生成器、损坏 Redis 快照和跨用户快照回源回归。
- VM/Runtime：Ubuntu VM Linux SDK 容器完整 Restore → Release Build（0 warnings / 0 errors）→ Test：Unit `533/533`、Architecture `1/1`、Contract `10/10`、Integration `103 passed / 2 skipped / 0 failed`；源码构建 Compose 四镜像健康启动，`developer-api-runtime-smoke` PASS。隔离 Compose、网络、容器和 worktree 已清理，VM 原工作树保留。
- 代码与 CI：`a111c9a` 已推送；[CI 33405514000](https://github.com/nekohands/InkFlow/actions/runs/33405514000)、[Docker 33405514007](https://github.com/nekohands/InkFlow/actions/runs/33405514007)、[Security 33405514040](https://github.com/nekohands/InkFlow/actions/runs/33405514040) 均 GREEN。
- 交接边界：自动化缓存安全缺口已关闭；真实账户/套餐/超额/停用、生产 Redis 故障、真实来源、PWA 跨设备、人工视觉和 MuMu/阅读 3.0 仍按第 6 节待定，整体保持 `1.0 Release Candidate`，不标记 `Accepted/Completed`。

### 5.13 Developer API 配额超额、账户隔离与停用自动化运行验收交接（本轮，2026-09-01）

- 工作包：在 5.12 的配额快照 fail-closed 基础上，补齐 Developer API Free 配额耗尽、跨账户隔离和停用用户拒绝的源码构建 Compose 运行证据；不改变生产 API、数据库 schema 或计费事实模型。
- 实现：`scripts/developer-api-runtime-smoke.sh` 使用四个活跃 API Key 分摊动态配额请求，验证内容路径的 `429`、`quota_exceeded`、`periodEnd`、`remainingUnits` 和 `Retry-After`；第二临时账户验证独立新配额，`AcceptanceFixtures disable-user` 后验证 Bearer/Developer Key 均为 `401`。新增 `scripts/disable-acceptance-user.sh`，兼容 CI Compose 与 SDK 容器 fixture runner。
- VM 证据：Ubuntu VM 隔离 worktree `f7b8e27` 的 Linux SDK 容器 Restore、Release Build（0 warnings / 0 errors）、Unit `533/533`、Architecture `1/1`、Contract `10/10`、Integration `105`（`103 passed / 2 skipped / 0 failed`）通过；源码构建四业务镜像和 PostgreSQL/Redis/OTel/API/Worker/Scheduler 健康启动通过；扩展 Developer API smoke PASS；`dotnet tool restore` 后 11 个 migration context 校验 PASS。验证后容器/网络/worktree 清理，持久卷保留，VM 原工作树改动保留。
- 远端证据：代码候选 `f7b8e27` 的 [CI 33409960296](https://github.com/nekohands/InkFlow/actions/runs/33409960296)、[Docker 33409960193](https://github.com/nekohands/InkFlow/actions/runs/33409960193)、[Security 33409960204](https://github.com/nekohands/InkFlow/actions/runs/33409960204) 均 GREEN 且 head SHA 一致。
- 当前状态：本轮自动化 Developer API 补充证据已闭合，但整体仍为 `1.0 Release Candidate`，不等同于 `Accepted/Completed`。真实账户/套餐/Provider、生产 Redis、人工 Operations/审计、真实来源、PWA 跨设备以及 MuMu/阅读 3.0 继续按第 6 节待定；首次 SDK smoke 缺少 `jq`、首次迁移校验未恢复 EF 设计程序集，均已修正后复验通过。

### 5.14 linovelib RuleAdapter 后端直连复核与上游阻塞记录交接（本轮，2026-09-01）

- 复核目标：验证现行 linovelib Rule DSL 的服务端搜索请求 `POST /S6/` + `searchkey={key}`，避免把 GPT 内置浏览器在 4.77 中取得的页面链路误当成 RuleAdapter 直连证据。
- Ubuntu VM 只读结果：`GET /novel/1.html` 为 HTTP 200/38811 bytes，`GET /novel/1/catalog` 为 HTTP 200/74342 bytes；带浏览器常见请求头的 `POST https://www.linovelib.com/S6/` 为 HTTP/2 200/0 bytes，正文无 `/novel/` 结果链接，响应来自 Cloudflare。
- 结论与边界：这是一条上游/站点挑战阻塞，不是适配器通过。未修改 Rule DSL、公共 Contract、Migration 或 SSRF 安全边界，也未尝试 Cookie 注入、TLS/SSRF 绕过等方式；`linovelib RuleAdapter 后端直连链路` 继续保持 BLOCKED，解除后需重新执行 Search → BookInfo → TOC → Content。

### 5.15 linovelib RuleAdapter 可选真实验收 harness 与 VM Release Gate 交接（本轮，2026-09-01）

- 工作包：新增服务端 linovelib RuleAdapter 的 opt-in 真实验收测试和脚本入口，默认不触发第三方网络；显式设置 `INKFLOW_LIVE_TESTS=1` 后才执行 Search → BookInfo → TOC → Content。
- 实现：`LinovelibSourceAdapterLiveTests` 复用生产安全 HTTP/SSRF 边界和当前 Rule DSL；`scripts/linovelib-live-acceptance.sh` 校验环境门槛并运行过滤测试，CI 对脚本执行 `bash -n`，最终提交 `b50001c` 补齐可执行权限。
- VM 证据：候选 `2ec2a43` 内容在 Ubuntu VM 隔离 worktree 完成源码构建四镜像；SDK 容器 Restore、Release Build 0 warnings / 0 errors、Unit `533/533`、Architecture `1/1`、Contract `10/10`、Integration `103 passed / 3 skipped / 0 failed`；`verify-migrations.sh` 的 11 个 context PASS；Compose Migration/packages-init 正常退出，PostgreSQL/Redis/OTel/API/Worker/Scheduler 健康，API/Worker/Scheduler `/health` 返回 healthy。随后切到只改文件模式的 `b50001c`，脚本语法和未设置 live 开关的 NOT RUN 门槛验证通过。
- 远端证据：提交 `8673bff` 的 [CI 33418330334](https://github.com/nekohands/InkFlow/actions/runs/33418330334)、[Docker 33418330294](https://github.com/nekohands/InkFlow/actions/runs/33418330294)、[Security 33418330318](https://github.com/nekohands/InkFlow/actions/runs/33418330318) 均 GREEN 且指向同一 head SHA；CI 包含新脚本回归、全量测试、Compose、前端、运行态、SLO、Redis/PostgreSQL 和备份恢复门禁。
- 清理与边界：未设置 `INKFLOW_LIVE_TESTS=1`，没有运行真实 linovelib 网络验收；隔离服务、网络、容器和临时 worktree 已清理，VM 原工作树与持久卷保留。脚本不能绕过 Cloudflare，也不能将浏览器证据等同于服务端通过；该真实链路继续为 BLOCKED，整体保持 `1.0 Release Candidate`，不标记 `Accepted/Completed`。

### 5.16 GPT 内置浏览器 VM Web 入口复核与客户端网络边界交接（本轮，2026-09-01）

- 复核目标：继续自动化非阅读 App 的 Web Reader 验收，确认最新源码构建 Compose 栈可运行，并尝试从 GPT 内置浏览器访问 VM Web 入口；本轮不启动 MuMu/阅读 3.0。
- VM 证据：基于 `origin/dev` 的 `8652c99` 建立隔离 worktree，源码构建四个业务镜像并健康启动 Compose；Migration/packages-init 正常退出，PostgreSQL/Redis/OTel/API/Worker/Scheduler 健康。经 SSH 本地转发访问 API `/health` 得到 HTTP 200：`{"status":"healthy","service":"InkFlow.Api"}`。验证后已停止并清理隔离 Compose、网络、容器、转发和临时 worktree，VM 原工作树与持久卷保留。
- 浏览器结果：内置浏览器访问公共 HTTPS 正常；访问 VM IP 和 SSH 转发后的本地/私网 HTTP 地址均返回 `net::ERR_BLOCKED_BY_CLIENT`。因此本轮无法取得新的页面级浏览器证据；这属于浏览器客户端网络策略，不是应用健康检查失败。未创建公共隧道，未绕过 HTTPS/SSRF 安全边界。
- 远端门禁：文档提交 `f4583c2` 的 [CI 33422098715](https://github.com/nekohands/InkFlow/actions/runs/33422098715)、[Docker 33422098588](https://github.com/nekohands/InkFlow/actions/runs/33422098588) 和 [Security 33422098584](https://github.com/nekohands/InkFlow/actions/runs/33422098584) 均 GREEN 且指向同一 head SHA；CI 的全量测试、Compose、前端/运行态、SLO、Redis/PostgreSQL、备份恢复以及 Security 的依赖、SBOM、Trivy、CodeQL 均通过。
- 交接边界：未修改代码或公共 Contract。4.75/4.85 已有的 Web Reader 页面自动化证据不被本轮结果推翻，但视觉、真实账户/PWA 安装与跨设备、阅读 3.0 及其他人工/真实环境验收仍按第 6 节待定；整体保持 `1.0 Release Candidate`，不标记 `Accepted/Completed`。

### 5.17 CollectionRun 直接地址采集 HTTP 状态契约与 VM Release Gate 交接（本轮，2026-09-01）

- 工作包：修复直接地址采集启动端点把来源解析失败误报为 `400` 的语义缺口；不改变 CollectionRun 状态机、任务控制、Source/Canonical 边界或打包格式。
- TDD 与实现：红态测试先验证缺少状态映射 seam 时不能编译；绿态新增 `CollectionRunEndpoints.GetStartStatusCode` 并由 `StartAudited` 使用。新建运行返回 `202`，复用活跃运行返回 `200`，来源地址无法解析返回 `422`，非法输入保持 `400`；定向 `CollectionRunEndpointTests` `2/2 PASS`，采集/打包脚本回归与语法检查 PASS，需求文档 11.2 已同步。
- 本机证据：Restore、Release Build（0 warnings / 0 errors）、Unit `534/534`、Architecture `1/1`、Contract `10/10` PASS；完整 Integration 106 项中 8 通过、3 跳过、95 项因 Windows 本机 Docker Engine `npipe://./pipe/docker_engine` 不可用而 BLOCKED，不计为本机全量 Integration 通过。
- Ubuntu VM 证据：候选 `9bda886` 在隔离 worktree 中使用 Linux .NET 10 SDK 完成 Restore、工具恢复、Release Build（0 warnings / 0 errors）和全量测试：Unit `534/534`、Architecture `1/1`、Contract `10/10`、Integration `103 passed / 3 skipped / 0 failed`；11 个 migration context 检查 PASS。源码构建 Compose 四业务镜像成功，Migration/packages-init 正常退出，PostgreSQL/Redis/OTel/API/Worker/Scheduler 健康，三个 `/health` 返回 healthy。
- 运行与清理：`collection-package-runtime-smoke` PASS，实际覆盖直接地址、`422`、暂停/恢复/停止/取消及幂等、ZIP/EPUB/TXT、完整性和审计。临时账户、隔离服务/网络/容器、临时 NuGet 卷和 worktree 已清理；VM 原工作树原有改动保留，敏感 `.env` 未提交。
- 远端门禁：代码候选 `9bda886` 的 [CI 33425459672](https://github.com/nekohands/InkFlow/actions/runs/33425459672)、[Docker 33425459913](https://github.com/nekohands/InkFlow/actions/runs/33425459913)、[Security 33425459745](https://github.com/nekohands/InkFlow/actions/runs/33425459745) 均 GREEN 且 head SHA 一致。
- 交接边界：本工作包自动化契约已闭合，但整体仍为 `1.0 Release Candidate`，不标记 `Accepted/Completed`。阅读 3.0/MuMu、真实 linovelib/17K/追更/第二来源、真实账户/Provider、PWA 安装跨设备、受保护页面人工操作和生产 OTLP/SLO/告警/备份治理仍在第 6 节待定；本轮不启动真机或第三方 live 测试。

### 5.18 CollectionRun 非法输入状态映射修复与最终 VM/Compose 回归交接（本轮，2026-09-01）

- 工作包：将 CollectionRun 启动输入按契约区分为 `400`（空/格式非法 URL）和 `422`（来源地址无法解析或语义不受支持），保留新建 `202`、复用活跃运行 `200` 及既有控制、进度、ZIP/EPUB/TXT 行为。
- TDD 与实现：先以 `source-url.invalid` 回归建立红态，再接入 `GetStartStatusCode`；定向 `CollectionRunEndpointTests` `3/3 PASS`，采集/打包 smoke、脚本语法、`git diff --check` PASS。
- 本机/VM 证据：本机 Restore/Release Build（0 warnings / 0 errors）、Unit `535/535`、Architecture `1/1`、Contract `10/10` PASS；本机 Integration 受 Windows Docker Engine `npipe://./pipe/docker_engine` 限制为 8 passed / 3 skipped / 95 BLOCKED。Ubuntu VM 候选 `c85975f` 源码构建 Compose 完成 Unit `535/535`、Architecture `1/1`、Contract `10/10`、Integration `103 passed / 3 skipped / 0 failed`，11 个 migration contexts PASS，四业务镜像、Migration/packages-init、PostgreSQL/Redis/OTel/API/Worker/Scheduler 健康，采集/打包 smoke 覆盖空 URL `400`、解析失败 `422`、四类控制、ZIP/EPUB/TXT、完整性和审计。
- 远端门禁：`bf4b09f` 的 [CI 33436420368](https://github.com/nekohands/InkFlow/actions/runs/33436420368)、[Docker 33436420254](https://github.com/nekohands/InkFlow/actions/runs/33436420254)、[Security 33436420383](https://github.com/nekohands/InkFlow/actions/runs/33436420383) 均 GREEN。由于官方 Collector `0.159.0` 扫描命中 `CVE-2026-56854`，仅核心 Collector scan 使用到期 2026-09-30 的 `.trivyignore-collector` VEX 例外；应用镜像/文件系统扫描仍严格执行，修复镜像发布后须移除例外并复验。
- 当前状态：采集/打包自动化契约已闭合，但整体保持 `1.0 Release Candidate`，不等同于 `Accepted/Completed`。阅读 3.0/MuMu、真实 Official Source/追更/第二来源、真实凭据/Provider、账户/PWA 跨设备、受保护页面人工操作和生产 OTLP/SLO/告警/备份治理继续按第 6 节待定；本轮不启动 ADB、阅读 App 或 live source。

### 5.19 ReadingProgress 领域状态变更回归覆盖交接（本轮，2026-09-01）

- 工作包：为 `ReadingProgress.Update` 增加直接领域回归，覆盖合法换章/段落/百分比/时间戳更新，以及非法输入失败后不部分修改既有状态。
- 验证：本机定向 `ReadingStateTests` `7/7 PASS`、完整 Unit `537/537 PASS`、Release Build 0 warnings / 0 errors、Architecture `1/1`、Contract `10/10`、`git diff --check` PASS；未改变 API、Migration、运行时和用户延期边界。
- 远端门禁：测试候选 `3ac8110` 的 [CI 33439541455](https://github.com/nekohands/InkFlow/actions/runs/33439541455)、[Docker 33439541466](https://github.com/nekohands/InkFlow/actions/runs/33439541466)、[Security 33439541469](https://github.com/nekohands/InkFlow/actions/runs/33439541469) 均 GREEN 且 head SHA 一致。
- 当前状态：本轮关闭阅读状态领域测试盲区；整体仍为 `1.0 Release Candidate`，不等同于 `Accepted/Completed`。阅读 3.0/MuMu、真实来源/凭据、PWA 安装跨设备、人工视觉/长时间阅读和生产治理继续按第 6 节待定。

### 5.20 最新 HEAD Ubuntu VM SDK 复验与 Compose 网络阻塞交接（本轮，2026-09-01）

- 范围：对最新 `dev` HEAD `5673dfc` 做 Ubuntu VM 自动化复验；`3ac8110` 之后只有文档提交，因此没有新增产品行为。按用户决定未启动 ADB、MuMu/阅读 3.0 或第三方 live source。
- SDK：隔离 worktree 中以 Linux .NET 10 SDK 完成 Restore、工具恢复、Release Build（0 warnings / 0 errors）、11 个 migration context 校验；Unit `537/537`，Integration `106`（`103 passed / 3 skipped / 0 failed`）。
- Compose：遵循源码构建优先，API/Worker/Scheduler/OTel 构建期间 Worker/Migrations 已完成；API/Scheduler restore 多次遇到 `api.nuget.org` 包下载 60 秒无数据超时。等待约 16 分钟后按环境阻塞中止，未进入 health 或业务 smoke 阶段，故不把本轮记为 Runtime PASS。
- 清理：诊断脚本以 status `130` 退出后清理钩子成功移除隔离 Compose、容器、临时目录和 worktree；VM 原工作树的 5 项用户改动仍在，敏感 `.env` 未提交。
- 远端：文档 HEAD `5673dfc` 的 [CI 33440663763](https://github.com/nekohands/InkFlow/actions/runs/33440663763)、[Docker 33440663778](https://github.com/nekohands/InkFlow/actions/runs/33440663778)、[Security 33440663728](https://github.com/nekohands/InkFlow/actions/runs/33440663728) 均 GREEN 且 SHA 一致。
- 结论：最新 HEAD 的 SDK/测试证据通过，Compose 运行态复验被 VM 到 NuGet 的外部网络可达性阻塞；5.18/5.19 中对应代码候选的源码 Compose/业务 smoke 证据保持有效。整体仍为 `1.0 Release Candidate`，不标记 `Accepted/Completed`；人工/真实来源、PWA 跨设备、生产治理及阅读 3.0/MuMu 继续按第 6 节待定。

### 5.21 源码 Dockerfile NuGet 缓存与 VM Compose 复验交接（本轮，2026-09-01）

- 工作包：为四个业务 Dockerfile 的 NuGet restore 增加 BuildKit cache mount，改善“源码构建优先”在网络短暂抖动时的可重复性；不改变产品行为、依赖版本或最终镜像内容。
- 实现：缓存 `/root/.nuget/packages` 和 NuGet HTTP cache，使用 `sharing=locked` 支持并行服务构建；缓存仅由 BuildKit 构建器持有，不进入最终镜像。
- VM/Runtime：候选 `26e5d82` 的隔离 worktree 源码 Compose 四镜像构建成功；Migration/packages-init 正常退出，PostgreSQL/Redis/OTel/API/Worker/Scheduler 健康，API/Worker/Scheduler `/health` 均 `healthy`。验证后隔离资源清理完成，VM 原工作树用户改动保留。
- 回归与远端：本机 `git diff --check` PASS；本机没有 Docker CLI，故未伪造本机 Compose 结果。`26e5d82` 的 [CI 33447522462](https://github.com/nekohands/InkFlow/actions/runs/33447522462)、[Docker 33447522530](https://github.com/nekohands/InkFlow/actions/runs/33447522530)、[Security 33447522397](https://github.com/nekohands/InkFlow/actions/runs/33447522397) 均 GREEN，CI 全量测试、Compose、前端/业务 Runtime、SLO、Redis、备份恢复和诊断均通过。
- 结论：源码构建 NuGet cache reliability gap 已关闭，最新 VM Runtime 健康证据恢复；整体仍为 `1.0 Release Candidate`，不等同于 `Accepted/Completed`。阅读 3.0/MuMu、真实来源/追更/第二来源、真实凭据/PWA 跨设备、人工验收和生产治理继续按第 6 节待定。

### 5.22 Acceptance fixture NuGet 缓存与重复运行复验交接（本轮，2026-09-01）

- 工作包：为源码 Compose acceptance profile 的 SDK fixture 增加两个有界命名 NuGet 缓存卷，避免每次 `dotnet run` 都从临时 `/tmp` 重新下载；不改变生产服务、最终镜像或数据库事实卷。
- VM/Runtime：候选 `e7f4414` 的隔离源码 Compose 健康启动后，独立非交互 `run -T ensure-reader-catalog` 连续两次均返回 `0` 和同一 fixture；第二次未重新创建 acceptance NuGet 卷，卷 label 与项目/卷名一致。
- 清理：发现 Compose `down --volumes` 对未参与 `up` 的 acceptance profile 卷不会自动回收后，已按完整名称显式删除两个缓存卷，并移除临时 worktree；VM 原工作树的 5 项用户改动保留。
- 远端：`e7f4414` 的 [CI 33449460834](https://github.com/nekohands/InkFlow/actions/runs/33449460834)、[Docker 33449460843](https://github.com/nekohands/InkFlow/actions/runs/33449460843)、[Security 33449460854](https://github.com/nekohands/InkFlow/actions/runs/33449460854) 均 GREEN。
- 结论：Acceptance fixture 重复 NuGet 下载稳定性缺口已关闭；本轮按决定不启动 ADB、阅读 3.0 或第三方 live source，整体仍为 `1.0 Release Candidate`，不标记 `Accepted/Completed`。

### 5.23 书籍打包租约丢失后的已发布文件清理回归覆盖交接（本轮，2026-09-01）

- 缺口：`BookPackageService.ProcessAsync` 在 artifact 已发布而最终租约保存被拒绝时需要清理临时文件和已发布文件；原有测试未覆盖这个发布后租约丢失边界。
- TDD 与实现：新增 `Process_Removes_Published_Artifact_When_Lease_Is_Lost_Before_Completion`，让测试替身在最终租约保存时拒绝并验证 Builder 执行、三次保存调用以及临时/最终 EPUB artifact 均不存在；未改变生产代码、API、Migration 或控制语义。
- 本机：Release Build 0 warnings / 0 errors；Unit `538/538`、Architecture `1/1`、Contract `10/10`、`git diff --check` 均 PASS。
- 远端：候选 `e96bd2f` 的 [CI 33451781181](https://github.com/nekohands/InkFlow/actions/runs/33451781181)、[Docker 33451781556](https://github.com/nekohands/InkFlow/actions/runs/33451781556)、[Security 33451781201](https://github.com/nekohands/InkFlow/actions/runs/33451781201) 均 GREEN；CI 全量 Test、Compose、Runtime、SLO、Redis、备份恢复和诊断步骤通过。
- 边界：本轮不启动 ADB、阅读 3.0、真实来源或真实凭据验收；整体保持 `1.0 Release Candidate`，不标记 `Accepted/Completed`。

### 5.24 书籍包下载缺失 artifact 根目录的错误映射修复交接（本轮，2026-09-01）

- 工作包：包文件已从数据库记录中标记完成、但文件根目录或挂载点暂时缺失时，下载服务应与“文件不存在”保持一致，返回 artifact not found，而不是把 Linux `DirectoryNotFoundException` 传播为 500。
- TDD 与实现：新增 `OpenCompleted_Returns_Null_When_Artifact_Root_Is_Missing`，先复现缺失目录异常，再在 `BookPackageService.OpenCompletedAsync` 增加仅匹配 `FileNotFoundException`/`DirectoryNotFoundException` 的异常过滤；权限错误、其他 I/O 错误和取消不被吞掉。
- 本机证据：Restore PASS；Release Build 0 warnings / 0 errors；定向书籍打包测试 `5/5`、Unit `539/539`、Architecture `1/1`、Contract `10/10`、`git diff --check` PASS。
- 远端门禁：`5157924` 的 [CI 33454092316](https://github.com/nekohands/InkFlow/actions/runs/33454092316)、[Docker 33454092239](https://github.com/nekohands/InkFlow/actions/runs/33454092239)、[Security 33454092192](https://github.com/nekohands/InkFlow/actions/runs/33454092192) 均 GREEN；CI 完成全量测试、源码 Compose、前端/业务 Runtime、SLO、Redis、备份恢复和诊断步骤。
- 当前状态：本工作包的下载错误映射已通过自动化门禁；整体继续保持 `1.0 Release Candidate`，不等同于 `Accepted/Completed`。阅读 3.0/MuMu、真实来源/追更/第二来源、真实凭据/Provider、账户/PWA 跨设备、受保护页面人工操作和生产治理仍按第 6 节待定；本轮不启动 ADB、阅读 App 或 live source。

### 5.25 采集运行成功进度与失败/取消计数语义修复交接（本轮，2026-09-01）

- 工作包：采集运行视图的 `progressPercent` 只表示成功完成比例，失败和取消任务由独立计数与终态表达，不再被计入成功进度。
- TDD 与实现：新增 `Progress_Percent_Does_Not_Count_Failed_Or_Cancelled_Tasks_As_Completed`，旧实现红态为预期 `25`、实际 `100`；修复后只计算 `CompletedTaskCount / TotalTaskCount`。
- 本机证据：Restore PASS；Release Build `0 warnings / 0 errors`；Unit `540/540`、Architecture `1/1`、Contract `10/10`、定向采集端点测试 `4/4`、`git diff --check` PASS。Windows 本机 Integration 因 Docker Engine npipe 不可用，`106` 项中 `8` 通过、`3` 跳过、`95` BLOCKED。
- VM 证据：Ubuntu 隔离 worktree 源码构建 Compose 健康；Linux SDK Unit `540/540`、Architecture `1/1`、Contract `10/10`、Integration `103 passed / 3 skipped / 0 failed`；11 个 migration contexts PASS；Legado/Admin/Operations/collection-package smoke PASS，collection-package 覆盖直接地址、暂停/恢复/停止/取消幂等、ZIP/EPUB/TXT、完整性和审计。验证后已清理隔离 worktree、Compose 与资源，未触碰 VM 原工作树改动。
- 当前状态：`bc119e5` 的三条远端门禁均 GREEN；本工作包已完成自动化验证，但整体仍为 `1.0 Release Candidate`。阅读 3.0/MuMu/ADB、真实来源/追更/第二来源、真实凭据/Provider、账户/PWA 跨设备、受保护页面人工操作和生产治理仍按第 6 节待定；本轮不启动阅读 App 或 live source。

### 5.26 GPT 内置浏览器 VM 本地转发真实交互复验交接（本轮，2026-09-01）

- 工作包：补强 1.0 前端自动化证据；在不启动 MuMu/阅读 3.0、不使用真实账户凭据、不触发第三方 live source 的前提下，使用 `bc119e5` 隔离 worktree 和源码构建 Compose。
- VM：Migration/packages-init 正常退出，PostgreSQL、Redis、OTel Collector、API、Worker、Scheduler 健康；通过 SSH 本地转发访问 API，未覆盖 VM 原工作树，验证后已清理 Compose、网络、卷、转发和临时 worktree。
- 浏览器：实际操作 `/reader` 搜索空结果、`/reader/account` 空表单校验、未登录书架/历史保护提示、离线回退页和匿名 Operations 页面；未输入或提交密码、Token、真实账户或其他敏感数据。`375×812`、`1440×900` 两个视口均无横向溢出，搜索区/导航可见。
- Runtime：`reader-frontend-runtime-smoke.sh` 返回 `PASS (Reader/PWA/Operations frontend contracts)`；浏览器临时页与视口覆盖已恢复默认状态。该证据增强自动化页面交互覆盖，不替代人工视觉、PWA 安装/跨设备、真实账户、阅读 3.0、真实来源或生产验收。
- 远端门禁：文档提交 `198dd61` 的 [CI 33459414177](https://github.com/nekohands/InkFlow/actions/runs/33459414177)、[Docker 33459414175](https://github.com/nekohands/InkFlow/actions/runs/33459414175) 和 [Security 33459414095](https://github.com/nekohands/InkFlow/actions/runs/33459414095) 均 GREEN 且 head SHA 一致；CI 全量测试、源码 Compose、前端/业务 Runtime、SLO、Redis、备份恢复和诊断步骤通过。
- 当前状态：本轮无产品代码变更；整体继续为 `1.0 Release Candidate`，不标记 `Accepted/Completed`。VM 原工作树原有用户改动保持不变。

### 5.27 GPT 内置浏览器匿名夹具书目实际阅读链路复验交接（本轮，2026-09-01）

- 工作包：在不启动 ADB、MuMu/阅读 3.0、不输入真实凭据且不访问第三方 live source 的前提下，补强 Web Reader 有数据路径的自动化证据；使用 `2162ac1` 独立 worktree 和 `docker-compose.build.yml` 源码构建。
- VM：Migration/packages-init 退出码为 0，API、Worker、Scheduler、PostgreSQL、Redis 健康；`ensure-reader-catalog` 返回稳定书目/章节 ID `3a9c9f4b-4975-4b64-949a-63c56bc5df19` / `20503455-be9e-4aa9-aaab-2e057b14757b`。`reader-frontend-runtime-smoke.sh` 与 `reader-content-runtime-smoke.sh` 均 PASS。
- 浏览器：经临时 SSH 转发，GPT 内置浏览器实际完成 `/reader` 搜索 `InkFlow Runtime Acceptance Fixture`、点击书目结果、打开详情目录、点击“开始阅读”进入正文；读取章节标题、已发布 Canonical Content、进度条 `aria-valuenow=100`，并打开阅读设置检查主题/字号/行高控件。未读取 Cookie/Storage/密码材料，未输入或提交账户凭据，未使用真实账户。
- 清理与边界：验证后浏览器临时页、转发、隔离 Compose 容器/网络/卷和 worktree 均清理；VM 原工作树原有用户改动保持不变。该证据不替代人工视觉/触控、真实账户/PWA 安装与跨设备、真实来源、生产环境或 MuMu/阅读 3.0 验收；整体仍为 `1.0 Release Candidate`，不标记 `Accepted/Completed`。

### 5.28 Web Reader 上一章/下一章自动化验收闭环交接（本轮，2026-09-01）

- 工作包：针对 5.27 单章夹具无法证明连续阅读的问题，扩展 `ensure-reader-catalog` 为同一本书的两章已发布 Canonical Content，并新增 `scripts/reader-navigation-runtime-smoke.sh` 及 fixture 回归；CI 已接入脚本回归和 Runtime 步骤。
- 本机证据：脚本 `bash -n`、fixture/Reader smoke、Release Build（0 warnings / 0 errors）通过；Unit/Architecture/Contract 通过。Windows Docker Engine 的 `npipe://./pipe/docker_engine` 不可用，Windows 全量 IntegrationTests 仍 BLOCKED，不作为本机集成证据。
- Ubuntu VM 证据：候选 `9a0b7df` 以 `docker-compose.build.yml` 源码构建并健康启动；Linux SDK 完整测试为 Unit 540/540、Architecture 1/1、Contract 10/10、Integration 103 passed / 3 skipped / 0 failed。前端、已发布正文和导航 smoke 均 PASS；GPT 内置浏览器经临时 SSH 转发实际完成搜索→详情→目录→首章→下一章→上一章，确认正文、进度 100、首章无上一章、末章无下一章。验证后隔离 Compose/卷、转发和 worktree 已清理，VM 原工作树用户改动保持不变。
- 远端门禁：候选 `9a0b7df` 的 [CI 33464240828](https://github.com/nekohands/InkFlow/actions/runs/33464240828)、[Docker 33464240871](https://github.com/nekohands/InkFlow/actions/runs/33464240871)、[Security 33464240909](https://github.com/nekohands/InkFlow/actions/runs/33464240909) 均 GREEN 且 head SHA 一致。
- 边界：本轮不启动 ADB、MuMu/阅读 3.0，不输入真实账户/凭据，不触发第三方 live source；人工视觉/触控、长时间阅读、PWA 安装/跨设备、真实来源和生产治理仍列在待定事项，整体保持 `1.0 Release Candidate`，不标记 `Accepted/Completed`。

### 5.29 1.0 非延期范围缺口审计交接（本轮，2026-09-01）

- 审计：重新核对路线图、Phase 1 验收、架构不变量、前端规范和待定清单，并用 CodeGraph 检查采集控制/直接地址/书籍打包、Reader/PWA、Operations、Content Policy、Source Authorization、CredentialReference、Admin Audit 及三类 Official Source 的代码入口和自动化证据。
- 结论：未发现新的、未实现且不属于延期范围的 1.0 功能缺口。上述非延期能力已有 Unit/Contract/Runtime smoke、确定性夹具或 VM 源码 Compose 证据；Reader 连续阅读的最新真实交互证据见 5.28。
- 当前回归：本机 Release Build、Unit `540/540`、Architecture `1/1`、Contract `10/10` 和相关脚本语法/diff 检查通过；Windows Docker Engine named pipe 不可用，完整 Testcontainers 仍 BLOCKED。`9a0b7df` 的 Ubuntu VM 全量测试、源码 Compose、业务 smoke、内置浏览器交互和远端三类 GREEN 门禁继续有效。
- 待定边界：不启动 ADB/MuMu/阅读 3.0，不输入账户密码，不访问第三方 live source；真实来源/追更/切源、真实账户/PWA 安装跨设备、真实 Provider/生产凭据、受保护页面登录后的浏览器输入和生产 OTLP/SLO/告警/备份治理继续按第 6 节处理。整体仍为 `1.0 Release Candidate`，不标记 `Accepted/Completed`。

### 5.30 Web Reader 长元数据边界自动化收口交接（本轮，2026-09-01）

- 工作包：补齐 Reader 详情页最大长度标题/作者、特殊字符和无封面书目的自动化边界；详情/书目卡片/目录/面包屑文本增加可断词样式，网格卡片和链接增加 `min-width: 0`，并新增 `ReaderHtml` 回归、`ensure-reader-edge-catalog` fixture、runtime smoke 与 CI 脚本回归。
- 本机：AcceptanceFixtures Release Build 0 warnings / 0 errors；ReaderHtml `22/22`；新增脚本语法/fixture 回归和 `git diff --check` 通过。
- Ubuntu VM：候选 `5dc59ab` 隔离 worktree 使用 `docker-compose.build.yml` 源码构建；Linux SDK Integration `103 passed / 3 skipped / 0 failed`，Build 0 warnings / 0 errors，11 个 migration context 无漂移；Migration/packages-init、PostgreSQL/Redis/OTel/API/Worker/Scheduler 健康，前端、导航和边界 smoke 均 PASS。
- GPT 内置浏览器：经临时 SSH 转发实际打开最大长度边界详情页；当前 `1280×720` 视口下页面文档/主体宽度均为 `1265px`，无横向溢出，标题/作者/目录计算样式为 `overflow-wrap:anywhere`，无封面没有详情图片，开始阅读入口可见。未输入账户、密码或 Token，未读取 Cookie/Storage。
- 远端门禁：文档同步提交 `067b21d` 的 [CI 33471120031](https://github.com/nekohands/InkFlow/actions/runs/33471120031)、[Docker 33471120041](https://github.com/nekohands/InkFlow/actions/runs/33471120041)、[Security 33471120007](https://github.com/nekohands/InkFlow/actions/runs/33471120007) 均 GREEN 且 head SHA 一致；CI 已包含新增 Reader edge metadata 脚本回归和源码 Compose Runtime smoke。
- 清理与边界：隔离 Compose、网络、转发和 worktree 已清理，VM 原工作树改动保留；本轮不启动 ADB、MuMu/阅读 3.0，不触发第三方 live source。375×812 等移动端人工视觉/触控、长时间阅读、真实账户/PWA 安装跨设备、真实来源/生产环境仍待定，整体保持 `1.0 Release Candidate`，不标记 `Accepted/Completed`。

### 5.31 Operations Center 内容政策管理 UI 与权限自动化闭环交接（本轮，2026-09-01）

- 工作包：运维中心新增 Administrator-only Content Policy 区块，支持下架/恢复、当前活动下架列表、理由确认和审计；Operator 继续可查看运维中心，但内容政策输入和按钮禁用。
- 代码：候选 `5bdb4ea` 已推送到 `dev`。UI 通过受保护 takedown 列表接口读取状态，动作复用已有 append-only 审计确认壳；新增 ReaderHtml、前端 runtime smoke 和 fixture 回归，无新数据模型或 migration。
- 本机：Release Build 0 warnings / 0 errors，Unit `541/541`、Architecture `1/1`、Contract `10/10`、前端 smoke PASS；Windows Docker Engine named pipe 不可用，本机整套 Testcontainers BLOCKED。
- Ubuntu VM：源码 Compose 启动健康；Linux SDK 全量测试为 Unit `541/541`、Architecture `1/1`、Contract `10/10`、Integration `103 passed / 3 skipped / 0 failed`；11 个 migration contexts 无漂移，`admin-runtime-smoke` PASS。
- GPT 内置浏览器：临时管理员完成下架→隐藏→恢复→可见；新签发 Operator 会话显示“运营”，内容政策表单和按钮 disabled。临时账号、Compose 资源、卷、SSH 转发及隔离 worktree 已清理，VM 原工作树用户改动保留。
- 远端门禁：文档提交 `7e7f242` 的 [CI 33477390879](https://github.com/nekohands/InkFlow/actions/runs/33477390879)、[Docker 33477390849](https://github.com/nekohands/InkFlow/actions/runs/33477390849)、[Security 33477390880](https://github.com/nekohands/InkFlow/actions/runs/33477390880) 均 GREEN 且 head SHA 一致。
- 后续：真实凭据/人工视觉、Reading 3.0/MuMu/ADB、PWA 安装跨设备、真实来源/生产治理仍不在本轮执行范围，整体保持 `1.0 Release Candidate`。

### 5.32 CollectionRun 取消终态领域回归补强交接（本轮，2026-09-01）

- 工作包：补齐采集运行取消语义的领域回归证据；覆盖运行中取消、重复取消幂等、取消后不可恢复/继续调度。
- 实现：`tests/InkFlow.UnitTests/CollectionRunTests.cs` 新增 `Cancelled_Run_Is_Terminal_And_Idempotent`，无生产代码、API 或 Migration 变更；候选 `3aab3e8` 已推送到 `dev`。
- 本机：Release Build 0 warnings / 0 errors，Unit `542/542`、Architecture `1/1`、Contract `10/10`，采集定向测试 `9/9`，`git diff --check` PASS。
- 运行边界：因无业务行为变更，本轮不重复执行 VM Compose、浏览器或真实来源；5.31 的 VM/Smoke/浏览器证据仍有效。本机 Windows Docker Engine named pipe 不可用，Testcontainers 仍 BLOCKED。
- 后续：Reading 3.0/MuMu/ADB、真实来源/账户、PWA 跨设备、人工视觉和生产治理继续按第 6 节待定，整体保持 `1.0 Release Candidate`。

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
- Scheduler/Worker 使用真实更新数据的追更验证；4.87 已自动化当前 Kanunu8 快照的扫描、消费、去重和发布，5.10 又补齐确定性新增章节/增量发布回归，但真实上游新增章节事件仍待定。
- 第二个真实 Official Source 与真实故障切源演练；当前只有确定性双来源夹具和 17K 离线 CodeAdapter 证据，不能替代真实来源验收。
- linovelib 已完成 Search 规则的离线定义与回归，真实网络验证仍受 DNS 污染影响，待可用环境复验。
- 本机 Docker 缺失导致 PostgreSQL Testcontainers 集成测试待本机可用容器环境复验；本轮一致性检查新增用例已在远端 CI PostgreSQL 容器中通过。

Phase 2 及以后：

- Source Health 的半开恢复、主动巡检探针与冷却参数配置化已完成；Crawler 死信受控重放、受保护 Repair/replay 入口、跨模块 Consistency Check v1、Operations Center Read Model v1 和 Center UI v1 自动化基线已完成，自动修复和更强运维治理仍待实现。
- Crawler 失败结构化日志与 OpenTelemetry counters、请求审计持久化、独立 `AuditRead` 有界查询、CI 级 PostgreSQL 备份恢复演练、告警快照/阈值/内部历史去重与恢复、来源级授权 v1 和已落地高风险命令审计基线已完成；审计有界 retention 代码基线已完成，但生产法律/合同保留、归档、删除授权和证据治理仍待部署环境确定。外部告警路由、生产异地备份/RPO-RTO、安全扫描治理、组织/更广泛资源权限仍待实现。限流已接入 Redis 原子分布式计数，并在 Redis 故障时保留同配额本地有界降级。
- 用户身份基础、Reading State v1、Reader/PWA 用户状态 v1（账户/书架/历史/进度/偏好接入、公开安装壳）、Personal Legado Token v1、Web Reader v1、Private Library 私有正文/TXT/EPUB 导入导出自动化基础和 Developer API / Entitlement / Billing v1 候选基线已完成；PWA Service Worker/离线壳已由 4.82 自动验收，真实安装、账户/跨设备验收、Private Library 与 Developer API 真实账户/凭据验收、Organization、Community Marketplace 仍未完成。

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
- [x] Reader/PWA 用户状态 v1 的账户/书架/历史/进度/偏好渐进增强、公开 PWA 壳与 CI Runtime smoke 已完成；Service Worker/壳缓存/离线回退已由 4.82 在 localhost 安全上下文自动验收。
- [x] Reader/PWA 账户与阅读状态 API 的非阅读 App runtime smoke 已由 4.84 在 Ubuntu VM 源码构建 Compose 中完成；PWA 页面内真实凭据输入仍待人工或真实环境。
- [x] Reader/PWA 页面临时账户的 GPT 内置浏览器自动化已完成：注册/刷新会话、Catalog fixture 加入书架、书架列表、章节未发布空状态、登出和匿名保护提示均通过；4.86 追加已发布章节正文页面验证；临时账户已禁用。
- [ ] Reader/PWA 真实账户、安装/独立窗口、生产 HTTPS、跨设备同步和长期体验仍待人工执行；按用户决定不执行阅读 3.0。
- [x] 已阅读并按 `phase-1-acceptance.md` 建立 Phase 1B 双来源自动化基线。
- [x] Capability Health v1 与确定性健康感知故障切源已建立自动化基线。
- [ ] 第二个真实 Official Source / 真实故障切源尚未验收。
- [x] 当前租约恢复与跨进程原子领取候选改动已完成 Docker/CI 验证；真实设备、真实来源和本机 Docker 集成复验仍未完成。
- [x] Source DSL v1 已定义可测试的最小 schema/AST，并已接入受控 XPath/JSONPath 执行子集、next-link Pagination、page-number/cursor Pagination、受控 response-cookie Session、有界请求模板变量、任务级 CredentialReference typed 初始认证、有界响应派生变量、来源级默认 CredentialReference 回退、Administrator-only 默认绑定管理 API 和 Owner Scope 解析契约；secret 材料 Owner/Admin 管理、真实 SecretProvider、持久会话及三种受控分页之外的多请求/递归预算仍待后续工作包。
- [x] Fixture 驱动，无真实第三方 Source PR-CI 依赖。
- [x] 新 Source 网络能力必须同步安全测试。
