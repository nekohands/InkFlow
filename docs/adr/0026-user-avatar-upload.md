# ADR 0026：用户头像上传与存储边界

- 状态：Accepted for 1.0 Release Candidate
- 日期：2026-09-03
- 范围：Reader / Identity / Profile Avatar

## 背景

账户中心已有头像占位和个人资料入口。用户需要自行上传头像，但 1.0 只要求单用户当前头像，不需要裁剪、审核、公共分享或图片处理平台。

## 决策

1. 在 Identity 增加 `identity.user_avatars`，按 `UserId` 一行保存当前头像的服务端确认 MIME、字节和更新时间；上传使用 PostgreSQL upsert 替换当前内容，用户删除时级联删除。
2. `PUT /api/v1/me/profile/avatar` 和 `GET /api/v1/me/profile/avatar` 必须通过认证，并且只能访问当前主体的头像。不存在头像时返回 404，前端继续显示显示名称首字符。
3. 上传上限为 2 MiB。服务端只接受由文件签名确认的 PNG、JPEG、WebP，不信任文件名或客户端 `Content-Type`；不接受 SVG 等可执行/主动内容格式。请求体另设小额 multipart 开销上限。
4. 头像响应使用 `private, no-store` 和 `nosniff`，不进入公共内容、搜索、Legado 或 CDN 缓存。审计只记录更新动作和主体，不记录文件名或内容。
5. 先使用 PostgreSQL `bytea` 保存小头像，不引入对象存储或新的图片依赖；当容量、备份或吞吐证据超过内联存储边界时，再单独设计 Object Storage 迁移。

## 备选方案

- 立即使用 Object Storage：部署、签名 URL、清理和备份配置明显增加，超出当前单头像需求。
- 把字节放进 `users`：会让账号认证和常规用户查询携带头像列，扩大热点行负担。
- 引入图片解码/裁剪库：本轮没有裁剪或重编码需求，新增依赖不能替代文件签名、大小和响应隔离。

## 后果

- 用户可在账户中心上传和替换自己的头像；刷新或重新登录后仍可读取。
- 头像是私有资料，不提供跨用户读取、恢复历史、单独删除或裁剪审核接口；这些需求出现时再扩展数据模型和验收范围。
- `bytea` 简化当前部署，但头像规模受数据库行大小、备份和响应吞吐约束，升级 Object Storage 前需要可观测性和迁移方案。

## 验证

- Unit 覆盖 PNG/JPEG/WebP 文件签名、大小边界、非法格式和 Identity 服务所有权边界。
- Integration 覆盖 `user_avatars` 迁移、PostgreSQL 字节往返和同一用户替换。
- Runtime smoke 覆盖匿名拒绝、认证上传、认证读取和前端头像入口；浏览器自动化覆盖布局、表单和错误状态，真实头像视觉效果仍可人工补充。
