# RF-02 一致性检查职责拆分与测量

状态：In Progress。基线：`aa15b512321f0cd7c3e991ec7ef9a3815a6b9743`。

目标是将快照/报告 DTO、报告编排和按 Sources、Library、Content、Crawling 分组的纯规则迁入 Operations.Application；EF 跨 schema 适配器与领域枚举到纯值集合的转换保留在 API 组合层。

非目标：RF-03 查询/内存优化、事务隔离增强、修复写入、数据库迁移、公共 JSON 或权限变更。仍然完整读取 13 组元数据、完整计数和排序后返回最多 1,000 个问题。

验收：固定空、健康、孤儿、重复、错配、超过 1,000 个问题的 fixture；对完整报告及顺序建立迁移前后等价证据；验证取消传播、枚举边界与模块依赖。保持 CheckedAt、Status、TotalIssueCount、ReturnedIssueCount、Truncated、IsHealthy、全部代码和消息语义。

测量：固定标识与数据，Release、单并发、预热 3 次，每轮 30 次、3 轮，1k/10k/100k Canonical Chapter，每章两条 SourceChapter 和两条 ContentVersion。保存原始耗时、分配量、进程峰值与报告摘要；PostgreSQL 记录 SQL 数量、行数和执行计划。纯规则与数据库端到端结果分开报告，峰值是进程生命周期指标，不冒充单次分配。测量不等于生产负载证明，也不宣称本包带来提速。

风险：迁移时漏规则、枚举漂移、排序和抽样变化、将顺序查询误称原子快照。通过固定输出、架构测试和真实 PostgreSQL 证据控制。

验证计划：Restore、Release Build、Unit/Architecture/Contract/Integration、11 Context 迁移漂移、Admin/Operations runtime、候选 SHA 的 CI/Docker/Security；本地 Docker 不可用时明确记录并取得远端证据。当前新增验证均 NOT RUN。

AI 权威记录：[RF-02](../../repowiki/work-packages/2026-10-22-rf02-consistency.md)。

## 本地证据

Restore、Release Build（含独立基准项目）PASS，0 warning / error；Unit 656/656、Architecture 51/51、Contract 12/12 PASS。全量 Integration 已执行：8 passed、128 DockerUnavailable、3 skipped；PostgreSQL 基准同样因 `npipe://./pipe/docker_engine` 不可用而 BLOCKED，不能视为通过。

迁移前后规则基准各 720 次采样、24 组轮次，全部完整问题列表摘要和报告摘要一致（包括 200,030 个问题的样本）。原始数据：[before](benchmarks/rf02/before-rules.json)、[after](benchmarks/rf02/after-rules.json)。基线生产源码为 aa15b51，fixture/测量程序保存于 a043e24；后测为本包提取后的源码。固定整数生成 UUID、固定时钟、固定数据，无随机种子漂移。

本机共享开发环境测量期间存在其他构建活动，且 Windows 基准运行时曾发生 apphost 文件锁重建失败，进程退出后重建已通过；这些数据用于等价性与成本量级分析，**不作提速结论或 RF-03 性能预算依据**。RF-03 必须先在隔离且相同条件下重测、确定噪声和阈值。SQL 测量使用独立 CI Job 与容器、预热缓存，输出查询、行数、EXPLAIN ANALYZE/BUFFERS、分配量及原始时延；唯一约束禁止的重复只在纯规则 fixture 中注入，数据库 fixture 使用合法持久化的跨 schema 孤儿。

复现：`dotnet run --project tools/InkFlow.ConsistencyBenchmarks -c Release -- artifacts/consistency/rules.json`；有 Docker 时追加 `--postgres` 测量数据库路径（仅创建自有临时容器，不接受生产连接字符串）。报告耗时不包含 fixture 生成和 hash 校验，分配量为进程总分配差值，峰值为进程生命周期 Working Set；不同轮次峰值不能作单次峰值比较。
