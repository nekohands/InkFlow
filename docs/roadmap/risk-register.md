# InkFlow Risk Register

| 风险 | 严重度 | 主要缓解措施 |
|---|---|---|
| Canonical Book 错误合并 | 极高 | 多证据评分、Candidate、Merge/Split、Redirect、Decision Record |
| Chapter 错位 | 极高 | 章节号+标题+序列上下文，后续内容指纹，Mapping 可 1:N/N:1 |
| 低质量正文被选为主内容 | 极高 | Content Version、Quality Evidence、人工 Lock、Cross-source validation |
| Source 改版造成批量错误解析 | 极高 | Rule Version、Fixture、Canary、Rollback、Replay/Reparse |
| Community Source SSRF / 内网探测 | 极高 | SafeHttpClient、DNS/Redirect 重校验、网络隔离、沙箱预算 |
| 私人内容越权泄漏 | 极高 | Public/Private endpoint 分离、Ownership 授权、Blob 去重与权限分离 |
| Legado Contract 被破坏 | 高 | 独立 DTO/API、兼容 Profile、Golden/Contract Test、Release Gate |
| Crawler 无限重试 | 高 | Error Classification、MaxAttempts、Backoff、DeadLetter |
| Redis 丢失导致任务状态丢失 | 高 | PostgreSQL 作为 Task Source of Truth，Redis 仅 Dispatch 加速 |
| Blob 与数据库不一致 | 高 | Consistency Checker、StoragePointer validation、Repair Job |
| Source Credential 泄漏 | 高 | SecretReference、最小权限、Task 不携带明文凭据、审计 |
| Playwright Worker 被利用横向移动 | 高 | Browser Worker 独立网络、无 DB 直连、临时 Context、资源限制 |
| 数据库迁移中断生产 | 高 | 独立 Migration App、Expand/Migrate/Contract、Staging Gate、Rollback Plan |
| Search/Projection 漂移 | 中 | Projection Version、可重建索引、Consistency Checker |
| 成本随 Playwright/带宽失控 | 中 | Source Cost Metrics、分层抓取、Browser Pool、Quota/Rate Limit |
| Web/PWA UI 缺陷 | 中 | E2E/UX metrics；优先级低于内容正确性与 Legado 稳定性 |

## 风险处理原则

1. 内容正确性优先于抓取速度和成本。
2. 用户隐私与权限边界优先于 Blob 去重和缓存命中率。
3. 自动决策必须留下 Evidence 与 AlgorithmVersion。
4. 高风险改动必须支持 Canary / Rollback / Replay 中至少一种恢复路径。
5. 第三方来源故障不能扩散为 InkFlow 核心 API 故障。
