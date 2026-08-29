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
- Cookie/Session 引用
- CSS Selector / XPath / JSONPath
- Regex（必须 Timeout）
- Replace / Trim
- Variable / Template
- Pagination

上面的列表描述 DSL v1 的目标 AST 能力，不等同于当前执行器全部可用。现阶段 `RuleAdapter` 的执行基线
覆盖 GET/POST、Header/Query/Form、路径占位符、CSS 选择器、带超时 Regex、Trim/Replace 和 Search/TOC
列表绑定；Schema 保留 XPath/JSONPath 枚举以保持 AST 的前向兼容，但当前 `CssSelectorEvaluator` 只执行 CSS。
`SourceRuleExecutionLimits` 已为当前单请求执行器接入有限的 MaxRequests、MaxBytes、MaxExecutionTime、
MaxRegexTime 和 MaxResultSize；生产 HTTP 客户端在解码前按流读取并拒绝超大响应。XPath/JSONPath 引擎、
Cookie/Session、Pagination、通用变量扩展，以及多请求/递归执行所需的 MaxRedirects/MaxDepth 策略化，仍需
后续运行时工作包和独立回归，不能仅凭 JSON 解析通过将规则标记为 Published 或宣称真实来源可用。

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
再直接连接同一批已验证 IP；80/443 之外的端口拒绝，自动重定向最多 5 跳且每个新目标重新走
连接级校验。`SsrfGuard` 的请求前字面量/DNS 检查保留为第一道防线，Handler 是防止默认
`HttpClient` 再次解析造成 rebinding 的执行约束。

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

错误或恶意规则不得耗尽整个 Worker Pool。

当前单请求基线的默认上限为：MaxRequests=1、MaxBytes=2 MiB、MaxExecutionTime=20 s、
MaxRegexTime=2 s、MaxResultSize=512 KiB。请求体、解码后的响应体、字段聚合结果和 Search/TOC 列表
均 fail-closed；`SsrfSafeHttpMessageHandler` 另以最多 5 跳限制自动重定向。Pagination/递归尚未进入
执行器，因此 MaxDepth 不在本基线中宣称已实现。

## 8. Credential

Task Payload 不包含明文账号、Cookie、Token 或代理密码，只传 `CredentialReferenceId`。

Credential 必须有 Owner Scope：Platform / Organization / User。私人凭据不能跨租户任务使用。

## 9. Rule 发布

`Draft -> Validate -> Fixture Test -> Canary -> Published`

Canary 按少量任务逐步放量。异常时回滚到上一个不可变 Published Version。

## 10. Parser Regression

每个 Official Source 保存允许范围内的 Fixture/可重放样本。普通 PR CI 使用 Fixture，不实时访问第三方站点。

Live Source Test 独立定时运行，检测 Search、BookInfo、TOC、少量 Content 和 Capability Health。
