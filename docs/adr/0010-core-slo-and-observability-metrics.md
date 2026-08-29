# ADR 0010: Core SLO 使用低基数 OpenTelemetry 指标

- 状态：Accepted
- 日期：2026-08-29

## 决策

Core SLO v1 以统一的低基数请求指标作为测量契约，覆盖四个稳定服务面：`public_api`、`legado_api`、`developer_api` 和 `reader`。

- 可用性目标为月度 `99.5%`。状态码 `1xx–4xx` 是预期客户端结果，`5xx` 或未处理异常是坏事件；认证拒绝和限流拒绝不会伪装成服务端错误。
- 延迟目标以 p95 表示：公共 API `750ms`、Legado API `1000ms`、Developer API `750ms`、Reader `1000ms`。
- `inkflow.slo.requests` 是按 `inkflow.slo.surface` 与 `inkflow.slo.outcome` 聚合的 Counter；`inkflow.slo.request.duration` 是同样维度的毫秒 Histogram；`inkflow.slo.server.errors` 只按服务面聚合。
- 仅允许稳定服务面进入 SLO 标签。请求路径、查询参数、BookId/ChapterId、用户、IP、异常原文和 Token 不进入这些指标。
- `/health`、管理静态页、未知路径和第三方来源内部请求不计入 Core SLO 服务面。标准 ASP.NET Core/HttpClient instrumentation 继续提供更细粒度的诊断指标。
- 只有配置 OTLP endpoint 时才启用 exporter：公共 `OTEL_EXPORTER_OTLP_ENDPOINT` 同时启用 traces/metrics，专用 `OTEL_EXPORTER_OTLP_TRACES_ENDPOINT` 或 `OTEL_EXPORTER_OTLP_METRICS_ENDPOINT` 只启用对应信号；没有 Collector 时不让应用默认向本机端口发送遥测。

## 非目标

- 这轮不伪造生产月度达标证据；目标需要真实运行窗口或受控压测/合成探针汇总后才能标记达标。
- 这轮不新增公开 `/metrics` 端点、Grafana/Alertmanager 部署、外部告警路由或生产 SLO 保留策略。

## 后果

SLO 查询不依赖高基数 URL 或敏感上下文，Collector/监控系统可以按服务面计算可用性、错误预算和 p95 延迟。生产部署仍必须提供 OTLP Collector、时间窗口、告警阈值和数据保留治理，才具备完整的 1.0 SLO 验收证据。
