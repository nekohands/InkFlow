using InkFlow.BuildingBlocks.Observability;
using InkFlow.BuildingBlocks.Persistence;
using InkFlow.Modules.Crawling.Orchestration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(TimeProvider.System);
builder.AddInkFlowObservability("InkFlow.Scheduler");
var connectionString = builder.Configuration.GetConnectionString("Database")
    ?? "Host=localhost;Port=5432;Database=inkflow;Username=inkflow;Password=inkflow";
builder.Services.AddInkFlowPersistence(connectionString);
builder.Services.AddScoped<SourceUpdateScheduler>();
builder.Services.AddHostedService<UpdateSchedulerWorker>();

var app = builder.Build();
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "InkFlow.Scheduler" }));
app.MapGet("/ready", async (SourcesDbContext database, CancellationToken cancellationToken) =>
{
    if (!await database.Database.CanConnectAsync(cancellationToken))
    {
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }

    return Results.Ok(new { status = "ready", service = "InkFlow.Scheduler" });
});
await app.RunAsync();

internal sealed class UpdateSchedulerWorker(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    TimeProvider timeProvider,
    ILogger<UpdateSchedulerWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var updateInterval = TimeSpan.FromMinutes(Math.Max(1, configuration.GetValue("Scheduler:UpdateIntervalMinutes", 15)));
        var pollInterval = TimeSpan.FromSeconds(Math.Max(5, configuration.GetValue("Scheduler:PollIntervalSeconds", 30)));
        logger.LogInformation("InkFlow update scheduler started with update interval {UpdateInterval}", updateInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var scheduler = scope.ServiceProvider.GetRequiredService<SourceUpdateScheduler>();
                var result = await scheduler.ScheduleDueAsync(timeProvider.GetUtcNow(), updateInterval, cancellationToken: stoppingToken);
                if (result.Enqueued > 0)
                {
                    logger.LogInformation("Scheduled {Enqueued} update tasks from {Considered} due source books", result.Enqueued, result.Considered);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Update scheduler loop failed; retrying on next poll");
            }

            await Task.Delay(pollInterval, stoppingToken);
        }
    }
}
