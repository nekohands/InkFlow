# InkFlow Architecture Invariants

以下规则是默认不可违反的工程不变量。任何突破必须通过明确 ADR 和证据证明其必要性。

1. `BookId` / `ChapterId` 对外长期稳定；来源切换、重排、Merge 不应让客户端身份失效。
2. `SourceBook != CanonicalBook`，`SourceChapter != CanonicalChapter`。
3. 正常阅读路径只读取 InkFlow Canonical Content，不实时依赖第三方抓取。
4. 有效新正文形成新的 Content Version，不覆盖历史正文。
5. Book Match、Chapter Alignment、Content Selection、Source Failover 等自动决策必须可解释、可追溯、可撤销。
6. Legado 是一级产品协议，拥有独立 Contract、API、兼容性测试和 SLO。
7. 公共内容、组织内容和用户私人内容必须保持明确授权边界；Blob 去重不能绕过权限。
8. Redis 不是任何关键业务事实或任务事实的唯一数据源。
9. Community Source 只能运行受限 DSL/Sandbox；任意代码只允许在可信官方执行域。
10. Modular Monolith 优先；只有明确的性能、部署、团队或隔离证据才允许拆服务。
11. Worker 负责执行，不负责决定 Canonical Identity 或最终 Content Selection。
12. Scheduler 负责任务时机，不拥有 Book/Chapter 业务事实。
13. Published Source Rule、Parser/Normalizer Algorithm Version 不可变；修改产生新版本。
14. Source URL 不是业务主键；优先保留外部稳定 ID，并维护历史 URL。
15. EF Core、Redis、ASP.NET 等基础设施依赖不得进入纯 Domain 模型。
16. PrivateBook、PrivateChapter 和私有正文必须与 CanonicalBook/CanonicalChapter/ContentVersion 使用独立身份，并且所有私有数据访问都必须绑定认证主体 UserId；TXT/EPUB 导入失败不得留下部分私有书籍。
17. Authoritative Data 与 Projection/Cache/Search Index 分离，Derived Data 必须可重建。
18. Developer API Key 原文只在签发/轮换响应中出现一次；持久化只保存 Prefix、不可逆摘要和生命周期/Scope 元数据，应用撤销必须使其密钥失效。
19. Developer API 只读已落库公共 Canonical 数据；不得触发第三方抓取、读取 Private Library 或提供管理写入；公共 API、Developer API 和 Personal Legado 的认证/授权边界必须独立。
20. Developer 配额准入必须以 PostgreSQL 用户级 UTC 月度事实和事务锁为准；固定版本的加权成本与 Usage Ledger 必须可追溯，Redis 只能加速展示/读取。
21. 外部调用均视为不可靠，必须考虑 Timeout、Retry、Backoff、Circuit Breaker 和 Idempotency。
22. 公共 API Contract 与内部 Entity/DTO 分离，`/api/v1` 默认向后兼容。
23. 生产数据库迁移由独立 Migration 流程执行，不由 API 启动隐式升级。
24. 关键修复通过 Admin Command、Repair Job 或 Migration Job 完成，不把手工 SQL 作为常规运营流程。
25. Content Policy 的当前状态只能由 `ContentPolicyDecision` 追加历史派生；公开读取必须在正文加载前经过策略门控，策略历史不得被更新或删除。
26. 每一个编码工作包必须遵守 `docs/engineering/development-workflow.md` 的完整闭环：实现 → Diff 自检 → 实际 Build → 自动化测试 → 运行/集成验收 → 安全/架构检查 → Candidate Commit → 实际 CI → Bug 修复与回归 → 文档同步 → Accepted。任何规定 Gate 未通过或未实际执行时，不得标记 `Completed`。
27. 所有主要用户前端页面必须遵守 `docs/engineering/frontend-design.md`：实现前研究至少 3 个当前活跃同类产品，优先优化操作路径、可读性、响应式和可访问性；核心 UI 不能只依据开发者个人偏好设计，也不能直接复制竞品视觉。用户可见页面在未完成 Mobile/Desktop/UX/Visual/Accessibility 验收前不得标记 `Completed`。
28. 业务事实与 Transactional Outbox 必须在同一 PostgreSQL 事务中提交；Integration Message 的 ID、类型和载荷摘要必须稳定可核对，Inbox 只有在消费成功后才标记已处理，消息载荷不得携带凭据、Token 或不必要的私有业务变量。
29. Outbox Dispatcher 只有在发布调用成功后才能确认消息；发布失败必须保留稳定失败码并按有界退避重新变为可领取，确认异常不得伪造成功。Inbox Consumer 只有在 Handler 成功后才能确认消费，未知类型和 Handler 异常必须可追踪且不得把异常文本写入消息事实。
30. Outbox/Inbox 保留清理只能删除 `ProcessedAt` 已设置且早于各自配置 cutoff 的记录；未处理、失败待重试、仍在 lease/锁定中的消息必须保留。清理每轮必须受批量与批次数上限约束，且不得成为消息事实的唯一来源。
31. Migration 流程在 `MigrateAsync` 前必须拒绝模型与快照漂移，且 CI 必须使用固定版本工具覆盖全部 `DbContext`；检测失败时不得对数据库执行结构变更。
32. `audit.events` 普通路径必须保持追加式：更新和直接删除由数据库拒绝；过期清理只能通过 `AuditRetentionService` / `IAuditRetentionStore` 的 UTC cutoff seam 执行，且每轮受批量与批次数上限约束。该代码边界不替代生产法律保留、归档和删除授权治理。
33. Source Rule DSL 的 JSON 输入与持久化结果必须经过版本化严格 codec/schema；未知属性、未知转换类型、缺失核心字段、超大文档和超出集合/表达式边界的规则必须 fail-closed，Fixture 回归不得依赖真实第三方网络。AST 声明能力不等同于运行时执行能力。
34. RuleAdapter 执行必须受有限的 MaxRequests、MaxBytes、MaxExecutionTime、MaxRegexTime、MaxResultSize 和请求模板变量上下文预算约束；变量上下文必须限制数量、标识符名称、单值长度、累计 UTF-8 字节并拒绝控制字符，变量值不得进入错误文本。路径、Header、Query、Form 模板只允许 `{name}` 占位符，发布期和执行期都必须拒绝残留花括号；生产 HTTP 必须在解码前执行响应体流式上限。受控 next-link、page-number 和 cursor Pagination 仅允许 Search/TOC 列表；next-link 后续必须同源并以 GET 跟随，page-number/cursor 只能改写规则已声明且唯一的 query/form 参数。响应派生变量仅允许在 page-number/cursor 的实际续页中从当前响应按受控 Selector/Regex 提取，必须复用变量预算，仅作为同一次执行的临时请求上下文，缺失、非法或超限时在续页出网前整体失败且不得进入结果、日志或错误文本。循环、重复游标、跨源或超出页面/请求预算时整体失败且不得暴露部分结果；更复杂的递归/多请求编排不得绕过这些边界。
35. RuleSession 只能声明执行期 Cookie 策略，不能在 Rule JSON、Task Payload、日志或结果中携带初始 Cookie/Token；仅接收成功响应的同源 `Set-Cookie`，按 Domain/Path/Secure/生命周期匹配后发送，Cookie 数量/累计字节/生命周期超限时整次执行失败，Cookie 状态不得跨执行或跨来源复用。任务级 CredentialReference 必须通过 `SourceExecutionContext` 和独立 `ISourceCredentialProvider` 解析，只能投影为有界的 Bearer、Basic 或受限 API-Key Header；引用、材料、secret 和最终头值不得进入 Task Payload、Variables、Rule JSON、日志、错误文本、结果或 `ToString()`。无效引用、Provider 缺失/失败/超时、非法材料和头冲突必须在 HTTP seam 前失败关闭；Source 的默认 CredentialReference 只能保存非敏感引用 ID，并且只在显式引用缺失时由规则型 Adapter/Worker 回退，Provider 必须实施 Owner Scope/跨租户权限；4.59 的 Administrator-only 管理 API 只能设置/清除引用并记录命令审计，不得接收或返回 secret；CodeAdapter 不继承该默认值，真实 secret 管理和持久会话仍需独立边界。

## 工程完成定义

以下表述均不等价于“完成”：

- 代码已经写完。
- 静态阅读认为应该能编译。
- 本地某个测试通过。
- CI 尚未触发或仍在 Pending。
- CI 失败后仅重新运行而未定位根因。

工程工作包只有在其适用的 Build、Test、Runtime、Security、CI、Regression 和 Documentation Gate 有真实证据通过后才可进入 `Accepted / Completed`。

对于用户可见的前端工作，还必须同时满足：

- 已完成同类产品 Benchmark Note 或等效记录。
- Mobile / Tablet / Desktop / Wide Desktop 中适用视口已检查。
- Primary Action 和高频操作路径通过人工 UX 验收。
- Loading / Empty / Error / Edge Case 已检查。
- Keyboard、Focus、Contrast、Touch Target 等可访问性要求已检查。
- Reader 类页面额外通过长时间阅读舒适性和章节切换体验检查。

详细强制流程见：

- `../engineering/development-workflow.md`
- `../engineering/frontend-design.md`
