# ADR 0015: Source Credential Provider 强制携带 Owner Scope

- 状态：Accepted
- 日期：2026-08-30

## 背景

4.56 至 4.59 已建立 CredentialReference 的 typed header 投影、来源默认绑定和
Administrator-only 绑定入口，但 Provider 解析契约只有 `SourceId` 与引用 ID。相同的
引用 ID 可能在平台、用户或组织范围内重名；如果 Provider 仅按引用 ID 查询，未来的
用户/组织来源可能越权读取另一个所有者的 secret。

## 决策

1. `ISourceCredentialProvider` 改为接收不可携带 secret 的
   `SourceCredentialResolutionContext`，其中同时包含 SourceId、CredentialReferenceId 和
   `SourceCredentialOwnerScope`。
2. Owner Scope 只有三种类型：`Platform`、`User`、`Organization`。Platform 不携带 OwnerId；
   User/Organization 必须携带非空稳定 Guid。非法类型、缺失身份或非法引用必须 fail-closed。
3. `SourceExecutionContext` 增加可选 Owner Scope；为兼容现有两参数调用，省略时按 Platform
   解释。Worker、Scheduler 侧的来源抓取路径显式传入 Platform；未来用户/组织请求必须显式
   传入对应 Owner Scope。
4. `ConfigurationSourceCredentialProvider` 只支持 Platform Scope。它仍是本地/容器配置适配器，
   不承担用户/组织授权，也不代表真实 Secret Manager 已接入。自定义 Provider 必须依据
   SourceId、引用和 Owner Scope 实施自己的所有者/跨租户授权。
5. Owner Scope 只约束解析授权，不改变 secret 生命周期边界：secret 不进入任务载荷、规则
   JSON、日志、错误、结果或解析上下文；最终仍只投影为受限请求头。

## 非目标

- 本 ADR 不引入用户/组织实体、支付、租户模型或凭据材料数据库。
- 本 ADR 不实现 Vault、云 Secret Manager、Docker Secret 或真实 secret 轮换。
- 本 ADR 不新增来源级持久会话，也不改变 CodeAdapter 的凭据继承规则。

## 后果

- Provider 实现必须显式处理 Owner Scope，避免把引用 ID 当作全局主键。
- 现有平台 Worker 仍可使用原有两参数 `SourceExecutionContext` 调用方式，但内部会归一化
  为 Platform；Provider 接口实现需要迁移到新的上下文参数。
- 后续接入用户/组织凭据时，可在不把所有者身份放入 secret 或任务载荷的情况下进行授权检查；
  在真实 SecretProvider 接入前，非平台范围会被当前配置 Provider 拒绝。
