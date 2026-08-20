# InkFlow Roadmap

## Phase 0 — Foundation / 0.1

目标：建立可长期演进、可编译、可测试、可部署的基础设施。

必须完成：

- Modular project boundaries + Architecture Tests
- UUIDv7 强类型 ID、TimeProvider、Result/Error
- PostgreSQL + EF Core + module schemas + migrations app
- Redis integration（非事实数据唯一存储）
- Transactional Outbox / Inbox
- OpenTelemetry
- 基础 Authentication / Authorization / Audit
- Docker Compose
- CI: restore/build/unit/architecture/integration/contract/container/security baseline
- Local/Test/Staging/Production 配置边界

验收：`dotnet build`、`dotnet test`、`docker compose up`、Migration、API/Worker/Scheduler health 全部可运行，CI Green。

## Phase 1A — Single Source Vertical Slice / 0.2

目标：跑通第一个真实 Source 到 Web/Legado 的完整链路。

- RuleAdapter DSL v1
- SafeHttpClient
- Official Source #1
- Search / BookInfo / TOC / Content
- SourceBook / SourceChapter
- Canonical Book / Chapter v1
- Content AST / Blob / Version / Quality v1
- Minimal Web Reader
- Legado v1 API + Rule Generator
- Scheduler 自动追更

验收：无需手工改 DB/JSON 完成 Source -> Canonical -> Content -> Web -> Legado -> Auto Update。

## Phase 1B — Dual Source Canonical Validation / 0.3

目标：证明 InkFlow 不是单源爬虫。

- Official Source #2
- 同书自动匹配
- Chapter Alignment
- >=2 SourceChapter / ContentVersion
- Quality Selection
- Failover Drill

验收：禁用 Source A 后 Web/Legado 继续读取，BookId/ChapterId 不变；恢复后 A 可重新进入候选。

## Phase 2 — Multi-Source Production / 0.4

- Book Matcher 多证据评分
- Chapter Alignment 多维匹配
- Content Quality Engine + CrossSourceAgreement
- Source Health / Capability Health
- Adaptive Scheduler + Event Trigger
- Parser/Normalizer/Algorithm Versioning
- Reparse / Replay
- Rule Canary/Rollback
- Repair Center 基础

## Phase 3 — User Product / 0.5–0.6

- Nuxt Web Reader / PWA
- 注册登录、Session、Refresh Token
- 书架 / 阅读历史 / ReaderPreference
- Personal Legado Token
- Private Library
- TXT / EPUB Import
- 排行榜 / 基础推荐
- 用户数据导出/删除基础

## Phase 4 — Commercial Platform / 0.7–0.9

- Developer Application / API Key
- Quota / Usage Metering / Webhook
- Plan / Entitlement / Billing
- Organization / RBAC
- Community / Private Source
- Private Worker
- Enterprise private deployment
- Feature Flag / advanced audit / operations

## 1.0 — Commercial Ready

至少满足：

- Core SLO 达标
- Legado Contract 稳定并成为 Release Gate
- 多源 Failover 实测
- 至少 3 个稳定 Official Source
- 监控、告警、备份恢复演练
- Security Scan / Rate Limit / SSRF / Audit
- 安全 Migration
- Repair / Consistency Check
- Content Policy / Takedown 基础
- 关键 E2E 与故障场景验证

## 版本原则

- `0.x`：允许快速演进，但稳定外部 ID 和已声明 Contract 仍尽量保持兼容。
- `1.0`：`/api/v1`、Legado 规则 URL、公共标识和 Developer API 进入严格兼容策略。
