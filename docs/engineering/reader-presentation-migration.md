# RF-04 Reader 基线与展示层迁移

状态：In Progress。基线提交 `9e9f9eb`。先建立行为与真实浏览器基线，再按 Reader、账户/PWA、Operations 顺序迁移到 API Presentation；当前已开始展示层迁移。

目标是让 Content 只保留内容业务，把页面、会话/PWA 和浏览器资源放入 API 展示层，并将资源嵌入发布程序集。保持 SSR 输出（仅统一跨平台换行）、URL/DOM/字段/转义、内联资源、会话、缓存、状态码和安全头，不改变设计、数据库、协议、静态资源 URL 或认证方式；删除旧实现，不保留转发壳。

当前代码要求非账户页面具备会话，否则跳转登录并携带安全 returnTo；早期 Benchmark Note 的匿名阅读描述属于历史状态，本轮以当前代码及基线固定会话行为。真实授权仍由服务端负责。

## 当前同类产品复核（2026-10-22）

Page：Reader / 账户 / 书架历史 / Operations。User goal：保持搜索、目录、阅读、设置和继续阅读路径稳定。

- [Royal Road 官方说明](https://www.royalroad.com/support/knowledgebase/80)：章节页设置入口、主题/字号/宽度等本机偏好。
- [Kobo Web Reader 官方说明](https://help.kobo.com/hc/en-us/articles/35996239522967-Kobo-Web-Reader-Navigation-Reading-Features)：目录、字体设置及继续阅读能力。
- [Wuxiaworld 官方说明](https://support.wuxiaworld.com/support/solutions/articles/157000302586-how-to-use-the-reader-bookmarking-dark-mode-font-adjustments-)：书签位置、主题、字体和行高。

Patterns worth learning：设置就近可达、目录与章节导航直接可用、继续阅读明确。Patterns to avoid：迁移时改动视觉或引入复杂路径。InkFlow-specific：只读入库正文、文本安全转义、同源会话与私人数据不进入 PWA 缓存。Desktop/Mobile：沿用现有布局，覆盖 390/768/1440/1920 宽度。Accessibility：键盘、焦点回归、dialog、200% 缩放、长文及错误状态。

## 验证范围

固定完整输出哈希：列表/搜索/空/降级/XSS，0/100/1k/10k 目录，短/长/缺失章节，账户/注册/书架/历史/离线/Operations/PWA 资源。独立 Chromium/Playwright 使用真实页面渲染与固定 API 响应，记录交互、控制台、网络、截图、DOM、payload 和渲染/导航延迟；规模测量每轮预热 3 次、采样 30 次、3 轮。结构迁移以行为与输出等价为验收，不宣称未经测量的提速。

浏览器夹具不替代真实认证与持久化；后者由现有源码 Compose CI 验证。另执行 Restore/Release Build/Tests、Architecture/Contract/Reading/Reader、脱离源码目录的发布资源验证、Migration、完整 Runtime/Security 与目标 SHA CI。DevTools MCP 未配置，使用独立 Playwright；本机 Docker 缺失，真实容器验证在 CI。真实设备、真实账户、PWA 安装及长时间阅读仍保留人工验收边界。

AI 权威记录：[RF-04 Wiki](../../repowiki/work-packages/2026-10-22-rf04-presentation.md)。

## 冻结基线（生产迁移前）

生产基点 `9e9f9eb`；完整输出 22 项，Reader/输出/Reading 定向测试 28 项通过。Chromium 151.0.7922.34 / Playwright 1.62.1 / Windows，浏览器独占采样；环境与全部 450 次原始样本见 [browser.json](benchmarks/rf04/browser-before/browser.json)，同目录保留 40 张截图。四种屏宽验证行为，性能固定在 1440×900；每项 3 轮，每轮 3 次预热、30 次采样。renderMs 为导航起点到脚本/字体就绪后两帧的测量代理，不等于 LCP 或真实用户耗时。200% 检查使用 CSS zoom 重排代理，不替代系统/浏览器缩放及真机验收。

| 输入 | UTF-8 字节 | DOM 节点 | 三轮 p50 范围 ms | 三轮 p95 范围 ms |
| --- | ---: | ---: | ---: | ---: |
| 短章节 | 68,607 | 81 | 48.3–48.5 | 63.2–65.5 |
| 长章节（500 段） | 402,851 | 577 | 65.4–81.1 | 82.1–98.3 |
| 100 章目录 | 76,724 | 255 | 64.9–65.0 | 66.0–66.4 |
| 1,000 章目录 | 191,026 | 2,055 | 115.5–132.4 | 148.3–196.0 |
| 10,000 章目录 | 1,352,028 | 20,055 | 279.7–330.9 | 390.5–480.8 |

四种屏宽行为检查通过，无未捕获 JS 错误；控制台仅出现故意注入的 404/503/401 与离线网络错误。运维确认框会预填常用理由，空理由校验须先清空后检查；测试已按当前行为固定。SW 的 `/reader/` scope 不覆盖 `/reader` 根地址，离线检查在 scope 内执行。早期探索轮与并行构建重叠，未纳入冻结数据。

复现：`dotnet run --project tools/InkFlow.ReaderFixtures -c Release -- artifacts/reader-fixtures`；在 `tools/InkFlow.ReaderBrowser` 执行 `npm ci --ignore-scripts`、`npx playwright install chromium`，再执行 `node verify.cjs ../../artifacts/reader-fixtures ../../artifacts/reader-browser [同环境基线目录]`。API 是固定夹具，所有账户/令牌均为测试值，无真实服务连接。

第一切片：基线已提交 `1c258c3`，重复浏览器验证 40 张截图完全一致。三个共享外壳的分部文件整体迁入 API Presentation，先拆出 Reader/共享外壳资源；完整输出哈希 22 项及定向测试 28 项通过。尚待账户/PWA、Operations 资源拆分和完整交付门禁。

第二切片：账户、书架/历史、详情/进度与 PWA 脚本/Manifest/图标已独立为嵌入资源；进度模板只替换 Guid。完整输出哈希 22 项及定向测试 28 项通过。
