# InkFlow 工程交接文档

> 本文档用于让新的开发者、AI Agent 或未来会话在最少上下文下安全接手 InkFlow。
>
> 交接原则：先理解产品优先级、强制开发流程与架构不变量，再改代码；任何状态描述必须以仓库、测试和 CI 的真实结果为准。

- 产品：墨流 / InkFlow
- 当前阶段：Phase 0 — Foundation
- 当前工作分支：`chore/bootstrap`
- 交接基线日期：2026-08-20
- 当前基线提交：以 `chore/bootstrap` 远端 HEAD 为准；交接记录必须注明实际 Commit。

## 1. 先读这些文档

接手后按顺序阅读：

1. `../product/product-vision.md`
2. `../engineering/development-workflow.md`
3. `../architecture/invariants.md`
4. `../architecture/architecture.md`
5. `../architecture/domain-model.md`
6. `../architecture/source-runtime.md`
7. `../architecture/legado-contract.md`
8. `../architecture/security-model.md`
9. `../roadmap/progress.md`
10. `../roadmap/phase-0-plan.md`
11. `../roadmap/phase-1-acceptance.md`
12. `../product/non-goals.md`
13. `../roadmap/risk-register.md`

其中 `../engineering/development-workflow.md` 是强制执行规范，不是参考建议。

如果这些文档与代码发生冲突：

- 先确认是不是代码尚未完成 Phase 0 重构。
- 不要静默改变架构方向。
- 如果确实需要改变已接受决策，应新增/更新 ADR，并说明原因、替代方案和影响。

## 2. InkFlow 是什么

InkFlow 不是单纯小说爬虫，也不是单纯 Legado 规则生成器。

它是：

> 以 Canonical Content 为核心、以 Legado 和 Web Reader 为主要消费端、支持多来源采集、自动追更、内容选优与开放 API 的小说内容平台。

产品优先级固定为：

1. 阅读 3.0 / Legado。
2. 在线阅读体验。
3. 自动追更。
4. 多源切换 / 容灾。
5. 多站点小说采集。
6. 统一书库。
7. 搜索。
8. 用户书架与阅读历史。

当开发资源冲突时，优先级高的能力优先。

## 3. 当前仓库状态

当前已经完成：

- 私有 GitHub 仓库初始化。
- .NET 10 / ASP.NET Core Bootstrap。
- `InkFlow.Api`、`InkFlow.Worker`、`InkFlow.Scheduler` 基础项目。
- 初始 Domain/Application/Infrastructure/Legado 项目。
- PostgreSQL + Redis Docker Compose。
- GitHub Actions 基础 Build CI。
- Product Vision / Non-Goals。
- Architecture / Invariants / Domain Model。
- Source Runtime / Legado Contract / Security Model。
- Roadmap / Phase 0 Plan / Phase 1 Acceptance / Risk Register。
- Progress Tracker。
- 强制 Development / Build / Test / Runtime / CI / Fix / Regression / Documentation 闭环规范。

当前尚未完成正式 Phase 0 Foundation，因此现有 `src/InkFlow.*` 目录是 Bootstrap 结构，不应被误认为最终目录。

## 4. 下一项工作

下一工作包应是：

> `refactor: establish modular InkFlow foundation`

目标结构：

```text
src/
├── Apps/
│   ├── InkFlow.Api/
│   ├── InkFlow.Worker/
│   ├── InkFlow.Scheduler/
│   └── InkFlow.Migrations/
│
├── BuildingBlocks/
│   ├── InkFlow.BuildingBlocks.Domain/
│   ├── InkFlow.BuildingBlocks.Application/
│   ├── InkFlow.BuildingBlocks.Persistence/
│   ├── InkFlow.BuildingBlocks.Messaging/
│   ├── InkFlow.BuildingBlocks.Security/
│   └── InkFlow.BuildingBlocks.Observability/
│
└── Modules/
    ├── Identity/
    ├── Library/
    ├── Sources/
    ├── Crawling/
    ├── Content/
    ├── Reading/
    ├── Search/
    └── Legado/
```

不要在同一个重构提交中顺便实现完整业务功能。结构变化、Foundation 能力和 Vertical Slice 应保持可审查的提交边界。

## 5. Phase 0 推荐实现顺序

1. 重构 Solution / Directory Layout。
2. 增加 Architecture Tests。
3. 建立 UUIDv7 强类型 ID。
4. 建立 `Result<T>` / `Error` / Problem Details 映射。
5. 统一使用 `TimeProvider`。
6. EF Core + Npgsql。
7. 模块化 DbContext + PostgreSQL Schema。
8. 独立 `InkFlow.Migrations` 应用。
9. Transactional Outbox / Inbox。
10. OpenTelemetry。
11. Testcontainers Integration Tests。
12. 强化 CI。
13. 实际 Build/Test/Docker 验收并修复到全绿。

每完成一项都应更新 `../roadmap/progress.md`，并对该工作包完整执行 `../engineering/development-workflow.md`。

## 6. 关键架构不变量

不得无 ADR 地破坏以下原则：

1. 对外 `BookId` / `ChapterId` 稳定。
2. `SourceBook != CanonicalBook`，`SourceChapter != CanonicalChapter`。
3. 正常阅读路径不得依赖同步实时爬取。
4. 新正文产生新 ContentVersion，不覆盖旧版本。
5. Book Match / Chapter Alignment / Content Selection / Failover 必须可解释、可追溯、可撤销。
6. Legado 是一级协议，有独立 API、Contract、测试和兼容策略。
7. 公共内容和私人内容授权边界严格隔离。
8. Redis 不是关键事实数据的唯一存储。
9. Community Source 不允许无限制任意代码执行。
10. Modular Monolith 优先，禁止无证据提前微服务化。
11. 每一个编码工作包必须经过真实 Build/Test/Runtime/CI/Fix/Regression/Documentation Gate；未通过或未执行的 Gate 不得被口头假定为成功。

完整版本见 `../architecture/invariants.md`。

## 7. 领域所有权

建议保持：

```text
Library
→ CanonicalBook / CanonicalChapter identity、metadata、matching/alignment

Sources
→ Source、Rule、RuleVersion、Credential Reference、Capabilities、Health Policy

Crawling
→ Task、Lease、Retry、DeadLetter、Fetch Artifact、execution

Content
→ Raw/Canonical content、ContentBlob、ContentVersion、Quality、Selection

Reading
→ Reader preference、progress、bookshelf-facing reading state

Legado
→ Protocol DTO、Rule Generator、Compatibility Profile、Legado endpoints

Identity
→ User/session/token/credential identity
```

Crawler/Worker 只负责执行抓取和产生结果，不负责 Canonical Match 或最终 Content Selection。

## 8. Source Runtime 约束

采用 Hybrid Adapter：

```text
ISourceAdapter
├── RuleAdapter   # DSL / 配置，大多数站点
└── CodeAdapter   # 官方可信代码，特殊站点
```

抓取能力分层：

1. HTTP + HTML/JSON。
2. HTTP + Cookie/Session/签名。
3. Playwright。
4. 人工辅助登录/验证后的会话。

Playwright 不是默认抓取方式。

Community Source：

- 只能使用受限 DSL。
- 必须经过 SafeHttpClient / SSRF 防护。
- 必须有 Request/Bytes/Time/Regex 等资源上限。
- 不允许任意 Shell/C#/JS eval/文件/Socket 权限。

## 9. Legado 约束

官方主路径固定为：

```text
阅读 3.0
→ InkFlow 官方 bookSource
→ /api/legado/v1/*
→ Canonical Content
```

第三方原生书源属于高级/备用能力，不得成为 InkFlow 主链路依赖。

计划中的最小 API：

```text
GET /api/legado/v1/search?q=
GET /api/legado/v1/books/{bookId}
GET /api/legado/v1/books/{bookId}/chapters
GET /api/legado/v1/chapters/{chapterId}
GET /legado/book-source.json
```

规则由 `ILegadoRuleGenerator` 生成，不维护一份长期手改静态 JSON 作为唯一事实来源。

Legado Contract Test 是未来 Release Gate：失败时禁止生产发布。

## 10. 数据与一致性约束

- PostgreSQL 是核心事实数据来源。
- Redis 用于缓存、调度加速、限流等可重建状态。
- Task Source of Truth 在 PostgreSQL，Redis 不是唯一 Queue State。
- 采用 Transactional Outbox。
- Consumer 使用 Inbox / Idempotency。
- 事件采用 At-Least-Once + Idempotent Consumer，而不是假设 Exactly Once。
- EF Core 生产 Migration 不允许由 API 启动时自动执行。
- 生产 Schema 变更遵循 Expand → Migrate → Contract。

## 11. 安全约束

Source 执行是高风险边界。至少保持：

- SSRF 防护，包括 DNS Rebinding、Redirect 重新校验、IPv4/IPv6 私网与 Metadata Endpoint 阻断。
- Crawler / Browser Worker 与核心业务网络隔离。
- Secret 只通过引用传递，不把 Source Credential 明文放进任务 Payload。
- Refresh Token、Legado Token、API Key 数据库只存 Hash + Prefix。
- API Rate Limit 区分 IP/User/Legado Token/API Key/Organization。
- 不提供任意 URL 公共代理接口。
- 上游 HTML 经过 Sanitize/Canonical AST 后才能给 Web Reader。
- 私有 Blob 即使 Hash 与公共 Blob 相同，也不能绕过 Ownership/Authorization。

## 12. 禁止提前做

当前阶段不要引入：

- 微服务拆分。
- Kubernetes。
- Kafka。
- Multi-Region Active/Active。
- 重社交、论坛、私信。
- 大型 AI 推荐系统。
- 自动 CAPTCHA 破解。
- 任意 Community Plugin 代码执行。
- 自研搜索引擎。
- 为了“以后可能会大”而提前 Sharding。

详见 `../product/non-goals.md`。

## 13. 测试策略

Phase 0/1 应建立：

```text
Unit Tests
Architecture Tests
Integration Tests (Testcontainers PostgreSQL/Redis)
Contract Tests
Crawler Fixture Tests
少量 E2E
```

真实第三方 Source 不应成为普通 PR CI 的强依赖。

真实 Source 检查应放到独立 Live/Nightly Pipeline。

## 14. 每轮编码的强制闭环

任何可验收工作包必须执行：

```text
明确工作包/验收条件
→ 实现
→ Diff 自检
→ 实际 Restore/Build
→ Unit/Architecture/Integration/Contract Tests（按范围）
→ Runtime/业务链路验收
→ Security/Architecture 检查
→ Candidate Commit
→ 实际 CI
→ 失败则读取日志、定位根因、修复
→ 局部验证
→ 全量回归
→ CI 重跑
→ 功能验收
→ 必要优化
→ 如果代码变化则再次 Build/Test/CI
→ Progress/Handoff/ADR/Contract 同步
→ Accepted / Completed
```

严禁：

- 把 `Implemented` 写成 `Completed`。
- 把 `CI Not Triggered/Pending` 写成 `CI Green`。
- CI 失败后只反复重跑而不定位根因。
- 修改代码后沿用修改前的验证结果。
- 为了变绿删除测试、弱化正确断言或隐藏警告。

完整规范、状态定义、Gate 和交付报告模板见 `../engineering/development-workflow.md`。

## 15. 接手前检查清单

接手者开始写代码前：

- [ ] 确认当前分支和 HEAD。
- [ ] 阅读 `../engineering/development-workflow.md`。
- [ ] 阅读 `../roadmap/progress.md`，确认真实阶段。
- [ ] 阅读 Architecture Invariants。
- [ ] 检查当前仓库 diff，不覆盖他人未合并工作。
- [ ] 明确当前工作是否属于 Phase 0 / Phase 1。
- [ ] 为本工作包定义目标、范围、非目标、验收条件和验证计划。
- [ ] 如果要改公共 API/Legado Contract，先检查兼容性影响。
- [ ] 如果要改领域边界，先检查是否需要 ADR。

## 16. 工作完成前检查清单

每次交接前至少：

- [ ] 本工作包状态使用规范术语记录。
- [ ] `dotnet restore` 结果已记录。
- [ ] `dotnet build -c Release` 结果已记录。
- [ ] `dotnet test -c Release` 结果已记录。
- [ ] 相关 Architecture/Integration/Contract Test 结果已记录。
- [ ] Runtime/业务链路验收结果已记录，或明确 N/A 原因。
- [ ] CI 状态已确认；没有运行就明确写 `NOT TRIGGERED`，Pending 就明确写 `PENDING`。
- [ ] CI 失败项已读取日志并完成根因修复/回归，或明确记录 Blocker。
- [ ] Migration 影响已说明。
- [ ] 新增风险/技术债已写入文档或 Issue。
- [ ] `progress.md` 已更新。
- [ ] 本文档的“当前状态/下一项工作”在阶段性交接时已更新。
- [ ] Commit message 清晰描述逻辑工作包。
- [ ] 所有声称 `Completed` 的工作包都满足 `development-workflow.md` 的适用 Gate。

## 17. 每轮交付摘要模板

```text
工作包：
状态：Planned / In Progress / Implemented / Locally Validated / CI Green / Accepted / Completed / Blocked
实现：
验收：
Build：PASS / FAIL / NOT RUN
Tests：PASS / FAIL / PARTIAL / NOT RUN
Runtime：PASS / FAIL / N/A / NOT RUN
CI：GREEN / RED / PENDING / NOT TRIGGERED
发现的问题：
已修复的问题：
剩余风险/Blocker：
Commit/PR：
Progress 更新：Yes / No
Handoff 更新：Yes / No / N/A
下一步：
```

报告必须与实际工具输出一致。

## 18. 交接记录模板

后续每次阶段性交接，在本文档底部追加简短记录：

```text
## Handoff YYYY-MM-DD

Branch:
HEAD:
Completed:
Validated:
Not validated:
Known issues:
Next task:
Do not change without ADR:
```

不要删除旧的重大交接记录；可以把非常旧的记录归档到 `docs/handoff/archive/`。

## 19. Handoff 2026-08-20

Branch: `chore/bootstrap`

HEAD at initial handoff creation: `e5da0b8e696ac9c16d2bffc176bf88237ac998c8`

Completed:

- Repository bootstrap。
- Grill Me 产品/架构对齐。
- 正式 Product/Architecture/Roadmap/Security 文档。
- 项目 Progress Tracker。
- 工程 Handoff 文档。
- 强制每轮开发/编译/测试/验收/CI/修复/回归流程规范。

Validated:

- 远端 `chore/bootstrap` 分支存在并承载 Bootstrap + 文档工作。
- `main` 保持初始仓库提交，未擅自合并。
- 强制流程已进入 README、Architecture Invariants、Progress 和 Handoff。

Not validated yet:

- Phase 0 最终结构下的本地/远端完整 `dotnet build`。
- Phase 0 `dotnet test`。
- EF Migration。
- Outbox/Inbox。
- Docker 全栈运行验收。
- 强化后的 GitHub Actions CI。

Known issues:

- 当前代码仍是 Bootstrap 目录结构。
- 当前 CI 仅为基础 Restore/Build，不代表 Phase 0 已通过。

Next task:

> 重构为 `Apps / BuildingBlocks / Modules` 并开始 Phase 0 Foundation；每个工作包严格按 `development-workflow.md` 的 Build → Test → Runtime → CI → Fix → Regression → Validate → Docs 循环推进。

Do not change without ADR:

- Canonical Book/Chapter 核心模型方向。
- Content Version 不覆盖策略。
- Legado 一级协议定位。
- Modular Monolith 优先。
- Community Source 沙箱与 SSRF 安全边界。
