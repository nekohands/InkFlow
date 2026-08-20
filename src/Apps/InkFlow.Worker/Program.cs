using InkFlow.BuildingBlocks.Observability;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(TimeProvider.System);
builder.AddInkFlowObservability("InkFlow.Worker");
builder.Services.AddHostedService<WorkerHeartbeat>();

var app = builder.Build();
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "InkFlow.Worker" }));
await app.RunAsync();

internal sealed class WorkerHeartbeat(ILogger<WorkerHeartbeat> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("InkFlow Worker started");
        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
    }
}
