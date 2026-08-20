# InkFlow 项目进度

> 本文档是 InkFlow 的持续进度账本，用于回答：当前做到哪里、已经验证什么、下一步是什么、哪些事项被阻塞。
>
> 更新规则：每完成一个可验收工作包，必须同步更新本文档；状态应基于实际代码、测试和 CI 结果，不以“计划完成”代替“已完成”。

- 产品：墨流 / InkFlow
- 当前阶段：Phase 0 — Foundation
- 当前工作分支：`chore/bootstrap`
- 最后人工更新日期：2026-08-20

## 1. 总体状态

| 阶段 | 状态 | 说明 |
| --- | --- | --- |
| Grill Me / 产品与架构对齐 | ✅ Completed | 产品定位、核心领域、Legado、Source Runtime、安全、商业化和路线已形成正式文档 |
| Repository Bootstrap | ✅ Completed | .NET 10 基础解决方案、API、Worker、Scheduler、Docker Compose、基础 CI 已建立 |
| Phase 0 — Foundation | 🚧 In Progress | 下一阶段需要按最终模块边界重构并建立可运行基础设施 |
| Phase 1A — Single Source Vertical Slice | ⏳ Not Started | 单来源 → Canonical → Content → Web → Legado 全链路 |
| Phase 1B — Dual Source Validation | ⏳ Not Started | 双来源匹配、内容版本、质量选优、故障切源 |
| Phase 2 — Multi-Source Production | ⏳ Not Started | 多源生产化、健康评分、自适应追更、规则 Canary |
| Phase 3 — User Product | ⏳ Not Started | Web Reader、账号、书架、历史、私人书库 |
| Phase 4 — Commercial Platform | ⏳ Not Started | Plan/Entitlement、Developer API、Billing、Organization、Community Source |

状态定义：

- `✅ Completed`：已实现且满足当前阶段验收要求。
- `🚧 In Progress`：已有实际工作，但尚未达到阶段验收线。
- `⏳ Not Started`：尚未进入实现。
- `⛔ Blocked`：存在明确阻塞项，必须记录原因和解除条件。

工作包状态必须进一步遵守 `../engineering/development-workflow.md`：`Implemented`、`Locally Validated`、`CI Green`、`Accepted` 和 `Completed` 不得混为一谈。

## 2. 已完成

### 2.1 产品与架构对齐

已确定并文档化：

- 最终目标为商业产品，而非个人 Demo。
- 产品优先级：Legado → Web 阅读 → 自动追更 → 多源容灾 → 多站点采集 → 统一书库 → 搜索 → 用户书架/历史。
- Canonical Book + Canonical Chapter 作为统一内容身份。
- 正文采用混合存储策略，底层支持全量持久化。
- Content Version 不覆盖旧版本，Quality Engine 动态选优并支持人工锁定。
- Legado 采用官方 InkFlow 聚合书源为主、第三方原生规则为辅的双轨模式。
- Legado 公共访问 + 个人 Token 双模式。
- Official Source + Community Source 双轨，Community Source 采用受限 DSL 与沙箱。
- RuleAdapter + CodeAdapter Hybrid Adapter 架构。
- HttpClient → Session/签名 → Playwright → 人工辅助的分层抓取。
- Canonical Book 和 Chapter 都使用多证据匹配并保留可解释决策记录。
- 自适应追更、事件触发多源检查和退避策略。
- PostgreSQL 搜索起步，未来通过抽象迁移 OpenSearch。
- 商业级身份底座、B2C + B2D/B2B 双轨商业模式。
- Modular Monolith 优先，不提前微服务、Kubernetes、多 Region。

### 2.2 仓库 Bootstrap

当前仓库已经具备：

- .NET 10 / ASP.NET Core 基础工程。
- `InkFlow.Api`。
- `InkFlow.Worker`。
- `InkFlow.Scheduler`。
- 初始 Domain/Application/Infrastructure 项目。
- 初始 Legado 模块。
- PostgreSQL + Redis Docker Compose。
- GitHub Actions 基础 Build CI。
- `.editorconfig`、`.gitignore`、`global.json`、`Directory.Build.props`。
- 初始 ADR 与架构说明。

注意：当前代码目录属于 Bootstrap 结构，不是 Phase 0 最终模块结构。

### 2.3 正式文档

已建立：

- `docs/product/product-vision.md`
- `docs/product/non-goals.md`
- `docs/architecture/architecture.md`
- `docs/architecture/invariants.md`
- `docs/architecture/domain-model.md`
- `docs/architecture/source-runtime.md`
- `docs/architecture/legado-contract.md`
- `docs/architecture/security-model.md`
- `docs/engineering/development-workflow.md`
- `docs/roadmap/roadmap.md`
- `docs/roadmap/phase-0-plan.md`
- `docs/roadmap/phase-1-acceptance.md`
- `docs/roadmap/risk-register.md`
- `docs/roadmap/progress.md`
- `docs/handoff/handoff.md`

## 3. 当前正在做

### Phase 0 — Foundation

下一批工作按照以下顺序执行：

1. 将目录重构为 `Apps / BuildingBlocks / Modules`。
2. 建立 Architecture Tests，强制模块依赖方向。
3. 建立 UUIDv7 强类型 ID 基础设施。
4. 建立 `Result<T>`、统一 Error、Problem Details 映射。
5. 统一 `TimeProvider`。
6. 接入 EF Core + PostgreSQL。
7. 按模块建立 PostgreSQL Schema 和 DbContext。
8. 增加 `InkFlow.Migrations` 独立迁移应用。
9. 建立 Transactional Outbox / Inbox。
10. 接入 OpenTelemetry traces / metrics / logs 基础。
11. 增加 Testcontainers Integration Tests。
12. 强化 GitHub Actions：Restore → Build → Unit → Architecture → Integration。
13. 实际运行 `dotnet build`、`dotnet test` 和 Docker Compose 验收。
14. 修复全部错误，确保 CI Green。

每一个上述工作包都必须独立遵守：

`明确验收条件 → 实现 → Diff 自检 → 实际 Build → Tests → Runtime/Integration 验收 → Security/Architecture 检查 → Candidate Commit → 实际 CI → Bug 修复/回归 → 文档更新 → Accepted`

完整流程见 `../engineering/development-workflow.md`。

## 4. 下一里程碑

### Phase 0 Exit Criteria

只有同时满足以下条件才能进入 Phase 1A：

- [ ] `dotnet restore` 成功。
- [ ] `dotnet build -c Release` 成功且 warnings-as-errors 下无警告。
- [ ] `dotnet test -c Release` 全绿。
- [ ] Architecture Tests 全绿。
- [ ] PostgreSQL / Redis 可通过 Docker Compose 启动并通过健康检查。
- [ ] Migration App 可对空数据库完成初始化。
- [ ] API、Worker、Scheduler 可启动并通过健康检查。
- [ ] Outbox / Inbox Integration Test 通过。
- [ ] OpenTelemetry 基础 instrumentation 已接入。
- [ ] CI 在目标分支/PR 上全绿。
- [ ] README、Progress、Handoff 与实际状态一致。

### Phase 1A Exit Criteria

详见 `phase-1-acceptance.md`。最核心的验收链路是：

`Official Source → Search → Canonical Book → TOC → Chapter Content → Web Reader → Legado bookSource → Legado Search/TOC/Content → 自动追更`

全程不得依赖人工直接修改数据库或手工拼接生产数据。

## 5. 已知未完成项

- 当前 `InkFlow.sln` 仍使用 Bootstrap 目录布局。
- 当前还没有正式 Persistence 模块、EF Core Migration 或真实数据库模型。
- 当前还没有 Outbox / Inbox 实现。
- 当前还没有 Source DSL v1。
- 当前还没有 Crawler Task Lease / Retry / Dead Letter。
- 当前还没有 Canonical Matching / Chapter Alignment 的正式实现。
- 当前还没有 Content AST / ContentVersion / Quality Engine。
- 当前还没有正式 Legado API Contract 实现和 Rule Generator。
- 当前还没有 Web Reader。
- 当前 CI 只验证基础 restore/build，尚未达到 Phase 0 Release Gate。
- 当前尚未执行 Phase 0 最终架构下的真实 CI/编译验收。

## 6. 当前阻塞

当前无已知产品级阻塞。

如果后续出现阻塞，在这里使用以下格式记录：

```text
BLOCK-XXX
状态：Open / Resolved
影响阶段：
问题：
影响：
解除条件：
Owner：
关联 Issue/PR：
```

## 7. 进度维护规则

每次工作结束前至少更新：

1. `当前阶段`。
2. `已完成`。
3. `当前正在做`。
4. `下一里程碑` 的 checkbox。
5. `已知未完成项`。
6. 新增/解除的 Blocker。
7. 本轮真实验证证据：Build / Tests / Runtime / CI 状态。
8. 本轮发现并修复的 Bug，以及仍未解决的风险。

禁止：

- 把“代码已写但未 Build/Test”标记为 Completed。
- 把“本地通过但 CI 未执行”写成 CI Green。
- CI Pending 时提前标记完成。
- CI Red 时通过反复重跑掩盖根因。
- 修改代码后沿用修改前的验证结果。
- 删除历史重大里程碑来让进度看起来更快。
- 让本文档长期落后于实际仓库状态。

### 每轮验证记录模板

每轮编码工作完成或暂停时，在对应进度记录中至少能够回答：

```text
工作包：
状态：Planned / In Progress / Implemented / Locally Validated / CI Green / Accepted / Completed / Blocked
Build：PASS / FAIL / NOT RUN
Tests：PASS / FAIL / PARTIAL / NOT RUN
Runtime：PASS / FAIL / N/A / NOT RUN
CI：GREEN / RED / PENDING / NOT TRIGGERED
验收结果：
发现的 Bug：
修复的 Bug：
剩余风险/Blocker：
Commit/PR：
```

## 8. 相关文档

- 产品愿景：`../product/product-vision.md`
- 非目标：`../product/non-goals.md`
- 架构规范：`../architecture/architecture.md`
- 架构不变量：`../architecture/invariants.md`
- **强制开发/验收流程：`../engineering/development-workflow.md`**
- 领域模型：`../architecture/domain-model.md`
- Source Runtime：`../architecture/source-runtime.md`
- Legado Contract：`../architecture/legado-contract.md`
- 安全模型：`../architecture/security-model.md`
- 路线图：`roadmap.md`
- Phase 0：`phase-0-plan.md`
- Phase 1 验收：`phase-1-acceptance.md`
- 风险登记：`risk-register.md`
- 工程交接：`../handoff/handoff.md`
