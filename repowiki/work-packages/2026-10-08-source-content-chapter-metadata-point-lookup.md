# 5.73 Source content chapter metadata point lookup

Status: Accepted

## Objective

来源正文抓取在触网前只读取目标来源书与目标章节的元数据，不因定位一个 `externalChapterId` 物化整本来源书的章节集合。

## Scope

- 为 `ISourceBookRepository` 增加保留“书不存在”与“章节不存在”区别的章节点查找结果。
- `EfSourceBookRepository` 使用来源书身份和外部章节 ID 的有界查询；`SourceContentService` 改走该点查询。
- 补充 SourceContentService Unit 回归和 PostgreSQL 仓储查询边界回归。
- 同步 Progress、Handoff、delivery profile 与 RepoWiki。

## Non-goals

- 不改公共 API、Legado contract、Source Adapter 或正文内容/版本语义。
- 不改变 `GetAsync` 的完整来源书聚合语义及目录/同步调用方。
- 不增加 Schema/Migration、缓存、durable cursor、重试/HTTP 预算或真实来源验收。
- 不触碰 `.workbuddy-ai/`。

## Acceptance

- 正文抓取路径不再调用完整来源书 `GetAsync` 以定位单个章节。
- 缺书仍返回现有 `catalog: book ... has not been imported` 错误；缺章仍返回现有 `catalog: chapter ... is not part of book` 错误。
- EF 查询按来源书身份和外部章节 ID 定位至多一行，跨书不命中，并传播取消。
- 既有适配器触网、正文落库、健康记录和幂等语义保持不变。

## Risks and verification

- 风险：点查询若丢失书存在状态会改变稳定错误分类；以 Unit 缺书/缺章回归和单次 SQL 证据覆盖。
- 先执行 TDD 红/绿 focused tests，再执行 Restore、Release Build、Unit、Architecture、Contract、迁移模型、脚本语法、Integration、diff/secret audit。
- 创建候选 commit 后推送 `origin/dev`，按候选 SHA 验证 CI、Docker、Security；完成后再写入真实证据并单独 closeout。

## Local verification

- TDD red: new point-lookup test first failed because `SourceChapterLookup` was not yet defined.
- Focused `SourceContentServiceTests`: `8/8` PASS.
- Unit `622/622`, Architecture `1/1`, Contract `12/12` PASS.
- Restore/tool restore PASS; Release Build `0 warnings / 0 errors`.
- PowerShell migration model check `11/11` PASS; `bash -n scripts/verify-migrations.sh` exit `0`; `git diff --check` and added-line secret audit PASS.
- Full Solution Test: Unit/Architecture/Contract PASS; Integration `8 passed / 3 skipped / 122 blocked` during Testcontainers class initialization because Windows Docker Engine `npipe://./pipe/docker_engine` is unavailable. The new PostgreSQL regression compiled but did not run locally.

## Delivery

- Candidate SHA `5892eca6d754ba53d3a6d496b4d37cb69e387643` passed exact-head CI `37739646911`, Docker `37739647011`, and Security `37739647021`.
- CI passed migrations, all Unit/Architecture/Contract tests, Compose validation, reader/Legado/source/runtime smoke, SLO probes, Redis, PostgreSQL backup/restore, and diagnostics.
- Docker built, scanned, published, and verified the four business images and release Compose images. Security passed SBOM, filesystem, NuGet, and CodeQL checks.
- Local Integration remains `8 passed / 3 skipped / 122 blocked` only because the Windows Docker Engine named pipe is unavailable; remote CI supplied PostgreSQL and runtime evidence.
- No public Contract, Schema/Migration, cache, or durable cursor change; `.workbuddy-ai/` remains untracked and untouched.
