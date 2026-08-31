# 采集工作台 v1 需求对齐草案

状态：需求已对齐，代码已落地；当前代码候选为 `80962fb`，在 Legado 四步门禁基础上新增正文读取重选与双来源 A→B→A Web/Legado 运行时 smoke 及脚本回归。候选已在 Ubuntu VM 以源码构建 Compose 完成全量测试、健康启动和实际 failover 验收；CI/Docker/Security 已全部 GREEN。真实 Official Source pair、真实凭据 Operations 验收和阅读 3.0 真机链路仍按待定事项处理
日期：2026-08-31
范围：采集运行控制、进度可视化、书籍地址采集、已入库书籍打包

## 1. 目标

让运营人员可以在一个受保护的运维页面中：

1. 直接粘贴一本已登记公开来源的书籍地址，创建一次采集运行；
2. 看到从书目、目录到正文的持久化进度，而不是只看到单个后台任务；
3. 对运行执行暂停、恢复、停止或取消，并在 Worker 重启后保留语义；
4. 对已完成且已发布的正典书生成一个可下载的书籍包。

本需求先解决“可控、可恢复、可解释”的采集闭环，不把阅读 3.0 真机验收纳入自动实现范围。

## 2. 已确认范围决策

| 项目 | v1 推荐方案 | 说明 |
| --- | --- | --- |
| 使用人 | Operator / Administrator | 采集是运维能力，不向普通阅读用户暴露 |
| 来源 | 已登记的公开来源 | 仅允许匹配 Source.BaseUrl 的地址；不做任意 URL 代理 |
| 运行模型 | `CollectionRun` 父运行 + 既有 CrawlerTask 子任务 | 目录和正文任务共享一条进度与控制边界 |
| 进度事实 | PostgreSQL 持久化查询 | Redis、浏览器轮询和内存计数都不是唯一事实 |
| 控制语义 | 暂停可恢复；停止优雅终止；取消立即终止后续工作 | 详见第 5 节 |
| 打包格式 | ZIP + EPUB 3 + 单文件 TXT | 三种格式独立生成，后续再考虑图片/音频 |
| 触发方式 | API + `/admin/operations` 运维界面 | 浏览器自动刷新，支持键盘和窄屏 |
| 常规验证 | Ubuntu VM 源码构建 | Docker 镜像构建只在发布/专门需要时执行 |

以上方案已按“优先采用推荐，重大决策单独确认”的原则完成对齐。非重大实现细节沿用现有模块边界、安全模型、审计约束和测试流程；发生新的重大范围变化时，回到 Grill Me 流程重新确认。

## 3. 用户故事

### 3.1 地址采集

作为运营人员，我可以粘贴一本公开书籍的 URL，系统自动识别已登记来源和外部书籍 ID，异步开始采集；我不需要先执行搜索或手工填写内部 SourceId。

### 3.2 运行控制

作为运营人员，我可以在运行过程中暂停、恢复、停止或取消；页面会明确显示控制请求正在生效、运行已结束或仍有正在完成的当前单元。

### 3.3 进度查看

作为运营人员，我可以看到当前阶段、已发现章节数、已完成数、失败数、待处理数和最后更新时间；目录尚未返回前，页面显示“正在发现总量”，而不是伪造百分比。

### 3.4 书籍打包

作为运营人员，我可以对已完成且有可见正文的正典书发起打包，并在打包完成后下载一个完整包；半成品、下架书、缺少当前正文的章节不能生成可下载的半包。

## 4. 端到端流程

```text
输入 URL
  → 匹配已登记来源并规范化 externalBookId
  → 创建 CollectionRun + BookInfo 子任务
  → 导入书目并建立/确认正典匹配
  → 创建 Toc 子任务并同步目录
  → 为待采集章节创建 Content 子任务
  → 抓取、质量检查、发布当前内容
  → 汇总运行进度并完成/失败
  → （可选）对已发布正典书创建 ZIP PackageJob
```

所有上游访问仍走现有 SourceAdapter、Safe HTTP、超时、重试、来源健康和凭据引用边界；阅读路径不实时访问第三方站点。

## 5. 运行状态和控制语义

### 5.1 CollectionRun 状态

| 状态 | 含义 | 是否终态 |
| --- | --- | --- |
| `Pending` | 已创建，等待首个任务领取 | 否 |
| `Running` | 至少一个阶段正在推进 | 否 |
| `Paused` | 暂停已生效，不再领取该运行的新任务 | 否 |
| `Stopping` | 已请求优雅停止，等待正在执行的原子单元结束 | 否 |
| `Stopped` | 优雅停止完成；不再继续此运行 | 是 |
| `Cancelled` | 已取消；不再重试或创建后续任务 | 是 |
| `Completed` | 所有必要章节成功采集并发布 | 是 |
| `Failed` | 运行无法完成，例如匹配失败、目录失败或存在不可恢复正文死信 | 是 |

### 5.2 四个控制动作

| 动作 | 生效方式 | 后续操作 |
| --- | --- | --- |
| 暂停 | 持久化为 `Paused`；禁止新任务领取。当前请求在安全检查点结束后不再扩展工作 | 允许恢复 |
| 恢复 | 仅 `Paused` 可恢复，回到 `Pending`，由调度/事件继续领取 | 继续原运行，不重置进度 |
| 停止 | 持久化为 `Stopping`；不再创建/领取新任务，允许当前原子章节完成 | 最终为 `Stopped`，不可恢复；需要重新采集时创建新运行 |
| 取消 | 持久化为 `Cancelled`；禁止新任务、重试和后续链路。正在进行的网络请求在协作取消或安全检查点结束 | 终态，不恢复；已发布历史内容不回滚 |

补充约束：

- 控制命令必须幂等；重复点击不能重复创建运行、任务或审计记录中的新业务事实。
- 暂停/停止/取消不物理删除父运行或子任务，保留状态、时间、操作者和理由，便于审计与复盘。
- 停止和取消不会删除已经写入的 SourceBook、FetchArtifact 或 ContentVersion；取消只阻止后续工作，不违反内容版本追加不覆盖不变量。
- Worker 重启、租约过期或事件重复投递后，运行控制仍以数据库状态为准。

## 6. 进度口径

进度由后端从 `CollectionRun` 和其子任务汇总返回，浏览器只负责展示。

### 6.1 展示字段

- `stage`：`bookInfo`、`toc`、`content`；书籍包有独立的 PackageJob，不混入采集运行阶段；
- `status`：运行状态及用户可读中文标签；
- `totalTaskCount`、`completedTaskCount`：BookInfo、Toc 和 Content 子任务的数据库汇总；
- `inFlightTaskCount`、`pendingTaskCount`、`cancelledTaskCount`、`failedTaskCount`：租约中、待领取、已取消和死信数；
- `remainingTaskCount`：尚未完成、死信或取消的子任务数；
- `progressPercent`：BookInfo/Toc 阶段为 `null`，进入 Content 阶段后按整体子任务完成比例计算；
- `updatedAt`、`lastError`、`canonicalBookId`（若已建立）。

重试不会增加总量或完成量。失败数单独展示，不能把失败章节伪装成成功进度；运行出现不可恢复失败时，状态为 `Failed`，即使页面曾显示过部分百分比。

### 6.2 页面表现

- 活跃运行每 3–5 秒刷新一次；无活跃运行时停止高频轮询。
- 进度条使用 `aria-valuenow`、`aria-valuemin`、`aria-valuemax`；BookInfo/Toc 尚未确定正文总量时使用可访问的“不确定进度”文本。
- 每个运行显示阶段、状态、计数、最近错误和可用控制按钮。
- 失败、权限不足、接口暂时不可用、空列表、离线和无 JavaScript 均有明确反馈。
- 页面不展示任务 Variables、CredentialReferenceId、原始上游 HTML、凭据或正文载荷。

## 7. 直接书籍地址采集

### 7.1 输入规则

- UI 只要求一个 URL；API 可接受可选的内部 `sourceId` 作为受控排障字段，但不允许借此绕过 URL 校验。
- URL 必须是 `http`/`https`，不能含用户名、密码、片段或未声明的跨域跳转；主机、端口和来源根地址必须匹配已登记 Source。
- 来源适配器负责把 URL 解析为稳定的 `externalBookId`；无法解析时返回稳定错误码，不猜测路径、不发起上游请求。
- 未登记来源、未支持的地址形态、来源没有 BookInfo/Toc/Content 能力时拒绝创建运行。
- 不提供“把任意 URL 当作 SourceBaseUrl”或通用网页代理能力，继续执行 SSRF 和来源安全模型。

### 7.2 API 草案

```text
POST /api/v1/admin/collection-runs
Body: { "url": "https://registered-source.example/book/123" }
Response: 202
{
  "status": "accepted",
  "run": { "id": "...", "status": "pending" }
}

GET  /api/v1/admin/collection-runs?limit=...
GET  /api/v1/admin/collection-runs/{runId}
POST /api/v1/admin/collection-runs/{runId}/control
Body: { "action": "pause | resume | stop | cancel", "reason": "..." }
```

控制请求统一接受 `{ "reason": "..." }`，理由长度和内容遵循现有运维命令约束。创建运行应对同一来源/外部书籍的活跃运行做幂等去重；终态运行允许明确创建新的重新采集运行。

## 8. 书籍打包

### 8.1 v1 包内容

v1 提供三种独立格式：`zip`、`epub`、`txt`。三种格式都读取同一份固定的当前正文快照，不允许因格式不同而重新抓取或选择不同版本。

ZIP 结构建议如下：

```text
manifest.json
book.json
chapters/000001.txt
chapters/000002.txt
...
```

- `book.json`：稳定 Canonical `bookId`、书名、作者、章节数、生成时间和格式版本；
- `manifest.json`：文件列表、章节稳定 ID、顺序、标题和内容哈希；
- 章节正文：只取已发布的当前 `ContentVersion`，纯文本 UTF-8，不带上游 HTML；
- 文件名按序号生成，标题仅作为内容元数据，避免路径穿越和非法文件名；
- ZIP 先写入受限临时文件，所有章节和校验成功后才转为可下载状态，不提供半包下载。

EPUB 3 结构至少包含 `mimetype`、`META-INF/container.xml`、`OEBPS/content.opf`、`OEBPS/nav.xhtml` 和按章节生成的 XHTML；章节顺序、标题和正文必须与快照一致。v1 不加入封面、图片、音频或复杂排版，正文文本必须经过 HTML 转义。

单文件 TXT 使用 UTF-8 编码，包含书名、作者、生成信息、章节序号/标题和章节正文；章节之间使用固定分隔线，统一换行符，不输出 HTML、来源内部字段或凭据。

### 8.2 打包边界

- 仅允许对存在且未被 Content Policy 下架的正典书创建包。
- 打包开始时固定章节与当前版本快照；生成期间版本变化不能造成同一包内的隐式混用。
- 缺少任一必要当前正文、读取超限、压缩失败或下架状态变化时，PackageJob 失败且不暴露下载链接。
- 下载使用受保护 API，记录操作者、书籍、包 ID、结果和理由；包文件不进入 Git，不通过日志输出正文。
- v1 不承诺图片/音频、多语言排版或向普通阅读用户公开下载；这些格式和权限作为后续扩展项。

### 8.3 API 草案

```text
POST /api/v1/admin/books/{bookId}/packages
Body: { "format": "zip | epub | txt" }
Response: 202 { "status": "accepted", "package": { "id": "...", "status": "queued" } }

GET  /api/v1/admin/packages/{packageId}
GET  /api/v1/admin/packages/{packageId}/download   # 仅 Completed
```

打包任务有独立的 `Queued`、`Running`、`Completed`、`Failed` 状态和进度；它不改变 Canonical Content，也不绕过下架、质量和当前版本选择规则。

## 9. 数据与模块边界草案

### 9.1 Crawling

- 新增 `crawler.runs`，保存运行身份、来源、外部书籍 ID、规范化输入地址、阶段、状态、错误和时间戳；
- `crawler.tasks` 增加可选 `RunId`，现有无父运行的周期追更任务保持兼容；
- CrawlerTask 的租约查询必须排除 `Paused`、`Stopping`、`Stopped`、`Cancelled`、`Failed`、`Completed` 运行下的新任务；
- 进度查询基于数据库事实，不能仅依赖消息或内存计数。

### 9.2 Sources / Library / Content

- Sources 负责来源地址解析和适配器安全边界；
- Sources 继续拥有 SourceBook、SourceChapter 和 FetchArtifact；
- Library 继续拥有稳定 Canonical Book/Chapter 和匹配映射；
- Content 继续拥有当前正文版本、质量与下架策略；
- 打包读取这些模块公开的应用契约，不直接跨模块操作对方实体或表。

### 9.3 PackageJob 归属

推荐由 Content/Library 侧拥有“正典书籍导出”应用契约和包状态，Crawling 只负责采集运行；具体落表位置在实现前通过 ADR 固化。若为降低 v1 复杂度暂放在 Crawling，也必须保持只读 Canonical/Content 契约，不复制正文事实。

## 10. 权限、安全与审计

- 创建、查看、控制采集运行：`Operator` / `Administrator`；
- 创建、查看、下载书籍包：同一运维权限，后续再拆分导出权限；
- 每个写操作从认证主体取得 actor，不接受客户端自报操作者；
- 创建、暂停、恢复、停止、取消、打包和下载均记录审计事件，至少包含资源 ID、前后状态、理由、结果和时间；
- URL、来源和外部 ID按敏感度记录，绝不记录 Cookie、Token、密码、CredentialReference 对应的秘密、原始正文或完整响应；
- 地址解析、上游请求、ZIP 条目名、包大小、章节数量都执行现有上限和安全校验；
- 取消不回滚已发布内容，避免用控制命令破坏追加式历史版本不变量。

## 11. 验收标准

### 11.1 Unit

- 运行状态机覆盖合法/非法流转、幂等控制、暂停恢复、优雅停止和取消；
- URL 解析覆盖已登记来源、非法 scheme、跨主机、查询/片段、未知路径和无适配器；
- 进度计算覆盖目录未知、重试、失败、重复任务、空章节和终态；
- ZIP/EPUB/TXT 条目名、格式结构、大小限制、快照一致性、缺正文拒绝和临时文件清理；
- 取消后的已发布历史内容不被删除。

### 11.2 Integration / Contract

- PostgreSQL migration、运行/任务关联、并发控制命令和租约排除；
- Worker 重启/租约过期后暂停、停止、取消语义仍成立；
- BookInfo → Toc → Content 的完整运行进度和失败收敛；
- API 的 202/404/409/422/401/403/409 状态及响应字段稳定；
- API 响应不含 Variables、CredentialReferenceId、原始正文或秘密；
- 包在版本变化、下架和缺正文场景下不产生可下载半包。

### 11.3 VM / 浏览器自动化

- Ubuntu VM 使用 `docker-compose.build.yml` 源码构建并执行 API/Worker/Migrations 健康检查；
- 用确定性 Fixture/测试源自动跑一轮地址采集、进度推进和四个控制动作；
- 用浏览器自动化验收运维页面的输入、轮询、进度条、按钮禁用/恢复、权限/错误/空状态和包下载链接；
- 不执行阅读 3.0 / MuMu 真机链路，保留为人工待定事项；
- 只有真实 Build、Tests、Runtime、Security、CI、文档和功能验收证据齐全后，才标记 Accepted。

## 12. 明确非目标

- 任意站点通用抓取或绕过登录、付费、VIP、验证码和访问控制；
- 直接把未登记来源地址转成代理请求；
- 取消时删除已入库内容或重写历史版本；
- 在 API 请求内同步执行长时间全书采集或同步生成大包；
- 本轮强制完成阅读 3.0 真机导入、阅读和安装验收；
- v1 不交付图片/音频、多语言复杂排版或普通用户下载权限。

## 13. 已确认决策

以下决策已确认，后续实现必须以此为准：

| 编号 | 已确认结论 |
| --- | --- |
| Q1 | 暂停可恢复；停止优雅终止且不可恢复；取消终止后续工作且不可恢复 |
| Q2 | 任一必要章节不可恢复失败时，整本运行失败，不生成完成包 |
| Q3 | 独立支持 ZIP、EPUB 3、单文件 TXT |
| Q4 | 书籍包仅 Operator / Administrator 可创建和下载 |
| Q5 | 只接受已登记公开来源，未知或无法解析 URL 直接拒绝 |
| Q6 | 入口为受保护 API 和 `/admin/operations`，不进入普通 Reader |
| Q7 | 采集和打包均异步执行，活跃采集运行幂等复用 |
| Q8 | 直接 URL 自动导入书目并复用现有正典匹配策略，不做模糊匹配 |
| Q9 | VIP、登录后可见、空正文或安全拒绝按失败处理，已成功内容保留 |
| Q10 | 包按格式生成不可变版本，不覆盖旧包；人工触发，不自动随追更生成 |
| Q11 | 运行失败支持重新采集，重新创建新运行并保留旧证据 |
| Q12 | 控制和重新采集要求理由；打包记录操作者，可不强制理由 |
| Q13 | 包文件落 Ubuntu VM 受限共享目录，数据库保存状态和哈希 |
| Q14 | 包文件默认保留 7 天；过期删除文件但保留审计元数据 |
| Q15 | 重大匹配和包存储方案已采用推荐值 |

## 14. 实施流程

### 阶段 0：需求冻结

- 以本文件作为 v1 的功能边界和验收基线；
- 非重大技术细节由实现按现有架构选择；
- 新增重大范围、权限、内容归属或存储决策时，重新进入 Grill Me，不直接改代码。

### 阶段 1：架构与领域设计

- 新增 ADR，固化 `CollectionRun`、控制状态、子任务关联、进度口径、URL Resolver 和 PackageJob 归属；
- 明确状态机、幂等键、并发控制、取消不回滚历史内容等不变量；
- 评审模块依赖，确保 Domain 不依赖 EF Core、ASP.NET、Redis 或文件系统。

### 阶段 2：持久化与应用契约

- 增加运行、任务关联、包任务和包元数据的 Migration；
- 增加运行状态/控制/进度查询契约、受保护 API、权限和命令审计；
- 迁移由独立 Migrations Host 执行，API/Worker 不绕过迁移流程直接改表。

### 阶段 3：采集执行链

- 实现已登记来源 URL 解析和拒绝策略；
- 接入 `BookInfo → Toc → Content` 子任务链；
- 让租约领取、重试、事件重复投递和 Worker 重启遵守父运行控制状态；
- 每个阶段完成后重算进度，完成/失败只由数据库事实收敛。

### 阶段 4：书籍打包

- 对固定的当前正文快照生成 ZIP、EPUB 3 或单文件 TXT；
- 先写受限临时文件，校验通过后原子转为可下载状态；
- 加入下架、缺正文、版本变化、路径安全、大小上限和过期清理处理；
- 包失败不得暴露半包下载链接。

### 阶段 5：运维前端

- 在 `/admin/operations` 增加 URL 输入、运行列表、阶段进度、错误状态和控制按钮；
- 增加完成书籍的格式选择、打包进度和下载入口；
- 自动刷新、权限/空/错误/离线状态、键盘操作、窄屏布局、焦点和 reduced-motion 均纳入验收；
- 前端只展示受控 DTO，不读取任务 Variables、凭据引用或原始正文。

### 阶段 6：分层验收

1. Unit：状态机、URL 解析、进度汇总、幂等、EPUB/TXT/ZIP 结构和安全边界；
2. Integration：PostgreSQL Migration、并发控制、租约排除、Worker 重启、完整采集链和包快照；
3. Contract：API 状态码、响应字段、授权、审计和敏感字段排除；
4. Ubuntu VM Runtime：使用 `docker-compose.build.yml` 源码构建，执行健康检查和确定性 Fixture 链路；
5. 浏览器自动化：验证运维页面输入、轮询、控制、打包和下载状态；
6. Release Gate：重新检查 diff、Secret、文档、Build/Test/Runtime/Security，创建 candidate commit，推送后确认 CI、Docker、Security 全部真实通过。

### 阶段 7：交付与待定事项

- 只有适用的自动化验收和运行证据齐全，才标记 `Accepted / Completed`；
- 阅读 3.0 / MuMu 真机导入、阅读和安装不在本工作包自动验收内，继续列为人工待定事项；
- 未执行的真实来源、真实账户、真机或人工视觉检查必须明确记录为 `NOT RUN`，不得以代码通过替代。

## 15. 变更控制

实现过程中如出现以下变化，必须暂停实现并重新对齐：

- 允许登录、付费、VIP、验证码或社区来源；
- 改变停止/取消是否可恢复，或要求回滚已发布内容；
- 允许普通用户访问书籍包或长期保存包文件；
- 增加 EPUB 之外的复杂媒体/排版能力；
- 允许任意 URL 代理或绕过已登记 Source 边界。
