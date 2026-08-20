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
