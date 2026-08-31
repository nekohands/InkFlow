# ADR 0013: Core SLO Runtime 合成探针基线

- 状态：Accepted
- 日期：2026-08-29

## 背景

Core SLO v1 已有低基数指标、窗口证据评估器和 Compose OTLP Collector 接收基线，但 Runtime smoke 还没有一次性验证四个服务面都能产生可聚合的请求与延迟样本。单独检查 Collector 健康只能证明接收进程启动，不能证明 API 与 Reader 的运行路径可探测。

## 决策

- 新增 `scripts/core-slo-runtime-smoke.sh`，对固定的四个稳定入口执行有界 GET 探针：`/api/v1/books`（200）、`/api/legado/v1/search?q=`（200）、`/api/developer/v1/books`（401）和 `/reader`（200）。401 是预期的认证边界响应，按 Core SLO 规则属于 good 请求。
- Legado 探针固定使用空查询，避免触发真实来源发现、抓取或第三方网络；Developer 探针不携带真实凭据。探针只记录 HTTP 状态和 curl 总耗时，不保存响应正文、身份信息、Token 或异常原文。
- 每个服务面默认执行 5 次请求，可通过环境变量调整为 1–20 次；单请求超时默认 10 秒且最多 60 秒。脚本不自动重试，传输失败、超时或非预期状态立即失败。
- 补充（2026-08-31）：每个服务面先执行一次不计入统计的预热请求，用于隔离源码 Compose 冷启动时的 JIT、数据库连接池和序列化初始化；预热仍必须返回预期 HTTP 状态。随后测量窗口的最近秩 p95 必须不超过 Core SLO 目标：`public_api` / `developer_api` 为 750ms，`legado_api` / `reader` 为 1000ms；等于边界通过，超过目标或传输/状态异常失败。
- 脚本生成包含 UTC 窗口、固定证据来源、请求数、5xx 数、延迟样本数和最近秩 p95 毫秒值的 JSON 证据；`durationSampleCount` 与 `requestCount` 必须一致，字段映射到 `CoreSloWindowEvidence` 的统一契约。
- CI 在源码 Compose Runtime smoke 成功后执行探针，并上传 JSON 作为短期构建产物，供审查和后续窗口聚合使用。
- 为使短时 Runtime smoke 能观察到 OTLP metrics，CI 通过 Compose 将 `OTEL_METRIC_EXPORT_INTERVAL` 设为 1000 毫秒；Collector metrics pipeline 使用 1 秒 batch。CI 临时将 signal-specific `debug/metrics` 设为 `detailed`，receipt smoke 校验 `inkflow.slo.requests`、`inkflow.slo.request.duration` 及四个稳定服务面标签；默认 Compose 仍使用 `basic`。

## 非目标

- 合成探针不是生产月度 SLO 证明，不替代受治理 OTLP 后端、真实窗口聚合、错误预算告警、访问控制、保留策略或人工验收。
- 不在 CI 中访问真实 Official Source，不执行 MuMu/阅读 3.0，不创建真实用户，不携带 API Key 或 Personal Legado Token。
- 不把探针路径、查询参数、用户、IP、Token 或响应内容加入 OpenTelemetry 指标维度或证据聚合。

## 后果

CI 现在能在真实 Compose 运行时固定覆盖四个 Core SLO 服务面，确认对应 metrics 已到达 Collector，并留下可复核的短窗口合成证据；脚本或 receipt smoke 失败会阻断该次 Runtime 门禁。由于证据窗口短且流量为合成流量，生产 `Accepted/Completed` 仍必须等待部署环境的后端到达、长窗口聚合、告警/保留治理以及用户已明确延后的人工/真实来源验收。
