using InkFlow.BuildingBlocks.Observability;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);
builder.AddInkFlowObservability("InkFlow.Worker");
builder.Services.AddHostedService<WorkerHeartbeat>();

await builder.Build().RunAsync();

internal sealed class WorkerHeartbeat(ILogger<WorkerHeartbeat> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("InkFlow Worker started");
        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
    }
}
