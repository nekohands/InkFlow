# RF-01 架构保护网

日期：2026-10-22。状态：`Accepted / Completed`，验收实现提交 `f90e6bd658ecc657dc68e34b1936990a4ce99555`。Agent 合同与完整证据见 [RF-01 工作包](../../repowiki/work-packages/2026-10-22-rf01-architecture-guards.md)。

## 目标与边界

将既定模块边界变成可执行检查，为后续重构提供保护。主体实现涉及测试、测试依赖与文档；强制 Docker 门禁另外发现既有 Collector 的 Go 标准库漏洞，补充了最小工具链补丁，详见下方记录。生产业务、公共协议、数据库与部署配置语义不变；RF-02 及之后的工作包保持 Planned。

模块内的 Domain、Application、Infrastructure 目前共用程序集，因此使用固定版本 `NetArchTest.Rules 1.3.2` 检查编译后的类型与方法体，不能用程序集引用代替分层检查。依赖只在 ArchitectureTests 中使用，标记 `PrivateAssets=all`。

## 检查规则

| 规则 | 拒绝的依赖 |
| --- | --- |
| DomainInfrastructure | Domain → EF Core、ASP.NET Core、StackExchange.Redis、Npgsql、BuildingBlocks.Persistence 或任一模块 Infrastructure |
| ModuleHost | 模块 → Api、Worker、Scheduler、Migrations 宿主 |
| ForeignInfrastructure | 模块 → 其他模块的 Infrastructure，包括 Persistence |
| ModuleGraph | 模块 → 下表未批准的其他模块 |

项目引用也检查宿主与模块依赖图，尚无类型使用的非法引用同样失败。仓库 `src/Modules` 项目清单、显式策略与测试项目引用必须一致，新模块、未知模块或漏掉的测试引用不能静默放行。

生产模块的手写类型必须位于所属模块命名空间。顶层编译器生成的无命名空间辅助类型，以及带对应 `GeneratedCode` 标记的正则生成器类型不参加命名空间归属检查；嵌套类型继承顶层归属。该例外不是业务依赖豁免。编译器生成的异步执行路径仍由依赖扫描器检查。

## 已审查的模块依赖

箭头仅表示允许引用的其他业务模块；BuildingBlocks 与模块自己的类型不在此表中。

| 模块 | 允许依赖的其他模块 |
| --- | --- |
| Identity | 无 |
| Library | Sources |
| Sources | 无 |
| Crawling | Library、Sources |
| Content | Library、Sources |
| Reading | Library、Content |
| Search | Library |
| Legado | Library、Content |
| Developers | 无 |
| Billing | 无 |
| Operations | 无 |

允许模块边不意味着允许访问其 Infrastructure。Search 当前没有业务类型，仍纳入项目清单、引用与程序集加载检查。新增依赖须先审查架构方向，再修改显式策略；不可自动将现有引用快照当作允许清单。

本包没有新增“所有 Application 均不得依赖本模块 Infrastructure”的规则，例如 Billing 的既有内部持久化调用不属于跨模块违规。反射字符串、运行时动态加载和业务语义不由这些静态测试证明。

## 测试与维护

```powershell
dotnet test tests/InkFlow.ArchitectureTests -c Release
```

测试从仓库内运行，项目清单检查需要找到 `InkFlow.sln` 和源项目文件。原程序集加载 smoke 保留。编译夹具覆盖四类违规以及方法体、异步状态机、泛型属性/约束、属性标注、内部嵌套类型和相似命名空间的合法对照。

初始四类负例在空策略下真实失败，添加规则后通过。扩展夹具发现库的命名空间正则筛选遗漏 `internal` 嵌套类型，已改为带点号边界的完整类型名前缀筛选，并保留回归。6 项无类型使用的非法项目引用也先失败再通过。目前 Architecture 为 **51/51**，11 个生产模块在本包约束下未检出违规。

额外的清单变异验证中，临时增加一个未审查模块项目，生产清单测试真实失败；移除临时项目后恢复通过，探针文件已清理。

本机 SDK 10.0.402。Restore/Release Build 通过（0 警告、0 错误），Unit 648/648、Contract 12/12、Migration 模型 11/11 通过。全量 Integration 实际结果为 8 通过、128 失败、3 跳过；128 项均因 Docker 初始化失败，3 项为两项可选真实源与未配置 Redis。`docker` 不在 PATH，Testcontainers 也无法连接 Docker named pipe，本地 Runtime 因此阻塞。修复候选的远端门禁已补齐容器/运行时证据，不将其写成本地通过。

源码入口：[策略](../../tests/InkFlow.ArchitectureTests/ModuleDependencyPolicy.cs)、[生产模块检查](../../tests/InkFlow.ArchitectureTests/ModuleBoundaryTests.cs)、[策略回归](../../tests/InkFlow.ArchitectureTests/ModuleDependencyPolicyTests.cs)、[编译夹具](../../tests/InkFlow.ArchitectureTests/Fixtures/DependencyFixtures.cs)。

## 强制门禁中的 Collector 修复

候选 `34cfa8f` 的 Security 与 CI 全部通过（含运行时/OTLP、Redis、备份恢复）；[Docker 39756402267](https://github.com/nekohands/InkFlow/actions/runs/39756402267)在 Collector 扫描发现 `CVE-2026-111943`（Go 1.26.9 标准库 HIGH）而失败，发布镜像拉取验证未执行，因此该候选不能验收。

依据 [Go 官方 1.26.10 发布说明](https://go.dev/doc/devel/release#go1.26.10)，仅将 Collector 的既有 Go 构建工具链升级一个补丁版本至 1.26.10，保留 Collector 0.162.0、x/net 0.60.0 和扫描门槛。该修复属于完成强制门禁所需的最小范围扩展，维护决策已记入 [ADR 0012](../adr/0012-compose-otel-collector-baseline.md)。修复提交 `f90e6bd` 已推送并通过全部门禁。

## 验收结果

| 门禁 | `f90e6bd` 的真实结果 |
| --- | --- |
| CI | [39758029891](https://github.com/nekohands/InkFlow/actions/runs/39758029891) 全部通过：构建、11 个模型、全量测试、Compose/业务 smoke、SLO/OTLP、Redis、备份恢复 |
| 测试 | Unit 648/648、Architecture 51/51、Contract 12/12；Integration 136 通过、3 跳过，Redis 独立配置后 1/1 通过；两项可选真实源仍未运行 |
| Docker | [39758029869](https://github.com/nekohands/InkFlow/actions/runs/39758029869) 全部通过：5 个镜像构建/扫描/发布与发布 Compose 镜像拉取 |
| Security | [39758029837](https://github.com/nekohands/InkFlow/actions/runs/39758029837) 全部通过：NuGet、文件扫描、SBOM、CodeQL |
| 文档 | 中文说明、计划、ADR、架构/安全文档、Progress/Handoff/profile 与 RepoWiki 已同步；索引和本地链接校验通过 |

RF-01 无剩余验收阻塞。静态检测的反射/动态加载限制与本地 Docker 缺失仍需保留；项目原有真机、真实来源/账户及生产观测治理验收不在本包关闭。下一工作包为 RF-02，目前未开始。
