# InkFlow 工程交接文档

> 用于开发者、AI Agent 或未来会话快速、安全接手 InkFlow。真实状态以仓库与 CI 为准。

- 产品：墨流 / InkFlow
- 当前阶段：Phase 1A — Single Source Vertical Slice
- 当前工作分支：`dev`（2026-08-25 起）
- `dev` 骨架 root commit：`c5f2048`
- 交接日期：2026-08-21；dev 骨架重建更新：2026-08-25

## 1. 接手顺序

1. `../product/product-vision.md`
2. `../engineering/development-workflow.md`
3. `../architecture/invariants.md`
4. `../architecture/architecture.md`
5. `../architecture/domain-model.md`
6. `../architecture/source-runtime.md`
7. `../architecture/legado-contract.md`
8. `../architecture/security-model.md`
9. `../roadmap/progress.md`
10. `../roadmap/phase-1-acceptance.md`
11. `../roadmap/risk-register.md`

`development-workflow.md` 是强制规范。

## 2. 产品定位

InkFlow 是以 Canonical Content 为核心、以 Legado 与 Web Reader 为主要消费端、支持多来源采集、自动追更、内容选优和开放 API 的小说内容平台。

固定产品优先级：

1. Legado
2. Web 阅读
3. 自动追更
4. 多源容灾
5. 多站点采集
6. 统一书库
7. 搜索
8. 书架/阅读历史

## 3. 当前真实仓库状态

**分支模型（2026-08-25 起）**：

- `dev`：当前唯一开发主线。仅包含基础设施骨架，业务代码按路线图重新实现，完成后经 PR 合入 `main`。
- 历史实现不迁移到 `dev`；已完成工作包的设计记录以 `../roadmap/progress.md` 第 4.1 节为准，落地时在 `dev` 上重新编写。

`dev` 骨架（root commit `c5f2048`）已重建并通过本地验证：

- `src/Apps`：API / Worker / Scheduler / Migrations（`/health` 探针骨架）。
- `src/BuildingBlocks`：Domain / Application / Persistence / Messaging / Security / Observability。
- `src/Modules`：Identity / Library / Sources / Crawling / Content / Reading / Search / Legado。
- Unit / Architecture / Integration / Contract 四个测试项目各含守卫用例。
- Central Package Management + 仓库级 `nuget.config`（单一 nuget.org 源）。
- Docker Compose 与 `deploy/docker/*.Dockerfile` 原样保留。
- CI 触发覆盖 `main` + `dev`。

`dev` 本地验证证据：

```text
Restore: PASS
Release Build: PASS (0 warnings / 0 errors)
Unit: PASS
Architecture: PASS
Integration: PASS
Contract: PASS
Compose validation: PASS
Runtime smoke: PASS
CI: GREEN (Run 32821162412)
```

## 4. 下一工作包

**Phase 1A 收尾状态（2026-08-27 更新）**：真实源端到端验证已通过——kanunu8 作为第一个 Official Source,经 `EndToEndDataFlowTests` 完成真实数据全链路验证(导入 → 目录 → 匹配 → 映射 → 发布 → 查询)。剩余外部依赖仅剩 **Legado 真机导入验证**(需阅读 3.0 客户端)。

1. ~~真实 Official Source 接入~~ ✅ kanunu8 CodeAdapter(`KanunuSourceAdapter`),GB18030 解码 + SSRF 校验,live 测试 3/3。
2. **Legado 真机验证**：在阅读 3.0 中导入 `/legado/book-source.json`,验证搜索/详情/目录/正文四步(契约端点已过容器 smoke)。
3. **追更真实验证**：Scheduler 扫描 + Worker 消费已在容器环境运行,新章检测需真实源数据佐证。

推荐顺序（当前进度）：

```text
✅ 真实源适配器(kanunu8)+ E2E 数据流验证
→ Legado 真机接入验证（需阅读 3.0 客户端）
→ 追更真实验证
→ Phase 1A Completed
→ Phase 1B: Dual Source Canonical Validation
```

扩展新来源的方式(书源兼容层):
- 规则型站点:在 sources 表登记含 RuleDsl 的 Source 记录,零代码;
- 复杂站点(特殊编码/签名):实现 `ISourceAdapter`(参考 `KanunuSourceAdapter`)并在适配器工厂注册。

普通 PR CI 不依赖真实第三方小说站点；Crawler 使用固定 Fixture/Mock Server。真实 Source 进入独立 Live/Nightly 检查。

## 5. 关键架构不变量

未经 ADR 不得破坏：

1. 对外 BookId / ChapterId 稳定。
2. `SourceBook != CanonicalBook`，`SourceChapter != CanonicalChapter`。
3. 正常阅读路径不得依赖同步实时爬取。
4. 新正文创建新 ContentVersion，不覆盖旧正文。
5. Match / Alignment / Selection / Failover 必须可解释、可追踪、可撤销。
6. Legado 是一级协议，有独立 Contract 与测试。
7. 公共与私人内容授权严格隔离。
8. Redis 不是关键事实数据唯一存储。
9. Community Source 禁止无限制代码执行。
10. Modular Monolith 优先，不提前微服务化。
11. 每个工作包必须经过真实 Build/Test/Runtime/CI/Fix/Regression/Documentation Gate。

## 6. Source Runtime 约束

```text
ISourceAdapter
├── RuleAdapter   # DSL / 配置，大多数站点
└── CodeAdapter   # 仅可信官方复杂适配
```

抓取层级：HTTP → Session/签名 → Playwright → 人工辅助会话。

Community Source 必须受限 DSL，并通过 SafeHttpClient；禁止任意 Shell/C#/JS eval、文件、Socket 权限。

安全至少覆盖：

- SSRF，包括 DNS rebinding / redirect 再校验。
- IPv4/IPv6 私网、loopback、link-local、metadata endpoint 阻断。
- Request / Bytes / Time / Regex 预算。
- Credential 只传引用，不放入 Task Payload。

## 7. 领域所有权

```text
Library  → CanonicalBook / CanonicalChapter / matching / alignment
Sources  → Source / Rule / RuleVersion / Capability / Health Policy
Crawling → Task / Lease / Retry / DeadLetter / Fetch Artifact
Content  → AST / ContentBlob / ContentVersion / Quality / Selection
Reading  → Reader preference / progress / bookshelf-facing state
Legado   → Protocol DTO / Rule Generator / Compatibility Profile
Identity → User / Session / Token / Credential identity
```

Crawler 只执行抓取并产出结果，不拥有 Canonical Match 或最终 Content Selection。

## 8. Legado 主路径

```text
阅读 3.0
→ InkFlow 官方 bookSource
→ /api/legado/v1/*
→ Canonical Content
```

最小目标 API：

```text
GET /api/legado/v1/search?q=
GET /api/legado/v1/books/{bookId}
GET /api/legado/v1/books/{bookId}/chapters
GET /api/legado/v1/chapters/{chapterId}
GET /legado/book-source.json
```

规则由 `ILegadoRuleGenerator` 生成，不长期手改静态 JSON 作为唯一事实来源。

## 9. 数据与一致性

- PostgreSQL 是事实数据来源。
- Redis 仅承载可重建状态。
- Crawler Task Source of Truth 在 PostgreSQL。
- Outbox + At-Least-Once + Inbox/Idempotent Consumer。
- 生产 Migration 由独立 Migrations App 执行，API 不自动迁移。
- Schema 变更遵循 Expand → Migrate → Contract。

## 10. 当前未完成

Phase 1A：

- CSS/XPath/JSONPath 具体 selector 引擎
- Safe HTTP / SSRF enforcement
- SourceBook / SourceChapter
- Canonical matching / chapter alignment
- Content AST / ContentVersion / Quality selection
- Public Content API
- Legado v1 Contract / Rule Generator
- Web Reader
- Auto update vertical slice

更后阶段：Identity product、Bookshelf、History、Local Import/Export、Developer API、Entitlement、Billing、Organization、Community Marketplace、Enterprise Deployment。

## 11. 每轮强制闭环

```text
明确目标/验收
→ 实现
→ Diff 自检
→ Restore/Build
→ Unit/Architecture/Integration/Contract Tests
→ Runtime/业务链路验收
→ Security/Architecture 检查
→ Candidate Commit
→ 实际 CI
→ 失败读取日志并修根因
→ 全量回归
→ Progress/Handoff/Contract 同步
→ Accepted / Completed
```

禁止通过删除测试、弱化断言、隐藏 warning 或反复重跑来伪造 Green。

## 12. 开始下一阶段前检查

- [x] `dev` 分支远端 CI（含 Runtime Smoke）首跑确认 GREEN（Run `32821162412`），骨架阶段 Completed。
- [ ] Phase 1A 各工作包在 `dev` 上按第 4 节顺序重建，参考 `main` 历史实现与设计文档，不直接复制代码。
- [ ] 当前 worktree/diff 无未确认冲突。
- [ ] 阅读 `phase-1-acceptance.md`。
- [ ] Source DSL v1 先定义可测试的最小 schema/AST，不提前做万能脚本语言。
- [ ] Fixture 驱动，无真实第三方 Source PR-CI 依赖。
- [ ] 新 Source 网络能力必须同步安全测试。
