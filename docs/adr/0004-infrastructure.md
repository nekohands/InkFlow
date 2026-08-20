# ADR 0004: PostgreSQL + Redis + Docker Compose

- 状态：Accepted
- 日期：2026-08-20

## 决策

第一阶段使用 PostgreSQL 作为主数据库，Redis 提供缓存、锁和任务协调，Docker Compose 作为本地开发和单机部署基线。

## 约束

Redis 不作为业务事实数据的唯一存储。后续是否引入 RabbitMQ、独立搜索引擎或对象存储，需要新的 ADR 和明确负载依据。
