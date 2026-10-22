# RF-05 Developer API 行为基线与职责拆分

日期：2026-10-23。状态：Accepted / Completed at `51fa810`。起点 `5841f30`，先固定认证、配额与审计行为，再拆分自助管理、Administrator 套餐管理和外部只读目录。

AI 合同与证据：[RF-05 Wiki](../../repowiki/work-packages/2026-10-23-rf05-developer-api.md)。本包落实既定方案与 ADR 0009，不改变协议、认证策略、数据库、查询、生命周期服务或 UI，也不进入 RF-06。

## 冻结的兼容性要求

| 边界 | 当前行为及验收要求 |
| --- | --- |
| 认证 | 管理接口使用 Web Bearer；外部读取只接受专用 API Key Header，检查 scope/environment；撤销、过期、停用用户拒绝 |
| 顺序 | 认证 → 参数校验 → 配额准入 → 正典读取；参数错误不预留配额，已通过配额后查无资源的 404 仍计账 |
| 配额 | 保留 operation、1/5 单位、TraceId、用户级 UTC 月份、quota-v1、429/Retry-After 与存储不可用时 503；复用现有 ReserveAsync |
| 响应 | DTO 字段/大小写、一次性 secret、401/403/404/409/429/503、缓存头位置均保持；当前早期参数错误及扣费后 404 没有 helper 的 no-store 头 |
| 审计 | 保留 actor/resource/reference/reason/TraceId；成功命令审计记 200，即使 HTTP 为 201/204；持久化后审计使用 CancellationToken.None，普通审计故障记录并吞掉，审计取消异常仍传播 |
| 所有权 | 用户只能管理自己的应用/密钥；管理员套餐入口独立策略与 Billing 服务端校验；明文 secret 仅成功签发/轮换返回 |

## 实施与验证顺序

1. 使用真实路由、认证 Handler 和策略建立 HTTP 特征测试，以确定性替身注入存储/审计故障；结合既有真实生命周期和 PostgreSQL 测试覆盖持久化边界。
2. 基线测试通过并单独提交后，按自助管理、管理员管理、外部读取拆分映射入口与协议辅助代码。保留 DTO 命名空间，Host 显式组合，不引入改变顺序的通用中间件或重复配额算法。
3. 每片检查 diff 并执行定向测试；最终执行 Restore/Release Build、全量测试、Architecture/Contract、11 个上下文迁移检查、真实 Developer/Billing PostgreSQL/Redis、Developer/Admin/既有 Runtime/SLO/备份及目标 SHA CI/Docker/Security。
4. 记录前后行为与诊断数据。本包以结构与行为等价验收，不宣称提速；RF-03 预算与 Core SLO 不放宽。替身验证不替代真实持久化，本机 Docker 缺失时明确标记并由 CI 补足。

真实设备、真实账户、来源及生产环境验收沿用原待验收清单，不由本次结构重构自动关闭。

## 基线准备记录

生产代码尚未修改。定向测试 70 项通过：新增路由/审计/取消 62 项，真实应用服务 8 项（含新增所有权和失效凭据 4 项）。TestHost 会把缓存头规范化为 `no-store, private`，测试按其规范化结果断言，生产仍写 `private, no-store`。

诊断程序 `tools/InkFlow.DeveloperBenchmarks` 使用 PostgreSQL 18 Alpine，固定 1 本书/1 章/1 个当前正文、UTC 时钟、并发 1。9 种请求分别预热 3 次，采样 30 次，共 3 轮/810 项。真实持久化密钥认证、Billing 配额账本、Library/Content/政策读取；用户资格、限流计数器、审计 sink 固定，配额缓存为空实现，不启用可选正文重选。记录响应/协议头、账本计数与单位增量、四个上下文 EF 执行 SQL 命令数/哈希及耗时；不输出参数值或原始密钥。行为和 SQL 必须完全等价，耗时仅诊断；完整运行时与 Redis 仍走原门禁。本机 docker 命令缺失，真实 PostgreSQL 基线由 CI 在生产拆分前生成。

本地基线门禁：Restore/Release Build 通过（0 警告/错误）；Unit 726/726、Architecture 53/53、Contract 12/12 通过。已实际执行全量测试：Integration 8 通过、128 项 DockerUnavailable 失败、3 跳过，Windows Docker named pipe 不可用；本地 PostgreSQL/Runtime 为 BLOCKED，等待远端实测。

已冻结生产基线 `4804dc0`：CI `39814163301` 的 developer-measurement Job `119948108625` 在生产改动前成功完成，810 项原始观察保存在 [before-postgres.json](benchmarks/rf05/before-postgres.json)。每请求 SQL 命令数：搜索/列表 10、书详情/目录 11、正文 13、无效密钥 1、无效参数 3、扣费后正文缺失 9、超额 7。比较器拒绝缺失/重复采样及协议/账本/SQL 漂移，故障注入测试通过；Host 组合测试补齐全部 15 条 Developer 路由的策略与限流元数据。此时完整基线 CI 尚未结束，不作为 RF-05 验收。

## 实现检查点

`4804dc0` 行为基线及 `9d7c558` PostgreSQL 观察冻结提交先于所有生产修改。原 904 行混合文件已删除，自助管理 326 行、管理员管理 79 行、外部目录 273 行；DTO、原配额/审计 helper、用户状态适配器各自归位。Host 显式组合三个入口。基线 fixture 仅改组合调用，断言未改；文本核对审计、配额 helper（仅身份类型归属变化）和公共 DTO 声明一致。相关 Developer/组合/AuditedResponse 测试 82/82 通过，最终完整门禁及验收仍待执行。

候选本地验证：Restore/Release Build 与测量程序 Build 通过，0 警告/错误；Unit 726、Architecture 53、Contract 12 全通过；全量 Integration 实际执行为 8 通过 / 128 DockerUnavailable 失败 / 3 跳过。迁移模型 11/11 通过。Diff/安全审查确认认证策略、Schema、事务、查询、公共 DTO 与密钥暴露规则未改，仅职责/组合及 helper 位置变化。候选 PostgreSQL 等价、运行时、安全和 CI 在推送前为 NOT RUN。

## PostgreSQL 前后对比

候选 `51fa810613ba90261f379905bc91b1cefc88ac93`，CI `39815455574` 的测量产物：810 项状态/协议头/完整响应、账本数量及单位增量、SQL 数量及命令哈希完全一致。原始数据：[before](benchmarks/rf05/before-postgres.json)、[after](benchmarks/rf05/after-postgres.json)、[比较报告](benchmarks/rf05/comparison.txt)。不同 CI runner 的耗时仅诊断，不宣称提速，也不放宽 Core SLO 与 RF-03 预算。

| 请求形态 | SQL 前后相同 | 基线 p50 / p95 ms | 候选 p50 / p95 ms |
| --- | ---: | ---: | ---: |
| book | 11 | 9.409 / 11.154 | 10.261 / 12.026 |
| chapters | 11 | 9.405 / 10.552 | 9.715 / 11.818 |
| content | 13 | 10.722 / 12.794 | 11.233 / 13.019 |
| exceeded | 7 | 5.684 / 6.816 | 5.949 / 7.041 |
| invalid-key | 1 | 1.255 / 1.365 | 1.475 / 1.793 |
| invalid-parameter | 3 | 2.798 / 3.470 | 3.079 / 3.636 |
| list | 10 | 9.244 / 10.381 | 9.934 / 11.328 |
| missing | 9 | 7.688 / 9.211 | 8.374 / 10.102 |
| search | 10 | 9.475 / 11.497 | 10.491 / 12.110 |

## 验收与交付

实现 SHA `51fa810613ba90261f379905bc91b1cefc88ac93` 的 [CI 39815455574](https://github.com/nekohands/InkFlow/actions/runs/39815455574)（4/4）、[Docker 39815455624](https://github.com/nekohands/InkFlow/actions/runs/39815455624)（6/6）、[Security 39815455651](https://github.com/nekohands/InkFlow/actions/runs/39815455651)（4/4）全部 Job 成功，无需重跑或放宽断言。前序基线 `4804dc0` 的 CI 39814163301、Docker 39814163499、Security 39814163269 也全部通过，测量先于生产拆分完成。

- Build：Restore、Release Build、测量程序 Build 全部 PASS，0 警告/错误。
- Tests：Unit 726、Architecture 53、Contract 12、迁移模型 11/11 PASS；真实 PostgreSQL Integration 136 通过 / 3 跳过，Redis 单独配置运行 1/1 PASS。
- Runtime：源码 Compose、Developer 生命周期/secret 脱敏/Header 认证/权重配额/429/账户隔离/禁用用户/轮换撤销、Admin 套餐和审计、既有 Reader/Legado/Source/采集包/私有书库、Core SLO、OTLP 四入口指标、PostgreSQL 备份恢复均 PASS；发布镜像 Compose 和全部安全扫描 PASS。
- 等价性：810 项前后响应/协议头/账本增量/SQL 数量及哈希完全相同；逐个处理器、原审计/配额 helper、公共 DTO 声明已核对，仅入口和 helper 类型限定名变化。
- 发现及修复：基线准备时修正 TestHost 缓存头排序断言，以及直接调用路由 fixture 的 sub claim/末尾斜杠处理；没有生产行为修复，也没有放宽冻结后的期望。
- 剩余限制：本地 Integration 仍为 8 通过 / 128 DockerUnavailable 失败 / 3 跳过，不算本地通过；缺失证据由上述真实 CI 补足。两项可选真实源 NOT RUN，默认 Redis skip 已由配置运行覆盖；真实设备、账户与生产验收继续开放。RF-05 无剩余验收阻塞。
- 文档同步：Wiki/中文架构、Developer 协议说明、模块/验证索引、方案、Progress/Handoff/profile 与原始数据均同步。文档收尾提交需另查自身 CI，不能沿用实现 SHA 的状态。
- 下一步：按原方案进入 RF-07 综合回归与证据收尾；RF-06 仍是按证据决定的条件项，本包不自动实施。
