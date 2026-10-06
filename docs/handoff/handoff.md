# InkFlow 工程交接文档

> 用于开发者、AI Agent 或未来会话快速、安全接手 InkFlow。真实状态以仓库与 CI 为准。

- 产品：墨流 / InkFlow
- 当前阶段：1.0 Release Candidate（本轮 Reader 顶部采集/下载/来源状态入口、书籍详情下载入口及来源只读权限已完成本机/VM/浏览器自动化验收，CI/Docker/Security 已通过；真实来源与外部验收待定）
- 当前工作分支：`dev`（2026-08-25 起）
- 文档状态：5.57 死信与任务状态同事务已实现并通过全部 Gate（`8a8fcd1` 三 workflow GREEN）；历史交接明细见 `handoff-history.md`。
- `dev` 骨架 root commit：`c5f2048`
- 交接日期：2026-10-06；dev 骨架重建更新：2026-08-25

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
- 历史实现不迁移到 `dev`；已完成工作包的设计记录以 `../roadmap/progress-history.md` 第 4.1 节为准，落地时在 `dev` 上重新编写。

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


> 4.x–5.52 历史交接明细已归档至 [handoff-history.md](handoff-history.md)；待定事项与最新交接如下。
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


### 5.53 Reader 顶部任务导航、来源状态与书籍详情下载入口交接（本轮，2026-09-04）

- 代码：`ReaderHtml`/Reader Operations 顶部统一提供采集、下载、来源状态页签；书籍详情支持 EPUB、单文件 TXT、ZIP，完成包使用 Bearer 会话下载，活动包跳转下载页，其他情况创建任务。
- 权限：Reader 通过 `OperationsSnapshotRead` 只读来源状态；来源编辑仍受 `SourceOperations` 保护，Administrator 可操作，既有 Operator 来源级授权不变。
- 证据：本机 Release Build、Unit `574/574`、Architecture `1/1`、Contract `12/12`、ReaderHtml `27/27`、前端 smoke 和 shell 语法 PASS；Windows Integration 因 Docker Engine named pipe 不可用 BLOCKED。Ubuntu VM 隔离源码 Compose build/health、Reader/Admin HTTP 角色矩阵和内置浏览器三项顶部入口检查 PASS。
- 清理：隔离 Compose 项目、验证卷/镜像和临时工作树已清理；远端既有手工栈未重启或修改。未使用真实上游账号、生产令牌、Cookie、阅读 3.0/MuMu 真机。
- 门禁：代码提交 `9233b82`、文档提交 `94ce713` 已推送 `dev`；[CI 33782382242](https://github.com/nekohands/InkFlow/actions/runs/33782382242)、[Docker 33782382191](https://github.com/nekohands/InkFlow/actions/runs/33782382191)、[Security 33782382185](https://github.com/nekohands/InkFlow/actions/runs/33782382185) 均 success 且 head SHA 为 `94ce713`。仍保持 `1.0 Release Candidate`，不得标记 `Accepted/Completed`。

### 5.54 Identity 令牌重放检测、族吊销与会话保留清理交接（本轮，2026-10-01）

- 代码：`IdentityService` 检测已轮换 refresh token 的重放（含竞态分支）并经 `RevokeSessionFamilyAsync` 事务内吊销整条轮换链；访问令牌验证改为联查所属会话；`RotateRefreshSessionAsync` 轮换即吊销旧访问令牌行；Worker 新增 `IdentityRetentionBackgroundService` 接线 `IdentityRetentionService`/`EfIdentityRetentionStore`（SKIP LOCKED + 子表优先 + NOT EXISTS 级联护栏）；新增 `AddIdentityRotatedTokenBackfill` 数据回填 Migration（无模型变更）；文档拆分为当前记录 + `*-history.md` 归档。
- 安全边界：审计事件只含 userId 与吊销会话数，不含 token/摘要/会话秘密；重放响应使用独立错误码 `refresh_token_replay_detected`（401），凭证泄露可被运维侧识别。
- 证据：本机 Restore、Release Build 0 warnings / 0 errors、Unit 583/583、Architecture 1/1、Contract 12/12、`verify-migrations.sh`（11 contexts）PASS；Windows 本机无 Docker，本机 Integration NOT RUN（BLOCKED），真实 PostgreSQL 用例由远端 CI 执行并全绿。未使用真实账户、生产令牌或生产数据库。
- CI 迭代：候选 `a661293` CI RED → 修复保留清扫缺口（候选会话外终态令牌，`3c67dba`）；再 RED → `reader-account-runtime-smoke` 跟随重放族吊销语义（断言错误码 + 族失效 + 重新登录）；Docker 门禁 RED → 四镜像显式升级 `libssl3t64`（CVE-2026-84782）。最终 `beb3e56` 三 workflow GREEN：[CI 36891052979](https://github.com/nekohands/InkFlow/actions/runs/36891052979)、[Docker 36891052985](https://github.com/nekohands/InkFlow/actions/runs/36891052985)、[Security 36891052989](https://github.com/nekohands/InkFlow/actions/runs/36891052989)。
- 下一步：API 宿主全局异常处理中间件（审查项 H1）为下一个已识别工作包；生产 Migration 仍由独立 Migrations 流程执行，本轮回填 Migration 需在部署时一并评审。

### 5.55 API 宿主全局异常处理中间件交接（本轮，2026-10-02）

- 代码：`src/Apps/InkFlow.Api/ApiErrorHandling.cs` 新增 `AddInkFlowProblemDetails`（剥除 `exceptionDetails`）与 `UseInkFlowExceptionHandler`；`Program.cs` 将其注册为最外层中间件。任何环境未捕获异常统一返回 `application/problem+json`，不含异常类型/消息/堆栈/路径；审计与 SLO 中间件在内层仍记录原始异常；既有端点错误体与 4xx 不重写。
- 测试：`ApiErrorHandlingTests`（TestServer，CPM 新增 `Microsoft.AspNetCore.TestHost` 10.0.4）覆盖 Development 注入异常零泄露与正常响应/404 不重写；本机 Unit 585/585、Architecture 1/1、Contract 12/12、Release Build 0 warnings / 0 errors。
- 交付结构：同日完成 project-delivery adoption（`repowiki/` + `docs/delivery/` profile/索引/adoption 记录，提交 `507e473` 三 workflow GREEN）。
- 门禁：代码提交 `d2cbbca` 的 [CI 37427977446](https://github.com/nekohands/InkFlow/actions/runs/37427977446)、[Docker 37427977438](https://github.com/nekohands/InkFlow/actions/runs/37427977438)、[Security 37427977441](https://github.com/nekohands/InkFlow/actions/runs/37427977441) 均 success（含真实 PostgreSQL Integration 与 Runtime smoke）。2026-09-11 审查三个高危项全部关闭（H2→5.54，H1→5.55，H3→5.52/ADR 0028 既定边界）。保持 `1.0 Release Candidate`。
- 下一步候选：审查中危项（Inbox/Outbox 租约心跳续约、乐观并发令牌、Canonical 匹配 check-then-act 原子化、Catalog 查询分页/N+1）；下一个工作包需先定义 intake（目标/范围/验收）。

### 5.56 Canonical 匹配入口原子化交接（本轮，2026-10-06）

- 代码：`ICanonicalBookRepository.BeginTitleAuthorScopeAsync`（默认无互斥回退 + EF 覆写：事务内 `pg_advisory_xact_lock`，键为归一化 title/author 的 SHA-256 稳定前缀）；`CanonicalBookMatchingService` 重构为互斥临界区 + 锁内候选双检，书与候选原子提交。消除并发匹配同一书身份产生重复正典身份的缺口（BookId 稳定不变量的并发面）。
- 测试：`CanonicalMatchConcurrencyTests`（真实 PostgreSQL，8 路并发 × 两个空白变体来源书）断言恰 1 个正典书、同一 BookId、2 候选、至多 1 次创建。夹具 `Migrate()` 竞争缺陷由提交 `8eb9162` 修复（类初始化迁移一次）。
- 门禁：本机 Unit 585/585、Architecture 1/1、Contract 12/12、Release Build 0 warnings / 0 errors；远端 `8eb9162` 的 [CI 37437471071](https://github.com/nekohands/InkFlow/actions/runs/37437471071)、[Docker 37437470761](https://github.com/nekohands/InkFlow/actions/runs/37437470761)、[Security 37437470720](https://github.com/nekohands/InkFlow/actions/runs/37437470720) 均 success。无 Schema/Migration 变更；`FindByTitleAuthorAsync` 全表加载（性能项）未动，单独立项。
- 下一步候选：Inbox/Outbox 租约心跳续约、乐观并发令牌、死信与任务状态同事务、Catalog 查询分页/N+1、适配器正则超时/无界读取、EntitlementService actor 校验；需先定义 intake。

### 5.57 死信与任务状态同事务交接（本轮，2026-10-06）

- 代码：`ICrawlerTaskRepository.AddDeadLetterWithTaskAsync`（默认顺序两写回退 + EF 覆写：单个 ReadCommitted 事务同时提交死信行与 DeadLettered 终态）；`CrawlerTaskProcessor.FailTaskAsync` 死信路径改用原子方法。消除崩溃/写失败留下的"有死信无终态"或"有终态无死信"半一致状态。
- 测试：`CrawlerTaskRepositoryTests` 新增两条真实 PostgreSQL 回归——双写同时可见；任务行缺失时死信整体回滚（旧两段式会留下孤儿死信行）。
- 门禁：本机 Unit 585/585、Architecture 1/1、Contract 12/12、Release Build 0 warnings / 0 errors；无 Schema/Migration 变更。远端 `8a8fcd1` 的 [CI 37469389070](https://github.com/nekohands/InkFlow/actions/runs/37469389070)、[Security 37469389126](https://github.com/nekohands/InkFlow/actions/runs/37469389126) GREEN；Docker 首跑遇 GHCR 推送瞬时 `unknown blob`（构建与 0 漏洞扫描均已成功），重跑该 job 后 GREEN。
- 下一步候选：Inbox/Outbox 租约心跳续约、乐观并发令牌、Catalog 查询分页/N+1、适配器正则超时/无界读取、EntitlementService actor 校验；需先定义 intake。

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
- 用户身份基础、Reading State v1、Reader/PWA 用户状态 v1（账户/书架/历史/进度/偏好接入、公开安装壳）、Personal Legado Token v1、Web Reader v1、Private Library 私有正文/TXT/EPUB 导入导出自动化基础和 Developer API / Entitlement / Billing v1 候选基线已完成；PWA Service Worker/离线壳已由 4.82 自动验收，真实安装、账户/跨设备验收、Private Library 与 Developer API 真实账户/凭据验收、Organization、Community Marketplace 仍未完成。Identity 令牌重放检测、族吊销与会话/令牌保留清理已接线（5.54）；API 宿主全局异常处理中间件已完成（5.55）。

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
- [x] Personal Legado Token v1 的自动化签发、Hash 持久化、header 认证、Personal API、撤销审计及“撤销即删除记录”已完成；阅读 3.0 导入、四步阅读和真机撤销后失效仍待人工执行。
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
