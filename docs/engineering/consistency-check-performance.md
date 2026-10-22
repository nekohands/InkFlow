# RF-03 一致性检查性能优化

状态：In Progress。基线提交 `2aaefce7ad2e43c782563c8867292ff3146cd4aa`。

目标是降低规则计算分配量与大数据集耗时，同时精确统计全部问题、按原 ordinal 顺序返回前 1,000 条。先复测、冻结目标，再改生产实现。范围限 Operations 一致性计算与测量/回归保护；不修改公共 JSON、权限、数据库结构、13 次 EF 查询及其非原子顺序读取语义，不进入 RF-04/05。

本地独立运行 Release DLL，.NET 10.0.13，`DOTNET_TieredCompilation=0`，并发 1，无同时构建；沿用 RF-02 固定数据，每轮预热 3 次、采样 30 次、重复 3 轮，覆盖空集与 1k/10k/100k 健康/损坏数据。进程生命周期峰值包含夹具与完整结果序列化，不作为单次扫描内存预算。远端重新运行 [PostgreSQL 基线](https://github.com/nekohands/InkFlow/actions/runs/39775842652)，使用原独立 runner/default JIT/warm cache 条件，只与同条件远端结果比较。

复测已完成，生产实现修改前冻结目标：本地 100k 健康/损坏分配中位数均降低至少 15%，损坏数据三轮 p50/p95 中位数均降低至少 20%；PostgreSQL 100k 健康/孤儿分配均降低至少 10%。各规模分配回归上限 5% +64 KiB，本地 p50/p95 上限 20% +1 ms；远端 >=10k 同上，<=1k 因已观察到 50.1% 波动，容忍 50% +5 ms，不宣称小样本提速。禁止优化后放宽门槛。

本地 100k 健康/损坏基线 p50 为 566.56/915.66 ms、p95 为 663.50/1042.45 ms、分配 290.48/379.34 MiB；轮间 p50 波动 13.1%/7.1%。远端对应 1186.06/1440.73 ms、1264.52/1521.09 ms、447.88/530.48 MiB，轮间波动均 1.1%。原始数据：[规则](benchmarks/rf03/before-rules.json)、[PostgreSQL](benchmarks/rf03/before-postgres.json)。验收须保留完整问题/报告哈希、计数、字段及排序；重点覆盖超过 1,000 条、后出现的小排序键、重复键首条语义与跨数据位置引用。示例保留有界不代表全量快照有界。

验证计划：diff 审查、Restore/Release Build/全量测试、Architecture/Contract/真实 PostgreSQL Integration、11 个上下文 Migration 检查、Admin JSON/Core SLO runtime、候选提交完整 CI/Docker/Security。重新确认本地 Docker；不能运行的项目写 BLOCKED/NOT RUN，依赖远端真实证据。未越过噪声或超出回归容忍度的优化撤回。

AI 权威记录与后续证据：[RF-03 Wiki](../../repowiki/work-packages/2026-10-22-rf03-consistency-performance.md)；前置证据：[RF-02](consistency-check-refactoring.md)。

## 实现及本地测量

目标冻结提交 `95184eb` 在生产改动之前。第一步 `0ba30b1` 将九个 GroupBy/First 查找表改为预分配字典/TryAdd，重复键仍取首条。第二步服务用最大堆保留 ordinal 最小的 1,000 条，完整累计每条问题，最后按原四字段排序；完整验证入口与服务共用规则。

100k 健康：p50 566.56 → 354.79 ms，p95 663.50 → 411.79 ms，分配 290.48 → 159.87 MiB。100k 损坏：p50 915.66 → 356.73 ms，p95 1042.45 → 392.84 ms，分配 379.34 → 217.09 MiB。仅字典优化时损坏 p50 为 603.29 ms，限制示例保留进一步下降 40.9%。所有冻结的本地门槛通过，24 组完整问题/报告哈希与计数一致。[字典阶段原始数据](benchmarks/rf03/lookups-rules.json)、[最终原始数据](benchmarks/rf03/after-rules.json)。

性能检查脚本已加入独立 PostgreSQL CI Job，检查环境/数据等价及冻结门槛；环境漂移必须显式复测。脚本正反例 6/6、一致性测试 15/15、Restore/Release Build（0 警告/错误）通过。全量本地 Unit 659/659、Architecture 51/51、Contract 12/12、Migration 11/11 通过；Integration 8 通过/128 因 Docker 不可用失败/3 跳过，定向一致性集成测试同样在容器初始化失败。候选 CI 待完成；本地 Docker 命令仍不存在，真实容器验收由 CI 提供。

局限：全量快照、索引及重复规则分组仍为 O(rows)，每条问题仍构造消息/对象，仅保留示例有界；13 次非原子顺序读取不变。生命周期峰值不等于单次扫描峰值，不宣称生产负载/SLO 收益，不关闭既有人工/真实环境待验收项。
