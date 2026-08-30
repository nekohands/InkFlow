# ADR 0018: Inbox 消费失败的有界退避与终态死信

- 状态：Accepted
- 日期：2026-08-30

## 背景

4.62 建立了 Inbox 的批量领取、Handler allowlist 和 Worker 轮询，但 Handler 失败后只释放
lease。消息会在下一轮立即再次领取，没有统一的退避时间，也没有可查询的终态，容易形成
热循环并掩盖持续失败。当前 Worker 尚未注册具体业务 Handler，因此本决策先补齐通用失败
语义，不虚构某个业务事件的消费闭环。

## 决策

1. `InboxConsumerOptions.MaxAttempts` 默认值为 5，允许范围为 1–100；每次成功领取并进入
   Handler 的执行都消耗一次尝试额度。继续复用 `IMessageRetryPolicy`，默认指数退避为
   5 秒起、1 小时封顶；策略返回值还必须满足通用的 7 天最大延迟边界。
2. Handler 未注册或执行异常时，Inbox 只保存稳定失败码，不保存异常原文。若仍有额度，
   `IInboxStore.MarkFailedAsync` 清除 lease 并写入 `AvailableAt`；未到该时间的领取返回
   `RetryScheduled`，不得再次调用 Handler。
3. 当前失败已达到 `MaxAttempts`，或消息的旧 attempt 已超过新配置上限时，写入
   `DeadLetteredAt`，清除 `AvailableAt` 和 lease，并返回 `DeadLettered`。死信仍保留原始
   消息、attempt、稳定失败码和身份字段；后续查询/修复入口另行设计，不自动重放。
4. PostgreSQL 的单条和批量 claim 都排除已处理、已死信和未到 `AvailableAt` 的行，并继续
   使用 `FOR UPDATE SKIP LOCKED` + lease。成功确认清除失败调度字段并写入 `ProcessedAt`。
   Inbox retention 仍只清理已处理记录，因此失败和死信事实不会被普通保留任务误删。
5. Worker 每轮输出 claimed、processed、failed、skipped 和 dead-lettered 计数；出现死信
   时提升为 Warning。当前 Worker 仍保持空 Handler registry 安全 idle，不领取未知类型。

## 非目标

- 不在本轮选择或实现 `crawler.task.created` 或其他具体业务 Inbox Handler。
- 不新增死信管理 API、自动重放、人工修复流程或外部 MQ。
- 不执行阅读 3.0、MuMu、真实来源、真实追更或其他人工验收。

## 后果

- 持久化失败时间使 Inbox 不再因瞬时故障形成无界热循环；达到上限的持续失败有明确终态，
  便于后续 Operations/Repair 接入。
- 语义仍是 at-least-once：Handler 必须幂等，成功确认与业务副作用的事务边界由具体
  Handler 负责。
- 本轮只完成通用可靠性基础；没有业务 Handler 时，不能据此宣称 Integration Event
  已形成完整消费闭环。真实 PostgreSQL 集成证据仍需在 Docker 可用环境取得。
