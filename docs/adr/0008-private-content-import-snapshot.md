# Private Content Imports Create Immutable Snapshots

- 状态：Accepted
- 日期：2026-08-28

TXT/EPUB 导入创建新的 Private Book、Private Chapter 和私有内容快照；导入失败不产生部分数据，重复导入也不覆盖既有书籍。私有章节正文使用独立身份和用户范围，只能由私有读取/导出路径访问，不复用公共 Canonical Content 或 `ContentVersion`，因为覆盖式重导入会破坏可追溯性，而把私有正文接入公共内容管线会扩大授权和缓存泄漏风险；后续若需要编辑、版本恢复或发布为公共内容，必须另行定义版本与授权语义。
