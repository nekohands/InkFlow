using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Exporter;

namespace InkFlow.BuildingBlocks.Observability;

/// <summary>
/// 为各宿主（Api / Worker / Scheduler）注册统一的可观测性基座：
/// 服务资源标识 + ASP.NET Core 与 HttpClient 的链路追踪和指标采集。
/// </summary>
public static class ObservabilitySetup
{
    public static IHostApplicationBuilder AddInkFlowObservability(
        this IHostApplicationBuilder builder,
        string serviceName)
    {
        var hasCommonOtlpEndpoint = HasConfigurationValue(
            builder.Configuration,
            "OTEL_EXPORTER_OTLP_ENDPOINT");
        var hasTracesOtlpEndpoint = hasCommonOtlpEndpoint || HasConfigurationValue(
            builder.Configuration,
            "OTEL_EXPORTER_OTLP_TRACES_ENDPOINT");
        var hasMetricsOtlpEndpoint = hasCommonOtlpEndpoint || HasConfigurationValue(
            builder.Configuration,
            "OTEL_EXPORTER_OTLP_METRICS_ENDPOINT");

        var telemetry = builder.Services
            .AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName));

        telemetry.WithTracing(tracing =>
        {
            tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation();
            if (hasTracesOtlpEndpoint)
            {
                tracing.AddOtlpExporter();
            }
        });

        telemetry.WithMetrics(metrics =>
        {
            metrics
                .AddAspNetCoreInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter(CrawlerFailureMetrics.MeterName)
                .AddMeter(CoreSloMetrics.MeterName);
            if (hasMetricsOtlpEndpoint)
            {
                metrics.AddOtlpExporter();
            }
        });

        return builder;
    }

    private static bool HasConfigurationValue(IConfiguration configuration, string key) =>
        !string.IsNullOrWhiteSpace(configuration[key]);
}
