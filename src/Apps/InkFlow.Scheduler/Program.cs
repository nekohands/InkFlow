using InkFlow.BuildingBlocks.Observability;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(TimeProvider.System);
builder.AddInkFlowObservability("InkFlow.Scheduler");
builder.Services.AddHostedService<SchedulerHeartbeat>();

var app = builder.Build();
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "InkFlow.Scheduler" }));
await app.RunAsync();

internal sealed class SchedulerHeartbeat(ILogger<SchedulerHeartbeat> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("InkFlow Scheduler started");
        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
    }
}
