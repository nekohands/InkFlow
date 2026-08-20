# 墨流 / InkFlow 产品愿景

## 定位

InkFlow 是以 Canonical Content 为核心的小说内容平台，同时面向普通读者、阅读 3.0（Legado）用户、开发者与企业客户。

InkFlow 不是单纯的小说爬虫，也不是单纯的书源生成器。平台负责多来源采集、作品与章节归一化、正文版本化、质量选优、自动追更、稳定 API 分发和 Web 阅读。

## 产品优先级

1. 阅读 3.0 / Legado
2. 在线阅读体验
3. 自动追更
4. 多源切换 / 容灾
5. 多站点小说采集
6. 统一书库
7. 搜索
8. 用户书架与阅读历史

当功能资源发生冲突时，优先保证靠前能力的稳定性。

## 产品形态

InkFlow 采用双层产品结构：

- **InkFlow Reader**：面向普通读者，提供 Web/PWA 阅读、搜索、书架、历史、私人书库和个性化能力。
- **InkFlow Platform**：提供采集、Canonical Library、Content Quality、Legado、Open API、规则管理、开发者能力和企业私有部署能力。

## 商业方向

同时服务两类商业路径：

- B2C：Free / Premium / Pro 阅读与个人能力。
- B2D/B2B：Developer API、组织能力、私有 Source、Private Worker、Enterprise 私有部署。

套餐名称不直接决定业务权限；业务通过 Entitlement 与 Quota 判断能力。

## 内容模型原则

- 一本作品由稳定 `CanonicalBook` 表示。
- 一个章节由稳定 `CanonicalChapter` 表示。
- 第三方来源只是 `SourceBook` / `SourceChapter`，不是平台主身份。
- 多来源正文作为不可覆盖的 Content Version 保存。
- 当前最佳正文由可解释的质量决策选择，并允许人工锁定。
- 正文采用混合存储策略，但底层模型必须具备全量持久化能力。

## Legado 定位

Legado 是一级产品协议。

主路径固定为：

`阅读 3.0 -> InkFlow Legado API -> Canonical Content`

同时允许生成第三方原生书源作为高级/备用能力，但它们不得成为官方聚合书源的核心依赖。

## Source 生态

Source 分为：

- Official：官方维护、可信运行域、具备健康监控和发布流程。
- Community：用户提交，必须经过 DSL、沙箱、安全检查、审核和质量治理。
- Private：用户或组织私有，不进入公共市场。

Official Source 可以使用 RuleAdapter 或受信任 CodeAdapter；Community Source 不允许任意 C#、JS 或 Shell 执行。

## 第一阶段成功标准

第一条真实产品链路必须无需手工改数据库即可完成：

`Source 搜索 -> 导入 -> Canonical Book -> TOC -> Chapter -> Content Version -> Web 阅读 -> Legado 导入 -> Legado 搜索/目录/正文 -> 自动追更`

加入第二来源后，还必须验证 Book/Chapter ID 稳定、双来源内容版本和故障切源。
