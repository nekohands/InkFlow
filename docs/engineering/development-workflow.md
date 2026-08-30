# InkFlow 强制开发、验收与交付流程

> 本文档定义 InkFlow 每一轮编码工作必须遵守的工程流程。它不是建议，而是默认强制规范。
>
> 核心原则：**代码写完不等于完成。只有实现、验收、实际编译、测试、运行验证、CI、缺陷修复、回归和文档状态全部达到该工作包要求，才允许标记 Completed。**

## 1. 适用范围

以下工作默认都受本流程约束：

- 新功能开发。
- Bug 修复。
- 重构。
- 架构调整。
- 数据库 Schema / Migration 修改。
- Source Rule / Parser / Normalizer 修改。
- Legado Contract 修改。
- API Contract 修改。
- CI / Docker / 部署配置修改。
- 安全、权限、缓存、消息和任务系统修改。

纯文档拼写修正可缩短验证范围，但仍必须确认链接、格式和内容与仓库状态一致。

## 2. 状态定义

每个工作包使用以下状态语义，禁止混用：

- `Planned`：已定义目标，尚未开始实现。
- `In Progress`：正在编码或仍有验证未完成。
- `Implemented`：代码已写完，但尚未完成全部验证；**不得等同 Completed**。
- `Locally Validated`：规定的本地/执行环境 Build、Test、运行验收已通过，但 CI 尚未确认。
- `CI Green`：目标 Commit/PR 的规定 CI 已真实执行并通过。
- `Accepted`：功能验收条件、回归、文档和状态更新均已完成。
- `Completed`：只有 `Accepted` 后才可使用。
- `Blocked`：存在无法继续的明确阻塞，并已记录原因和解除条件。

## 3. 每轮开发的强制闭环

每一个可验收工作包必须按以下闭环执行：

```text
明确工作包与验收条件
        ↓
实现 / 重构
        ↓
代码自检与 Diff 审查
        ↓
实际 Restore / Build
        ↓
自动化测试
        ↓
运行时 / 集成验收
        ↓
安全与架构检查
        ↓
提交候选 Commit
        ↓
实际 CI 验证
        ↓
失败？ ── Yes → 定位根因 → 修复 → 局部验证 → 全量回归 → CI 重跑
        │
        No
        ↓
功能验收 / 回归检查
        ↓
必要的优化（不改变既定范围）
        ↓
再次 Build / Test / CI（如代码发生变化）
        ↓
更新 Progress / Handoff / ADR / 文档
        ↓
Accepted / Completed
```

任何一步失败，都必须回到修复循环；禁止跳过失败项继续把工作包标记完成。

## 4. Step 0 — 定义工作包

编码前至少明确：

1. 目标：这轮解决什么问题。
2. 范围：允许修改哪些模块/文件类别。
3. 非目标：本轮明确不做什么。
4. 验收条件：什么结果才算成功。
5. 风险：是否涉及架构不变量、数据、权限、API、Legado、Migration 或安全边界。
6. 验证计划：至少需要哪些 Build/Test/Runtime/CI 检查。

如果工作会修改已接受的架构方向，必须先新增或更新 ADR，而不是在代码中静默改变方向。

## 5. Step 1 — 实现

实现阶段必须遵守：

- 保持工作包边界，不顺手混入无关重构。
- 优先修改根因，不用临时绕过隐藏问题。
- 新增业务行为必须有相应测试计划。
- 修改公共 Contract 必须考虑兼容性。
- 修改数据库必须提供 Migration 策略。
- 修改 Source/Parser/Normalizer 必须保留版本化与可重放原则。
- 修改 Canonical Match / Selection 等自动决策必须保持可解释性。

## 6. Step 2 — 代码自检

进入编译前必须检查实际 Diff：

- 是否只包含本工作包内容。
- 是否有误提交的 Secret、Token、Cookie、密码或生产配置。
- 是否出现 Debug/临时代码。
- 是否破坏模块依赖方向。
- 是否错误修改公共 API/Legado Contract。
- 是否需要新增/更新测试。
- 是否需要 Migration、文档或配置同步修改。

发现无关修改必须在提交前剔除。

## 7. Step 3 — 实际编译验证

.NET 工作包默认至少执行等价于：

```text
dotnet restore
dotnet build -c Release
```

Phase 0 完成后应在 warnings-as-errors 条件下通过。

要求：

- 必须真实执行，不能仅通过静态阅读判断“应该能编译”。
- Build 失败时不得继续声称实现完成。
- 修复 Build 问题后必须重新完整 Build。
- 如果环境导致无法执行，状态只能记录为 `Implemented` / `Blocked`，并明确缺失的验证证据。

前端、脚本或其他技术栈进入项目后，使用其对应真实 production build 命令，并纳入同一 Gate。

## 8. Step 4 — 自动化测试

根据改动范围执行对应层级：

### Unit Tests

用于纯领域逻辑、Normalizer、Matcher、Quality、Value Object、权限规则等。

### Architecture Tests

任何模块结构或依赖修改必须执行，确保：

- Domain 不依赖 EF Core / ASP.NET / Redis。
- 模块间依赖符合既定方向。
- Legado、Web 等 Presentation 不反向污染核心领域。

### Integration Tests

涉及 PostgreSQL、Redis、Outbox/Inbox、Migration、Task Lease、Object Storage 等必须执行真实兼容环境测试，优先 Testcontainers。

### Contract Tests

涉及以下内容必须执行：

- Legado API / Rule。
- Developer/Public API。
- Webhook 等稳定外部协议。

### Regression Tests

Bug 修复原则上必须先有可复现测试，修复后该测试通过，并执行受影响区域的回归测试。

测试失败必须修复；禁止通过删除测试、放宽断言或无理由 Skip 来制造绿色结果。

## 9. Step 5 — 运行时与集成验收

仅 Build/Test 通过仍不足以证明运行时正确。按工作包范围验证：

- Docker Compose 服务可启动。
- 日常 Docker 验证默认使用 `docker-compose.build.yml` 从当前源码构建并启动；`docker-compose.yml` 是 GHCR 发布镜像编排，只在发布产物、镜像一致性或明确要求镜像验证时使用。CI 的常规 Runtime smoke 遵循源码构建路径，Docker 发布工作流按需构建、扫描并推送镜像。
- PostgreSQL / Redis 健康检查通过。
- Migration 能对空数据库执行。
- Release Build 后运行 `scripts/verify-migrations.sh`，确认所有 DbContext 的当前模型与迁移快照无漂移。
- 对已有数据库的兼容 Migration 路径可验证时必须验证。
- API / Worker / Scheduler 能启动。
- Health Endpoint 返回预期结果。
- 涉及 Source 时验证 Fixture；Live Source 验证按规定独立执行。
- 涉及 Legado 时验证 Search → BookInfo → TOC → Content 的 Contract 链路。
- 涉及多源时验证稳定 BookId / ChapterId 和 Failover。

不能用“接口代码存在”代替实际运行验收。

## 10. Step 6 — 安全与架构验收

按改动范围检查：

- SSRF / SafeHttpClient 边界。
- 权限与私人内容隔离。
- Secret 不落库明文、不进入日志和 Commit。
- Community Source 沙箱边界。
- API Rate Limit / Token Scope。
- Migration 是否存在破坏性数据风险。
- Migration 模型漂移是否在应用迁移前 fail-closed，且所有 DbContext 都纳入检查。
- Redis 是否被误当作唯一事实来源。
- Canonical 数据是否被 Source 数据直接覆盖。
- Content Version 是否被错误覆盖。

涉及高风险边界的工作包在安全验收未通过前不得 Completed。

## 11. Step 7 — Commit 规范

提交应保持原子性和可审查性。

推荐：

```text
feat(library): add canonical book identity
feat(crawler): implement leased task execution
fix(content): reject truncated content candidate
test(legado): cover toc contract regression
docs: update phase 0 progress
```

禁止使用无法表达内容的提交信息，例如：

```text
update
fix
misc
changes
```

如果必须先提交才能触发远端 CI，该 Commit 只是 **candidate commit**；在 CI 通过以前，工作包仍不得标记 Completed。

## 12. Step 8 — 实际 CI 验证

CI 是独立 Gate，不能用本地测试替代。

至少要求确认：

1. CI 确实针对目标 Commit/PR 运行。
2. 所有强制 Job 都结束，而不是仍处于 Pending。
3. Build/Test/Architecture/Integration/Contract 等规定 Job 全绿。
4. 没有被忽略的失败、取消或超时 Job。

如果当前分支配置不会触发 CI：

- 明确记录 `CI Pending / Not Triggered`。
- 不得写成 `CI Green`。
- 在进入需要 CI Gate 的里程碑前，必须通过 PR、目标分支或规定方式触发真实 CI。

## 13. Step 9 — CI 失败修复循环

CI 失败后执行：

```text
读取失败 Job / Step / Log
        ↓
确定失败类别
        ↓
找到根因，不仅修表象
        ↓
最小必要修复
        ↓
本地/局部验证
        ↓
完整 Build + 相关 Test
        ↓
提交修复
        ↓
重跑 CI
```

常见失败类别应区分：

- Compilation。
- Unit/Architecture/Integration/Contract Test。
- Migration。
- Container Build。
- Dependency/Security Scan。
- 环境/CI 配置。
- Flaky Test。

Flaky Test 不允许简单重跑到绿色后忽略；必须记录并修复或创建明确的跟踪事项。

## 14. Step 10 — 功能验收

CI Green 之后还需要对照工作包验收条件逐项检查。

验收必须回答：

- 用户/调用方真实目标是否达成。
- Happy Path 是否通过。
- 关键 Error Path 是否通过。
- 是否存在行为回归。
- 是否满足对应 Phase Exit Criteria。
- 是否破坏架构不变量。

对于 InkFlow 关键能力，应优先进行真实业务链路验收，而不是只看类和方法是否存在。

## 15. Step 11 — 优化规则

功能正确后可以做本轮范围内的优化，但必须遵守：

- 先测量或有明确质量依据，再做性能优化。
- 优化不得扩大为无关重构。
- 优化导致代码变化后，必须重新执行对应 Build/Test/CI Gate。
- 不为了“更高级”提前引入微服务、Kafka、Kubernetes 等 Non-Goals。

## 16. Step 12 — 文档与状态同步

工作包结束前必须根据实际结果更新：

### `docs/roadmap/progress.md`

至少更新：

- 已完成内容。
- 当前状态。
- Exit Criteria checkbox。
- 未完成项。
- Blocker。
- 实际 Build/Test/CI 结果。

### `docs/handoff/handoff.md`

在阶段性交接、工作方向变化或重要基础能力完成后更新：

- 当前 Commit / Branch。
- 已完成能力。
- 实际验证结果。
- 已知问题。
- 下一工作包。

### ADR / Architecture / Contract

如果行为或决策发生变化，同步更新对应规范。

## 17. 完成 Gate

默认一个编码工作包只有同时满足以下条件才能标记 `Completed`：

- [ ] 工作包目标与范围明确。
- [ ] 实现完成。
- [ ] Diff 已自检，无无关修改和 Secret。
- [ ] 实际 Release Build 成功。
- [ ] 相关 Unit Tests 全绿。
- [ ] 相关 Architecture Tests 全绿。
- [ ] 相关 Integration Tests 全绿。
- [ ] 相关 Contract Tests 全绿。
- [ ] 需要的运行时/业务链路验收通过。
- [ ] 需要的安全检查通过。
- [ ] 目标 Commit/PR 的强制 CI 已真实执行并全绿。
- [ ] CI/测试发现的 Bug 已修复并完成回归。
- [ ] Progress 已更新。
- [ ] 阶段性交接需要时 Handoff 已更新。
- [ ] ADR/Contract/README 等受影响文档已同步。

不适用的 Gate 可以标记 N/A，但必须有合理原因；不能通过大量 N/A 规避验证。

## 18. 明确禁止的完成方式

禁止：

- “代码已经写了，所以完成”。
- “看起来能编译，所以完成”。
- “本地通过，所以 CI 也算通过”。
- CI 还在 Pending 就宣称全绿。
- CI 失败后不读日志直接反复重跑。
- 为通过测试删除失败测试或弱化正确断言。
- 为通过编译关闭 warnings-as-errors，而不解决新引入问题。
- Bug 修复后只跑单个测试，不做必要回归。
- 修改代码后沿用修改前的 Build/Test/CI 结果。
- 文档状态领先于真实代码状态。
- 未验证 Migration 就声称数据库改造完成。
- 未实测 Legado Contract 就声称 Legado 功能完成。

## 19. 每轮交付报告格式

每轮编码结束时，交付摘要至少包含：

```text
工作包：
实现：
验收：
Build：PASS / FAIL / NOT RUN
Tests：PASS / FAIL / PARTIAL / NOT RUN
Runtime：PASS / FAIL / N/A / NOT RUN
CI：GREEN / RED / PENDING / NOT TRIGGERED
发现的问题：
已修复的问题：
剩余风险/Blocker：
Commit：
Progress 更新：Yes / No
Handoff 更新：Yes / No / N/A
下一步：
```

报告必须与实际工具输出一致。

## 20. 与阶段验收的关系

本流程定义“每一轮如何做对”。

阶段 Exit Criteria 定义“累计做到什么程度才能进入下一阶段”。

两者必须同时满足：

- 单轮工作包全部 Completed，不代表 Phase 结束。
- Phase Checklist 勾完，但其中工作包没有真实 Build/Test/CI 证据，也不能结束 Phase。

相关文档：

- `../architecture/invariants.md`
- `../roadmap/progress.md`
- `../roadmap/phase-0-plan.md`
- `../roadmap/phase-1-acceptance.md`
- `../handoff/handoff.md`
