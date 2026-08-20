# Legado Compatibility Contract

## 1. 产品角色

Legado 是 InkFlow 第一优先级客户端协议。官方主路径固定为：

`Legado -> InkFlow Legado API -> Canonical Content`

第三方原生书源只作为高级/备用能力，不影响官方聚合书源的稳定性。

## 2. 独立 API

Legado 不直接复用 Web/Developer API DTO。第一阶段专用接口：

- `GET /api/legado/v1/search?q=`
- `GET /api/legado/v1/books/{bookId}`
- `GET /api/legado/v1/books/{bookId}/chapters`
- `GET /api/legado/v1/chapters/{chapterId}`
- `GET /legado/book-source.json`

后续可增加 rssSource、replaceRule 和个人订阅能力，但不得破坏 v1 已发布 Contract。

## 3. Rule Generator

官方规则由 `ILegadoRuleGenerator` 生成，而不是散落的手工 JSON。

生成器输入包括 Compatibility Profile、公开 Base URL、API Version 和可选认证模式。输出必须进行 Schema/Snapshot/Golden Test。

官方规则尽量只解析 InkFlow 自有稳定 JSON，复杂性全部留在服务端。

## 4. ID 与 URL 稳定性

BookId、ChapterId 是长期稳定协议身份。

发生 Book Merge 时，旧 BookId 通过 Canonical Redirect 继续解析到新身份；章节重排、切源、Content Version 变化不得改变 ChapterId。

## 5. 公共与个人模式

- Public：无需登录，允许基础 Search/Book/TOC/Content，实施合理匿名限流。
- Personal：使用独立 Legado Access Token，支持更高配额、个人订阅/书架等能力。

Legado Token 与 Web Access/Refresh Token 分离。数据库只保存 Prefix + Hash，可单独撤销、过期、审计和限制 Scope。

## 6. 内容一致性

Web 与 Legado 必须共享同一个 `CanonicalContentService` 和 Selected Content 决策。

默认情况下，同一章节在 Web 与 Legado 返回相同 Canonical Content Version。用户显式 PreferredSource 是用户阅读偏好，不修改平台全局 Selection。

## 7. 缓存

Chapter Cache Key 包含 ChapterId + SelectedVersionId 或 ContentHash。Content Version 切换后不依赖全局大规模删除缓存。

Book/TOC/Content 可以使用 ETag/Revision 降低重复传输。

## 8. 同步边界

产品目标是尽可能统一 InkFlow Web 与 Legado 状态，但只实现客户端协议可可靠支持的同步能力。

第一阶段优先个人书源、订阅和 InkFlow 侧阅读历史；无法可靠双向同步的客户端状态保持端内，不宣称伪双向同步。

## 9. Release Gate

每个生产 Release 必须通过 Legado Contract Test：

`Generate Rule -> JSON Validate -> Search -> BookInfo -> TOC -> Content`

Legado Contract Test 失败时禁止生产发布。

兼容性变更需要 `LegadoCompatibilityProfile`，记录 SchemaVersion、MinSupportedVersion、TestedVersion、Capabilities 和 DeprecatedAt。
