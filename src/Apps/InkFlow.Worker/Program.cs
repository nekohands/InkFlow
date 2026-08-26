// 后台任务宿主:轮询 crawler.tasks 队列,按能力分派给对应处理器。
// 普通阅读路径不经过这里——Worker 只负责"检测更新 → 抓取 → 落库"的写侧链路。
using InkFlow.Modules.Content.Application;
using InkFlow.Modules.Content.Domain;
using InkFlow.Modules.Crawling.Application;
using InkFlow.Modules.Crawling.Domain;
using InkFlow.Modules.Crawling.Infrastructure.Persistence;
using InkFlow.Modules.Library.Application;
using InkFlow.Modules.Sources.Application;
using InkFlow.Modules.Sources.Domain;
using InkFlow.Modules.Sources.Infrastructure;
using InkFlow.Modules.Sources.Infrastructure.Persistence;
using InkFlow.Modules.Library.Infrastructure.Persistence;
using InkFlow.Modules.Content.Infrastructure.Persistence;
using InkFlow.BuildingBlocks.Security;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString =
    builder.Configuration.GetConnectionString("Database")
    ?? "Host=localhost;Port=5432;Database=inkflow;Username=inkflow;Password=inkflow";

builder.Services.AddDbContext<CrawlingDbContext>(o => o.UseNpgsql(connectionString));
builder.Services.AddDbContext<SourcesDbContext>(o => o.UseNpgsql(connectionString));
builder.Services.AddDbContext<LibraryDbContext>(o => o.UseNpgsql(connectionString));
builder.Services.AddDbContext<ContentDbContext>(o => o.UseNpgsql(connectionString));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IIpAddressResolver, DnsIpAddressResolver>();
builder.Services.AddScoped<ICrawlerTaskRepository, EfCrawlerTaskRepository>();
builder.Services.AddScoped<ISourceRepository, EfSourceRepository>();
builder.Services.AddScoped<ISourceBookRepository, EfSourceBookRepository>();
builder.Services.AddScoped<IMatchCandidateRepository, EfMatchCandidateRepository>();
builder.Services.AddScoped<IChapterMappingRepository, EfChapterMappingRepository>();
builder.Services.AddScoped<IContentVersionRepository, EfContentVersionRepository>();

builder.Services.AddHttpClient<ISourceHttpClient, ProductionSafeSourceHttpClient>();
builder.Services.AddScoped<ISelectorEvaluator, CssSelectorEvaluator>();
builder.Services.AddScoped<RuleAdapter>();
builder.Services.AddScoped<SourceCatalogService>();
builder.Services.AddScoped<CanonicalChapterMappingService>();
builder.Services.AddScoped<SourceContentService>();
builder.Services.AddScoped<TocSyncTaskHandler>();
builder.Services.AddScoped<ContentFetchTaskHandler>();
builder.Services.AddHostedService<TaskPollingService>();

var app = builder.Build();

// compose healthcheck 依赖此端点。
app.MapGet("/health", () => Results.Json(new { status = "healthy", service = "InkFlow.Worker" }));

await app.RunAsync();

/// <summary>按能力分派的执行器组合根。</summary>
internal sealed class CompositeTaskExecutor(
    TocSyncTaskHandler tocHandler,
    ContentFetchTaskHandler contentHandler) : ICrawlerTaskExecutor
{
    public Task<CrawlOutcome> ExecuteAsync(CrawlerTask task, CancellationToken cancellationToken = default) =>
        task.Payload.Capability switch
        {
            SourceCapability.Toc => tocHandler.ExecuteAsync(task, cancellationToken),
            SourceCapability.Content => contentHandler.ExecuteAsync(task, cancellationToken),
            _ => Task.FromResult(CrawlOutcome.Fail($"capability {task.Payload.Capability} has no handler.")),
        };
}

/// <summary>
/// 轮询消费:领取可执行任务 → 执行 → 完成/失败落库。v1 串行消费,批量并发属后续优化。
/// </summary>
internal sealed class TaskPollingService(
    IServiceScopeFactory scopeFactory,
    CrawlerLeaseService leases) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var tasks = scope.ServiceProvider.GetRequiredService<ICrawlerTaskRepository>();

                var leasable = await tasks.FindLeasableAsync(DateTimeOffset.UtcNow, 1, stoppingToken)
                    .ConfigureAwait(false);
                foreach (var task in leasable)
                {
                    if (!leases.TryLease(task, "inkflow-worker"))
                    {
                        continue;
                    }

                    task.MarkRunning(DateTimeOffset.UtcNow);
                    await tasks.SaveAsync(task, stoppingToken).ConfigureAwait(false);

                    var executor = scope.ServiceProvider.GetRequiredService<CompositeTaskExecutor>();
                    var outcome = await executor.ExecuteAsync(task, stoppingToken).ConfigureAwait(false);

                    if (outcome.Succeeded)
                    {
                        task.Complete(DateTimeOffset.UtcNow);
                    }
                    else
                    {
                        Console.WriteLine($"task {task.Id} failed: {outcome.FailureReason}");
                        task.Fail(DateTimeOffset.UtcNow);
                        if (task.Status == CrawlerTaskStatus.DeadLettered)
                        {
                            await tasks.AddDeadLetterAsync(
                                DeadLetterTask.From(task, outcome.FailureReason ?? "unknown", DateTimeOffset.UtcNow),
                                stoppingToken).ConfigureAwait(false);
                        }
                    }

                    await tasks.SaveAsync(task, stoppingToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Console.WriteLine($"polling error: {ex.Message}");
            }

            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
    }
}
