# InkFlow 项目进度

> 持续进度账本。状态只以真实代码、测试、Runtime 和 CI 结果为准。

- 产品：墨流 / InkFlow
- 当前阶段：Phase 1A — Single Source Vertical Slice
- 当前工作分支：`dev`（2026-08-25 起）
- 最后更新日期：2026-08-25

## 1. 总体状态

| 阶段 | 状态 | 说明 |
| --- | --- | --- |
| Grill Me / 产品与架构对齐 | ✅ Completed | 产品定位、核心领域、Legado、Source Runtime、安全、商业化和路线已文档化 |
| Repository Bootstrap | ✅ Completed | .NET 10 基础仓库与最初 CI 已建立 |
| Phase 0 — Foundation | ✅ Completed | 模块边界、Persistence、Migration、Outbox/Inbox、OTel、测试与 Runtime CI Gate 已验收 |
| Phase 1A — Single Source Vertical Slice | 🚧 In Progress | Source DSL/Adapter → Crawl → Canonical → Content → API/Legado/Web。已完成的 DSL/Crawler 工作包实现暂存于 `main` 历史，`dev` 上按原顺序重建 |
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

> **分支说明（2026-08-25）**：项目已切换到 `dev` 分支重新起步，`dev` 为唯一开发主线，完成后经 PR 合入 `main`。`dev` 目前仅包含基础设施骨架（见第 8 节）；Phase 1A 各工作包按原顺序与设计文档在 `dev` 上重新推进。

按以下顺序推进，每一项继续执行完整工程闭环：

1. ✅ Source DSL v1 与校验模型。（已实现，本地验证通过）
2. ✅ `RuleAdapter` 与 Fixture 驱动执行器。（已实现，本地验证通过）
3. 🚧 Safe HTTP / SSRF 基础防线、请求预算与错误分类。（当前）
4. ✅ Crawler Task / Lease / Retry / DeadLetter。（已实现）
5. ⏳ SourceBook / SourceChapter 持久化。
6. ⏳ Canonical Book 创建与 Match Candidate 基础。
7. ⏳ Canonical Chapter / Chapter Mapping。
8. ⏳ Content AST / ContentVersion / ContentHash。
9. ⏳ 最小 Quality Engine 与 Selected Version。
10. ⏳ Public API：Search / Book / TOC / Chapter。
11. ⏳ Legado v1 API Contract。
12. ⏳ `ILegadoRuleGenerator` 与 `/legado/book-source.json`。
13. ⏳ Web Reader 最小纵向体验。
14. ⏳ 单来源自动追更链路。
15. ⏳ Phase 1A E2E / Contract / Runtime 验收。

### 4.1 Phase 1A 已完成工作包记录

**Source DSL v1 与校验模型**（`dev` @ `1f01918`）：

- `InkFlow.Modules.Sources.Domain`：`SourceCapability`、`RuleHttpMethod`、`RuleRequest`（路径模板 `{name}` 占位符 + Header/Query/Form）、`RuleSelector`（CSS/XPath/JSONPath）、`RuleRegex`（强制正超时，上限 2000ms）、`TrimTransform`/`ReplaceTransform`、`RuleField`、`CapabilityRule`、`SourceRuleDsl`。
- `SourceRuleDslValidator`：一次性返回全部违规——schemaVersion 固定为 "1"、sourceId 非空且无空白、能力规则不重复、路径模板以 `/` 开头且占位符合法、POST 必须带 Form、字段名唯一、每字段恰好一个抽取来源、replace 的 from 非空。
- 校验为纯声明检查：不触网、不执行正则。
- UnitTests 新增 16 个校验用例；Release Build 0 warnings / 0 errors；全部测试通过。

**Safe HTTP / SSRF 防线**（`dev` @ `5f3ae4b`）：

- `InkFlow.BuildingBlocks.Security`：`IpAddressClassification`（loopback/RFC1918/link-local 含云 metadata/CGNAT/ULA/组播/IPv4-mapped-IPv6 判定）、`IIpAddressResolver` 抽象 + DNS 实现、`SsrfGuard` 两阶段校验。
- 字面量检查：仅允许 http/https、端口白名单 80/443、拒绝字面内网 IP 与纯数字混淆主机；字面量失败短路，不发起任何网络请求。
- 解析后复检：DNS 全部结果逐一遍过网段判定，防 rebinding（执行器须配合使用已验证地址连接）。
- 新增 18 个单元测试覆盖阻断网段、scheme/port 规则与 DNS 路径；Release Build 0 warnings / 0 errors。

**RuleAdapter 与 Fixture 驱动执行器**（`dev` @ `6efdfcf`，CI Run `32834072324` GREEN）：

- `Sources.Application`：`ISourceHttpClient` / `ISelectorEvaluator` 抽象（生产 SafeHttpClient 与选择器引擎后续以适配器接入）、`RuleAdapter` 执行管道。
- 执行顺序固定：URL 构建（路径模板 + Query/Form 占位符填充、正确编码）→ `SsrfGuard` 字面量校验（内网目标拒绝且绝不出网）→ 发请求 → 状态码检查 → 字段抽取 → Trim/Replace 变换。
- 失败分类：模板变量缺失 / SSRF 拒绝 / 传输异常 / 上游状态码 / 字段抽取失败（含正则超时）；任一环节失败即整体失败，不产生部分结果。
- 正则抽取强制使用声明超时；灾难性回溯模式被报告为超时而非挂起。
- 9 个 Fixture 驱动测试全程内存执行，普通 CI 无真实第三方流量；Release Build 0 warnings / 0 errors，UnitTests 44 用例全绿。

**Crawler Task / Lease / Retry / DeadLetter**（已完成，待在 `dev` 重建）：

- Domain：`CrawlerTask` 状态机、`CrawlerTaskStatus`。
- Application：`CrawlerLeaseService`、`RetryPolicy`、`DeadLetterTask`、`ICrawlerTaskExecutor`。
- Infrastructure：`CrawlerTaskRepository` 契约 + EF 实体/配置/`CrawlingDbContext`/`DeadLetterEntity` 骨架。

**Source DSL v1 与校验模型**（本轮）：

- `InkFlow.Modules.Sources.Domain`：`SourceCapability`、`RuleHttpMethod`、`SelectorKind`/`RuleSelector`、`RuleRegex`（强制 Timeout）、`RuleTransform`（Replace/Trim）、`RuleField`、`RuleRequest`、`CapabilityRule`、`SourceRuleDsl`（含 `schemaVersion`）。
- `SourceRuleDslValidator`：结构与安全校验（schemaVersion 受支持、SourceId 必填、能力/字段唯一、正则必须正 Timeout、字段必须有抽取来源、Replace 必须有 from/to）。
- 非目标（留待后续）：JSON 序列化、Safe HTTP/SSRF、RuleVersion 发布流程。
- 验证：Release Build 0 warnings / 0 errors；Unit Tests 通过；Architecture Tests 3 通过。
- CI：本地验证通过，远端 CI 未触发（`CI Pending / Not Triggered`）。

**RuleAdapter 与 Fixture 驱动执行器**（本轮）：

- `InkFlow.Modules.Sources.Application`：`RuleAdapter`（URL 模板替换、请求构建、regex 提取 + Timeout、selector 抽象、Replace/Trim 变换）、`ISourceHttpClient`、`ISelectorEvaluator`、`SourceHttpRequest`/`SourceHttpResponse`、`CapabilityResult`。
- 执行通过 `ISourceHttpClient` 抽象（生产为 SafeHttpClient，测试用内存 Fixture），普通 PR CI 不依赖真实第三方站点。
- 非目标（留待后续）：CSS/XPath/JSONPath 具体引擎、Safe HTTP/SSRF。
- 验证：Release Build 0 warnings / 0 errors；Unit Tests 24 通过；Architecture Tests 3 通过。
- CI：本地验证通过，远端 CI 未触发（`CI Pending / Not Triggered`）。

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

- CSS/XPath/JSONPath 具体 selector 引擎尚未实现。
- Safe HTTP / SSRF 基础防线尚未实现。
- SourceBook / SourceChapter 持久化尚未实现。
- Canonical Matching / Chapter Alignment 尚未实现。
- Content AST / ContentVersion / Quality Engine 尚未实现。
- 正式 Legado API Contract 与 Rule Generator 尚未实现。
- Web Reader 尚未实现。
- 用户身份、书架、阅读历史尚未进入产品实现阶段。
- Developer API / Plan / Entitlement / Billing / Organization 尚未实现。

## 7. 当前阻塞

当前无已知产品级阻塞。

## 8. dev 分支骨架重建记录（2026-08-25）

`dev` 是当前唯一开发主线（root commit `c5f2048`）；业务代码在本骨架上按路线图重新实现。

### 8.1 本轮完成

- CI 触发扩展：`push` / `pull_request` 覆盖 `main` + `dev`，保留 `workflow_dispatch`。
- 重建 solution 全部 22 个项目骨架（4 Apps / 6 BuildingBlocks / 8 Modules / 4 Tests），目录与 `InkFlow.sln` 引用一致，项目引用关系遵循 `architecture/architecture.md` 的模块依赖矩阵。
- 全部源码为 `dev` 上重新编写（宿主 `/health` 探针、Observability 扩展 `ObservabilitySetup`、四个测试项目的守卫用例），**未从 `main` 复制任何代码**。
- 新增仓库级 `nuget.config`：CPM 下固定单一 nuget.org 源，解决本机多源导致的 `NU1507`，同时消除对开发者全局源配置的依赖。
- 移除 `src/`、`tests/` 旧代码；业务实现待按路线图重建。

### 8.2 验证证据

```text
dotnet restore InkFlow.sln            PASS
dotnet build -c Release               PASS (0 warnings / 0 errors)
dotnet test -c Release                PASS (Unit/Architecture/Integration/Contract 各 1 用例)
```

远端 CI（Run `32821162412`）：**GREEN**，Restore → Release Build → Tests → Compose Validation → Runtime Smoke 全部通过，API / Worker / Scheduler `/health` 验证 OK。

### 8.3 剩余风险

无已知阻塞；后续工作包按第 4 节顺序在 `dev` 上推进，每个工作包维持完整 Gate 闭环。

## 9. 强制维护规则

每完成一个工作包至少记录：Build、Tests、Runtime、CI、验收结果、发现/修复的 Bug、剩余风险、Commit/PR。

禁止把 `Implemented` 当作 `Completed`；CI Pending/Red 时不得声称已验收；修改代码后必须重新执行适用 Gate。

完整流程见 `../engineering/development-workflow.md`。
