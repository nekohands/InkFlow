# Web Reader v1 Benchmark Note

> 日期：2026-08-28
> 页面：Web Reader（书目搜索/列表、书籍详情、章节正文）

## User goal

用户可以在不理解 Source、Canonical 或 Content Version 的前提下完成：

```text
搜索 → 书籍详情 → 开始阅读 → 目录/上一章/下一章
```

阅读页应优先保证长时间正文阅读的舒适性，同时在移动端和桌面端都能快速打开阅读设置。

## Reference products

### Royal Road

Royal Road 的官方阅读说明把阅读设置放在章节页的明显入口，支持主题、背景暗度、字号、阅读宽度、字体、缩进和段间距；移动端还支持通过双击正文进入全屏阅读。设置按设备本地保存，不强制同步到其他设备。

来源：[Royal Road — Optimize the Reading Experience](https://www.royalroad.com/support/knowledgebase/80)

### Rakuten Kobo Web Reader

Kobo 将阅读设置集中在字体图标菜单中，并提供字体、字号、背景（Light/Sepia/Dark）、页边距、行距和正文宽度等控制；目录也属于阅读器的直接导航能力。

来源：[Kobo — Web Reader Navigation & Reading Features](https://help.kobo.com/hc/en-us/articles/35996239522967-Kobo-Web-Reader-Navigation-Reading-Features)

### Wuxiaworld

Wuxiaworld 的官方帮助文档把书签/阅读位置、深色模式、字体、字号和行高作为阅读器核心能力，并在网页端提供独立的阅读设置入口；文档同时强调网页与 App 的设置入口可以不同，但功能目标保持一致。

来源：[Wuxiaworld — How to Use the Reader](https://support.wuxiaworld.com/support/solutions/articles/157000302586-how-to-use-the-reader-bookmarking-dark-mode-font-adjustments-)

## Patterns worth learning

- 阅读设置必须在正文页一跳可达，移动端使用紧凑的工具栏/面板，桌面端使用固定但不遮挡正文的工具区。
- 主题、字号、行高和正文宽度是第一版最有价值的控制项；设置应即时生效，并通过 `localStorage` 保留匿名用户的本机偏好。
- 目录、上一章、下一章都应从正文页直接触达；下一章按钮应拥有清晰的 Primary Action 视觉权重。
- 正文使用受限最大宽度和舒适行高，桌面宽屏不能让行长无限扩张；移动端则减少外围留白以保留正文空间。
- 设置控件必须有可见标签、键盘焦点和明确的状态反馈；主题不能只用颜色差异表达。

## Patterns to avoid

- 不复制任何参考产品的品牌色、布局或文案。
- 不把 SourceId、ContentVersion、健康状态等平台内部概念放入普通阅读路径。
- 不用大面积固定遮罩、广告式卡片或持续动画打断正文。
- 不在本轮加入分页式阅读、评论、书签服务端同步、字体上传或实时抓取。

## InkFlow-specific requirements

- 阅读路径继续只读已落库的 Canonical Content，不在 `/reader/read/*` 触发第三方实时抓取。
- 所有书名、章节名和正文段落均按 HTML 文本转义；不允许把上游 HTML 直接拼接进页面。
- 书籍详情仍以“开始阅读”为 Primary Action；章节页保留目录、上一章和下一章。
- 匿名用户的设置保存到本机；Reading State 服务端同步不在本轮改变既有 API Contract。
- 所有异常、空目录和未发布正文都提供人话状态及返回路径，不泄漏内部异常细节或 SourceId。

## Desktop interaction

- 页面正文最大宽度约 44–48rem，外围使用低对比度背景。
- 阅读设置按钮位于章节页顶部/工具栏，打开原生可访问的 `dialog`；Escape 可关闭，Tab 顺序稳定。
- 工具栏包含目录、阅读设置、上一章和下一章；下一章在存在时保持突出。
- 章节长文不使用多列布局，避免破坏中文连续阅读节奏。

## Mobile interaction

- 单列正文，触控目标至少 44px；工具栏允许换行或使用底部区域，不遮挡正文。
- 章节页提供轻量“显示工具栏”按钮；不依赖 hover。
- 设置面板在窄屏内可滚动，修改字号/行高/主题后立即看到结果。
- 上一章/下一章使用等宽或近似等权按钮，避免窄屏溢出。

## Accessibility concerns

- 页面使用 `header`、`nav`、`main`、`article`、`footer` 等语义结构。
- 设置面板使用 `dialog`、`aria-labelledby` 和可见标签；关闭后焦点返回打开按钮。
- 正文与背景保持舒适对比度，焦点环不能被背景或 sticky 工具栏吞掉。
- 提供 `prefers-reduced-motion` 降级；核心功能不依赖 JavaScript，脚本不可用时正文和章节导航仍可用。
- 搜索、空态、错误和设置保存状态使用 `role="status"` 或等价语义反馈。

## Acceptance criteria

- [ ] `/reader`、`/reader/books/{bookId}`、`/reader/read/{chapterId}` 可完成搜索→详情→阅读主路径。
- [ ] 章节页可直接打开目录、上一章、下一章和阅读设置。
- [ ] 设置支持 System/Light/Sepia/Dark、字号和行高，匿名用户刷新后仍保留。
- [ ] Mobile、Tablet、Desktop、Wide Desktop 使用统一 token 且无明显溢出。
- [ ] 错误、空目录、未发布正文和部分来源失败都有可理解反馈。
- [ ] HTML 注入回归测试、键盘/语义结构测试和现有 Content/Canonical 回归通过。
- [ ] 本轮不将真实 MuMu/阅读 3.0 验收、真实来源验证或 Reading State 服务端同步标为完成。
