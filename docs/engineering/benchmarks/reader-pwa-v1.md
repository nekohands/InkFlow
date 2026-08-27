# Reader/PWA 用户状态 v1 Benchmark Note

日期：2026-08-28

## Page

Web Reader 的登录入口、书架、阅读历史和可安装 PWA 基础壳；章节页继续作为阅读主路径。

## User goal

读者可以在同一浏览器会话中登录，保存书架、阅读进度、历史和阅读偏好，并在支持的平台将 InkFlow 添加为独立应用；未登录时仍可继续匿名阅读和使用本地阅读设置。

## Reference products

- [Royal Road Personalized Lists（官方帮助）](https://www.royalroad.com/support/knowledgebase/81)：把 Follow、Read Later、Favorites 和 History 分成清晰的个人列表，并在列表中提供继续阅读/查看最近章节的高频入口。
- [MDN Making PWAs installable](https://developer.mozilla.org/en-US/docs/Web/Progressive_web_apps/Guides/Making_PWAs_installable)：manifest 负责应用身份、图标、启动地址和 standalone 展示；service worker 是离线体验的可选增强，不应成为页面可用性的前置条件。
- [MDN Web App Manifest](https://developer.mozilla.org/en-US/docs/Web/Progressive_web_apps/Manifest)：manifest 通过 HTML head 同源引用，`start_url`、`display` 和图标应与产品入口保持一致。

## Patterns worth learning

- 书架与历史是读者自己的列表，页面第一屏直接回答“继续读什么”，不展示 Source、Crawler 或 ContentVersion 等内部模型。
- 认证不是匿名阅读的前置条件；匿名用户可以阅读，登录后才获得跨页面/设备的状态同步能力。
- PWA 以渐进增强方式接入：浏览器不支持安装或 service worker 时，普通页面仍完整可用。
- 安装入口只在浏览器报告可安装时出现，并清楚说明“保存到主屏幕/桌面”这一收益。

## Patterns to avoid

- 不把书架列表做成后台表格，不要求读者理解状态枚举或内部 ID。
- 不在 service worker 中缓存 `/api/v1/me/*`、认证响应或未来私人内容，避免跨用户泄露。
- 不把 Access Token 或 Refresh Token 放入 URL、HTML、日志或长期 `localStorage`；本轮浏览器客户端仅在 `sessionStorage` 保存会话级凭据，并在 401/登出时清理。
- 不把 PWA 安装、离线 shell 与真实来源抓取绑定；离线时只提供明确的网络不可用反馈，不伪造正文更新。

## InkFlow-specific requirements

- 复用既有 opaque Bearer `/api/v1/auth/*` 与 `/api/v1/me/reading/*` Contract，不改变 Legado 认证边界。
- 读写用户状态始终由服务端认证 `sub` 确定用户，前端不提交 UserId。
- Reader 首次加载优先保持正文；登录/同步失败只能显示可理解的降级状态，不能阻塞 Canonical Content 阅读。
- 书架页面使用现有 ReadingShelfItem，历史页面使用现有 ReadingHistoryItem；书籍与章节链接仍指向稳定 Canonical ID。
- manifest 与 service worker 同源、只服务 `/reader/` 范围；缓存只包含公开的 PWA shell，不缓存用户状态和认证响应。

## Desktop interaction

- 站点 header 提供书库、书架、历史、登录/账户和安装入口；桌面端保留文字标签。
- 书架卡片的主操作是“继续阅读”，次操作是移除；历史条目的主操作是打开最近章节。
- 登录页同时提供登录/注册，并在成功后回到书库；不会在阅读路径中插入额外的中间页。

## Mobile interaction

- header 操作保持触控尺寸，次级导航折叠为短标签；书架和历史采用单列列表。
- 安装提示采用非阻塞 banner；iOS 等不提供 `beforeinstallprompt` 的平台不显示失效按钮。
- 网络不可用时显示离线状态和返回书库入口；已有页面的正文/章节导航仍依赖正常服务器响应。

## Accessibility concerns

- 登录表单有 label、错误状态和 `aria-live` 反馈；密码输入不回显到页面状态。
- 书架/历史使用语义列表、明确的主操作和可见 focus；不只依赖颜色表达同步或空状态。
- 安装 banner 可键盘关闭；service worker 注册失败不影响键盘阅读与主流程。

## Acceptance criteria

- [ ] `/reader` 全部页面引用有效 manifest，manifest 包含同源 `start_url`、`display`、主题色和图标。
- [ ] service worker 注册失败时页面仍可读；同源认证/个人 Reading API 不进入缓存。
- [ ] 登录/注册成功后会话只保存在当前浏览器会话，并能打开书架与历史。
- [ ] 登录用户在章节页保存进度和偏好；401 后尝试一次 refresh，失败则清理会话并保留匿名阅读。
- [ ] 书架/历史空态、加载态、未登录态和 API 错误态均有清晰文案。
- [ ] 单测覆盖 token 不进入 URL/HTML、401 降级、manifest/service-worker 内容和 Reader 页面入口；CI Runtime smoke 覆盖公开 PWA 资源。
- [ ] PWA 真正安装、跨设备同步和长时间浏览器视觉验收继续作为人工验收，不在本轮自动声称完成。
