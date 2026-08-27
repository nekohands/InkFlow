# ADR 0006: Reader v1 当前标签会话存储边界

- 状态：Accepted
- 日期：2026-08-28

## 背景

Reader v1 是服务端渲染页面加渐进增强的 Web/PWA 入口。登录、书架、阅读历史、进度和阅读偏好需要跨同一标签页的页面跳转保持会话，但当前 Identity API 尚未提供 HttpOnly BFF Cookie 会话。

## 决策

Reader v1 在当前浏览器标签页的 `sessionStorage` 中保存短期 Web Access Token 与 Refresh Token 的原始值，以支持 SSR 页面重新加载；该选择是有界的兼容方案，不扩展为长期客户端凭据存储：

- 只使用当前标签页 `sessionStorage`，不使用 `localStorage`、URL、HTML、Cookie、日志或 Service Worker Cache 保存令牌。
- Web API 仅接受固定的同源 `/api/v1/` 路径；Access Token 只放入 `Authorization` Header，认证和刷新请求使用 `cache: no-store`。
- Refresh Token 继续遵循一次性轮换；刷新失败、会话无效或令牌形状不符合长度限制时立即清理当前标签页会话。
- Service Worker 只缓存公开 Reader 壳资源和离线提示，不处理或缓存 `/api/v1/me/*`、认证响应及私人内容。
- 当前标签页会话不承诺跨标签页、跨设备或离线私人内容同步；这些能力另行设计和验收。

## 后果与风险

- 页面刷新和 Reader 内部导航可以继续使用账户状态，而关闭标签页会清除会话。
- `sessionStorage` 仍会扩大 XSS 或浏览器扩展读取令牌的影响面，因此页面继续避免第三方脚本，服务端输出和客户端列表渲染均使用安全文本 API；完整浏览器安全、PWA 安装和设备验收仍是人工验收项。
- 后续若提供 HttpOnly BFF Cookie，应迁移到服务端会话并删除这套浏览器原始令牌存储，不改变后端 token hash、轮换和资源授权不变量。
