# InkFlow 项目工作流档案

Status: Active profile
Last reviewed: 2026-10-02
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

当前状态：Intake required（无进行中工作包）。下一候选为 2026-09-11 审查中危项
（Inbox/Outbox 租约心跳续约、乐观并发令牌、Catalog 查询分页/N+1、适配器正则
超时/无界读取、EntitlementService actor 校验），开工前先填：目标/用户结果、
范围与非目标、验收条件（正常/错误/回归）。

最近完成工作包（5.57，2026-10-06，`Accepted` 级证据）：

```text
Name: 死信与任务状态同事务
Objective / user outcome: 任务失败进死信时，死信行与 DeadLettered 终态原子落库，不再产生半一致状态
In scope: Crawling 模块（ICrawlerTaskRepository.AddDeadLetterWithTaskAsync + EF 事务 + Processor 接线）
Non-goals: 无 Schema/Migration 变更；乐观并发令牌另行立项
Acceptance criteria（全部满足）:
  - happy path: 死信 + 终态同时可见（真实 PostgreSQL 回归）
  - error path: 任务行缺失时整体回滚，不留孤儿死信行（真实 PostgreSQL 回归）
  - regression: Unit 585/585、Architecture 1/1、Contract 12/12
Evidence: Build 0 warnings/0 errors；CI/Security GREEN at `8a8fcd1`（37469389070/37469389126）；
     Docker 37469389018 GREEN（首跑 GHCR 推送瞬时 unknown blob，重跑通过）
```

## Adoption 记录

2026-10-02 完成 in-progress adoption：文档盘点映射、RepoWiki 建立、占位符
替换为实测值。见 [adoption-2026-10-02.md](adoption-2026-10-02.md)。
