# Operations/Repair Center UI v1 Benchmark Note

日期：2026-08-29（告警历史分页增量）

## Page

- 目标用户：`Operator` / `Administrator`；用于观察来源健康、采集死信与一致性，不向普通读者暴露。
- 页面性质：只读运维快照 + 告警历史 + 受控修复入口。当前基线 API 为 `GET /api/v1/admin/operations/overview` 与 `GET /api/v1/admin/operations/alerts/history`，查询结果有界且按区块隔离故障。
- 非目标：自动修复、直接改数据库、把凭据/任务变量/正文载荷放入仪表盘。

## 本轮增量（2026-09-01）

- Content Policy 已纳入 Center：Administrator 可在页面发起正典书下架/恢复，查看当前活动下架记录；Operator 可进入 Center，但管理控件只读/禁用。
- 下架与恢复均要求理由，复用受保护 API、明确确认对话框和 append-only 审计；UI 不提供删除历史的路径，公开书目/详情/阅读可见性由服务端政策状态决定。
- 验收边界：临时夹具账号的 VM 源码 Compose 与 GPT 内置浏览器自动化已通过；真实凭据、生产截图和人工视觉/触控仍是第 6 节待定事项，不改变本基准的非目标。

## 本次增量参考

- Grafana Alert State History：用独立历史视图呈现告警状态转折，并允许继续缩小范围查看事件；InkFlow 采用更小的表格和不透明游标，只提供管理员需要的稳定事件字段。
- GitHub Actions Workflow Run History：以时间顺序列出运行记录，并将状态与详细日志分开；InkFlow 将“刷新最新”和“加载更早记录”分成明确操作，不在首屏堆叠全部历史。
- Prometheus Alertmanager：将 firing/resolved 作为生命周期状态，并把通知投递与告警状态分开；InkFlow 本轮只展示内部 opened/resolved 转折，外部通知仍由后续运维集成负责。

取舍：保留“当前快照”作为默认工作面，历史作为第二级区块；平台级历史仅 Administrator 可见，Operator 继续使用来源过滤快照。移动端允许历史表横向滚动，操作按钮保持可触控和可键盘访问。

## 状态分组

按后端稳定区块组织页面，避免把所有指标堆成一张表：

1. **Sources / 来源健康**：按来源再按能力展示 `Healthy`、`Degraded`、`Unhealthy`、`Disabled`、`Unknown`；同时显示连续失败次数、最近成功/失败、失败原因、算法版本和更新时间。`Disabled` 是人工禁用，`Unknown` 表示尚未由真实探针确认可用。
2. **Crawler / 死信队列**：显示原因、尝试次数、进入死信时间和重放状态；区分未重放、已重放及重放任务 ID，并明确“还有更多”分页提示。
3. **Consistency / 一致性**：显示总体状态、问题总数、截断标记和问题列表；问题至少保留代码、资源类型/ID和可行动消息。
4. **Alert History / 告警历史**：管理员查看按时间倒序排列的 opened/resolved 转折；显示稳定告警代码、资源坐标、发生时间和出现次数，支持刷新最新页与加载更早页，不显示动态 message、异常原文或凭据。
5. **页面总览状态**：区块全为 `ready` 时为 `ready`；任一区块失败时为 `partial`。区块级 `partial` / `unavailable` 必须独立呈现，不能用整页成功或失败覆盖局部事实。

## 错误与空状态

- `ready + 空数据` 是合法空状态：分别说明“当前没有死信”“没有一致性问题”或“暂无该来源能力记录”，保留刷新/返回入口；不能显示成错误。
- `partial`：保留可用区块和数据，同时说明受影响区块及稳定错误码，例如 `source_health_unavailable`、`crawler_unavailable`、`consistency_unavailable`；提供重试。
- `unavailable`：区块显示不可用、下一步（重试或联系运维）和时间戳，不展示基础设施异常、堆栈、凭据引用或任务变量。
- `401/403`：分别表达“会话无效/需要重新登录”和“没有 OperationsRead 或命令权限”；不把权限拒绝伪装成空数据。
- `404/409/400` 的修复反馈：分别说明目标不存在、状态已改变/已被重放、或请求缺少合法理由；成功也要显示服务端返回的状态和新重放任务 ID。
- 初次加载、刷新和网络不可用均需要局部 skeleton、可重试错误和可键盘操作的状态反馈，避免全屏 spinner 阻断其他区块。

## 可访问性与交互

- 以 WCAG 2.2 AA 为基线：语义化标题/分组/表格，完整键盘导航，明显且不被 sticky header 或弹窗遮挡的 focus。
- 状态不能只靠颜色：同时使用文字、图标/标记和可读的错误说明；颜色对比度、200% 缩放和长错误文本都要可用。
- 每个按钮有明确名称和操作结果；批量/分页控件、刷新、重试、展开详情支持键盘和合理触控尺寸。
- 死信、来源能力和一致性问题使用可扫描的列表/表格；移动端允许横向信息分层或详情抽屉，但不能隐藏关键状态和操作结果。
- 异步读取、修复结果和权限错误通过 `aria-live` 或等效可读反馈通知辅助技术；支持 Reduced Motion。

## 破坏性操作确认

- **Replay dead letter**：这是受控修复，不是删除；操作前展示死信 ID、来源、失败原因、尝试次数，并要求操作者填写非空理由。确认文案应说明：原任务保持 `DeadLettered`，系统只创建新的 `Pending` 重放任务；提交后显示 `Replayed` / `AlreadyReplayed` 与 `replayTaskId`。
- **Disable source capability**：按单个 `(SourceId, Capability)` 确认，展示当前状态和影响范围；必须填写理由，并明确禁用会使该能力不可用。成功后显示审计结果。
- **Enable source capability**：仍要求理由并二次确认；文案必须说明恢复只回到 `Unknown`，不会伪造 `Healthy`，需等待下一次真实探针。
- **Content Takedown/Restore（若纳入 Center）**：仅 `Administrator`；必须明确书籍标识、影响范围和理由。确认后说明这是追加式策略决定并写入审计，不提供“静默删除历史”的选项。
- 所有高风险按钮默认不在列表首屏制造误触；采用明确动词、不可含糊的确认对话框、理由输入、提交中禁用和成功/失败结果。服务端权限、理由校验和审计是最终边界，UI 确认不能替代它们。

## InkFlow-specific requirements

- 普通读者导航不显示 Source/Crawler/ContentVersion 等内部概念；Center 使用独立 Admin 信息架构。
- 前端只消费受保护 API，不缓存认证响应、死信详情或其他管理数据；不显示秘密、凭据引用、任务变量或正文载荷。
- Center 的正式实现仍需补充 Mobile / Tablet / Desktop / Wide Desktop 人工验收、键盘/对比度检查和截图证据；本记录不替代这些验收。
- 告警历史的管理员角色、分页边界、恢复转折和服务不可用提示已有自动化结构/运行时基线；真实凭据操作和视觉截图仍属于待定验收。

## 来源

- `src/Apps/InkFlow.Api/OperationsCenter.cs:6-190` — 区块模型、稳定状态码、字段、有界查询、故障隔离。
- `src/Apps/InkFlow.Api/Program.cs:473-660` — OperationsRead、CrawlerRepair、SourceOperations、ContentModeration 路由与权限边界。
- `src/Apps/InkFlow.Api/RepairEndpoints.cs:9-80` — 死信重放结果及 `Replayed` / `AlreadyReplayed` / 错误映射。
- `src/Apps/InkFlow.Api/SourceHealthEndpoints.cs:9-96` — 来源能力命令、理由规范化与审计结果。
- `src/Apps/InkFlow.Api/ContentPolicyEndpoints.cs:1-66` — 下架/恢复理由与命令审计。
- `src/Modules/InkFlow.Modules.Sources/Domain/SourceCapabilityHealth.cs:3-11, 110-111, 210-224` — 来源健康状态和恢复语义。
- `src/Apps/InkFlow.Api/ConsistencyCheck.cs:126-134` — 一致性报告字段与健康判定。
- `docs/architecture/architecture.md:94-100` — 任务、Repair/Replay、Operations Center 的架构不变量。
- `docs/architecture/security-model.md:30-38, 109-115` — 角色策略、理由、审计和敏感数据边界。
- `docs/engineering/frontend-design.md:1-8, 14-16, 20, 26, 30, 40` — 状态设计、WCAG 2.2 AA、错误/空状态、响应式与 UI 验收要求。
- [Grafana Alert State History](https://grafana.com/docs/grafana/latest/alerting/monitor-status/view-alert-state-history/) — 状态转折历史与筛选模式。
- [GitHub Actions workflow run history](https://docs.github.com/en/actions/monitoring-and-troubleshooting-workflows/monitoring-workflows/viewing-workflow-run-history) — 时间序列状态列表与详情分层。
- [Prometheus Alertmanager notification model](https://prometheus.io/docs/alerting/latest/notifications/) — firing/resolved 生命周期与通知分离。
