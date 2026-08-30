# ADR 0017: Inbox Consumer 轮询与 Worker 消费宿主边界

- 状态：Accepted
- 日期：2026-08-30

## 背景

4.61 已把 Outbox 可靠写入同一 PostgreSQL 事实库中的 Inbox，但 Inbox 只有可测试的
`IntegrationMessageConsumer`，没有持久化批量领取和 Worker 轮询宿主。继续把所有消息直接交给
Consumer 会让未注册的消息类型进入无界失败重试，也无法为未来的接收模块提供稳定接入点。

## 决策

1. `IInboxStore.ClaimBatchAsync` 作为 Inbox 轮询端口：在事务中使用
   `FOR UPDATE SKIP LOCKED` + lease，按 `MessageType` allowlist 领取最多 `BatchSize` 条未处理消息。
   领取顺序使用 `ReceivedAt, Id`，消息类型、owner、lease 和批量大小均有界校验。
2. 持久化层返回带 `IntegrationMessage` 与 attempt 的 `InboxMessageRecord`。新行保存
   `OccurredAt`；旧行没有该列时回退到 `ReceivedAt`。恢复时优先使用保存的 `RawPayload` 校验
   PayloadHash，旧行没有原文时保留已存 hash，不把 PostgreSQL `jsonb` 规范化文本冒充原文。
3. `InboxConsumerPump` 只把当前 `IIntegrationMessageHandlerResolver` 已注册的消息类型交给
   `IntegrationMessageConsumer.ConsumeClaimedAsync`；Handler 成功后才写 `ProcessedAt`，失败使用
   稳定失败码并释放 lease，异常原文和消息载荷不得进入日志或失败事实。
4. Worker 新增独立 `InboxConsumerBackgroundService`，通过 `Messaging:Inbox` 控制启用、启动延迟、
   轮询间隔、lease 和批量大小。每轮使用独立 scope，数据库和 Handler 生命周期不跨轮次共享。
   当前 Worker 尚未注册业务 Inbox Handler；空注册表安全 idle，不领取未知消息。
5. 未来接收模块按消息类型注册 Handler；Handler 必须自行保持幂等，Inbox 消费保持 at-least-once，
   不承诺 Exactly Once，也不改变既有 Crawler 任务轮询或阅读路径。

## 非目标

- 不在本轮选择或实现 `crawler.task.created` 的业务 Handler。
- 不引入 RabbitMQ、Kafka、云消息服务或其他外部 MQ。
- 不把 Inbox 轮询包装成 API 请求，不在 Worker 中实时访问第三方来源。
- 不执行阅读 3.0、MuMu、真实来源、真实追更、故障切换或其他人工验收。

## 后果

- 同一 PostgreSQL Inbox 现在具有可恢复的批量 claim/consume 宿主边界，多 Worker 可通过
  `SKIP LOCKED` 并行领取，租约到期后可重试。
- 未注册消息不会被后台轮询领取；因此当前工作包提供的是消费基础设施，不代表全部 Integration Event
  已形成业务消费闭环。
- 新增 nullable `OccurredAt` 和领取查询索引属于 Expand Migration；生产结构变更仍由独立 Migration
  流程执行。本机 Docker 不可用时，真实 PostgreSQL 集成证据必须标记为 BLOCKED。
