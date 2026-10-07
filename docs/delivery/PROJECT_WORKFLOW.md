# InkFlow 项目工作流档案

Status: Active profile
Last reviewed: 2026-10-08
Owner: InkFlow maintainers

本文件是唯一的操作型工作流 profile。项目自身的强制工程规范仍以
`AGENTS.md` 与 `docs/engineering/development-workflow.md` 为准，本档案只汇
总入口、命令、门禁、状态路径和当前工作包，不复制其完整规则。

## 项目概览

- 产品：墨流 / InkFlow —— 以 Canonical Content 为核心的小说内容平台（Legado 与 Web Reader 为主要消费端）。
- 技术栈：.NET 10（本机 SDK 10.0.401）、ASP.NET Core、PostgreSQL 18、Redis、Docker Compose、服务端渲染 Web/PWA。
- 当前阶段：`1.0 Release Candidate`（保持不标记 `Accepted/Completed`，人工/真实环境验收见 progress.md 第 6 节）。
- 分支模型：`dev` 为唯一开发主线，经 PR 合入 `main`；CI/Docker/Security 三个 workflow 覆盖 `main` + `dev` 的 push/PR。
- AI 权威项目参考（RepoWiki）：[repowiki/README.md](../../repowiki/README.md)；中文人文文档入口见根 README 与 AGENTS.md 阅读顺序。

## Project Profile

```text
Project: InkFlow
Primary stack/runtime: .NET 10 modular monolith; ASP.NET Core; PostgreSQL 18; Redis; Docker Compose; server-rendered Web/PWA
Project instructions: AGENTS.md; docs/engineering/development-workflow.md（强制）
Optional code index: CodeGraph queried with `codegraph explore "..."`
Root `.codegraph/` present; query "project structure, entrypoints, and verification commands" executed 2026-10-02 with results (39 symbols, 5 files)
Authoritative architecture/invariant docs: docs/architecture/architecture.md; invariants.md; domain-model.md; overview.md; Wiki 镜像 repowiki/architecture.md
Security/input/permission docs: docs/architecture/security-model.md; AGENTS.md 安全与不变量章节
Public/external contracts: docs/architecture/legado-contract.md; docs/architecture/developer-api.md; docs/contracts/source-rule-dsl-v1.schema.json; tests/InkFlow.ContractTests
UI/design/UX docs: docs/engineering/frontend-design.md
Roadmap/progress/handoff docs: 见下方「交付状态记录」
Project-defined completion/release/phase gates: development-workflow.md §§3/17/20; progress.md 第 5.5/5.6 节与 Phase Exit Criteria; 1.0 Release Gate（前端/runtime/security 扫描）
Required manual/real-environment acceptance: progress.md 第 6 节清单（阅读 3.0 真机、真实上游/故障切换、真实账户/PWA、生产 OTLP/SLO/备份治理等）
Source-of-truth data stores: PostgreSQL 业务数据与迁移；领域要求处追加式历史
Derived stores/caches/projections: Redis、Projection、Search Index、fetch artifacts —— 均可重建且不作为唯一事实
Build/restore: `dotnet restore InkFlow.sln`; `dotnet build InkFlow.sln -c Release`（0 warnings / 0 errors，warnings-as-errors）
Unit tests: `dotnet test tests/InkFlow.UnitTests -c Release`
Architecture/static checks: `dotnet test tests/InkFlow.ArchitectureTests -c Release`
Integration/migration checks: `dotnet test tests/InkFlow.IntegrationTests`（Testcontainers PostgreSQL 18）; `bash scripts/verify-migrations.sh`（11 contexts fail-closed）
Contract/browser checks: `dotnet test tests/InkFlow.ContractTests -c Release`; `bash scripts/legado-runtime-smoke.sh http://localhost:8080`; `bash scripts/reader-frontend-runtime-smoke.sh http://localhost:8080` 等受影响 smoke
Runtime smoke: `docker compose -f docker-compose.build.yml config --quiet`; `docker compose -f docker-compose.build.yml up -d --build --wait api worker scheduler otel-collector`（日常验证用源码构建编排；docker-compose.yml 为 GHCR 发布镜像编排）
Security/scan checks: AGENTS.md SSRF/授权/Secret/Migration 检查；.github/workflows/security.yml；Docker 发布前 Trivy
CI trigger/status: .github/workflows/ci.yml、docker.yml、security.yml；push/PR 覆盖 main + dev；Completed 前必须确认目标 commit 的强制 Job 全部真实通过
Status vocabulary: Planned; In Progress; Implemented; Locally Validated; CI Green; Accepted; Completed; Blocked（沿用项目自有词汇，见 development-workflow.md §2）
Known environment blockers: Windows 开发机无 Docker Engine —— Testcontainers Integration 与源码 Compose runtime smoke 本机 BLOCKED，由远端 CI 容器作为证据来源；GitHub 网络（push/API）偶发中断需重试
```

## 交付状态记录（当前/历史）

四个权威状态文件保留在项目既有路径（AGENTS.md 指定，不迁移）：

| 记录 | 当前 | 历史 |
| --- | --- | --- |
| 进度 | [docs/roadmap/progress.md](../roadmap/progress.md) | [docs/roadmap/progress-history.md](../roadmap/progress-history.md) |
| 交接 | [docs/handoff/handoff.md](../handoff/handoff.md) | [docs/handoff/handoff-history.md](../handoff/handoff-history.md) |

当前/历史拆分于 2026-10-01 完成（工作包 5.54 内），索引见
[delivery README](README.md)；Wiki 侧仅链接不复制。

## 工作流摘要

1. 先定义工作包：目标、范围、非目标、验收、风险和验证计划；改变已接受架构方向时先更新 ADR（[docs/adr/](../adr/)）。
2. 先读取相关权威文档、配置、测试和当前状态；存在 `.codegraph/` 时先用 CodeGraph 定位，再做定向搜索。
3. 实现最小完整切片，保持模块依赖、公共 Contract、数据不变量和安全边界。
4. 编译前审查 Diff、Secret、依赖方向、Migration、测试和文档同步。
5. 按影响范围执行上表 Gate：Restore/Release Build、Unit、Architecture、Integration、Contract、Runtime、Security 和真实环境验收；`N/A`/`NOT RUN`/`BLOCKED` 必须写明原因。
6. 创建可审查的 candidate commit，确认实际 CI 所有强制 Job 通过；失败必须读日志、修根因并完整回归。
7. 对照验收条件更新 Progress/Handoff/ADR/Contract 等状态并同步受影响的 Wiki 与中文页面；只有所有适用证据齐全才可标记 `Accepted / Completed`。

## Work Package

当前状态：Accepted（5.63，Canonical match query bounding）；当前无 In Progress 工作包。前一包为：[Private book optimistic concurrency](../../repowiki/work-packages/2026-10-08-private-book-optimistic-concurrency.md)，再前一包为：[Code adapter response and regex bounds](../../repowiki/work-packages/2026-10-08-code-adapter-bounds.md)。

最近完成工作包（5.63，2026-10-08，Accepted）：

```text
Name: Canonical match query bounding
Objective / user outcome: Canonical book matching no longer materializes every canonical book for a normalized title/author lookup.
In scope: PostgreSQL-backed FindByTitleAuthorAsync, parameterized normalized predicates, deterministic first-match ordering, focused PostgreSQL regression.
Non-goals: matching-policy redesign, public API/Legado contract changes, schema/migration changes, duplicate cleanup, full-text search, UI.
Acceptance: whitespace/case semantics and stable BookId preserved; miss does not load the full table; SQL is parameterized and bounded with LIMIT 1.
Status: Accepted.
Verification: Restore/Release Build 0 warnings / 0 errors; Unit 597/597; Architecture 1/1; Contract 12/12; Windows migration model check 11/11; focused local PostgreSQL Integration BLOCKED by unavailable Docker named pipe. Exact SHA `2f20125806b1bc3464fc5d2e299d75f5623ad7a0` passed CI 37661014830, Docker 37661014858, and Security 37661014826.
Boundary: local Testcontainers may remain BLOCKED by the unavailable Docker named pipe; .workbuddy-ai/ is preserved and untracked.
```

最近完成工作包（5.62，2026-10-08，`Accepted`）：

```text
Name: Private book optimistic concurrency
Objective / user outcome: 私有书目元数据并发编辑不再静默覆盖较新的编辑
In scope: PrivateBook Version、private_books Migration、原子条件更新、PUT version/409 contract、回归与 PostgreSQL evidence
Non-goals: 其他聚合、删除 CAS、章节正文编辑、自动合并、ETag/If-Match、UI
Acceptance: 当前 version 更新成功并递增；过期/缺失 version 不写入并返回稳定错误；真实 PostgreSQL 仅一个并发写成功
Evidence status: Accepted; implementation and verification complete
Verification: focused Library/API 13/13; Unit 597/597; Architecture 1/1; Contract 12/12; Release Build 0 warnings / 0 errors; migration model check 11/11. Local Integration attempted but BLOCKED by unavailable Docker named pipe; remote PostgreSQL/runtime passed.
CI: exact SHA `9610b76fa14b572da4a3203c9047ec0a9ae2d8e0` passed CI `37655196433`, Docker/Compose `37655726362`, and Security `37655196520`.
Delivery boundary: no changes to other aggregate concurrency, delete CAS, chapter/content editing, automatic merge/retry, ETag/If-Match, or UI.
```

最近完成工作包（5.61，2026-10-08，`Accepted` 级证据）：

本包复用既有 SourceRuleExecutionLimits，为 Kanunu8/17K 自定义适配器补齐响应体上限，并为 Kanunu8 静态正则设置配置化有限超时；不引入公共适配器契约、乐观并发、Schema 或 Migration 变化。远端 CI/Docker/Security 全部 GREEN。

最近完成工作包（5.61，2026-10-08，`Accepted` 级证据）：

```text
Name: Code adapter response and regex bounds
Objective / user outcome: 可信 CodeAdapter 不再无界读取响应体或执行无超时静态正则
In scope: shared streaming HttpContent reader; ProductionSafeSourceHttpClient reuse; Kanunu8/17K MaxBytes; Kanunu8 configured MaxRegexTime; focused regressions and source-runtime docs
Non-goals: Rule DSL redesign, new adapters, host/retry policy, public ISourceAdapter contract, Schema/Migration, live-source acceptance
Acceptance: Kanunu8 GB18030 and 17K JSON fixtures remain valid; oversized responses fail before decode/parse; regex timeout is finite and no partial result escapes; SSRF/host/VIP/identity boundaries unchanged
Evidence: Restore/Release Build 0 warnings/0 errors；focused 13/13、Unit 595/595、Architecture 1/1、Contract 12/12；migration 11/11；local Integration attempted but BLOCKED by unavailable Docker named pipe
CI: `0d7d5ce` 的 CI 37650394342、Docker 37650394297、Security 37650394257 均 GREEN 且 head SHA 一致，含远端 PostgreSQL/runtime/Compose smoke
Boundary: no Schema/Migration or phase-exit change; real upstream/live-source and Release Candidate manual acceptance remain pending
```

最近完成工作包（5.60，2026-10-07，`Accepted` 级证据）：

```text
Name: Entitlement actor validation
Objective / user outcome: 直接调用 Billing entitlement assignment service 也只能由 active Administrator 执行
In scope: Billing actor-status port + Identity composition adapter + service guard + stable 403 mapping + regression tests
Non-goals: role policy redesign, token/session redesign, optimistic concurrency, adapter bounds, plan/quota changes, Schema/Migration
Acceptance criteria（全部满足）:
  - happy path: active Administrator assignment remains valid with existing reason/audit behavior
  - error path: empty/unknown/inactive/non-Administrator actor is rejected before assignment persistence and maps to 403
  - regression: route, payload, target/plan lookup, authentication, and data model remain unchanged
Evidence: Restore/Release Build 0 warnings/0 errors；Unit 592/592、Architecture 1/1、Contract 12/12；migration 11/11；remote CI/Docker/Security GREEN at d0413f2 (37638597477/37638597479/37638597492)
Boundary: local Testcontainers BLOCKED by unavailable Docker named pipe；remote CI supplied PostgreSQL/runtime evidence；real-account/manual Release Candidate gates remain pending
```

最近完成工作包（5.58，2026-10-06，`Accepted` 级证据）：

```text
Name: Inbox/Outbox 批次租约续约
Objective / user outcome: 长批次处理不再因租约过期被其他实例重复领取/投递
In scope: Messaging 端口与 EF 存储（ExtendLeaseBatchAsync）+ Dispatcher/Inbox 泵接线
Non-goals: 单条 Handler 超长租约的中途续约（幂等消费兜底，已记录边界）；无 Schema/Migration 变更
Acceptance criteria（全部满足）:
  - happy path: 每条消息处理前续约整批剩余租约（单元接线断言）
  - error path: 过期后经续约不可被其他 owner 领取；终态行不参与续约（真实 PostgreSQL 回归）
  - regression: Unit 587/587、Architecture 1/1、Contract 12/12
Evidence: Build 0 warnings/0 errors；CI/Docker/Security GREEN at `1658b87`（37478901744/37478902070/37478901915）
```

## Adoption 记录

2026-10-02 完成 in-progress adoption：文档盘点映射、RepoWiki 建立、占位符
替换为实测值。见 [adoption-2026-10-02.md](adoption-2026-10-02.md)。
