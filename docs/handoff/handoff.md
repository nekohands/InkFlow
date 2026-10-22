# InkFlow 工程交接文档

> 用于开发者、AI Agent 或未来会话快速、安全接手 InkFlow。真实状态以仓库与 CI 为准。

- 产品：墨流 / InkFlow
- 当前阶段：1.0 Release Candidate（历史自动化验收不替代当前提交门禁；真实设备、来源与账户验收仍待完成）
- 当前工作分支：`dev`（2026-08-25 起）
- 文档状态：RF-02 一致性检查职责拆分与测量 In Progress；RF-01 已验收，其他历史与真实环境待验收项继续保留。
- `dev` 骨架 root commit：`c5f2048`
- 交接日期：2026-10-22；dev 骨架重建更新：2026-08-25

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

- RF-00 方案保存在 `df6c701`；用户随后明确授权先实施 RF-01。先读 [RF-01 工作包](../../repowiki/work-packages/2026-10-22-rf01-architecture-guards.md) 与 [中文架构保护网](../engineering/architecture-guards.md)。
- 已实现四类编译依赖规则、11 模块显式图、项目引用和清单检查，保留加载 smoke。正反例暴露并修复 internal 嵌套类型筛选漏检；临时模块清单探针失败/清理后通过。
- 本地 Restore/Release Build PASS，Unit 648/648、Architecture 51/51、Contract 12/12、Migration 模型 11/11 PASS。全量本地 Integration：8 通过、128 DockerUnavailable 失败、3 跳过，本地 Runtime BLOCKED。候选 `34cfa8f` 的 Docker HIGH 失败保留为历史证据，Collector Go 1.26.10 补丁已修复该门禁。
- 验收实现 SHA：`f90e6bd658ecc657dc68e34b1936990a4ce99555`；CI `39758029891`、Docker `39758029869`、Security `39758029837` 所有 Job 均成功，证据链接见 RF-01。CI Integration 136 通过/3 跳过，Redis 独立 1/1；Runtime/SLO/OTLP、备份恢复、镜像扫描与发布拉取均通过。
- `.codegraph/` 实查不存在；SDK 10.0.402。`docker` 不在 PATH，Testcontainers 无法连接 `npipe://./pipe/docker_engine`，不能把容器验证标为本地通过。`.workbuddy-ai/` 保持不变。
- RF-01 Accepted / Completed，无剩余包内验收阻塞。下一步 RF-02（未开始）；既有真实设备、来源、账户、生产 OTLP/SLO 等验收不在本包关闭。保留静态规则的反射/动态加载限制与本地 Docker 缺失。

### 2026-10-15 历史交接快照

以下保留当时状态与下一步，不作为当前阻塞或实施指令：

- `dev` 是唯一开发主线，包含现有业务实现；旧骨架说明与旧验证数字已归档，不是当前状态。
- 当前整理范围是按模块澄清职责、收敛重复和保留扩展 seam；已完成宿主组合/审计响应去重，Sources 当前完成两批局部重构，不重写领域模型。
- Batch 2 本地 Release Build 0 warnings / 0 errors；Unit 630/630、Architecture 1/1、Contract 12/12，Sources 相关回归 93/93。集成测试及 Compose 受本机 Docker 环境阻塞，远端门禁待本批提交。
- 接续时先读 RepoWiki 工作包，复核实际 diff / HEAD / Actions。`.workbuddy-ai/` 为用户目录，禁止纳入本次变更。
- 当时下一步：处理 `49e3515` / `6989779` 的 OTLP collector Docker 扫描 RED，并等待最终 SHA 的 CI/Security 完成；该远端门禁未清除前不进入 RuleAdapter 执行职责批次。当前复核结果见本节上方。

## 4. 当前工作包与未完成验收

当前工作包见 [优化与重构方案](../../repowiki/work-packages/2026-10-22-optimization-refactoring-plan.md)；前序 [模块化重构](../../repowiki/work-packages/2026-10-15-module-refactor.md) 保留当时证据；已完成包及早期记录见 [历史交接](handoff-history.md)。下方待定验收项不由文档计划或基线 CI 自动关闭。

### 4.2 待定事项（人工/真实环境，后续处理）

> 本轮按用户决定不执行；完成后补充可复核证据，未完成前不关闭 Phase 1A/1B Release Gate。

- [ ] **阅读 3.0 真机**：在 MuMu 中导入 `/legado/book-source.json`，验证 Search → BookInfo → TOC → Content，并记录结果。
- [ ] **Web Reader 人工 UX/视觉验收**：移动端、桌面端、宽屏、长标题/缺封面/长作者、加载/空/错、键盘焦点、触控和上下章导航。
- [ ] **Reader/PWA 用户状态人工验收**：验证账户登录/注册、刷新会话、书架/历史/进度/偏好同步、登出、PWA 安装提示、Service Worker 注册和离线提示；本轮按用户决定跳过。
- [x] **Private Library 非阅读 App 自动化 runtime smoke**：源码构建 Compose 已覆盖认证、所有权隔离、书目 CRUD、TXT/EPUB 导入/导出、章节/正文、重复导入不覆盖原书、失败导入无半本书、私有缓存头、公共 API/Legado 直接路径 404 和公共 Catalog/Reading Shelf 不泄漏。
- [ ] **Private Library 真实账户/人工体验补充验收**：如需发布前补充，使用专用真实测试账户和真实 TXT/EPUB 验证浏览体验与长期使用；不替代自动化门禁。
- [x] **Developer API / 商业基础非阅读 App 自动化 runtime smoke**：源码构建 Compose 已覆盖 Free Entitlement、应用/密钥创建与列表脱敏、目录读取、Header-only 鉴权、轮换和撤销；真实账户、套餐管理、配额超额和用户停用仍待真实环境补充。
- [ ] **真实追更**：用真实来源数据验证 Scheduler → Worker → 目录增量 → 正文发布闭环。
- [ ] **真实第二来源故障切换**：4.99 已在源码 Compose 确定性 fixture 中验证 Web/Legado A→B→A、稳定 BookId/ChapterId 和恢复；仍需用可稳定访问的真实第二 Official Source 验证真实故障、响应和恢复，不得产生重复 Canonical 身份。
- [x] **linovelib 真实公开页面只读链路**：GPT 内置浏览器已完成 Search → BookInfo → TOC → Content 页面证据；不等同于服务端 RuleAdapter 直连通过。
- [ ] **linovelib RuleAdapter 后端直连链路**：当前普通 HTTP POST 搜索返回 200 但空响应体；已提供 `scripts/linovelib-live-acceptance.sh` 与 `INKFLOW_LIVE_TESTS=1` opt-in 测试入口，待网络/站点挑战可稳定处理后验证服务端 Search → BookInfo → TOC → Content，并纳入真实第二来源/故障切换演练。
- [ ] **17K 真实 Search/阅读链路**：已在 Ubuntu VM 只读探测，但当前 API 证书链校验失败或返回“请升级版本/图书信息不存在”，仍待可用网络环境验证 Search → BookInfo → TOC → 免费 Content、VIP 访问边界和安全重定向。
- [ ] **本机 Docker 集成复验**：Windows 本机 Docker Engine 仍不可用；Ubuntu VM 已在 5.11 使用源码构建 Compose 完成 Unit 530/530、Architecture 1/1、Contract 10/10、Integration 102 passed / 2 skipped / 0 failed 的完整容器证据。若需关闭本机复验项，仍待 Windows Docker 恢复后在本机重跑 Testcontainers。
- [ ] **生产 OTLP 后端与 SLO 窗口验收**：在部署环境将 Collector 接入受治理的持久化后端，验证 API/Worker/Scheduler/Reader 观测到达，执行合成探针和窗口聚合，并验收错误预算告警、访问控制与保留策略；Compose debug exporter/健康 smoke 仅为接收基线。

扩展新来源的方式(书源兼容层):
- 规则型站点:在 sources 表登记含 RuleDsl 的 Source 记录,零代码;
- 复杂站点(特殊编码/签名):实现 `ISourceAdapter`(参考 `KanunuSourceAdapter`)并在适配器工厂注册。

普通 PR CI 不依赖真实第三方小说站点；Crawler 使用固定 Fixture/Mock Server。真实 Source 进入独立 Live/Nightly 检查。


> 5.53–5.68 的完整交接已移至 [历史交接](handoff-history.md)。

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
→ /api/legado/v1/*（公共）或 /api/legado/v1/personal/*（Personal Token）
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

Phase 1A / 1B 外部验收：

- 阅读 3.0 导入 `/legado/book-source.json`，Search → BookInfo → TOC → Content 真机验证（按用户决定后续人工执行）。
- Scheduler/Worker 使用真实更新数据的追更验证；4.87 已自动化当前 Kanunu8 快照的扫描、消费、去重和发布，5.10 又补齐确定性新增章节/增量发布回归，但真实上游新增章节事件仍待定。
- 第二个真实 Official Source 与真实故障切源演练；当前只有确定性双来源夹具和 17K 离线 CodeAdapter 证据，不能替代真实来源验收。
- linovelib 已完成 Search 规则的离线定义与回归，真实网络验证仍受 DNS 污染影响，待可用环境复验。
- 本机 Docker 缺失导致 PostgreSQL Testcontainers 集成测试待本机可用容器环境复验；本轮一致性检查新增用例已在远端 CI PostgreSQL 容器中通过。

Phase 2 及以后：

- Source Health 的半开恢复、主动巡检探针与冷却参数配置化已完成；Crawler 死信受控重放、受保护 Repair/replay 入口、跨模块 Consistency Check v1、Operations Center Read Model v1 和 Center UI v1 自动化基线已完成，自动修复和更强运维治理仍待实现。
- Crawler 失败结构化日志与 OpenTelemetry counters、请求审计持久化、独立 `AuditRead` 有界查询、CI 级 PostgreSQL 备份恢复演练、告警快照/阈值/内部历史去重与恢复、来源级授权 v1 和已落地高风险命令审计基线已完成；审计有界 retention 代码基线已完成，但生产法律/合同保留、归档、删除授权和证据治理仍待部署环境确定。外部告警路由、生产异地备份/RPO-RTO、安全扫描治理、组织/更广泛资源权限仍待实现。限流已接入 Redis 原子分布式计数，并在 Redis 故障时保留同配额本地有界降级。
- 用户身份基础、Reading State v1、Reader/PWA 用户状态 v1（账户/书架/历史/进度/偏好接入、公开安装壳）、Personal Legado Token v1、Web Reader v1、Private Library 私有正文/TXT/EPUB 导入导出自动化基础和 Developer API / Entitlement / Billing v1 候选基线已完成；PWA Service Worker/离线壳已由 4.82 自动验收，真实安装、账户/跨设备验收、Private Library 与 Developer API 真实账户/凭据验收、Organization、Community Marketplace 仍未完成。Identity 令牌重放检测、族吊销与会话/令牌保留清理已接线（5.54）；API 宿主全局异常处理中间件已完成（5.55）。

更后阶段：Developer API / Commercial Foundation 的真实运营与产品化深化、Organization、Community Marketplace、Enterprise Deployment。

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
- [x] Phase 1A 自动化链路与 kanunu8 真实源端到端验证已在 `dev` 上重建并通过相应证据。
- [ ] Legado 真机导入/阅读与真实追更仍待执行。
- [x] Personal Legado Token v1 的自动化签发、Hash 持久化、header 认证、Personal API、撤销审计及“撤销即删除记录”已完成；阅读 3.0 导入、四步阅读和真机撤销后失效仍待人工执行。
- [x] Web Reader v1 的服务端渲染、响应式结构、阅读设置与 HTML 安全回归已完成；浏览器四尺寸视觉、焦点、触控和长时间阅读仍待人工执行。
- [x] Reader/PWA 用户状态 v1 的账户/书架/历史/进度/偏好渐进增强、公开 PWA 壳与 CI Runtime smoke 已完成；Service Worker/壳缓存/离线回退已由 4.82 在 localhost 安全上下文自动验收。
- [x] Reader/PWA 账户与阅读状态 API 的非阅读 App runtime smoke 已由 4.84 在 Ubuntu VM 源码构建 Compose 中完成；PWA 页面内真实凭据输入仍待人工或真实环境。
- [x] Reader/PWA 页面临时账户的 GPT 内置浏览器自动化已完成：注册/刷新会话、Catalog fixture 加入书架、书架列表、章节未发布空状态、登出和匿名保护提示均通过；4.86 追加已发布章节正文页面验证；临时账户已禁用。
- [ ] Reader/PWA 真实账户、安装/独立窗口、生产 HTTPS、跨设备同步和长期体验仍待人工执行；按用户决定不执行阅读 3.0。
- [x] 已阅读并按 `phase-1-acceptance.md` 建立 Phase 1B 双来源自动化基线。
- [x] Capability Health v1 与确定性健康感知故障切源已建立自动化基线。
- [ ] 第二个真实 Official Source / 真实故障切源尚未验收。
- [x] 当前租约恢复与跨进程原子领取候选改动已完成 Docker/CI 验证；真实设备、真实来源和本机 Docker 集成复验仍未完成。
- [x] Source DSL v1 已定义可测试的最小 schema/AST，并已接入受控 XPath/JSONPath 执行子集、next-link Pagination、page-number/cursor Pagination、受控 response-cookie Session、有界请求模板变量、任务级 CredentialReference typed 初始认证、有界响应派生变量、来源级默认 CredentialReference 回退、Administrator-only 默认绑定管理 API 和 Owner Scope 解析契约；secret 材料 Owner/Admin 管理、真实 SecretProvider、持久会话及三种受控分页之外的多请求/递归预算仍待后续工作包。
- [x] Fixture 驱动，无真实第三方 Source PR-CI 依赖。
- [x] 新 Source 网络能力必须同步安全测试。

## RF-02 当前工作包（2026-10-22）

[实施记录](../engineering/consistency-check-refactoring.md)：行为固定测试与职责拆分已实现，测量和完整门禁进行中；当前不标记 Completed。无 Schema、API JSON、权限或查询隔离变更。
