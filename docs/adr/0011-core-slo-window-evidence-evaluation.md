# ADR 0011: Core SLO 窗口证据评估契约

- 状态：Accepted
- 日期：2026-08-29

## 背景

Core SLO v1 已经从 API 记录低基数 OpenTelemetry 指标，但仅有指标出口并不能回答某个时间窗口是否具备可审计的达标证据。若每个 Collector 查询或人工报表自行解释零流量、p95 缺失、直方图样本不一致和错误预算，容易把“没有证据”误报为“达标”。

## 决策

在 Observability Building Block 中提供纯函数 `CoreSloEvidenceEvaluator`，把外部聚合结果转换为稳定、可序列化的窗口评估结果：

- 输入包含明确的 UTC 时间窗口、证据来源标识，以及 `public_api`、`legado_api`、`developer_api`、`reader` 四个服务面的请求数、5xx 数、延迟直方图样本数和 p95 毫秒值。p95 由 OTLP/探针聚合端计算，评估器不从不完整输入猜测它。
- 只有四个服务面都有正流量、延迟样本数与请求数一致、p95 存在且数据合法时，窗口才可能通过。零流量、缺失 p95、样本数不一致统一输出 `InsufficientEvidence`；未知服务面或负数、越界、非法延迟等聚合结果输出 `InvalidEvidence`。
- 可用性按 `1 - serverErrorCount / requestCount` 计算；错误预算按 Core SLO 99.5% 目标计算，剩余预算允许为负数以保留超预算证据。p95 只能与对应服务面目标比较，边界值视为通过。
- 评估结果使用 `Passed`、`Failed`、`InsufficientEvidence`、`InvalidEvidence` 四种状态，并使用固定 reason code；不携带路径、用户、Token、异常原文或其他高基数值。`IsPassing` 只有在完整四面均通过时为真。

## 非目标

- 本 ADR 不接入 Collector 查询、合成探针、Grafana、Alertmanager、外部通知或生产保留系统。
- 本 ADR 不把测试夹具或单个窗口报告当作生产月度达标证据；真实窗口、探针覆盖、错误预算告警和 RPO/RTO/保留治理仍需要部署环境验收。

## 后果

后续 OTLP 查询或合成探针只需映射到统一输入，即可获得一致的达标/失败/缺证据结论和错误预算数字；Release Gate 可以明确区分“未达到”与“尚无足够证据”。当前实现仍保持无状态，不新增数据库表或公开 API，避免把派生观测结果误当成业务事实。
