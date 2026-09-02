# InkFlow 账户中心需求与验收

- 状态：1.0 Release Candidate
- 范围：Reader / Identity / Personal Legado Token
- 更新：2026-09-02

## 设计对齐

账户中心采用常见的设置型信息架构：

- Google Account 将个人信息与安全设置作为账户中心的主要分区；安全设置覆盖密码、恢复信息和更强的登录验证。
- GitHub 将个人账户设置、访问令牌和安全日志分开管理；令牌具备独立的创建、撤销和审计边界。
- Discord 将显示名、邮箱、密码放在独立的账户设置中，敏感资料变更要求再次验证密码。
- Steam 将个人资料与隐私可见性作为单独设置，而不是与阅读/内容入口混在一起。
- 阅读类产品的书架与历史是高频导航入口，InkFlow 继续放在 Reader 顶部导航，账户页不重复展示“阅读空间”。

参考：

- [Google Account 帮助：管理账号](https://support.google.com/accounts/answer/16124968?hl=zh-Hans)
- [GitHub Docs：管理个人访问令牌](https://docs.github.com/en/authentication/keeping-your-account-and-data-secure/managing-your-personal-access-tokens)
- [GitHub Docs：安全日志](https://docs.github.com/en/authentication/keeping-your-account-and-data-secure/reviewing-your-security-log)
- [Discord：修改密码](https://support.discord.com/hc/en-us/articles/218410947-How-to-Reset-or-Change-your-Forgotten-Password-on-Discord)
- [Discord：修改邮箱](https://support.discord.com/hc/en-us/articles/4423385681175-How-to-Change-your-Discord-Account-s-Email)
- [Steam：个人资料隐私](https://help.steampowered.com/en/faqs/view/588C-C67D-0251-C276)

## 1.0 范围

### 个人资料

- 展示并修改显示名称，长度 0–64 个字符；清空时恢复为邮箱 `@` 前的本地部分。
- 展示登录邮箱、权限角色、账号状态和当前会话。
- 登录邮箱在没有邮箱验证/恢复链路前只读，不提供看似成功但无法验证的直接修改。
- 当前头像使用显示名称首字符作为安全的无上传占位，不新增文件存储和图片处理链路。

### 账户安全

- 修改密码必须提交当前密码、新密码和确认值。
- 服务端再次校验当前密码；新密码沿用 Identity 的 12–256 字符边界。
- 修改成功后在同一数据库事务内更新密码并撤销该用户全部 Web 会话；浏览器清理当前标签页会话并回到登录状态。
- 结果和审计事件不得包含明文密码、访问令牌、刷新令牌或 Cookie。

### 阅读器令牌

- 复用现有 Personal Legado Token API：创建、列出元数据、按所有权撤销。
- 创建时可填写名称；列表只展示名称、前缀、状态和过期时间。
- 原始令牌和包含令牌的阅读 3.0 书源配置只在创建成功响应后展示一次；服务端只保存不可逆摘要。
- 创建和撤销继续写入既有审计边界；撤销会立即失效并删除令牌记录，不可恢复，令牌默认有效期沿用 `Identity:LegadoTokenLifetimeDays`。

## 待定事项

以下事项不在本次 1.0 账户中心切片中，避免在没有配套基础设施时上线不完整安全流程：

- 邮箱修改、邮箱验证、忘记密码和账号恢复。
- 头像上传、图片裁剪/审核、个人简介、手机号、地区和时区。
- 设备/会话列表、单独撤销其他设备和登录活动日志。
- TOTP/MFA、Passkey、安全密钥和备用恢复码。
- GitHub 风格的开发者应用/API Key 管理页面；仓库已有开发者 API Key 后端，待单独确定开发者设置的信息架构。
- 账号注销、数据导出、隐私可见性、通知和语言设置。

## 验收条件

- 匿名用户访问 Reader 仍被要求登录；登录页与注册页不平铺在同一页面。
- 已登录用户可以加载账户资料、修改显示名称、修改密码并看到明确结果。
- 错误当前密码不会改变密码；成功改密后旧访问令牌和旧刷新令牌都不能继续使用。
- 已登录用户可以创建阅读 3.0 令牌、刷新列表并撤销自己的令牌；撤销会立即删除记录且不能恢复，其他用户的令牌不能被操作。
- 原始令牌不出现在列表、页面初始 HTML、日志、数据库字段或 URL 中；页面数据渲染不使用 `innerHTML`。
- 桌面和窄屏布局无水平溢出，键盘焦点、表单标签和状态消息保持可用。
