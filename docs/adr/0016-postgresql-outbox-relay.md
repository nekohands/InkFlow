# ADR 0016: PostgreSQL Outbox Relay 与 Worker 宿主接线

- 状态：Accepted
- 日期：2026-08-30

## 背景

4.43–4.45 已建立 PostgreSQL Outbox/Inbox 事实表、事务写入、lease/重试执行层和保留清理，
但仍缺少实际 Publisher 与 Worker 后台循环。仅有可测试的接口不能把 Crawler 产生的
`crawler.task.created` 可靠地送入 Inbox，也无法在运行时形成可观测的投递链路。

## 决策

1. v1 选择 PostgreSQL 作为当前内部 relay 的持久传输边界，不引入尚未选型的外部消息代理。
   Worker 注册 `PostgreSqlInboxMessagePublisher`、`OutboxDispatcher` 和
   `OutboxRelayBackgroundService`，使用 `MessagingDbContext` 的事实表。
2. Relay 每轮以 `FOR UPDATE SKIP LOCKED` + lease 领取 Outbox，重建并核对消息类型、PayloadHash
   和 TraceId 后，以消息 ID 幂等写入 Inbox；Inbox 写入成功后才确认 Outbox。确认失败时允许
   重复写入，Inbox 主键和身份核对负责保持 at-least-once 语义。
3. Inbox 持久化保留可选 TraceId；字段通过独立 Expand Migration 增加。接收时间由注入的
   `TimeProvider` 记录，重复投递不覆盖第一次接收时间。
4. Relay 通过 `Messaging:Relay` 配置节控制 `Enabled`、owner 前缀、启动延迟、轮询间隔、
   lease 和批量大小；所有值有上下限，进程 owner 使用实例名加随机 ID，后台日志不写载荷、
   异常文本或 secret。
5. 本 ADR 只接入 Outbox → Inbox 的耐久 relay。Inbox 消费轮询和具体业务 Handler 等待接收
   模块与消息类型明确后再接入；在此之前不宣称所有 Integration Event 已完成消费闭环。

## 非目标

- 不在本轮引入 RabbitMQ、Kafka、云消息服务或其他外部 MQ。
- 不实现 Exactly Once；业务 Handler 仍必须可幂等，重复投递是允许的恢复路径。
- 不新增 Crawler 业务 Handler，也不改变现有任务轮询和阅读路径。
- 不执行阅读 3.0、真实来源或其他人工验收。

## 后果

- Outbox 事实现在有真实的 Worker 投递宿主，进程重启、租约到期和数据库故障可沿既有退避路径恢复。
- 同库 Inbox 为当前 v1 的可重建消费事实边界；未来替换为受治理外部传输时，可保留 Publisher
  接口与 Dispatcher 语义，仅替换传输适配器。
- Inbox 仍需要后续接收模块注册 Handler；在 Handler 未选定前，Relay 不会伪造业务处理成功。
- 本机没有 Docker 时无法取得真实 PostgreSQL 集成证据，远端 CI 必须继续验证迁移、重复投递和
  Outbox 确认顺序。
