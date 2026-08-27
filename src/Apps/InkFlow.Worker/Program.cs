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
using InkFlow.Sources.Adapters.Kanunu8;
using InkFlow.Sources.Adapters.Seeding;
using InkFlow.Modules.Sources.Infrastructure;
using InkFlow.Modules.Sources.Infrastructure.Persistence;
using InkFlow.Modules.Library.Infrastructure.Persistence;
using InkFlow.Modules.Content.Infrastructure.Persistence;
using InkFlow.BuildingBlocks.Security;
using InkFlow.BuildingBlocks.Observability;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddInkFlowObservability("InkFlow.Worker");

// 支持 GB2312/GBK 等老站点编码(书源兼容层)。
System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

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
builder.Services.AddScoped<IFetchArtifactRepository, EfFetchArtifactRepository>();
builder.Services.AddScoped<ISourceHealthRepository, EfSourceHealthRepository>();
builder.Services.AddScoped<SourceHealthService>();
builder.Services.AddScoped<ISourceHealthReader>(sp =>
    sp.GetRequiredService<SourceHealthService>());
builder.Services.AddScoped<ISourceHealthRecorder>(sp =>
    sp.GetRequiredService<SourceHealthService>());
builder.Services.AddSingleton<RetryPolicy>();
builder.Services.AddScoped<IMatchCandidateRepository, EfMatchCandidateRepository>();
builder.Services.AddScoped<IChapterMappingRepository, EfChapterMappingRepository>();
builder.Services.AddScoped<IContentVersionRepository, EfContentVersionRepository>();
builder.Services.AddScoped<IContentSelectionDecisionRepository, EfContentSelectionDecisionRepository>();
builder.Services.AddScoped<IContentSelectionService, ContentSelectionService>();
builder.Services.AddScoped<ContentPublishingService>();

builder.Services.AddHttpClient<ISourceHttpClient, ProductionSafeSourceHttpClient>();
builder.Services.AddHttpClient<KanunuSourceAdapter>();
builder.Services.AddScoped<ISelectorEvaluator, CssSelectorEvaluator>();
builder.Services.AddScoped<RuleAdapter>();
builder.Services.AddScoped<SourceCatalogService>();
builder.Services.AddScoped<CanonicalChapterMappingService>();
builder.Services.AddScoped<SourceContentService>();
builder.Services.AddScoped<ISourceAdapterFactory>(sp => new SourceAdapterFactory(
    sp.GetRequiredService<ISourceRepository>(),
    sp.GetRequiredService<RuleAdapter>(),
    sp.GetRequiredService<ISelectorEvaluator>(),
    [sp.GetRequiredService<KanunuSourceAdapter>()]));
builder.Services.AddScoped<TocSyncTaskHandler>();
builder.Services.AddScoped<ContentFetchTaskHandler>();
builder.Services.AddScoped<ContentFetchChainService>();
builder.Services.AddScoped<CompositeTaskExecutor>();
builder.Services.AddHostedService<TaskPollingService>();
builder.Services.AddHostedService<SourceSeedService>();


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
    TimeProvider clock,
    RetryPolicy retryPolicy) : BackgroundService
{
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var tasks = scope.ServiceProvider.GetRequiredService<ICrawlerTaskRepository>();

                var task = await tasks.TryLeaseAsync(
                        clock.GetUtcNow(),
                        "inkflow-worker",
                        LeaseDuration,
                        stoppingToken)
                    .ConfigureAwait(false);
                if (task is not null)
                {
                    await ProcessTaskAsync(task, tasks, scope.ServiceProvider, stoppingToken)
                        .ConfigureAwait(false);

                    // 追更联动一次可能入队整批正文任务;有活时短轮询尽快消化,
                    // 空闲时才回到低成本长等待。
                    await Task.Delay(TimeSpan.FromMilliseconds(250), stoppingToken)
                        .ConfigureAwait(false);
                    continue;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Console.WriteLine($"polling error: {ex.Message}");
            }

            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
    }

    private async Task ProcessTaskAsync(
        CrawlerTask task,
        ICrawlerTaskRepository tasks,
        IServiceProvider services,
        CancellationToken stoppingToken)
    {
        try
        {
            task.MarkRunning(clock.GetUtcNow());
            await tasks.SaveAsync(task, stoppingToken).ConfigureAwait(false);

            var executor = services.GetRequiredService<CompositeTaskExecutor>();
            var outcome = await executor.ExecuteAsync(task, stoppingToken).ConfigureAwait(false);
            if (outcome.Succeeded)
            {
                task.Complete(clock.GetUtcNow());
                await tasks.SaveAsync(task, stoppingToken).ConfigureAwait(false);
                return;
            }

            await FailTaskAsync(
                task,
                tasks,
                outcome.FailureReason ?? "unknown",
                stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await FailTaskAsync(task, tasks, exception.Message, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task FailTaskAsync(
        CrawlerTask task,
        ICrawlerTaskRepository tasks,
        string reason,
        CancellationToken stoppingToken)
    {
        Console.WriteLine($"task {task.Id} failed: {reason}");
        if (task.Status != CrawlerTaskStatus.Running)
        {
            return;
        }

        var now = clock.GetUtcNow();
        DateTimeOffset? nextAttemptAt = task.AttemptCount < task.MaxAttempts
            ? now + retryPolicy.DelayFor(task.AttemptCount)
            : null;
        task.Fail(now, nextAttemptAt);
        if (task.Status == CrawlerTaskStatus.DeadLettered)
        {
            await tasks.AddDeadLetterAsync(
                DeadLetterTask.From(task, reason, now),
                stoppingToken).ConfigureAwait(false);
        }

        await tasks.SaveAsync(task, stoppingToken).ConfigureAwait(false);
    }
}
