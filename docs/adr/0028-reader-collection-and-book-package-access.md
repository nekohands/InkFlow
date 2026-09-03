# ADR 0028：Reader 采集与书籍包使用权限

- 状态：Accepted for 1.0 Release Candidate
- 日期：2026-09-03
- 范围：Identity / Crawling / Content / Operations / API / Reader UI

## 背景

采集运行和书籍打包原先全部复用运维权限，普通 Reader 无法使用下载能力。产品需要让登录后的普通用户输入书籍地址、查看采集进度并下载符合内容政策的书籍包；同时，书源状态可以被查看，但来源启停、能力控制和运维修复不能下放。

现有 `CollectionRun` 与 `BookPackageJob` 是平台级任务，当前没有任务所有者字段或按用户隔离的查询模型。本次需求没有要求引入任务归属、配额或私有历史，因此先调整能力边界，不虚构用户级隔离。

## 决策

1. 新增独立的 `OperationsSnapshotRead`、`CollectionUse` 和 `BookPackageUse` policy，均允许已认证 `Reader`、`Operator` 和 `Administrator`。
2. Reader 可以：
   - 查看不含死信、一致性和告警运维区块的来源状态快照；
   - 创建和查看采集运行；
   - 创建、查看和下载已完成的 EPUB 3、单文件 TXT、ZIP 书籍包。
3. Reader 不能执行来源停用/恢复、来源能力停用/恢复、死信重放、失败任务删除、取消任务清理、内容治理、告警历史和其他运维修复命令；这些入口继续使用原有 Operator/Administrator policies。
4. Reader 页面只显示采集、打包和来源状态页签；来源卡片不渲染写操作按钮，运维页签和运维摘要由角色门禁隐藏。后端 policy 是最终安全边界，前端隐藏不是授权依据。
5. 书籍地址校验、Source 边界、Canonical Content、Content Policy、包完整性、审计和既有速率限制不因角色放宽。包下载仍只允许已完成且有合法 artifact 的任务。

## 不采用的方案

- 不把 `CrawlerRepair` 或 `OperationsRead` 直接扩大到 Reader；这会同时放开死信、清理、来源或一致性运维能力。
- 不在本次需求中新增任务 `OwnerId`、私有列表和按用户配额；当前平台级任务事实仍按现有模型提供有限列表。

## 后果与后续

- Reader 可以完成采集到书籍包下载的用户闭环，运维操作仍保持最小权限边界。
- 当前采集/打包列表仍是平台级有限列表，不代表私有任务历史。若后续要求 Reader 只能看或控制自己的任务，必须先增加稳定的任务归属、查询过滤、控制授权和数据库迁移，再开放对应按钮。
- `0021-collection-run-control-and-book-packages.md` 中关于“仅运维角色使用”的权限结论由本 ADR supersede；其状态机、包格式和内容安全决策继续有效。
