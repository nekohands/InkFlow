using InkFlow.BuildingBlocks.Observability;
using InkFlow.BuildingBlocks.Persistence;
using InkFlow.Modules.Crawling.Orchestration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(TimeProvider.System);
builder.AddInkFlowObservability("InkFlow.Worker");
var connectionString = builder.Configuration.GetConnectionString("Database")
    ?? "Host=localhost;Port=5432;Database=inkflow;Username=inkflow;Password=inkflow";
builder.Services.AddInkFlowPersistence(connectionString);
builder.Services.AddScoped<CrawlerTaskProcessor>();
builder.Services.AddHostedService<CrawlerWorker>();

var app = builder.Build();
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "InkFlow.Worker" }));
await app.RunAsync();

internal sealed class CrawlerWorker(IServiceScopeFactory scopeFactory, ILogger<CrawlerWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var workerId = $"{Environment.MachineName}:{Environment.ProcessId}";
        logger.LogInformation("InkFlow crawler worker {WorkerId} started", workerId);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var processor = scope.ServiceProvider.GetRequiredService<CrawlerTaskProcessor>();
                if (!await processor.ProcessOneAsync(workerId, stoppingToken))
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Crawler worker loop failed; retrying after a short delay");
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }
    }
}
