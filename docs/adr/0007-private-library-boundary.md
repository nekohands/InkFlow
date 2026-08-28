# ADR 0007: Private Library 与公共 Canonical Library 的边界

- 状态：Accepted
- 日期：2026-08-28

## 背景

路线图中的 Private Library、TXT/EPUB 导入和导出删除属于用户产品能力，但当前系统的 Library、Content、Reading 与 Legado 契约都围绕公共 Canonical 身份构建。若把用户私有书籍直接写入 Canonical 表或复用公共 BookId，会破坏公共可见性、稳定身份和阅读路径的授权边界。

## 决策

- Private Book 由 Library 模块维护为独立的用户所有书目元数据；每条记录绑定一个 Owner UserId，并使用独立的 PrivateBookId。
- 私有数据的查询、更新和删除必须以认证主体的 UserId 为范围；请求体、路径或客户端声明的用户 ID 不参与授权。非所有者与不存在的记录统一按未找到处理，避免泄露私有资源存在性。
- Private Book 不自动进入 Canonical Library、公共搜索、Legado、Source Match、Content Policy 或公共 Reading Shelf；本阶段不做跨用户去重或自动 Canonical 匹配。
- 本工作包只建立私有书目元数据的 CRUD 基础。TXT/EPUB 导入、私有正文与版本存储、导出、恢复策略以及是否允许用户将私有内容发布为公共 Canonical 内容，另行设计和验收。
- Private Book 删除在当前仅有元数据的阶段采用按所有者执行的直接删除；后续引入正文、版本或导出保留策略时，必须先更新 ADR 并定义级联、恢复与审计语义。

## 后果与风险

- 公共 CatalogQuery、Legado 和现有 Reading State 无需改变即可保持公共数据语义。
- Library schema 会同时承载公共与私有书目，但通过独立表、独立标识和每次查询的 UserId 过滤保持边界；后续若私有内容规模或生命周期明显分化，可在不改变外部标识语义的情况下拆出独立模块。
- 当前 CRUD 不等于 Private Library 完成：导入、正文阅读、导出删除全链路、跨设备同步和人工设备验收仍是后续工作。
