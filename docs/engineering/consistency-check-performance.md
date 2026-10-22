# RF-03 一致性检查性能优化

状态：Accepted / Completed，验收实现提交 `0a6e64191e43f2abf32d5088dda4bc0399730bcd`。基线提交 `2aaefce7ad2e43c782563c8867292ff3146cd4aa`。

目标是降低规则计算分配量与大数据集耗时，同时精确统计全部问题、按原 ordinal 顺序返回前 1,000 条。先复测、冻结目标，再改生产实现。范围限 Operations 一致性计算与测量/回归保护；不修改公共 JSON、权限、数据库结构、13 次 EF 查询及其非原子顺序读取语义，不进入 RF-04/05。

本地独立运行 Release DLL，.NET 10.0.13，`DOTNET_TieredCompilation=0`，并发 1，无同时构建；沿用 RF-02 固定数据，每轮预热 3 次、采样 30 次、重复 3 轮，覆盖空集与 1k/10k/100k 健康/损坏数据。进程生命周期峰值包含夹具与完整结果序列化，不作为单次扫描内存预算。远端重新运行 [PostgreSQL 基线](https://github.com/nekohands/InkFlow/actions/runs/39775842652)，使用原独立 runner/default JIT/warm cache 条件，只与同条件远端结果比较。

复测已完成，生产实现修改前冻结目标：本地 100k 健康/损坏分配中位数均降低至少 15%，损坏数据三轮 p50/p95 中位数均降低至少 20%；PostgreSQL 100k 健康/孤儿分配均降低至少 10%。各规模分配回归上限 5% +64 KiB，本地 p50/p95 上限 20% +1 ms；远端 >=10k 同上，<=1k 因已观察到 50.1% 波动，容忍 50% +5 ms，不宣称小样本提速。禁止优化后放宽门槛。

本地 100k 健康/损坏基线 p50 为 566.56/915.66 ms、p95 为 663.50/1042.45 ms、分配 290.48/379.34 MiB；轮间 p50 波动 13.1%/7.1%。远端对应 1186.06/1440.73 ms、1264.52/1521.09 ms、447.88/530.48 MiB，轮间波动均 1.1%。原始数据：[规则](benchmarks/rf03/before-rules.json)、[PostgreSQL](benchmarks/rf03/before-postgres.json)。验收须保留完整问题/报告哈希、计数、字段及排序；重点覆盖超过 1,000 条、后出现的小排序键、重复键首条语义与跨数据位置引用。示例保留有界不代表全量快照有界。

验证计划：diff 审查、Restore/Release Build/全量测试、Architecture/Contract/真实 PostgreSQL Integration、11 个上下文 Migration 检查、Admin JSON/Core SLO runtime、候选提交完整 CI/Docker/Security。重新确认本地 Docker；不能运行的项目写 BLOCKED/NOT RUN，依赖远端真实证据。未越过噪声或超出回归容忍度的优化撤回。

AI 权威记录与后续证据：[RF-03 Wiki](../../repowiki/work-packages/2026-10-22-rf03-consistency-performance.md)；前置证据：[RF-02](consistency-check-refactoring.md)。

## 实现及本地测量

目标冻结提交 `95184eb` 在生产改动之前。第一步 `0ba30b1` 将九个 GroupBy/First 查找表改为预分配字典/TryAdd，重复键仍取首条。第二步服务用最大堆保留 ordinal 最小的 1,000 条，完整累计每条问题，最后按原四字段排序；完整验证入口与服务共用规则。

100k 健康：p50 566.56 → 354.79 ms，p95 663.50 → 411.79 ms，分配 290.48 → 159.87 MiB。100k 损坏：p50 915.66 → 356.73 ms，p95 1042.45 → 392.84 ms，分配 379.34 → 217.09 MiB。仅字典优化时损坏 p50 为 603.29 ms，限制示例保留进一步下降 40.9%。所有冻结的本地门槛通过，24 组完整问题/报告哈希与计数一致。[字典阶段原始数据](benchmarks/rf03/lookups-rules.json)、[最终原始数据](benchmarks/rf03/after-rules.json)。

性能检查脚本已加入独立 PostgreSQL CI Job，检查环境/数据等价及冻结门槛；环境漂移必须显式复测。脚本正反例 6/6、一致性测试 15/15、Restore/Release Build（0 警告/错误）通过。全量本地 Unit 659/659、Architecture 51/51、Contract 12/12、Migration 11/11 通过；Integration 8 通过/128 因 Docker 不可用失败/3 跳过，定向一致性集成测试同样在容器初始化失败。候选 CI 已全部通过；本地 Docker 命令仍不存在，真实容器验收由 CI 提供。

局限：全量快照、索引及重复规则分组仍为 O(rows)，每条问题仍构造消息/对象，仅保留示例有界；13 次非原子顺序读取不变。生命周期峰值不等于单次扫描峰值，不宣称生产负载/SLO 收益，不关闭既有人工/真实环境待验收项。

## 远端 PostgreSQL 复测

候选 `0a6e641` 的 [CI 测量 Job](https://github.com/nekohands/InkFlow/actions/runs/39778042207) 与冻结门槛检查通过；完整 runtime 已通过。[原始结果](benchmarks/rf03/after-postgres.json) 保留 720 个样本，24 组完整问题/报告哈希、13 条 SQL 与完整读取行数均一致，312 个查询计划实际行数合计也与快照一致。

100k 健康：p50 1186.06 → 733.04 ms，p95 1264.52 → 777.75 ms，分配 447.88 → 317.23 MiB。100k 孤儿：p50 1440.73 → 777.71 ms，p95 1521.09 → 814.28 ms，分配 530.48 → 385.98 MiB。优化后三轮 p50 波动为 2.34%/1.41%，各规模回归容忍度和主要收益门槛均通过。

远端进程生命周期峰值 902.87 → 908.53 MiB，**不能宣称进程峰值下降**；其中包括完整诊断结果及序列化。本地对应基线/字典/最终为 663.45/608.55/569.32 MiB，同样不等于扫描专属峰值。当前已达目标，因此本轮不扩展 SQL/流式读取；全量输入内存仍需后续独立测量。

## 复测命令

在仓库根目录串行执行，构建结束后再运行测量，避免并行 Build/Test 干扰；原始基线源码为 `2aaefce`，冻结预算为 `95184eb`，不要用优化后的代码覆盖基线文件。

```powershell
dotnet build tools/InkFlow.ConsistencyBenchmarks -c Release
$env:DOTNET_TieredCompilation = '0'
dotnet tools/InkFlow.ConsistencyBenchmarks/bin/Release/net10.0/InkFlow.ConsistencyBenchmarks.dll artifacts/consistency/current-rules.json
python scripts/compare-consistency-benchmarks.py docs/engineering/benchmarks/rf03/before-rules.json artifacts/consistency/current-rules.json
```

PostgreSQL 在 CI 独立 Ubuntu runner 中使用默认 JIT，工具只创建自有临时容器，不接受用户数据库连接：

```sh
dotnet run --project tools/InkFlow.ConsistencyBenchmarks -c Release -- artifacts/consistency/postgres.json --postgres
python3 scripts/compare-consistency-benchmarks.py docs/engineering/benchmarks/rf03/before-postgres.json artifacts/consistency/postgres.json
```

环境不匹配或门槛失败时保留失败样本并调查；变更环境需先明确新的测量范围、重新稳定复测，再冻结新预算，不自动调高阈值。Core SLO 既有门槛独立保留。

## 验收结果

实现提交 `0a6e641` 的 [CI](https://github.com/nekohands/InkFlow/actions/runs/39778042207) 2/2、[Docker](https://github.com/nekohands/InkFlow/actions/runs/39778042314) 6/6、[Security](https://github.com/nekohands/InkFlow/actions/runs/39778042320) 4/4 Job 全部成功。远端 Restore/Release Build、11 上下文 Migration、Unit 659、Architecture 51、Contract 12 通过；真实 PostgreSQL Integration 136 通过/3 跳过，配置 Redis 后独立 1/1 通过。源码 Compose、Reader/Legado/Source failover/质量失败/私有库/Developer/Admin/采集与包/OTLP、Core SLO 和备份恢复均通过。Admin JSON 断言在 12:42:00 UTC 通过，Core SLO 在 12:43:48、备份恢复在 12:43:58 通过。

本地及 PostgreSQL 冻结预算全部达成，两项优化均保留，未降低阈值。基线自比较故意触发收益门槛失败，证明检查会拒绝无收益结果；不是实现失败。37 页 Wiki 目录与修改文档链接校验通过。工作包内无剩余阻塞；本地 Docker 缺失已如实保留并由真实 CI 验证补足。既有真实设备/账户/上游/生产验收仍开放。下一计划为 RF-04，本次未实施。
