# InkFlow 工程交接文档

> 用于开发者、AI Agent 或未来会话快速、安全接手 InkFlow。真实状态以仓库与 CI 为准。

- 产品：墨流 / InkFlow
- 当前阶段：Phase 1B — Dual Source Validation（自动化切源基线进行中）
- 当前工作分支：`dev`（2026-08-25 起）
- `dev` 骨架 root commit：`c5f2048`
- 交接日期：2026-08-21；dev 骨架重建更新：2026-08-25

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
- `src/Modules`：Identity / Library / Sources / Crawling / Content / Reading / Search / Legado。
- Unit / Architecture / Integration / Contract 四个测试项目各含守卫用例。
- Central Package Management + 仓库级 `nuget.config`（单一 nuget.org 源）。
- Docker Compose 与 `deploy/docker/*.Dockerfile` 原样保留。
- CI 触发覆盖 `main` + `dev`。

`dev` 本地验证证据：

```text
Restore: PASS
Release Build: PASS (0 warnings / 0 errors)
Unit: PASS
Architecture: PASS
Integration: PASS
Contract: PASS
Compose validation: PASS
Runtime smoke: PASS
CI: GREEN (Run 32821162412)
```

## 4. 下一工作包

**当前状态（2026-08-28 更新）**：Phase 1A 的自动化链路与 kanunu8 真实源验证已通过；Legado 真机导入/阅读和真实追更仍待人工验收。Phase 1B 已完成确定性双来源自动化切源基线（含 Capability Health v1），但尚未宣称完成真实故障切源验收。Worker 已具备过期租约恢复、跨进程原子领取和持久化重试退避调度；Crawler 死信受控重放基线已补齐，Identity 基础认证/授权与受保护 Repair/replay 入口也已落地，公开修复中心仍待后续安全/运维工作。

本轮另完成 API 安全基线与三宿主可观测性接线：公共 API/Legado API 已有可配置单实例限流，拒绝返回 `429/Retry-After`；API 请求审计已覆盖业务 API 且不记录 query string，`CompositeAuditEventSink` 同时写入 PostgreSQL `audit.events` 与结构化日志；API、Worker、Scheduler 均接入统一 OpenTelemetry 注册入口。Identity 基础认证/授权、会话轮换和死信重放命令审计已补齐；Redis 分布式限流、查询/资源授权和更完整的权限/告警治理仍待后续工作包。

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

1. **Legado 真机验证（后续人工）**：在阅读 3.0 中导入 `/legado/book-source.json`，验证搜索/详情/目录/正文四步；本轮按用户决定不执行。
2. **追更真实验证**：Scheduler 扫描 + Worker 消费已在容器环境运行，新章检测需真实源数据佐证。
3. **Phase 1B 真实切源验收**：补充第二个真实 Official Source，验证 Source A 不可用时 Web/Legado 仍读取，且 BookId/ChapterId 不变。
4. **Content Policy 管理人工验收**：使用 Administrator 凭证验证下架/恢复、Operator/匿名拒绝、全公开读取路径隐藏/恢复和命令审计记录；本轮只完成自动化基线，未执行人工操作。
5. **继续推进 1.0**：在上述证据基础上完善第三个稳定 Official Source、完整 Repair Center、Security/Operations 与商业化能力。

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
✅ Identity 基础认证/授权 + refresh 轮换 + 受保护死信 Repair/replay（`09ea265` / `9f9d5c7`，`33102831333` / `33102831388`）
✅ 跨模块 Consistency Check v1：只读四 schema 扫描 + 受保护 Admin 入口（`7dac6ce`，CI `33106044634` / Docker `33106044677` 均 GREEN）
✅ Content Policy / Takedown v1：公开读取门控 + Administrator 命令审计 + 追加式决策历史（`34c5c71`，CI `33109068649` / Docker `33109068630` 均 GREEN）
✅ Source Health Operator Controls v1：来源能力查询 + Operator/Administrator 停用/恢复 + 命令审计（`49e0fc1`，CI `33110684551` / Docker `33110684410` 均 GREEN）
→ Legado 真机导入/阅读（后续人工）
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
- `RequestAuditMiddleware` / `IAuditEventSink`：业务 API 请求和 `429` 拒绝均记录结构化 `AuditEvent`，去除 query string；`CompositeAuditEventSink` 同时写入 PostgreSQL `audit.events` 与结构化日志，数据库触发器保证普通路径追加式写入。高风险命令的 before/after、查询授权和保留策略仍未完成。
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
- `AddAuditEvents` Migration 创建 `audit.events`、时间索引和数据库追加式触发器；更新/删除被拒绝，避免普通应用路径静默改写审计历史。
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
- [ ] **真实追更**：用真实来源数据验证 Scheduler → Worker → 目录增量 → 正文发布闭环。
- [ ] **真实第二来源故障切换**：禁用 Source A 后验证 Web/Legado 可继续读取，BookId/ChapterId 不变；恢复后不得产生重复 Canonical 身份。
- [ ] **linovelib 真实 Search/阅读链路**：网络环境可用后验证 Search → BookInfo → TOC → Content，并把该来源纳入真实第二来源/故障切换演练；本轮仅完成离线规则回归，未触网。
- [ ] **本机 Docker 集成复验**：Docker 可用后重跑完整 Testcontainers 集成测试；当前全量 43 项中 36 项因 `docker_engine` 不可用而 BLOCKED，不记为通过。

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
→ /api/legado/v1/*
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
- 第二个真实 Official Source 与真实故障切源演练；当前只有确定性双来源夹具自动化证据。
- linovelib 已完成 Search 规则的离线定义与回归，真实网络验证仍受 DNS 污染影响，待可用环境复验。
- 本机 Docker 缺失导致 PostgreSQL Testcontainers 集成测试待本机可用容器环境复验；本轮一致性检查新增用例已在远端 CI PostgreSQL 容器中通过。

Phase 2 及以后：

- Source Health 的半开恢复、主动巡检探针与冷却参数配置化已完成；Crawler 死信受控重放、受保护 Repair/replay 入口和跨模块 Consistency Check v1 已完成，完整 Repair Center/公开修复中心、自动修复和更强运维治理仍待实现。
- Crawler 失败结构化日志与 OpenTelemetry counters、请求审计持久化基线已完成；外部告警路由、阈值治理、备份恢复、安全扫描仍待实现。限流已形成单实例基线，Redis 分布式配额、认证/授权和命令级高风险审计仍待实现。
- 用户身份、书架、阅读历史、导入/导出、Developer API、Entitlement、Billing、Organization、Community Marketplace。

更后阶段：Identity product、Bookshelf、History、Local Import/Export、Developer API、Entitlement、Billing、Organization、Community Marketplace、Enterprise Deployment。

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
- [x] 已阅读并按 `phase-1-acceptance.md` 建立 Phase 1B 双来源自动化基线。
- [x] Capability Health v1 与确定性健康感知故障切源已建立自动化基线。
- [ ] 第二个真实 Official Source / 真实故障切源尚未验收。
- [x] 当前租约恢复与跨进程原子领取候选改动已完成 Docker/CI 验证；真实设备、真实来源和本机 Docker 集成复验仍未完成。
- [ ] Source DSL v1 先定义可测试的最小 schema/AST，不提前做万能脚本语言。
- [ ] Fixture 驱动，无真实第三方 Source PR-CI 依赖。
- [ ] 新 Source 网络能力必须同步安全测试。
