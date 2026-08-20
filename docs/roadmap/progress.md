# InkFlow 项目进度

> 持续进度账本。状态只以真实代码、测试、Runtime 和 CI 结果为准。

- 产品：墨流 / InkFlow
- 当前阶段：Phase 1A — Single Source Vertical Slice
- 当前工作分支：`feat/phase-0-foundation`（Phase 0 验收收尾，合并后从 `main` 创建 Phase 1 分支）
- 最后更新日期：2026-08-20

## 1. 总体状态

| 阶段 | 状态 | 说明 |
| --- | --- | --- |
| Grill Me / 产品与架构对齐 | ✅ Completed | 产品定位、核心领域、Legado、Source Runtime、安全、商业化和路线已文档化 |
| Repository Bootstrap | ✅ Completed | .NET 10 基础仓库与最初 CI 已建立 |
| Phase 0 — Foundation | ✅ Completed | 模块边界、Persistence、Migration、Outbox/Inbox、OTel、测试与 Runtime CI Gate 已验收 |
| Phase 1A — Single Source Vertical Slice | 🚧 In Progress | Source DSL/Adapter → Crawl → Canonical → Content → API/Legado/Web |
| Phase 1B — Dual Source Validation | ⏳ Not Started | 双来源匹配、内容版本、质量选优、故障切源 |
| Phase 2 — Multi-Source Production | ⏳ Not Started | 多源生产化、健康评分、自适应追更、规则 Canary |
| Phase 3 — User Product | ⏳ Not Started | Web Reader、账号、书架、历史、私人书库 |
| Phase 4 — Commercial Platform | ⏳ Not Started | Entitlement、Developer API、Billing、Organization、Community Source |

## 2. Phase 0 验收记录

### 2.1 模块化 Foundation

已完成：

- 目录重构为 `Apps / BuildingBlocks / Modules / tests`。
- API / Worker / Scheduler / Migrations 独立 Host。
- UUIDv7、`Result<T>`、`TimeProvider` 基础。
- Central Package Management。
- Architecture Tests 强制模块依赖矩阵。
- 业务模块禁止依赖 Application Host 与 Persistence 实现。

### 2.2 Persistence / Messaging

已完成：

- EF Core + Npgsql。
- PostgreSQL 模块化 DbContext。
- Schema：`identity / library / sources / crawler / content / reading / messaging`。
- 独立 `InkFlow.Migrations`。
- Transactional Outbox / Inbox。
- Inbox 唯一约束与消费幂等。

### 2.3 Observability / Runtime / CI

已完成：

- OpenTelemetry traces / metrics / runtime instrumentation 基础。
- Testcontainers PostgreSQL 18 Integration Tests。
- Docker Compose：PostgreSQL / Redis / Migration / API / Worker / Scheduler。
- API / Worker / Scheduler `/health`。
- 容器使用 read-only filesystem、`no-new-privileges`、drop capabilities 等约束。
- CI：Restore → Release Build → Tests → Compose Validation → Runtime Smoke。

### 2.4 实际验证证据

最近 Phase 0 技术验收 HEAD：`e0a2b3cebfc0aac8895555427f7cc172df2d3f37`。

GitHub Actions CI Run `32383751475`：**GREEN**。

验证结果：

- Restore：PASS
- Release Build：PASS，0 warnings / 0 errors
- Unit Tests：PASS
- Architecture Tests：PASS
- Integration Tests：PASS
- Contract Tests：PASS
- PostgreSQL 18 Migration：PASS
- Outbox transaction：PASS
- Inbox idempotency：PASS
- Compose Validation：PASS
- Runtime Smoke：PASS
- API `/health`：PASS
- Worker `/health`：PASS
- Scheduler `/health`：PASS

Phase 0 开发过程中真实发现并修复：

1. Npgsql 10.0.3 要求 EF Core `>= 10.0.4`，统一依赖版本后解决 restore downgrade。
2. OpenTelemetry DI extension namespace 缺失导致 Build 失败，补齐引用。
3. MSTest Analyzer 在 warnings-as-errors 下阻止恒真 smoke assertion，改为真实验证。
4. PostgreSQL 18 官方镜像改变 volume layout，卷从 `/var/lib/postgresql/data` 修正到 `/var/lib/postgresql`。

## 3. Phase 0 Exit Criteria

- [x] `dotnet restore` 成功。
- [x] `dotnet build -c Release` 成功且 warnings-as-errors 无警告。
- [x] `dotnet test -c Release` 全绿。
- [x] Architecture Tests 全绿。
- [x] PostgreSQL / Redis 可通过 Docker Compose 启动并通过健康检查。
- [x] Migration App 可对空数据库完成初始化。
- [x] API、Worker、Scheduler 可启动并通过健康检查。
- [x] Outbox / Inbox Integration Test 通过。
- [x] OpenTelemetry 基础 instrumentation 已接入。
- [x] CI 在 Phase 0 PR 上全绿。
- [x] README、Progress、Handoff 与实际状态同步。

**Phase 0：Accepted / Completed。**

## 4. 当前正在做 — Phase 1A

按以下顺序推进，每一项继续执行完整工程闭环：

1. Source DSL v1 与校验模型。
2. `RuleAdapter` 与 Fixture 驱动执行器。
3. Safe HTTP / SSRF 基础防线、请求预算与错误分类。
4. Crawler Task / Lease / Retry / DeadLetter。
5. SourceBook / SourceChapter 持久化。
6. Canonical Book 创建与 Match Candidate 基础。
7. Canonical Chapter / Chapter Mapping。
8. Content AST / ContentVersion / ContentHash。
9. 最小 Quality Engine 与 Selected Version。
10. Public API：Search / Book / TOC / Chapter。
11. Legado v1 API Contract。
12. `ILegadoRuleGenerator` 与 `/legado/book-source.json`。
13. Web Reader 最小纵向体验。
14. 单来源自动追更链路。
15. Phase 1A E2E / Contract / Runtime 验收。

## 5. Phase 1A 核心验收链路

```text
Official Source
→ Search
→ SourceBook
→ Canonical Book
→ SourceChapter / TOC
→ Canonical Chapter
→ Chapter Content
→ ContentVersion / Selected Version
→ Public API
→ Web Reader
→ Legado bookSource
→ Legado Search / TOC / Content
→ 自动追更
```

不得依赖人工直接修改数据库或手工拼生产数据。

## 6. 已知未完成项

- Source DSL v1 尚未实现。
- Crawler Task Lease / Retry / DeadLetter 尚未实现。
- Canonical Matching / Chapter Alignment 尚未实现。
- Content AST / ContentVersion / Quality Engine 尚未实现。
- 正式 Legado API Contract 与 Rule Generator 尚未实现。
- Web Reader 尚未实现。
- 用户身份、书架、阅读历史尚未进入产品实现阶段。
- Developer API / Plan / Entitlement / Billing / Organization 尚未实现。

## 7. 当前阻塞

当前无已知产品级阻塞。

## 8. 强制维护规则

每完成一个工作包至少记录：Build、Tests、Runtime、CI、验收结果、发现/修复的 Bug、剩余风险、Commit/PR。

禁止把 `Implemented` 当作 `Completed`；CI Pending/Red 时不得声称已验收；修改代码后必须重新执行适用 Gate。

完整流程见 `../engineering/development-workflow.md`。
