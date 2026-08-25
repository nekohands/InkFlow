using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

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
        var telemetry = builder.Services
            .AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName));

        telemetry.WithTracing(tracing => tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation());

        telemetry.WithMetrics(metrics => metrics
            .AddAspNetCoreInstrumentation()
            .AddRuntimeInstrumentation());

        return builder;
    }
}
