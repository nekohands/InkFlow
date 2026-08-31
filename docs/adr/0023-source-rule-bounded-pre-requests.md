# ADR 0023：Source Rule 有界串行前置请求链

- 状态：Accepted for 1.0 Release Candidate
- 日期：2026-08-31
- 范围：Sources / Crawling / Source Rule DSL

## 背景

部分来源在主请求前需要先访问同源入口、建立短期响应 Cookie，或从 bootstrap 响应提取一次性 token。
已有 `RulePagination` 只覆盖 Search/TOC 的分页续页；把这类流程留给任意脚本会绕过 Community Source
的 DSL、安全和预算边界，把它们直接扩展成通用请求编排又会引入递归、分支和持久会话风险。

## 决策

1. `CapabilityRule` 增加可选 `PreRequests`。每个 `RuleRequestStep` 包含稳定的诊断名称、一个现有
   `RuleRequest` 和可选的 `RuleResponseVariable` 列表；步骤严格按声明顺序执行，最后再执行主请求。
2. 前置请求最多 8 步，必须通过现有 Rule Request 校验；每个请求只能由来源 `BaseUrl` 加路径模板构建，
   出网前继续执行绝对 URL、SSRF、userinfo/fragment 和同源检查。成功响应的最终 URI 必须保持来源 origin。
3. 前置步骤与主请求、分页续页共享一次执行的 `MaxRequests`、累计响应字节、执行时间、Regex、结果大小和
   临时变量预算。前置响应正文不写入 `RuleExecutionResult`；任一步失败、派生变量缺失、跨源或预算超限，
   整次执行失败且不返回部分结果。
4. 前置步骤可以通过既有受控 Selector/Regex 与 Trim/Replace 派生临时变量，变量只留在当前执行内，供后续
   前置步骤、主请求和 page-number/cursor 续页使用。`RuleSession` 若声明，则使用同一个执行期 Cookie jar；
   CredentialReference 解析一次后按既有 typed Header 规则应用到每个请求。
5. DSL 不提供动态请求 URL、分支、循环、递归、跨执行状态或持久化 Session。动态多请求和递归仍须另行设计
   独立的深度、重定向、资源和审计策略，不由本 ADR 隐式开启。

## 考虑过的方案

- 只支持主请求和三种分页：无法表达同源 bootstrap/token/cookie 前置步骤，来源适配器会被迫升级为可信代码。
- 允许规则声明任意 URL 或脚本：会扩大 SSRF、凭据泄露和无限执行面，违反 Community Source 的受限 DSL 边界。
- 引入递归/通用 DAG 调度器：能力远超当前需求，需要新的持久化状态、取消、深度和可观测性设计，本轮不采用。

## 后果

- Source Rule 能覆盖有限的同源预热、认证握手和 token 派生流程，同时复用既有请求、Cookie、凭据、SSRF
  和执行预算门禁。
- 前置响应不会成为可读结果或日志数据，执行结束后变量和 Cookie 自动丢弃；规则发布仍可由 Validator 与严格
  JSON Schema fail-closed。
- `PreRequests` 仍不是真实来源可用性的证明；真实站点、反爬、凭据、阅读 3.0/MuMu 和人工验收继续按待定
  清单处理。

