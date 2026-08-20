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
- PostgreSQL
- Redis
- Modular Monolith + 独立 API / Worker / Scheduler
- Docker Compose
- GitHub Actions
- OpenTelemetry（Phase 0）

## 当前状态

当前 `chore/bootstrap` 分支已经完成项目骨架与产品/架构对齐文档，下一工程阶段是 Phase 0 Foundation：重构模块边界、建立 Persistence/Migrations、Outbox/Inbox、Architecture Tests、Observability 和完整 CI 基线。

现有源代码目录仍是最初 bootstrap 结构；目标结构与迁移计划以 Phase 0 文档为准，保留 Git 历史渐进重构，不重新初始化仓库。

## 强制工程流程

所有编码、Bug 修复、重构、数据库、Source、Legado、API、CI 和部署改动都必须遵守：

`docs/engineering/development-workflow.md`

每个工作包默认执行完整闭环：

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

**代码写完、本地单测通过或 CI 尚未触发，都不能单独作为 Completed 的依据。**

## 前端设计原则

所有主要用户页面必须遵守：

`docs/engineering/frontend-design.md`

核心要求：

- 实现主要页面前至少研究 3 个当前活跃同类产品，形成 Benchmark Note 或等效记录。
- 持续参考番茄、七猫、起点、晋江、Kakuyomu、Royal Road 等成熟阅读产品的交互模式，但禁止直接复制视觉和品牌设计。
- 优先保证操作路径短、页面清晰、视觉友好、长时间阅读舒适。
- Mobile 与 Desktop 都作为正式产品体验进行验收。
- 高级 Source/ContentVersion/Quality 等内部能力采用 Progressive Disclosure，不污染普通读者界面。
- Reader 页面以正文优先，必须单独验收排版、主题、目录、章节切换和下一章加载体验。
- 可访问性目标以 WCAG 2.2 AA 为基线。

前端功能完成但未通过 Responsive / UX / Visual / Accessibility 验收时，不得标记 `Completed`。

## 核心文档

### 产品

- `docs/product/product-vision.md`：产品定位、用户、商业方向和优先级
- `docs/product/non-goals.md`：明确暂不实现的能力和范围护栏

### 架构

- `docs/architecture/architecture.md`：总体架构规范
- `docs/architecture/invariants.md`：不可轻易违反的工程不变量
- `docs/architecture/domain-model.md`：Canonical Book/Chapter/Content 领域模型
- `docs/architecture/source-runtime.md`：Official/Community/Private Source 与 Hybrid Adapter
- `docs/architecture/legado-contract.md`：Legado 专用协议与 Release Gate
- `docs/architecture/security-model.md`：认证、授权、SSRF、沙箱、Secrets 与内容安全

### 工程规范

- `docs/engineering/development-workflow.md`：**每轮强制实现、编译、测试、验收、CI、修复、回归与交付流程**
- `docs/engineering/frontend-design.md`：**前端竞品调研、信息架构、交互、视觉、Reader、响应式、可访问性与 UI 验收规范**

### 路线、进度与验收

- `docs/roadmap/progress.md`：项目持续进度、当前阶段、完成项、阻塞与下一步
- `docs/roadmap/roadmap.md`：Phase 0 → 1.0 路线
- `docs/roadmap/phase-0-plan.md`：Foundation 实施计划与验收
- `docs/roadmap/phase-1-acceptance.md`：Single/Dual Source Vertical Slice 验收标准
- `docs/roadmap/risk-register.md`：核心工程风险与缓解措施

### 工程交接

- `docs/handoff/handoff.md`：接手顺序、当前状态、架构不变量、下一工作包与交接检查清单

### ADR

- `docs/adr/0001-modular-monolith.md`
- `docs/adr/0002-content-source-model.md`
- `docs/adr/0003-legado-compatibility.md`
- `docs/adr/0004-infrastructure.md`
