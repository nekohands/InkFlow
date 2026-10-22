# RF-02 一致性检查职责拆分与测量

状态：In Progress。基线：`aa15b512321f0cd7c3e991ec7ef9a3815a6b9743`。

目标是将快照/报告 DTO、报告编排和按 Sources、Library、Content、Crawling 分组的纯规则迁入 Operations.Application；EF 跨 schema 适配器与领域枚举到纯值集合的转换保留在 API 组合层。

非目标：RF-03 查询/内存优化、事务隔离增强、修复写入、数据库迁移、公共 JSON 或权限变更。仍然完整读取 13 组元数据、完整计数和排序后返回最多 1,000 个问题。

验收：固定空、健康、孤儿、重复、错配、超过 1,000 个问题的 fixture；对完整报告及顺序建立迁移前后等价证据；验证取消传播、枚举边界与模块依赖。保持 CheckedAt、Status、TotalIssueCount、ReturnedIssueCount、Truncated、IsHealthy、全部代码和消息语义。

测量：固定标识与数据，Release、单并发、预热 3 次，每轮 30 次、3 轮，1k/10k/100k Canonical Chapter，每章两条 SourceChapter 和两条 ContentVersion。保存原始耗时、分配量、进程峰值与报告摘要；PostgreSQL 记录 SQL 数量、行数和执行计划。纯规则与数据库端到端结果分开报告，峰值是进程生命周期指标，不冒充单次分配。测量不等于生产负载证明，也不宣称本包带来提速。

风险：迁移时漏规则、枚举漂移、排序和抽样变化、将顺序查询误称原子快照。通过固定输出、架构测试和真实 PostgreSQL 证据控制。

验证计划：Restore、Release Build、Unit/Architecture/Contract/Integration、11 Context 迁移漂移、Admin/Operations runtime、候选 SHA 的 CI/Docker/Security；本地 Docker 不可用时明确记录并取得远端证据。当前新增验证均 NOT RUN。

AI 权威记录：[RF-02](../../repowiki/work-packages/2026-10-22-rf02-consistency.md)。
