# Source Runtime Specification

## 1. Source 类型

- Official：官方维护，允许 RuleAdapter 与可信 CodeAdapter。
- Community：用户提交，仅允许受限 DSL/Sandbox，通过审核和安全治理后公开。
- Private：用户或组织私有，可选择 Cloud Trusted Worker 或 Private Worker。

公共 Canonical Library 的最终写入只接受可信执行域结果。

## 2. Adapter 抽象

统一 `ISourceAdapter`，以 Capability 声明能力而不是依赖 `NotImplementedException`：

- Search
- BookInfo
- Toc
- Content
- Update
- Login
- BrowserRequired
- Images

来源健康按 Capability 细分，例如 Search 可故障而 Content 仍健康。

当前已落地的 Capability Health v1 保持在 `sources.capability_health`，以
`(SourceId, Capability)` 为复合键。`Unknown`、`Healthy`、`Degraded` 默认仍可用；
同一能力连续 3 次失败进入 `Unhealthy` 并暂时退出调度/正文候选，`Disabled` 支持运营侧主动停用，
恢复或成功探测会清除失败连续计数。状态转移保存 `source-health-v1`、时间戳和受限失败原因，
不把 Redis 或缓存当作事实来源。

所有成功、失败、停用和恢复变更均通过 `ISourceHealthRepository.MutateAsync` 在权威存储内完成；
PostgreSQL 实现以 `(SourceId, Capability)` 的稳定摘要获取事务级 advisory lock，
在锁内重新读取、执行领域状态转移、保存并提交，避免 API/Worker 并发上报覆盖连续失败计数。
该并发协调不新增模型字段或 Migration，健康事实仍只由 PostgreSQL 保存。

运维侧通过受 `Operator` / `Administrator` 保护的 Source Operations API 查看单来源能力健康，
并以带理由的 disable/enable 命令控制单个 `(SourceId, Capability)`。停用立即阻止该能力进入
调度/候选；恢复只回到 `Unknown`，必须由后续真实探针确认 `Healthy`，命令和操作者写入审计。

Content 选优通过 `ContentSelectionService` 读取该能力状态：优先在可用来源中按质量选优，
全来源不可用时保留已落库当前版本；每次选择追加 `content.selection_decisions` 审计记录，
保存算法版本、候选/排除数量、选中版本和来源及回退标志。读取路径仍只读取 Canonical Content，
不会因故障切源而实时访问第三方站点。

## 3. RuleAdapter DSL v1

当前已落地的是最小声明式契约：`SourceRuleDslJson` 以版本化严格边界读取/写出 Rule JSON，配套
`docs/contracts/source-rule-dsl-v1.schema.json` 与离线 Fixture；未知属性、未知转换类型、缺失核心字段、
超大文档和超出集合/表达式预算的文档必须 fail-closed。`trim` / `replace` 以显式 `kind` 表示，新的持久化
文档使用字符串枚举和稳定 wire shape。

允许：

- HTTP GET / POST
- Header / Query / Form
- 受控 response-cookie Session（仅执行期策略；任务级初始凭据通过 CredentialReference 解析）
- CSS Selector / XPath / JSONPath
- Regex（必须 Timeout）
- Replace / Trim
- Variable / Template
- Pagination

上面的列表描述 DSL v1 的目标 AST 能力，不等同于当前执行器全部可用。现阶段 `RuleAdapter` 的执行基线
覆盖 GET/POST、Header/Query/Form、路径占位符、CSS 选择器、受控 XPath/JSONPath、带超时 Regex、
Trim/Replace、Search/TOC 列表绑定和三种受控 Pagination。`CapabilityRule.Session` 可为一次
RuleAdapter 执行声明受控的 response-cookie 会话：只接收成功响应的 `Set-Cookie`，按同源最终响应、
Domain/Path/Secure 和 Max-Age/Expires 规则向后续同源请求发送；策略最多 32 个 Cookie、累计 4 KiB、
最长 3600 秒，状态不进入 Rule JSON 值、日志、任务载荷或跨执行存储。Session 的最终响应 URI 若因
重定向离开来源 origin，整次执行失败；Cookie 数量/字节超限也整体失败。API、Worker、Scheduler 均注入统一的
`RuleSelectorEvaluator`：CSS 继续由 AngleSharp 处理；XML 兼容响应使用禁止 DTD/外部实体的 XML 导航，
非 XML HTML 使用有界的路径、子路径、属性/文本谓词和属性终端；JSONPath 仅开放
`$` root、property、quoted property、array index、wildcard 和 recursive-property 子集。
Search/TOC 列表绑定可声明 `itemsSelectorKind` 与可选 `textAttribute`，未声明时保持 CSS/文本的向后兼容。
不支持的 XPath/JSONPath 语法、非法 CSS、超大文档、超量匹配和超深 JSON 均 fail-closed；
`RulePagination` 仅用于 Search/TOC 列表：`nextLink` 首个请求沿用规则方法、后续链接统一以 GET 跟随；
`pageNumber` 在规则已声明且唯一的 query/form 参数中按 `startPage`/`pageStep` 递增，并用
`nextPageSelector` 判断是否继续；`cursor` 从 `cursorSelector` 读取游标并写入同样的已声明参数，保留原请求方法。
页码/游标参数不得同时出现在 query 和 form，GET 只能使用 query。next-link 必须保持来源
scheme/host/port 同源；所有模式都受 `maxPages`、`MaxRequests`、响应字节和执行时间预算约束。
循环链接、重复游标、跨源/带凭据/带 fragment 的链接、非法游标和超出边界的链路整体失败，
不暴露已抓页面。
`SourceRuleExecutionLimits` 已接入有限的 MaxRequests、MaxBytes、MaxExecutionTime、MaxRegexTime 和
MaxResultSize，以及调用方临时请求模板变量上下文的数量、名称、单值长度和累计 UTF-8 字节预算；默认
变量边界为 32 个、名称 128 字符、单值 2,048 字符、累计 16 KiB。`RuleRequest` 的路径、Header、Query
和 Form 模板值可使用 `{name}` 占位符；发布期与执行期都会拒绝未闭合/非法占位符、非法变量名和控制字符，
执行失败不回显变量值。生产 HTTP 客户端在解码前按流读取并拒绝单响应超限。完整 XPath/JSONPath 语法、
来源级默认绑定、Owner/Admin 凭据管理和真实 SecretProvider 之外的基于 CredentialReference 的任务级初始
认证已形成 typed Bearer/Basic/API-Key Header 基线；持久化 Session，以及
next-link/page-number/cursor 之外的多请求/递归执行所需的 MaxRedirects/MaxDepth 策略仍需后续运行时工作包和独立回归，
不能仅凭离线选择器测试将规则标记为 Published 或宣称真实来源可用。

`CapabilityRule.ResponseVariables` 已补齐有界的响应派生变量能力：仅允许在 page-number/cursor
续页实际存在时从当前响应按受控 Selector 或带超时 Regex 提取，并经过 Trim/Replace 后合并到同一次
执行的临时请求模板上下文；变量数量、名称、单值、累计 UTF-8 字节和控制字符继续复用同一预算。
派生值缺失、非法或超限会在下一次续页请求前整体失败，失败结果不暴露已抓响应、派生值或部分页面；
最后一页不要求派生变量。该能力不提供持久化状态、跨执行状态、通用多请求序列或递归编排。

禁止：

- 任意文件系统访问
- 任意 Socket
- Process / Shell
- Reflection / 动态程序集
- 任意 JavaScript eval
- 无限循环或图灵完备控制流

每个 Rule JSON 必须包含 `schemaVersion`。Published Rule Version 不可修改，变更创建新版本。

## 4. CodeAdapter

只允许 Official/Trusted Source 使用，用于 DSL 无法覆盖的复杂签名、登录流程、特殊 API 或浏览器自动化。

CodeAdapter 与 RuleAdapter 暴露相同 Application Contract，Scheduler/Crawler 不关心实现类型。

## 5. 分层抓取

按最低成本能力优先：

1. HTTP + HTML/JSON
2. HTTP + Cookie/Session/Header/Referer/签名/代理
3. Playwright：动态渲染、登录、交互
4. 人工辅助：首次登录、CAPTCHA 后会话接管

Playwright 使用独立 Browser Worker Pool、临时 BrowserContext、资源限制和受限网络。CAPTCHA 不自动破解。

## 6. Safe HTTP / SSRF

Community/Private Rule 的网络请求必须统一经过 SafeHttpClient：

- 拒绝 loopback、RFC1918、link-local、metadata endpoint、Docker/Kubernetes/数据库/Redis/Admin 内部地址。
- IPv4/IPv6、非标准 IP 表示和 DNS 解析后都需要校验。
- 每次 Redirect 重新解析并校验目标。
- 防 DNS rebinding，连接目标必须与已验证解析结果保持安全约束。

当前 API、Worker、Scheduler 的来源 HTTP typed client 及 Kanunu8 adapter 均接入
`SsrfSafeHttpMessageHandler`：关闭环境代理，在每次新 TCP 连接时重新解析并检查全部结果，
再直接连接同一批已验证 IP；80/443 之外的端口拒绝。无 Cookie 请求自动重定向最多 5 跳且每个
新目标重新走连接级校验；带显式 Cookie 的请求关闭自动重定向，避免 Cookie 被复制到跨源目标，
此类链路必须使用 RuleAdapter 的受控同源分页。`SsrfGuard` 的请求前字面量/DNS 检查保留为第一道
防线，Handler 是防止默认 `HttpClient` 再次解析造成 rebinding 的执行约束。

外部 API 永远不能接受任意 URL 并将 InkFlow 变成公共代理。

## 7. 执行限制

每次 Rule 执行具有预算：

- MaxRequests
- MaxBytes
- MaxRedirects
- MaxDepth
- MaxExecutionTime
- MaxRegexTime
- MaxResultSize
- RuleSession 的 Cookie 数量、累计字节和执行期生命周期上限

错误或恶意规则不得耗尽整个 Worker Pool。

当前基线的默认上限为：MaxRequests=8、MaxBytes=2 MiB、MaxExecutionTime=20 s、MaxRegexTime=2 s、
MaxResultSize=512 KiB。请求体、分页累计解码响应体、字段聚合结果和 Search/TOC 列表均 fail-closed；
`SsrfSafeHttpMessageHandler` 另以最多 5 跳限制自动重定向。递归尚未进入执行器，因此 MaxDepth 不在本基线中
宣称已实现。

## 8. Credential

Task Payload 不包含明文账号、Cookie、Token 或代理密码，只传 `CredentialReferenceId`。

活动 Worker 的 TOC、联动正文任务和 RuleAdapter 通过非敏感 `SourceExecutionContext` 传递该引用；
`ISourceCredentialProvider` 负责按 `SourceId + CredentialReferenceId` 解析，`ConfigurationSourceCredentialProvider`
仅是本地/容器配置适配器，读取 `SourceCredentials:<sourceId>:<referenceId>`，生产环境应替换为受治理的
Docker Secret、Vault 或云 Secret Manager。当前只允许 Bearer、Basic 和受限 API-Key Header 三种 typed
请求头投影；不支持凭据的 CodeAdapter 必须拒绝带引用的上下文。

引用 ID、材料和最终请求头均有长度/字符/禁止头名边界，引用解析受 Rule 执行时间预算约束；无效引用、
提供器缺失/失败/超时、非法材料和请求头冲突都在 HTTP seam 前失败关闭。secret 不进入 Task Payload、
Variables、Rule JSON、日志、错误文本、结果或对象的 `ToString()`。Provider 仍负责 Owner Scope、
跨租户授权和安全存储；本轮不提供来源级默认绑定、Admin 凭据管理或跨执行持久会话。

`RuleSession` 只处理本次 RuleAdapter 链路中由来源响应产生的短期 Cookie，不是凭据存储，也不能替代
`CredentialReferenceId`、初始登录、跨任务会话接管或人工 CAPTCHA 流程。

Credential 必须有 Owner Scope：Platform / Organization / User。私人凭据不能跨租户任务使用。

## 9. Rule 发布

`Draft -> Validate -> Fixture Test -> Canary -> Published`

Canary 按少量任务逐步放量。异常时回滚到上一个不可变 Published Version。

## 10. Parser Regression

每个 Official Source 保存允许范围内的 Fixture/可重放样本。普通 PR CI 使用 Fixture，不实时访问第三方站点。

Live Source Test 独立定时运行，检测 Search、BookInfo、TOC、少量 Content 和 Capability Health。
