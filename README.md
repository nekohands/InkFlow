# 墨流 / InkFlow

多源小说采集、聚合、阅读与分发平台。

InkFlow 将小说来源采集、内容标准化、在线阅读、开放 API 与阅读 3.0（Legado）兼容分发统一在一个可演进的平台中。

## 技术栈

- .NET 10 / ASP.NET Core
- PostgreSQL
- Redis
- Modular Monolith + 独立 Worker / Scheduler
- Docker Compose
- GitHub Actions

## 目录

- `src/InkFlow.Api`：HTTP API
- `src/InkFlow.Worker`：采集与内容处理 Worker
- `src/InkFlow.Scheduler`：采集任务调度
- `src/InkFlow.Domain`：领域模型
- `src/InkFlow.Application`：应用服务与用例
- `src/InkFlow.Infrastructure`：基础设施实现
- `src/InkFlow.Modules.Legado`：阅读 3.0 兼容层
- `docs/architecture`：架构文档
- `docs/adr`：架构决策记录

## 当前阶段

当前仓库处于基础骨架阶段。第一条产品闭环目标为：来源接入 → 小说导入 → 章节采集 → Web/API 阅读 → Legado 书源/订阅源导入。
