# InkFlow — Codex 工作约定

本文件适用于仓库根目录及所有子目录。处理任何代码、测试、数据库、Source、Legado、API、UI、CI、Docker、部署或相关文档任务前，先读取本文件；随后按任务分支读取下列权威文档。

## 权威文档

- 所有工程改动：`docs/engineering/development-workflow.md`
- 架构、模块边界、数据不变量：`docs/architecture/architecture.md`、`docs/architecture/invariants.md`、`docs/architecture/domain-model.md`
- Source、采集运行时、Legado、内容与安全：按需读取 `docs/architecture/source-runtime.md`、`docs/architecture/legado-contract.md`、`docs/architecture/security-model.md`
- 用户可见前端：实现前读取 `docs/engineering/frontend-design.md`
- 阶段状态与交接：需要更新时读取 `docs/roadmap/progress.md`、`docs/handoff/handoff.md`

这些文档是单一事实来源；本文件只保留执行入口和不可遗漏的门槛，不复制其完整内容。

## 强制工作流

1. 先明确目标、范围、非目标、验收条件、风险和验证计划。涉及已接受架构方向的变化时，先新增或更新 ADR。
2. 先检查相关代码、测试、配置和文档。仓库存在 `.codegraph/` 时，理解或定位代码前优先使用 `codegraph explore`；再按需使用文本搜索。
3. 做最小、可审查的修改，修复根因；不混入无关重构。新增或改变业务行为必须补充相应测试。
4. 编译前检查实际 diff：确认范围、模块依赖、公共 Contract、Migration、文档同步正确，并确认没有 Secret、Token、Cookie、密码或临时代码。
5. 真实执行适用的验证：
   - 默认：`dotnet restore InkFlow.sln`、`dotnet build InkFlow.sln -c Release`、`dotnet test InkFlow.sln -c Release`
   - 领域逻辑：Unit Tests；模块依赖：Architecture Tests
   - PostgreSQL、Redis、Migration、消息、任务或持久化：Integration Tests；需要 Docker 时不得假定环境已验证
   - Legado、API 或其他稳定外部协议：Contract Tests
   - Bug 修复：先建立可复现回归测试，再修复并回归
   - 运行时改动：按范围验证 Compose、Migration、Health、API/Worker/Scheduler 或真实业务链路
6. 对外部输入和调用保持安全、超时、重试退避、幂等与可追溯；高风险改动完成安全和架构检查后再交付。
7. 需要远端验证时创建可审查的 candidate commit，并确认目标 CI 真实运行且所有强制 Job 通过。CI Pending、未触发或失败时，不得称为完成。
8. 代码变化后重新执行受影响的 Build/Test/Runtime/CI；工作结束时按实际证据更新 Progress、Handoff、ADR、Contract 或其他受影响文档。

## 不可违反的不变量

- 对外 `BookId` / `ChapterId` 长期稳定；`SourceBook` / `SourceChapter` 与 Canonical 实体分离。
- 阅读路径读取已入库的 Canonical Content，不实时依赖第三方站点；正文版本新增而非覆盖历史版本。
- Canonical Match、章节对齐、内容选择和 Source Failover 必须可解释、可追溯、可撤销。
- Redis、缓存、Projection、Search Index 均不是关键事实的唯一来源，且应可重建。
- Domain 不依赖 EF Core、ASP.NET、Redis 等基础设施；模块保持既定依赖方向，优先模块化单体。
- Source URL 不是业务主键；Published Rule 与 Parser/Normalizer 版本不可变，变更产生新版本。
- Community Source 运行在受限 DSL/Sandbox；公共 API 默认保持向后兼容；生产 Migration 由独立流程执行。

## 完成定义与交付报告

`Implemented`、本地某个测试通过或代码看起来可编译，都不等于 `Completed`。只有适用的 Build、Tests、Runtime、Security、CI、Regression、Documentation 和功能验收均有真实证据后，才可标记 `Accepted / Completed`；未执行项必须明确写 `NOT RUN` 或 `BLOCKED` 及原因。

每次交付至少报告：

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
文档同步：
下一步：
```
