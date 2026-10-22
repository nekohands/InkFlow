# RF-05 Developer API 行为基线与职责拆分

日期：2026-10-23。状态：In Progress。起点 `5841f30`，先固定认证、配额与审计行为，再拆分自助管理、Administrator 套餐管理和外部只读目录。

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
