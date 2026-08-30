# ADR 0019: crawler.task.created 的 Inbox 业务消费闭环

- 状态：Accepted
- 日期：2026-08-30

## 背景

4.61–4.63 已具备 Crawler 任务与 Transactional Outbox、PostgreSQL relay、Inbox
批量领取、失败退避和死信语义，但 Worker 仍没有具体业务 Handler。`crawler.task.created`
消息如果只进入 Inbox 而不被消费，任务只能依赖旧的周期轮询入口，Outbox → Inbox → 业务
执行链路没有完整的自动化闭环。

## 决策

1. 由 Crawling Application 提供 `CrawlerTaskCreatedMessageHandler`，只接收精确类型
   `crawler.task.created`。消息载荷只包含任务 ID、来源 ID、能力、状态、尝试次数和创建时间等
   稳定字段，不携带 Variables、CredentialReference、secret 或正文；接收方必须回到
   `CrawlerTask` 权威仓储读取完整任务。
2. Handler 校验消息类型、JSON 结构、消息 ID、枚举和稳定字段，并核对任务的 SourceId、
   Capability 与 CreatedAt。缺失任务或身份不一致直接失败，交给通用 Inbox 的有界重试/死信
   策略，不伪造成功确认。
3. 增加按任务 ID 的 `ICrawlerTaskRepository.TryLeaseAsync`。EF 实现使用 PostgreSQL
   事务中的 `FOR UPDATE SKIP LOCKED`，在同一数据库事务中完成目标筛选、过期租约回收和新租约
   写入；因此事件 Handler 与周期轮询不会通过“先读后写”重复领取同一任务。
4. 把原 Worker 内联的任务状态机抽为 `CrawlerTaskProcessor`。周期轮询和 Inbox Handler
   共用该处理器，统一执行 `Leased → Running → Completed` 或失败后的 `Pending` 重试/
   `DeadLettered` 持久化和失败观测。任务级失败由 Crawler 任务重试预算负责，Inbox 只在
   Handler 自身失败时执行消息级退避。
5. Inbox 成功确认与任务状态提交保持独立事务，语义继续是 at-least-once。重复投递在任务已
   完成/死信、已有租约或 Inbox 已处理时安全吸收；周期轮询保留为消息延迟、并发竞争和异常恢复
   的可靠兜底。Worker 组合根注册 Handler，并补齐其章节映射链所需的 Canonical Book 仓储。

## 非目标

- 不在本轮接入其他 Integration Event、外部 MQ、自动重放 API 或新的数据库 Migration。
- 不执行阅读 3.0、MuMu、真实来源、真实追更、真实切源或其他人工验收。
- 不把本轮的一个业务事件 Handler 表述为“所有 Integration Event 已消费”。

## 后果

- `crawler.task.created` 现在具有可回归的 Outbox → Inbox → Handler → CrawlerTask 执行闭环，
  且周期轮询与消息触发共享同一状态机。
- 业务任务失败与消息处理失败分层，避免一次抓取失败同时消耗两套无界重试；Handler 身份错误
  仍会进入 Inbox 的稳定失败码、退避和死信路径。
- 本机 Docker 不可用时无法取得 PostgreSQL 并发/端到端证据；该验证必须由 CI 或 Docker
  可用环境完成。真实来源和人工验收继续按 Progress 第 6 节处理。
