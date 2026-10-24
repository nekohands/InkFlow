# RF-07 综合回归与文档收尾

日期：2026-10-24。状态：In Progress。起点 `dev` / `6a5b1c9`。

本轮先核对 RF-01 至 RF-05 的验收提交、完整 CI Job 与冻结数据，再执行当前候选的综合回归，最后整理验收与交接。权威合同和证据入口见 [RF-07 Wiki](../../repowiki/work-packages/2026-10-24-rf07-regression-closeout.md)。

## 范围与验收

- 核对历史实现 SHA、远端 workflow/Job/attempt、Git 祖先关系、原始测量与已知失败修复。历史通过仅作为前序验收证据，当前候选仍需实际回归。
- 执行 Restore/Release Build、全量 Unit/Architecture/Integration/Contract、11 个上下文迁移检查；通过候选 CI 补齐真实 PostgreSQL/Redis、Compose、Reader/Legado/Developer/Admin/Source/私有书库/采集包、SLO/OTLP 与备份恢复。
- 复核一致性性能预算、Reader 输出/浏览器等价和 Developer 协议/账本/SQL 等价；保留既定阈值和断言。
- 同步 Wiki/目录元数据、方案、验证入口、Progress/Handoff/profile 及本记录；所有适用门禁通过后验收。

本轮计划修改文档、状态索引和非敏感证据；已有 `dev` 推送会按配置触发扫描后的候选镜像构建发布。RF-06 是条件项，本轮保留延期决定。真实设备、账户、上游来源及生产环境验收沿用既有开放清单。

## 文档预检

发现 RF-05 Wiki 的来源链接使用行内 Sources 标签，未符合索引生成器的章节格式，`--check` 因此失败。修复仅补齐 Sources 章节边界，完整保留来源链接、验收事实和历史失败证据，再重新生成索引/元数据。其余未变化区域复用已验证的文档采纳基线；完成检查后再进入本轮测试门禁。

## 当前限制

本机 SDK 10.0.402；docker 命令不在 PATH。后续仍实际执行适用测试并保留环境失败，真实容器与运行时结论以本轮候选 CI 为准。历史人工/生产验收不由本轮自动化回归关闭。

预检修复结果：生成器 `--write` / `--check` 通过（40 个 Wiki 页面），本地链接和 `git diff --check` 通过。Workflow conformance = COMPLIANT，受影响范围 RepoWiki sync = PASS，进入前序证据核对。

## 前序证据核对

已核对五个实现和五个文档收尾提交，共 10 个 SHA 的祖先关系与 30 条 workflow 的定义、完整 Job 集合、attempt 和目标 SHA。实现提交的 15 条全部通过；文档收尾的 15 条中有 14 条通过。

发现 RF-02 文档收尾 `c3347ca` 的 CI `39770903564` attempt 1 在 2026-10-22 11:32:59 UTC 被取消，测量与运行时未完成，测量上传因没有生成文件而失败。已读取原始日志并保存 [初始核对快照](benchmarks/rf07/historical-audit-initial.json)，正在原 SHA 补跑完整 CI；取消由谁发起、具体原因未获得证据，不作推断。RF-02 实现 `3a38ad1` 的独立验收通过结论仍有证据支持，文档收尾缺口不得混为绿灯。

当前 `6a5b1c9` 自身已有全部门禁通过，当前代码的独立回归可与历史补跑并行；RF-07 最终验收必须等待两边完整通过。既有原始数据复核确认：RF-02 24 组完整/公共报告哈希及数量一致，720 项一致性采样，PostgreSQL 每项 13 次 SQL；RF-04 的四屏行为一致、前后各 450 项采样、40 张 Linux 截图零像素差异，无未捕获页面错误。新候选的性能与运行时仍需实测。

## 当前候选的本地回归

| Gate | 实测结果 |
| --- | --- |
| Restore / Release Build | PASS，解决方案及三个测量/Fixture 工具均 0 警告/错误 |
| Unit / Architecture / Contract | PASS，726 / 53 / 12 |
| 本地全量 Integration | 8 通过、128 DockerUnavailable 失败、3 跳过；本地容器门禁 BLOCKED，须由候选 CI 补足 |
| Migration drift | PASS，11/11 |
| 比较器回归 | PASS，一致性 6、Developer 2、截图边界 3 |
| 冻结数据重放 | RF-03 原始本地/PG 预算及 RF-05 810 项协议/账本/SQL 比较全部 PASS |
| GitHub 产物核对 | 六份关键文件与 Git Blob 逐字节相同，并绑定五个原始产物 ID/目标 SHA |
| 本地隔离测量 | 720 项 / 24 组，全部冻结预算与报告等价 PASS |

首次本地复测的 1k 健康场景 p95 为 4.3031ms，高于 4.23496ms 限值；运行与本轮产物下载核对重叠，隔离前提不满足。原始失败完整保留，停止本轮并行构建/下载后，以相同二进制、预热、轮次、采样、阈值和哈希协议隔离重测，p95 为 2.3926ms，所有门禁通过。本轮没有修改性能代码或放宽预算。后续每次本地性能门禁继续执行“无本轮并行构建/下载”的运行控制（RF07-M01，维护者负责），外部主机活动仍是测量限制。

原始结果：[产物来源与字节核对](benchmarks/rf07/artifact-provenance.json)、[本地验证清单](benchmarks/rf07/local-verification.json)、[初次失败](benchmarks/rf07/initial-rules-comparison.txt)、[隔离复测](benchmarks/rf07/isolated-rules-comparison.txt)。当前候选完整远端门禁及历史 RF-02 补跑仍为 PENDING，尚不标记 RF-07 完成。
