# ADR 0022：公共正文读取前的正典候选重选

- 状态：Accepted
- 日期：2026-08-31

## 决策

`InkFlow.Api` 的 `CatalogQueryService.GetChapterContentAsync` 在加载正文列前，
通过已注入的 `IContentSelectionService`，依据 PostgreSQL 中的 `ContentVersion` 候选和
`SourceCapabilityHealth(Content)` 重选当前版本。重选完成后再次执行书籍策略门控，再读取
当前正文并返回 Web/Legado DTO。

该步骤只访问 InkFlow 自己的权威存储，不调用来源适配器、不访问第三方 URL。选择结果继续追加
`content.selection_decisions`，保留算法版本、候选排除和回退证据；所有候选不可用时保持已有
当前版本的既有语义。

## 原因

来源能力被管理员停用或进入不可用状态后，下一次正文读取必须能够从已落库的有效候选切换，
而不依赖下一轮采集或一个尚未存在的外部调用方。来源恢复后，同一入口重新评估候选并恢复质量
更高的版本，同时不改变 `BookId`、`ChapterId` 或历史正文版本。

## 后果

- 公共正文读取需要 Content 选择仓储和 Source Health 读取能力，且可能追加一次选择证据。
- 读取仍不依赖第三方可用性；数据库故障或选择失败按正文不可用处理，不返回未经选择的正文。
- Web Reader、公开 Legado 和其他复用 `CatalogQueryService` 的正文入口共享同一故障切换语义。
