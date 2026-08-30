# ADR 0021：采集运行控制与正典书籍多格式打包

- 状态：Accepted for 1.0 Release Candidate
- 日期：2026-08-30
- 范围：Crawling / Sources / Content / Library / Operations

## 背景

现有 CrawlerTask 适合表达一次来源能力调用，但不能把 BookInfo、目录和正文任务归并为一次可观察、可暂停和可恢复的全书运行；已有书籍内容也缺少面向运维的异步 EPUB、单文件 TXT 和 ZIP 导出契约。若把控制状态放在浏览器或 Redis 中，会在 Worker 重启、租约过期和重复消息时丢失事实；若直接打包来源响应，则会绕过 Canonical Content、质量和下架规则。

## 决策

1. 在 Crawling 增加 `CollectionRun` 父运行。既有 `CrawlerTask` 通过可选 `RunId` 归属到父运行；没有父运行的周期追更任务保持兼容。PostgreSQL 保存运行状态、阶段、来源/外部书籍标识、规范化 URL、正典书籍 ID、错误和时间戳，进度从运行与子任务权威事实汇总。
2. 运行状态采用 `Pending`、`Running`、`Paused`、`Stopping`、`Stopped`、`Cancelled`、`Completed`、`Failed`。暂停只禁止新任务领取并可恢复；停止等待当前原子单元结束后进入不可恢复的 `Stopped`；取消禁止重试和后续链路并进入不可恢复的 `Cancelled`。控制命令在数据库锁范围内幂等处理，Worker 领取条件必须排除暂停或终态父运行。
3. 直接书籍 URL 只允许匹配已登记公开 Source 的安全地址，由来源适配器声明并执行 source-specific URL Resolver；无法识别的 URL 直接拒绝，不提供任意 URL 代理，也不绕过登录、付费、VIP、验证码或 SSRF 防线。创建运行异步排队 `BookInfo → Toc → Content`，同一来源/外部书籍的活跃运行幂等复用。
4. `BookInfo` 成功后复用现有 Library v1 匹配策略：已有确认映射优先，书名+作者严格归一化匹配其次，否则创建新的 Canonical Book；不引入模糊匹配。章节不可用或不可恢复失败时运行失败，已成功的 Source/Canonical/Content 历史事实不删除、不回滚。
5. 书籍打包由 Content/Library 侧的 PackageJob 契约拥有，读取固定的当前 `ContentVersion` 快照，不重新抓取、不复制内容事实、不覆盖旧包。v1 支持独立的 `zip`、`epub`（EPUB 3）和 `txt`（单文件 UTF-8）格式；包必须完整生成并校验后才可下载。EPUB 不包含图片/音频和复杂排版，TXT 使用固定章节分隔线。
6. 包文件保存于 Ubuntu VM 上 API/Worker 可访问的受限共享目录，数据库保存 PackageJob 状态、格式、快照元数据和哈希；默认保留 7 天，过期删除文件但保留审计元数据。创建和下载仅向 Operator/Administrator 开放，所有命令写入审计。

## 考虑过的方案

- 仅扩展单个 CrawlerTask：无法可靠表达目录展开出的正文任务、全书进度和跨任务控制，因此不采用。
- 仅用 Redis 保存暂停/进度：Redis 不是关键事实唯一来源，重启和故障恢复不可审计，因此不采用。
- API 同步抓完整本书或同步生成大包：会占用请求生命周期，无法稳定处理长任务，因此不采用。
- 直接保存上游 HTML/响应作为导出源：会绕过当前正文、质量、下架和版本选择规则，因此不采用。

## 后果

- 运行控制和进度在 Worker 重启、事件重复投递和租约回收后仍有持久化依据；前端只消费有限 DTO 并轮询展示。
- 新增父运行、包任务和 Migration，必须保持模块依赖方向，并增加 Unit、Integration、Contract、VM Runtime 和浏览器自动化验收。
- 本地文件目录适合 1.0 Release Candidate 的单 VM 部署，但长期保存、跨主机扩展和备份恢复需要后续迁移到对象存储。
- 阅读 3.0/MuMu 真机、真实来源和人工视觉验收仍不属于本 ADR 的自动化完成条件。
