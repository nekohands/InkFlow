# ADR 0005: 来源健康策略参数运行时配置化

- 状态：Accepted
- 日期：2026-08-29

## 决策

`SourceHealthPolicy` 的探针冷却曲线参数（连续失败阈值、基础冷却、冷却上限）从编译期 `const` 升级为运行时可配置：

- 曲线的**唯一算法实现**放在 Domain 不可变 record `SourceHealthParameters` 上（`ProbeCooldown`）；静态门面 `SourceHealthPolicy` 变为「当前装载参数」的只读视图。
- 组合根在宿主启动时调用 `SourceHealthPolicy.Configure(parameters)` 装载；参数来源是 `BuildingBlocks.Application.SourceHealthOptions.FromConfiguration(IConfiguration)`（配置节 `SourceHealth`，环境变量形式如 `SourceHealth__ProbeCooldownBaseMinutes`），经 Sources 模块的 `ToParameters()` 扩展映射。
- 未配置时使用与 v1 完全一致的默认值（3 次 / 30 分钟 / 1440 分钟）；配置校验失败（非整数、越界、max < base）在启动时快速失败。
- 算法版本 `source-health-v1` 与持久化状态保持不变——本次只改变常量的来源，不改变持久化语义，无 Migration。

## 约束

- `SourceHealthPolicy.Configure` 是进程级静态状态：只在组合根启动时装载一次，运行期不热更新；测试装载必须 try/finally 还原默认。
- `SourceHealthParameters.Default` 必须引用编译期常量而非静态属性快照，保证 `Configure(null)` 能真正恢复 v1 默认。
- BuildingBlocks 不依赖模块：配置类型在 BuildingBlocks.Application（依赖 `Microsoft.Extensions.Configuration.Abstractions`），模块映射扩展放 Sources.Application。

## 后果

- 运营可在不改代码、不重发布的情况下调整失败容忍度与重探节奏（尤其 linovelib 类污染源的高频重试成本）。
- 搜索排序/分页 v2 等后续工程项可复用「Options + 模块映射扩展 + Domain 参数 record」这一配置化路径。
