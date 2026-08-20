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

## 3. RuleAdapter DSL v1

允许：

- HTTP GET / POST
- Header / Query / Form
- Cookie/Session 引用
- CSS Selector / XPath / JSONPath
- Regex（必须 Timeout）
- Replace / Trim
- Variable / Template
- Pagination

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

## 8. Credential

Task Payload 不包含明文账号、Cookie、Token 或代理密码，只传 `CredentialReferenceId`。

Credential 必须有 Owner Scope：Platform / Organization / User。私人凭据不能跨租户任务使用。

## 9. Rule 发布

`Draft -> Validate -> Fixture Test -> Canary -> Published`

Canary 按少量任务逐步放量。异常时回滚到上一个不可变 Published Version。

## 10. Parser Regression

每个 Official Source 保存允许范围内的 Fixture/可重放样本。普通 PR CI 使用 Fixture，不实时访问第三方站点。

Live Source Test 独立定时运行，检测 Search、BookInfo、TOC、少量 Content 和 Capability Health。
