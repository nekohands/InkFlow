# RF-02 一致性检查职责拆分与测量

状态：Accepted / Completed（实现提交 `3a38ad1`）。基线：`aa15b512321f0cd7c3e991ec7ef9a3815a6b9743`。

目标是将快照/报告 DTO、报告编排和按 Sources、Library、Content、Crawling 分组的纯规则迁入 Operations.Application；EF 跨 schema 适配器与领域枚举到纯值集合的转换保留在 API 组合层。

非目标：RF-03 查询/内存优化、事务隔离增强、修复写入、数据库迁移、公共 JSON 或权限变更。仍然完整读取 13 组元数据、完整计数和排序后返回最多 1,000 个问题。

验收：固定空、健康、孤儿、重复、错配、超过 1,000 个问题的 fixture；对完整报告及顺序建立迁移前后等价证据；验证取消传播、枚举边界与模块依赖。保持 CheckedAt、Status、TotalIssueCount、ReturnedIssueCount、Truncated、IsHealthy、全部代码和消息语义。

测量：固定标识与数据，Release、单并发、预热 3 次，每轮 30 次、3 轮，1k/10k/100k Canonical Chapter，每章两条 SourceChapter 和两条 ContentVersion。保存原始耗时、分配量、进程峰值与报告摘要；PostgreSQL 记录 SQL 数量、行数和执行计划。纯规则与数据库端到端结果分开报告，峰值是进程生命周期指标，不冒充单次分配。测量不等于生产负载证明，也不宣称本包带来提速。

风险：迁移时漏规则、枚举漂移、排序和抽样变化、将顺序查询误称原子快照。通过固定输出、架构测试和真实 PostgreSQL 证据控制。

验证计划：Restore、Release Build、Unit/Architecture/Contract/Integration、11 Context 迁移漂移、Admin/Operations runtime、候选 SHA 的 CI/Docker/Security；本地 Docker 不可用时明确记录并取得远端证据。实际执行结果见下文，远端验收见下文。

AI 权威记录：[RF-02](../../repowiki/work-packages/2026-10-22-rf02-consistency.md)。

## 本地证据

Restore、Release Build（含独立基准项目）PASS，0 warning / error；Unit 656/656、Architecture 51/51、Contract 12/12 PASS。全量 Integration 已执行：8 passed、128 DockerUnavailable、3 skipped；PostgreSQL 基准同样因 `npipe://./pipe/docker_engine` 不可用而 BLOCKED，不能视为通过。

迁移前后规则基准各 720 次采样、24 组轮次，全部完整问题列表摘要和报告摘要一致（包括 200,030 个问题的样本）。原始数据：[before](benchmarks/rf02/before-rules.json)、[after](benchmarks/rf02/after-rules.json)。基线生产源码为 aa15b51，fixture/测量程序保存于 a043e24；后测为本包提取后的源码。固定整数生成 UUID、固定时钟、固定数据，无随机种子漂移。

本机共享开发环境测量期间存在其他构建活动，且 Windows 基准运行时曾发生 apphost 文件锁重建失败，进程退出后重建已通过；这些数据用于等价性与成本量级分析，**不作提速结论或 RF-03 性能预算依据**。RF-03 必须先在隔离且相同条件下重测、确定噪声和阈值。SQL 测量使用独立 CI Job 与容器、预热缓存，输出查询、行数、EXPLAIN ANALYZE/BUFFERS、分配量及原始时延；唯一约束禁止的重复只在纯规则 fixture 中注入，数据库 fixture 使用合法持久化的跨 schema 孤儿。

复现：`dotnet run --project tools/InkFlow.ConsistencyBenchmarks -c Release -- artifacts/consistency/rules.json`；有 Docker 时追加 `--postgres` 测量数据库路径（仅创建自有临时容器，不接受生产连接字符串）。报告耗时不包含 fixture 生成和 hash 校验，分配量为进程总分配差值，峰值为进程生命周期 Working Set；不同轮次峰值不能作单次峰值比较。

后测规则成本（p50 为三轮 p50 的中位数，p95 列为三轮范围；分配为 90 个样本中位数，单位 MiB）：

| Canonical 章节 | 数据 | p50 ms | p95 ms 范围 | 分配 MiB |
| --- | --- | --- | --- | --- |
| 1,000 | 健康 | 1.60 | 2.35–5.39 | 3.02 |
| 1,000 | 混合异常 | 2.14 | 3.19–5.44 | 3.94 |
| 10,000 | 健康 | 30.25 | 42.25–61.95 | 30.74 |
| 10,000 | 混合异常 | 69.30 | 80.44–105.84 | 39.91 |
| 100,000 | 健康 | 572.14 | 625.33–718.67 | 290.48 |
| 100,000 | 混合异常 | 883.29 | 986.88–1040.56 | 379.34 |

后测进程生命周期峰值达到 754.89 MiB，包含 fixture、完整报告 hash 等非计时工作。前测同类分配为 290.48 / 381.63 MiB；RF-02 没有解决全量元数据和问题集合的线性成本。

## PostgreSQL 端到端基线

候选 `3a38ad1` 的 CI `39766807840` / `consistency-measurement` Job 通过。环境：PostgreSQL 18.6、.NET 10.0.13、Ubuntu 26.04.1、4 CPU，单并发、缓存预热，每形状 3 轮 × 30 次。共 720 个样本，SQL 数量全部为 13，完整问题列表与报告摘要在每轮保持一致。[原始数据、SQL、行数与执行计划](benchmarks/rf02/postgres.json)。这是提取后的数据库基线，不能与本机纯规则耗时比较提速。

| 章节 | 数据 | 读取元数据行 | 问题总数 | p50 ms | p95 ms 范围 | 分配 MiB |
| --- | --- | --- | --- | --- | --- | --- |
| 1,000 | 健康 | 5,005 | 0 | 28.84 | 33.94–47.19 | 4.93 |
| 1,000 | 孤儿 | 5,005 | 2,000 | 15.23 | 22.23–35.33 | 5.51 |
| 10,000 | 健康 | 50,005 | 0 | 118.67 | 146.74–161.78 | 46.82 |
| 10,000 | 孤儿 | 50,005 | 20,000 | 132.40 | 148.90–173.57 | 55.19 |
| 100,000 | 健康 | 500,005 | 0 | 1,128.02 | 1,178.66–1,197.69 | 447.86 |
| 100,000 | 孤儿 | 500,005 | 200,000 | 1,362.18 | 1,409.93–1,463.04 | 530.45 |

空场景读取 0 行、0 问题，p50 为 6.90–7.30ms。100k 健康场景的 EXPLAIN ANALYZE 显示 13 个 Seq Scan，实际行数与快照相符；完整扫描符合当前无过滤查询，不能据此盲目加索引。进程生命周期峰值达到 819.93 MiB。形状顺序固定且仅预热 3 次，分层 JIT/缓存及 runner 噪声可能影响小样本；不把 1k 孤儿较快解释为异常数据更高效。RF-03 需复测稳定性后选定完整计数下的问题保留成本或输入投影成本作为目标。

候选首轮主验证 Job 的全部测试通过，但 Runtime 源码构建遇到 Ubuntu `noble-security` Packages.gz SHA256 mismatch，apt 以 exit 100 安全退出。独立 Docker 与 Security 工作流已全绿；已保留失败日志并仅重跑失败 Job，不绕过校验或修改 Docker 安全策略。重跑结果见下方验收结论。

## 验收结论

实现提交 `3a38ad175faaf041dea94df599477ac5f32cc869` 达到 Accepted / Completed：[CI 39766807840](https://github.com/nekohands/InkFlow/actions/runs/39766807840)（attempt 2）、[Docker 39766808118](https://github.com/nekohands/InkFlow/actions/runs/39766808118)、[Security 39766807728](https://github.com/nekohands/InkFlow/actions/runs/39766807728) 的全部 Job 均成功。CI Unit 656、Architecture 51、Contract 12；Integration 136 passed / 3 skipped，配置 Redis 后独立 1/1。Admin/Operations、其他业务 runtime、Core SLO/OTLP 与备份恢复通过。两项 opt-in 真实源与既有人工/生产验收仍未运行且不在 RF-02 关闭。

首轮 apt 校验失败已通过只重跑失败 Job 解除，保留原失败证据，没有关闭完整性检查。收尾另补管理端 `/api/v1/admin/consistency` 的已授权 JSON 字段/计数/截断断言；本机无 jq，实际执行依赖收尾提交的 CI。本机 Docker 仍缺失，不能把远端结果写成本地通过。最终源码格式整理后再次 Release Build 零警告、Unit/Architecture/Contract 同计数通过；本地 Integration 保持相同 Docker 阻塞。

下一步为 RF-03 测量决策：先隔离复测并冻结目标/容忍度，再决定是否优化；RF-02 没有实现查询裁剪、流式处理、保留问题上限优化或隔离级别变化。

## 2026-10-24 RF-07 历史收尾复核

RF-02 实现 `3a38ad1` 的验收证据有效；文档收尾 `c3347ca` 的 CI `39770903564` attempt 1 曾在测量和运行时结束前被取消，未生成测量文件，上传因此失败。RF-07 保存原始记录并读取日志后，在原 SHA 补跑完整 CI；[attempt 2](https://github.com/nekohands/InkFlow/actions/runs/39770903564/attempts/2) 两个 Job 均完成并通过，未改源码、断言、workflow 或预算。该 SHA 的 Docker `39770903527`、Security `39770903581` 原已通过。历史缺口已补齐，首次取消记录继续保留，取消发起者与原因未获得证据。整体综合回归与当前状态见 [RF-07 记录](optimization-refactoring-closeout-2026-10-24.md)。
