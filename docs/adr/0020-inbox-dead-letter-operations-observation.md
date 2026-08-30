# ADR 0020：Inbox 终态死信纳入 Operations 告警观测

- 状态：Accepted for 1.0 Release Candidate
- 日期：2026-08-30
- 范围：Messaging / Operations / API

## 背景

Inbox 已具备有界失败重试与 `DeadLetteredAt` 终态，但 Operations 告警快照此前只读取 Crawler 死信。消息处理持续失败时，平台运维只能从日志或数据库侧发现，告警历史也无法反映该事实。

## 决策

1. 在 `InkFlow.BuildingBlocks.Messaging` 暴露 `IInboxDeadLetterReader`。接口只接受有界 limit，并返回数量与 `HasMore`，不暴露载荷、失败文本、TraceId 或消息身份。
2. PostgreSQL 实现只统计 `DeadLetteredAt IS NOT NULL AND ProcessedAt IS NULL` 的 Inbox 行，按死信时间和消息 ID 有界读取；为该查询增加 `(ProcessedAt, DeadLetteredAt, Id)` 索引。
3. Operations 告警增加独立的 `InboxDeadLetterCountThreshold`。平台级未过滤快照产生 `inbox_dead_letters_present`；读取失败产生稳定的 `inbox_dead_letter_snapshot_unavailable`，并把快照标记为 partial。
4. 来源过滤的 Operator 快照不查询或返回平台级 Inbox 死信，避免把不属于授权来源范围的消息运维状态泄漏给来源级视图。
5. partial 快照继续不能驱动告警历史的 resolved 转折；本 ADR 不引入外部通知、自动重放、Inbox 管理 API 或真实生产渠道。

## 后果

- Inbox 失败现在能沿用既有 Operations 快照、去重/恢复历史和受保护查询链路被发现。
- 查询只读取摘要且有界，消息载荷与错误原文继续留在 Messaging 事实表，不进入 Operations 告警模型。
- 外部通知路由、生产渠道和 Inbox 死信人工修复仍需单独的运维工作包与部署环境验收。
