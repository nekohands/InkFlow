# 墨流 / InkFlow

多源小说采集、聚合、阅读与分发平台。

InkFlow 以 Canonical Content 为核心，将多来源小说采集、作品与章节归一化、正文版本化与质量选优、自动追更、Web 阅读、开放 API 和阅读 3.0（Legado）兼容分发统一到一个可演进的平台中。

## 产品优先级

1. 阅读 3.0 / Legado
2. 在线阅读体验
3. 自动追更
4. 多源切换 / 容灾
5. 多站点小说采集
6. 统一书库
7. 搜索
8. 用户书架与阅读历史

## 技术基线

- .NET 10 / ASP.NET Core
- PostgreSQL 18
- Redis 8
- Modular Monolith + 独立 API / Worker / Scheduler / Migrations
- EF Core + Npgsql
- Transactional Outbox / Inbox
- OpenTelemetry
- Docker Compose
- GitHub Actions
- Unit / Architecture / Integration / Contract Tests

## 当前状态

**Phase 0 — Foundation 已完成工程验收。**

已实际验证：

- `dotnet restore InkFlow.sln` 成功。
- Release Build 在 warnings-as-errors 下 `0 warnings / 0 errors`。
- Unit / Architecture / Integration / Contract Tests 全绿。
- PostgreSQL 18 Testcontainers 集成测试通过。
- 空数据库可通过 `InkFlow.Migrations` 初始化模块 Schema。
- Transactional Outbox / Inbox 的事务一致性与幂等测试通过。
- Docker Compose 全栈可构建并启动。
- PostgreSQL / Redis / Migration / API / Worker / Scheduler 启动链路通过。
- API / Worker / Scheduler `/health` Runtime Smoke Test 通过。
- Architecture Tests 强制模块依赖矩阵与 Host/Persistence 边界。
- GitHub Actions 完整 Release Gate 已全绿。

下一阶段是 **Phase 1 — Source → Canonical → Content → Legado/Web Vertical Slice**。

## 源码结构

```text
src/
├── Apps/
│   ├── InkFlow.Api/
│   ├── InkFlow.Worker/
│   ├── InkFlow.Scheduler/
│   └── InkFlow.Migrations/
├── BuildingBlocks/
│   ├── InkFlow.BuildingBlocks.Domain/
│   ├── InkFlow.BuildingBlocks.Application/
│   ├── InkFlow.BuildingBlocks.Persistence/
│   ├── InkFlow.BuildingBlocks.Messaging/
│   ├── InkFlow.BuildingBlocks.Security/
│   └── InkFlow.BuildingBlocks.Observability/
└── Modules/
    ├── InkFlow.Modules.Identity/
    ├── InkFlow.Modules.Library/
    ├── InkFlow.Modules.Sources/
    ├── InkFlow.Modules.Crawling/
    ├── InkFlow.Modules.Content/
    ├── InkFlow.Modules.Reading/
    ├── InkFlow.Modules.Search/
    └── InkFlow.Modules.Legado/
```

## 强制工程流程

所有编码、Bug 修复、重构、数据库、Source、Legado、API、CI 和部署改动都必须遵守 `docs/engineering/development-workflow.md`：

```text
明确目标/验收
→ 实现
→ Diff 自检
→ 实际 Build
→ Tests
→ Runtime/Integration 验收
→ Security/Architecture 检查
→ Candidate Commit
→ 实际 CI
→ Bug 修复与回归
→ 文档/Progress/Handoff 同步
→ Accepted / Completed
```

代码写完、本地单测通过或 CI 尚未触发，都不能单独作为 Completed 的依据。

## 部署

### 快速开始（Docker Compose）

前置要求:Docker Engine 24+ 与 Docker Compose v2。

**方式一(开发与日常验证推荐):源码构建**，直接验证当前工作区代码:

```bash
docker compose -f docker-compose.build.yml up -d --build
```

**方式二:使用 GHCR 预构建镜像**，适合部署或明确的发布镜像复验:

```bash
docker login ghcr.io -u <GitHub用户名>   # 密码为 PAT,需 read:packages 权限
docker compose up -d
```

镜像由 GitHub Actions 在 main/dev 分支每次推送时自动构建发布(`.github/workflows/docker.yml`),标签包含分支名(`dev`/`main`)与完整 commit SHA。

启动顺序由编排自动保证:PostgreSQL/Redis 健康检查通过后，migrations 服务对空库执行全部迁移并退出，随后 api / worker / scheduler 启动。

### 服务与端口

| 服务 | 端口 | 说明 |
| --- | --- | --- |
| api | 8080 (宿主) | 公共 API + Web Reader + Legado 契约 |
| worker | 8081 (宿主) | 抓取任务消费(轮询 crawler.tasks 队列) |
| scheduler | 8082 (宿主) | 追更扫描(默认 30 分钟周期) |
| postgres | 未暴露 | 事实数据存储(仅容器网络内) |
| redis | 未暴露 | 可重建状态缓存 |

### 环境变量

本地复验可先复制 [`.env.example`](.env.example) 为 `.env`，再填写本机或验证环境的值。根目录 `.env` 已被忽略，允许保存本地密码等敏感配置，但不得提交；GHCR 登录 Token 不写入仓库，也不复制到 CI。日常 Docker 验证使用 `docker-compose.build.yml`，只有发布镜像或明确进行镜像一致性复验时才使用 `docker-compose.yml`。

| 变量 | 默认值 | 说明 |
| --- | --- | --- |
| `INKFLOW_DB_PASSWORD` | `inkflow` | PostgreSQL 密码，生产环境务必修改 |
| `ConnectionStrings__Redis` | `redis:6379,abortConnect=false`（Compose） | API 分布式限流计数连接；生产环境应配置认证/TLS 连接串 |
| `RateLimiting__RedisKeyPrefix` | `inkflow:rate-limit` | Redis 限流键前缀；客户端身份只以短哈希进入键名 |
| `Operations__Alerts__DeadLetterCountThreshold` | `1` | 告警快照触发死信阈值 |
| `Operations__Alerts__InboxDeadLetterCountThreshold` | `1` | 告警快照触发 Inbox 终态死信阈值 |
| `Operations__Alerts__UnavailableCapabilityCountThreshold` | `1` | 告警快照触发来源能力不可用阈值 |
| `Operations__Alerts__ConsistencyIssueCountThreshold` | `1` | 告警快照触发一致性问题阈值 |
| `Operations__Alerts__MaxReturnedAlerts` | `100` | 单次告警快照最大返回数量 |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | 未配置（Compose 默认为 `http://otel-collector:4317`） | OTLP Collector 基地址；Compose 内部默认接收 traces/metrics，部署环境可覆盖为受管端点 |
| `OTEL_EXPORTER_OTLP_TRACES_ENDPOINT` | 未配置 | 可选 traces 专用 OTLP endpoint |
| `OTEL_EXPORTER_OTLP_METRICS_ENDPOINT` | 未配置 | 可选 metrics 专用 OTLP endpoint |
| `OTEL_METRIC_EXPORT_INTERVAL` | `60000`（Compose；CI smoke 为 `1000`） | metrics 周期导出间隔，单位为毫秒 |

### 部署验证

```bash
curl --fail http://localhost:8080/health
curl --fail --silent http://localhost:8080/api/v1/books   # 目录查询(JSON)
curl --fail --silent http://localhost:8080/reader          # Web Reader(HTML)
```

Web Reader 入口:`http://<主机>:8080/reader`。Legado 书源:`http://<主机>:8080/legado/book-source.json`(baseUrl 自动取请求地址)。

### API 分布式限流

公共 API 与 Legado API 使用独立 fixed-window 策略，计数由 Redis Lua 原子脚本在多个 API 实例间共享；匿名请求按连接层 IP、认证请求按主体短哈希分桶，拒绝返回 `429/Retry-After`。Redis 临时不可用时只切换到同配额的本地有界限流，不会无界放行；该降级不等同于跨实例全局配额，生产环境仍需监控 Redis 可用性并配置告警。

### Operations 告警快照

`GET /api/v1/admin/operations/alerts` 受 `Operator` / `Administrator` 保护，返回有界、可轮询的当前告警快照，覆盖来源能力不可用、Crawler/Inbox 终态死信、一致性问题、Operations 区块不可用和 Redis 限流存储不可用。Inbox 死信仅以数量和截断标记进入平台级快照；来源过滤的 Operator 视图不返回平台级 Inbox 状态。阈值通过 `Operations__Alerts__*` 配置；快照不执行修复、不保存告警历史、不去重也不发送外部通知，外部监控系统需自行轮询并负责通知/保留治理。

### PostgreSQL 备份恢复演练

CI 在 Runtime smoke 产生审计数据后执行 `scripts/backup-restore-drill.sh`：以 custom format 导出当前 PostgreSQL，恢复到隔离数据库，并比较所有非系统表的行数签名及 `audit.events` 数量。也可在已启动源码 Compose 且数据库中已有审计事件时手动运行；该演练验证恢复可用性，不替代生产异地备份、保留策略或 RPO/RTO 演练。

### CI 安全扫描基线

`.github/workflows/security.yml` 在 `main` / `dev` 的 push 和 Pull Request 上执行 NuGet 传递依赖漏洞审计、Trivy 源码/配置/依赖的 HIGH/CRITICAL 漏洞与 Secret/Misconfiguration 扫描、C# CodeQL SAST 和 CycloneDX 源码 SBOM，并将审计、扫描、SAST 与 SBOM 报告作为构建产物保留。当前仓库未启用 GitHub Code Scanning API，因此 CodeQL/Trivy 结果不上传到代码扫描面板，而以可下载报告作为证据。

### Core SLO 可观测性

Core SLO v1 通过 OpenTelemetry 记录 `public_api`、`legado_api`、`developer_api` 和 `reader` 四个服务面：`inkflow.slo.requests`（可用性好/坏事件）、`inkflow.slo.request.duration`（毫秒延迟）和 `inkflow.slo.server.errors`（5xx）。指标只携带稳定服务面和有限结果标签，不包含 URL 参数、用户、IP、Token 或异常原文；目标与正式达标条件见 [ADR 0010](docs/adr/0010-core-slo-and-observability-metrics.md)。配置 OTLP endpoint 后才启用 exporter，没有 Collector 时不会默认向本机端口发送数据。

`CoreSloEvidenceEvaluator` 将 OTLP/合成探针聚合出的单窗口数据转换为可审计结果：四个服务面必须都有正流量、完整延迟样本和 p95，缺证据或非法聚合不会被判为通过；结果同时给出 99.5% 可用性、p95 目标和错误预算剩余量。该评估器不连接生产 Collector、不保存窗口报告，也不替代真实月度窗口验收，契约见 [ADR 0011](docs/adr/0011-core-slo-window-evidence-evaluation.md)。

### Compose OTLP Collector 监控基线

两份 Compose 编排均包含固定版本的官方 `otel/opentelemetry-collector:0.159.0`。API、Worker、Scheduler 默认通过 Compose 内部网络把 traces/metrics 发送到 `otel-collector:4317`；OTLP 接收端口不发布到宿主机，Collector 健康端口 `13133` 仅绑定 loopback。CI Runtime smoke 会实际请求该健康端点，验证 Collector 已启动并可提供健康响应。

当前 Collector 使用 `deploy/observability/otel-collector-config.yaml` 的 `debug` exporter，仅作为本地/CI 接收与诊断基线，不提供持久化、查询、告警或长期保留。生产环境必须通过 `OTEL_EXPORTER_OTLP_ENDPOINT` 或替换 Collector 配置接入受治理的后端，并另行取得窗口证据、错误预算告警、访问控制和保留策略验收；Collector 健康通过不等同于 Core SLO 月度达标。决策见 [ADR 0012](docs/adr/0012-compose-otel-collector-baseline.md)。

CI Runtime smoke 随后执行 `scripts/core-slo-runtime-smoke.sh`，对四个服务面各发起 5 次有界请求：公共目录、空查询 Legado、预期 401 的未授权 Developer API 和 Reader 页面。脚本计算每面 p95 并上传 UTC JSON 证据；空查询不触发真实来源，探针不使用真实凭据或保存响应正文。该证据是 Compose/CI 合成基线，不等同于生产窗口达标，决策见 [ADR 0013](docs/adr/0013-core-slo-runtime-synthetic-probe.md)。

同一门禁还会等待 metrics 周期导出，并从 Collector 的临时诊断输出校验两个 Core SLO instrument 和四个服务面标签确实到达；详细 metrics 输出只由 CI 显式打开，默认 Compose 保持 basic 诊断。

### Transactional Outbox / Inbox

跨模块消息事实保存在 PostgreSQL 的 `messaging.outbox_messages` 和 `messaging.inbox_messages`。业务 DbContext 通过 `ITransactionalOutboxWriter` 在同一事务中追加 Outbox；`OutboxDispatcher` / `IntegrationMessageConsumer` 以成功后确认、lease、稳定失败码和有界退避支持至少一次执行，Inbox 以消息 ID、类型和载荷摘要保护重复消费与消息身份。Inbox 失败按 `Messaging:Inbox:MaxAttempts`（默认 5）和指数退避调度 `AvailableAt`，达到上限写入 `DeadLetteredAt`，Worker 会记录死信计数；已处理记录由 Worker 按 `Messaging:Retention` 配置以有界批次周期清理，失败/待重试/未处理/死信记录保留。发布传输、宿主后台循环和具体 Handler 仍由宿主按需接入，本 Building Block 不绑定未选定的 MQ。当前 Crawler 任务创建已接入 `crawler.task.created` 最小事件，并由 Worker Handler 回读权威任务、原子领取后复用任务处理器执行；消息不包含变量或凭据引用，其他业务事件需按相同事务 seam 接入。

安全审计事实保存在 PostgreSQL `audit.events`。Worker 按 `Audit:Retention`（环境变量 `Audit__Retention__*`）以默认 365 天、有界批次周期清理过期事件；只有受保护的 retention 事务可以删除过期行，普通更新/删除仍由数据库追加式触发器拒绝。该代码基线不替代生产法律保留、归档、恢复授权和删除治理。

`.github/workflows/docker.yml` 会先扫描 Compose 使用的固定版本 OTLP Collector，再构建并加载 API、Migrations、Scheduler、Worker 四个镜像，逐一执行 Trivy HIGH/CRITICAL 漏洞扫描；全部通过后才推送业务镜像标签。该基线不替代生产镜像准入、报告保留、Secret 轮换和部署环境策略治理。

### 生产注意事项

- 通过反向代理(Nginx/Caddy)提供 HTTPS,api 容器只暴露给内网;
- 修改 `INKFLOW_DB_PASSWORD`,数据持久化在命名卷 `inkflow-postgres` / `inkflow-redis`;
- worker/scheduler/api 容器以 read-only 文件系统、no-new-privileges、drop capabilities 运行;
- 数据库结构变更由 migrations 服务在启动链路中自动应用(Expand → Migrate → Contract)。
- Migrations 服务在应用前拒绝模型快照漂移；Release 构建后可运行 `dotnet tool restore` 与 `bash scripts/verify-migrations.sh` 复核全部 11 个数据库上下文。

## 核心文档

- `docs/product/product-vision.md`
- `docs/product/non-goals.md`
- `docs/architecture/architecture.md`
- `docs/architecture/invariants.md`
- `docs/architecture/domain-model.md`
- `docs/architecture/source-runtime.md`
- `docs/architecture/legado-contract.md`
- `docs/architecture/security-model.md`
- `docs/engineering/development-workflow.md`
- `docs/engineering/frontend-design.md`
- `docs/roadmap/progress.md`
- `docs/roadmap/roadmap.md`
- `docs/roadmap/phase-0-plan.md`
- `docs/roadmap/phase-1-acceptance.md`
- `docs/roadmap/risk-register.md`
- `docs/handoff/handoff.md`
