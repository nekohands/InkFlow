using InkFlow.BuildingBlocks.Observability;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSingleton(TimeProvider.System);
builder.AddInkFlowObservability("InkFlow.Scheduler");
builder.Services.AddHostedService<SchedulerHeartbeat>();

await builder.Build().RunAsync();

internal sealed class SchedulerHeartbeat(ILogger<SchedulerHeartbeat> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("InkFlow Scheduler started");
        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
    }
}
