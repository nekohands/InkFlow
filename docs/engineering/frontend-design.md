# InkFlow 前端设计与 UX 规范

> 本文档定义 InkFlow Web/PWA 前端的默认设计标准。目标不是“功能能用”，而是做到：操作路径短、信息清晰、视觉友好、长时间阅读舒适、移动端优先且桌面端完整。
>
> 前端页面在实现前必须先研究当前主流同类产品的交互模式，并结合 InkFlow 的产品定位重新设计；禁止直接照搬某一家网站的视觉、布局或品牌元素。

## 1. 设计目标

InkFlow 前端必须同时满足以下目标：

1. 新用户无需教程即可完成搜索、进入书籍、查看目录和开始阅读。
2. 常用操作尽量在 1~2 次交互内完成。
3. 页面视觉干净、现代、友好，不采用高噪声信息堆砌。
4. 阅读器优先保证长时间阅读舒适性，而不是装饰性视觉效果。
5. 移动端与桌面端都必须作为正式产品体验，不允许把桌面页面简单压缩到手机宽度。
6. 状态、错误、加载、空数据和权限限制都必须给用户清晰反馈。
7. 高级能力逐步暴露，默认界面不能因为多源、Source、Quality Engine 等平台能力而变复杂。
8. 可访问性目标以 WCAG 2.2 AA 为基线。

## 2. 同类产品参考原则

每个主要用户页面在进入正式实现前，至少重新检查 3 个当前活跃的同类产品；如果距离上次调研超过 6 个月，应重新调研，因为竞品页面会持续变化。

当前建议长期关注以下产品：

| 产品 | 重点参考 |
| --- | --- |
| 番茄小说 | 低学习成本、搜索/榜单入口、书籍信息组织、移动端用户路径 |
| 七猫中文网 | 分类、排行榜、热度信息、首页发现结构 |
| 起点中文网 | 大型书库的信息架构、分类、榜单、作品详情与成熟网文阅读习惯 |
| 晋江文学城 | 大规模作品库、筛选、收藏/历史等高频读者功能 |
| Kakuyomu | 简洁作品详情、目录、章节组织、长篇作品阅读体验 |
| Royal Road | 作品元数据、Trending/Ranking、Follow/History、桌面端信息密度 |

调研的目的不是选一个模板照抄，而是回答：

- 用户最容易从哪里开始？
- 关键操作放在哪里最符合预期？
- 哪些信息用户第一眼真正需要？
- 哪些信息应该折叠到二级区域？
- 移动端如何减少导航层级？
- 阅读器如何降低干扰？
- 目录很长时如何仍然快速定位？
- 如何表达连载状态、最新章节、更新时间、热度和来源状态？

## 3. 每个页面实现前必须产出 Benchmark Note

主要页面包括但不限于：

- 首页 / Discover。
- Search。
- Rankings。
- Library / Categories。
- Book Detail。
- TOC。
- Reader。
- Bookshelf。
- History。
- Login / Account。
- Admin Console 的关键工作台页面。

在开始编码前至少记录：

```text
Page:
User goal:
Reference products:
Patterns worth learning:
Patterns to avoid:
InkFlow-specific requirements:
Desktop interaction:
Mobile interaction:
Accessibility concerns:
Acceptance criteria:
```

如果只是非常小的局部调整，可以省略独立文档，但 PR/Commit 说明中仍应体现参考与取舍。

## 4. 信息架构原则

普通读者看到的是“阅读产品”，不是“爬虫控制台”。

默认用户导航建议围绕：

```text
首页
搜索
分类 / 书库
排行榜
书架
阅读历史
```

Source、Canonical Match、ContentVersion、Quality Evidence 等复杂概念默认只出现在高级用户或 Admin Console。

普通书籍详情页优先展示：

```text
封面
书名
作者
状态
简介
最新章节
更新时间
字数 / 章节数
分类 / 标签
开始阅读
加入书架
目录
```

多源能力应以“稳定、可切源”体现价值，而不是强迫普通用户理解内部数据模型。

## 5. 操作简便原则

### 5.1 主任务路径

核心路径目标：

```text
搜索 → 书籍详情 → 开始阅读
```

不应引入不必要的中间页面。

```text
书架 → 继续阅读
```

应优先直接回到上次进度。

```text
Reader → 下一章
```

必须始终容易触达。

### 5.2 Primary Action

每个页面只能有少量明确的 Primary Action。

例如 Book Detail：

- `开始阅读 / 继续阅读`：Primary。
- `加入书架`：Secondary。
- `目录`：Secondary。
- `切换来源`：Advanced。

禁止把 8~10 个按钮全部做成相同视觉权重。

### 5.3 Progressive Disclosure

高级功能使用逐步暴露：

```text
默认：Auto Source
高级设置：查看来源 / 手动切源 / 质量信息
```

普通用户不应该被迫处理 SourceId、ContentVersion 等内部概念。

## 6. 视觉设计原则

整体方向：

> 简洁、温和、现代、内容优先。

避免：

- 过多渐变。
- 大面积炫光。
- 大量玻璃拟态叠加。
- 装饰动画干扰阅读。
- 每个 Card 都使用重阴影。
- 一屏几十种颜色。
- 密集小按钮。
- 为了“高级感”牺牲可读性。

推荐采用统一 Design Token：

```text
Color
Typography
Spacing
Radius
Shadow
Elevation
Motion
Breakpoints
```

禁止不同页面自行发明新的字号、圆角和间距系统。

## 7. Typography

阅读产品对字体系统要求高于普通后台。

至少区分：

```text
Display / Hero
Page Title
Section Title
Book Title
Body
Reader Body
Metadata
Caption
```

Reader Body 必须独立于普通 UI Body Token，可以由用户调整。

中文正文默认不能过密。

建议提供：

- 字号调整。
- 行高调整。
- 阅读宽度调整。
- 字体选择。
- 段落间距调整。
- 字间距可选调整。

## 8. Reader 是最高优先级视觉页面

Reader 页面遵守“正文优先”。

默认状态减少：

- 导航干扰。
- 卡片边框。
- 广告式区域。
- 复杂背景纹理。
- 固定悬浮组件数量。

核心能力：

```text
上一章
下一章
目录
阅读设置
书架
进度
返回书籍
```

移动端可通过点击正文区域或轻量 Bottom Sheet 暴露工具栏。

桌面端保持合理阅读宽度，不允许正文铺满超宽显示器。

### Reader Theme

至少考虑：

- Light。
- Sepia / Warm。
- Dark。
- Follow System。

深色主题不是简单黑底白字，应保证正文对比度舒适。

## 9. 长目录 UX

数百或数千章节必须仍然可用。

目录应考虑：

- Volume 分组。
- 搜索章节。
- 跳转章节号。
- 正序 / 倒序。
- 当前阅读章节高亮。
- 最新章节快速入口。
- Virtual List / Progressive Rendering。
- 移动端 Drawer/Sheet。

禁止在数千章节时一次创建大量昂贵 DOM 节点导致卡顿。

## 10. Search UX

搜索框需要支持用户习惯：

```text
书名
作者
别名
```

后续可以支持：

```text
主角名
标签
模糊匹配
```

搜索结果必须快速回答：

- 是不是我要找的书？
- 作者是谁？
- 连载还是完结？
- 更新到哪里？
- 最近什么时候更新？

过滤器不能默认占据大量屏幕空间，移动端优先通过 Filter Sheet 展开。

## 11. Homepage / Discover

首页的目标是让用户快速找到“下一本想读的书”，而不是展示平台内部能力。

第一阶段推荐结构控制在有限模块：

```text
继续阅读（登录用户）
热门 / Trending
最近更新
分类入口
新书 / 推荐
```

不要一开始复制大型商业网文站十几个频道和广告区块。

InkFlow 初期内容量不足时尤其要避免“空洞的大型门户布局”。

## 12. Book Card

Book Card 建议只展示足够做选择的信息：

- Cover。
- Title。
- Author。
- Status。
- 简短分类/Tag。
- 热度或更新时间中的少量关键指标。

Card 不应同时展示十多个 Metadata 字段。

列表场景要允许 Dense Variant，排行榜与 Search 不强制全部使用大型封面卡片。

## 13. Responsive Design

必须至少验证：

```text
Mobile
Tablet
Desktop
Wide Desktop
```

原则：

- Mobile First，但不是 Mobile Only。
- 桌面端应利用额外空间改善信息组织，不只是把手机页面居中放大。
- 导航可以在 Mobile 与 Desktop 使用不同组件。
- Hover 不能成为唯一交互入口。
- 关键操作必须可触控。

## 14. Accessibility

目标：WCAG 2.2 AA。

最低要求：

- 完整 Keyboard Navigation。
- 清晰 Visible Focus。
- Focus 不被 Sticky Header / Modal 遮挡。
- 语义化 HTML。
- Form 有 Label / Error Description。
- 不只靠颜色表达状态。
- 合理 Contrast。
- 图片 Alt。
- Reduced Motion。
- Zoom 至 200% 时核心功能仍可使用。
- Click/Touch Target 至少满足 WCAG 2.2 最低要求；产品组件尽量采用更友好的触控尺寸。

## 15. Loading / Empty / Error State

所有页面必须设计：

```text
Loading
Empty
Partial
Error
Offline
Permission Denied
Not Found
```

禁止只有 Happy Path。

加载时：

- 优先 Skeleton / 局部占位。
- 避免全屏 Spinner 阻断整页。
- Reader 已缓存正文切换章节时应尽量无闪烁。

错误提示必须告诉用户：

```text
发生了什么
是否可以重试
下一步能做什么
```

## 16. Feedback 与 Motion

交互反馈必须立即可见：

- 收藏成功。
- 进度保存。
- Source 切换。
- 设置更新。
- Copy Token。

Motion 只用于解释状态变化和层级，不作为装饰主体。

避免：

- 长时间页面进入动画。
- 阅读页面持续动态背景。
- Button Hover 夸张移动。

## 17. InkFlow 的差异化 UX

InkFlow 的多源架构应转化成用户可理解的价值：

普通用户：

```text
Auto
稳定阅读
来源异常自动切换
```

高级用户：

```text
查看可用来源
选择 Preferred Source
查看来源健康状态
```

Admin：

```text
Quality Evidence
Content Version
Source Health
Match Decision
```

三种界面层级必须分离。

## 18. Design System

进入 Phase 3 完整 Web Product 前必须建立统一 Component/Token System。

推荐组件类别：

```text
Button
Input
SearchBox
Tabs
Dialog
Drawer
BottomSheet
Dropdown
Tooltip
Toast
BookCard
BookListItem
RankItem
ChapterList
ReaderToolbar
Skeleton
EmptyState
ErrorState
Pagination / InfiniteList
```

业务页面优先组合 Design System，不在每个页面复制一套组件。

## 19. 前端性能体验

不仅关注 Lighthouse 分数，还关注实际用户路径。

至少跟踪：

```text
Search usable latency
Book detail usable latency
TOC usable latency
Chapter render latency
Next chapter latency
Cover loading
Layout shift
Interaction latency
```

原则：

- 非首屏资源延迟加载。
- Cover 使用适当尺寸和现代格式。
- 大目录虚拟化。
- Reader 预加载下一章。
- 路由级代码拆分。
- 避免为了动画引入大型依赖。

## 20. 前端每轮验收

任何用户可见页面的工作包，除 `development-workflow.md` 的通用 Build/Test/CI 外，还必须完成：

### Functional

- [ ] 主流程可以完成。
- [ ] Back/Forward/Refresh 后状态合理。
- [ ] Loading / Empty / Error 已验证。

### Responsive

- [ ] Mobile 验收。
- [ ] Tablet 验收。
- [ ] Desktop 验收。
- [ ] Wide Desktop 无明显布局问题。

### UX

- [ ] Primary Action 清晰。
- [ ] 高频操作路径没有不必要步骤。
- [ ] 高级选项没有污染普通用户界面。
- [ ] 文案能让用户理解，不暴露内部技术术语。

### Visual

- [ ] 间距、字号、圆角使用统一 Token。
- [ ] Light/Dark（适用页面）检查。
- [ ] 无明显跳动、溢出、截断。
- [ ] 长标题、长作者名、无封面等 Edge Case 检查。

### Accessibility

- [ ] Keyboard 可操作。
- [ ] Focus 可见。
- [ ] Contrast 合理。
- [ ] Touch Target 合理。
- [ ] Reduced Motion 不破坏功能。

### Reader-specific

- [ ] 长时间正文可读性人工检查。
- [ ] 上/下一章容易操作。
- [ ] 目录切换容易操作。
- [ ] 阅读设置不会破坏正文布局。
- [ ] 下一章加载体验可接受。

## 21. UI Review Evidence

前端工作包进入 `Accepted` 前至少保留：

```text
Reference products reviewed
Desktop validation result
Mobile validation result
Accessibility result
Known UX compromises
Screenshots or visual test artifacts（有条件时）
```

如果具备浏览器 E2E/截图能力，应逐步建立 Playwright Visual / Screenshot Regression；UI 改动不能只依赖开发者肉眼回忆。

## 22. 禁止事项

- 禁止完全不参考成熟产品就凭开发者个人习惯设计核心阅读页面。
- 禁止直接复制竞品页面、CSS、素材或品牌设计。
- 禁止为了展示技术能力把 Source/Crawler 内部概念塞给普通用户。
- 禁止只验收 Desktop，不检查 Mobile。
- 禁止只检查功能，不检查视觉和操作路径。
- 禁止 Reader 使用和普通后台相同的排版体系。
- 禁止因追求视觉效果降低可访问性或阅读舒适性。

## 23. 第一阶段推荐借鉴结论

根据 2026-08 当前同类产品观察，InkFlow 第一版应优先吸收以下成熟模式：

1. 首页/榜单使用清晰分类与有限的推荐区块，避免过度复杂门户。
2. Search 作为一级入口，并支持书名/作者等直接意图。
3. Book Detail 直接突出“开始/继续阅读”和目录。
4. 对作品状态、最新章节、更新时间使用高可扫描性 Metadata。
5. 长篇目录按卷/章节组织，并提供当前章节定位。
6. Reader 尽可能降低页面噪声，并提供可调阅读样式。
7. 桌面端允许更高信息密度，移动端减少同时可见操作。
8. Ranking/Trending 是发现内容的重要入口，但不应让首页全部变成排行榜。

这些是设计方向，不是固定 UI；正式实现时仍须重新查看当时的主流网站并记录 Benchmark Note。

## 24. 相关规范

- 强制开发流程：`development-workflow.md`
- 产品愿景：`../product/product-vision.md`
- 架构不变量：`../architecture/invariants.md`
- Phase 1 验收：`../roadmap/phase-1-acceptance.md`
- Progress：`../roadmap/progress.md`
- Handoff：`../handoff/handoff.md`
