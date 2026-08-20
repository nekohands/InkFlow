# ADR 0003: 阅读 3.0 兼容层访问 InkFlow API

- 状态：Accepted
- 日期：2026-08-20

## 决策

InkFlow 将提供 Legado `bookSource`、`rssSource` 和可选 `replaceRule` 分发能力。规则面向 InkFlow 自有稳定 API，不直接包含第三方小说站的页面解析逻辑。

## 结果

上游站点变化被限制在服务端 Source Adapter 内；Legado 用户不需要因为每个来源的 DOM 变化重复更新规则。
