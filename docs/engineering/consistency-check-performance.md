# RF-03 一致性检查性能优化

状态：In Progress。基线提交 `2aaefce7ad2e43c782563c8867292ff3146cd4aa`。

目标是降低规则计算分配量与大数据集耗时，同时精确统计全部问题、按原 ordinal 顺序返回前 1,000 条。先复测、冻结目标，再改生产实现。范围限 Operations 一致性计算与测量/回归保护；不修改公共 JSON、权限、数据库结构、13 次 EF 查询及其非原子顺序读取语义，不进入 RF-04/05。

本地独立运行 Release DLL，.NET 10.0.13，`DOTNET_TieredCompilation=0`，并发 1，无同时构建；沿用 RF-02 固定数据，每轮预热 3 次、采样 30 次、重复 3 轮，覆盖空集与 1k/10k/100k 健康/损坏数据。进程生命周期峰值包含夹具与完整结果序列化，不作为单次扫描内存预算。远端重新运行 [PostgreSQL 基线](https://github.com/nekohands/InkFlow/actions/runs/39775842652)，使用原独立 runner/default JIT/warm cache 条件，只与同条件远端结果比较。

复测已完成，生产实现修改前冻结目标：本地 100k 健康/损坏分配中位数均降低至少 15%，损坏数据三轮 p50/p95 中位数均降低至少 20%；PostgreSQL 100k 健康/孤儿分配均降低至少 10%。各规模分配回归上限 5% +64 KiB，本地 p50/p95 上限 20% +1 ms；远端 >=10k 同上，<=1k 因已观察到 50.1% 波动，容忍 50% +5 ms，不宣称小样本提速。禁止优化后放宽门槛。

本地 100k 健康/损坏基线 p50 为 566.56/915.66 ms、p95 为 663.50/1042.45 ms、分配 290.48/379.34 MiB；轮间 p50 波动 13.1%/7.1%。远端对应 1186.06/1440.73 ms、1264.52/1521.09 ms、447.88/530.48 MiB，轮间波动均 1.1%。原始数据：[规则](benchmarks/rf03/before-rules.json)、[PostgreSQL](benchmarks/rf03/before-postgres.json)。验收须保留完整问题/报告哈希、计数、字段及排序；重点覆盖超过 1,000 条、后出现的小排序键、重复键首条语义与跨数据位置引用。示例保留有界不代表全量快照有界。

验证计划：diff 审查、Restore/Release Build/全量测试、Architecture/Contract/真实 PostgreSQL Integration、11 个上下文 Migration 检查、Admin JSON/Core SLO runtime、候选提交完整 CI/Docker/Security。重新确认本地 Docker；不能运行的项目写 BLOCKED/NOT RUN，依赖远端真实证据。未越过噪声或超出回归容忍度的优化撤回。

AI 权威记录与后续证据：[RF-03 Wiki](../../repowiki/work-packages/2026-10-22-rf03-consistency-performance.md)；前置证据：[RF-02](consistency-check-refactoring.md)。
