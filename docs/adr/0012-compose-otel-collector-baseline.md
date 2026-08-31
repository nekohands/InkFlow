# ADR 0012: Compose OTLP Collector 监控基线

- 状态：Accepted
- 日期：2026-08-29

## 背景

Core SLO v1 已由 API、Worker、Scheduler 和 Reader 相关宿主产生低基数 OpenTelemetry 指标，`CoreSloEvidenceEvaluator` 也已经定义窗口证据的判定语义。但两份 Compose 编排此前没有 OTLP 接收端，运行时只能验证应用健康，不能验证观测数据有明确的接收边界。

## 决策

- 在生产镜像 Compose 和源码构建 Compose 中加入官方 core `otel/opentelemetry-collector:0.160.0-nightly.779aeb2@sha256:c1490bb380998b9246b8ea054867ee05b2e9fc6be34cd1f3c4f0e1ec88b9fe91`，使用仓库内只读配置文件，不使用浮动的 `latest` 标签。
- Collector 只在 Compose 内部网络监听 OTLP gRPC `4317` 和 HTTP `4318`；不把这两个接收端口发布到宿主机。健康检查扩展监听 `13133`，只绑定宿主机 loopback，供本机/CI smoke 使用。
- API、Worker、Scheduler 的 Compose 默认 `OTEL_EXPORTER_OTLP_ENDPOINT` 指向 `http://otel-collector:4317`，仍允许部署环境通过同名环境变量覆盖到受管的外部 OTLP 端点。应用依赖 Collector 已启动，但不把 Collector 当作业务事实源或数据库依赖。
- Collector 使用 `read_only`、`tmpfs`、`no-new-privileges` 和 `cap_drop: ALL`；配置以只读 bind mount 注入。CI Runtime smoke 显式请求 loopback 健康端点，验证接收服务真实启动。
- 当前配置使用 `debug` exporter 作为本地/CI 接收基线，便于诊断但不提供持久化、查询、告警或长期保留。生产部署必须替换为经过治理的 OTLP 后端/exporter，并单独验收保留、告警、访问控制和错误预算窗口。

## 非目标

- 不公开 OTLP 接收端口，不新增应用 `/metrics` 公共 API。
- 不在本 ADR 中引入 Grafana、Prometheus、Tempo、Alertmanager、外部通知或生产数据保留系统。
- Collector 健康响应和 CI 中的启动验证不等同于四个服务面均已产生生产窗口证据，也不等同于 Core SLO 月度达标。
- 不执行 MuMu/阅读 3.0、真实来源、真实追更或其他人工验收。

## 后果

Compose 与远端 Runtime smoke 现在具备可回归的 Collector 启动/健康边界，且应用默认不会把 telemetry 发往宿主机的未管理端口。部署环境仍需要提供真实 OTLP 后端和窗口聚合证据；在本机 Docker 不可用时，Compose、Collector 和 Testcontainers 相关证据必须明确标记为 BLOCKED，不能以本地应用测试替代。

## 维护更新（2026-09-01）

Docker 发布门禁扫描 `0.159.0` 时发现其内置 `golang.org/x/crypto` 存在已修复的高危漏洞（CVE-2026-56854）。截至本次验证，官方稳定 `0.160.0` 标签尚未发布，因此两个 Compose 文件和 Docker 门禁统一暂时固定到已发布的 `0.160.0-nightly.779aeb2` 及其 manifest digest；待稳定标签可用后必须升级并重新经过 Docker Trivy、Compose Runtime 和 Ubuntu VM 源码构建验证。其余网络暴露和运行时加固边界不变。
