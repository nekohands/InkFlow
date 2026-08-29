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
using InkFlow.Sources.Adapters.SeventeenK;
using InkFlow.Sources.Adapters.Seeding;
using InkFlow.Modules.Sources.Infrastructure;
using InkFlow.Modules.Sources.Infrastructure.Persistence;
using InkFlow.Modules.Library.Infrastructure.Persistence;
using InkFlow.Modules.Content.Infrastructure.Persistence;
using InkFlow.BuildingBlocks.Application;
using InkFlow.BuildingBlocks.Messaging;
using InkFlow.BuildingBlocks.Persistence;
using InkFlow.BuildingBlocks.Security;
using InkFlow.BuildingBlocks.Observability;
using InkFlow.Worker;
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
builder.Services.AddDbContext<MessagingDbContext>(o => o.UseNpgsql(connectionString));
builder.Services.AddDbContext<AuditDbContext>(o => o.UseNpgsql(connectionString));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(
    MessageRetentionOptions.FromConfiguration(builder.Configuration));
builder.Services.AddSingleton(
    AuditRetentionOptions.FromConfiguration(builder.Configuration));
var relayOptions = OutboxRelayOptions.FromConfiguration(builder.Configuration);
builder.Services.AddSingleton(relayOptions);
builder.Services.AddSingleton(relayOptions.CreateDispatcherOptions(
    relayOptions.CreateOwner(Environment.MachineName)));
builder.Services.AddScoped<EfMessagingMessageStore>();
builder.Services.AddScoped<IOutboxStore>(sp => sp.GetRequiredService<EfMessagingMessageStore>());
builder.Services.AddScoped<IInboxStore>(sp => sp.GetRequiredService<EfMessagingMessageStore>());
builder.Services.AddScoped<IInboxTransportStore>(sp =>
    sp.GetRequiredService<EfMessagingMessageStore>());
builder.Services.AddScoped<IMessageRetentionStore>(sp =>
    sp.GetRequiredService<EfMessagingMessageStore>());
builder.Services.AddScoped<IMessageRetentionService, MessageRetentionService>();
builder.Services.AddScoped<IIntegrationMessagePublisher, PostgreSqlInboxMessagePublisher>();
builder.Services.AddScoped<IOutboxDispatcher, OutboxDispatcher>();
builder.Services.AddScoped<EfAuditRetentionStore>();
builder.Services.AddScoped<IAuditRetentionStore>(sp =>
    sp.GetRequiredService<EfAuditRetentionStore>());
builder.Services.AddScoped<IAuditRetentionService, AuditRetentionService>();
builder.Services.AddScoped<ITransactionalOutboxWriter, EfTransactionalOutboxWriter>();
var sourceHealthOptions = SourceHealthOptions.FromConfiguration(builder.Configuration);
SourceHealthPolicy.Configure(sourceHealthOptions.ToParameters());
builder.Services.AddSingleton(SourceRuleExecutionLimits.Default);
builder.Services.AddSingleton<IIpAddressResolver, DnsIpAddressResolver>();
builder.Services.AddSingleton<ISourceCredentialProvider, ConfigurationSourceCredentialProvider>();
builder.Services.AddScoped<EfCrawlerTaskRepository>();
builder.Services.AddScoped<ICrawlerTaskRepository>(sp =>
    sp.GetRequiredService<EfCrawlerTaskRepository>());
builder.Services.AddScoped<ICrawlerTaskRepairRepository>(sp =>
    sp.GetRequiredService<EfCrawlerTaskRepository>());
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
builder.Services.AddSingleton<ICrawlerFailureSink, LoggingCrawlerFailureSink>();
builder.Services.AddSingleton<ICrawlerFailureSink, OpenTelemetryCrawlerFailureSink>();
builder.Services.AddSingleton<CrawlerFailureReporter>();
builder.Services.AddScoped<IMatchCandidateRepository, EfMatchCandidateRepository>();
builder.Services.AddScoped<IChapterMappingRepository, EfChapterMappingRepository>();
builder.Services.AddScoped<IContentVersionRepository, EfContentVersionRepository>();
builder.Services.AddScoped<IContentSelectionDecisionRepository, EfContentSelectionDecisionRepository>();
builder.Services.AddScoped<IContentSelectionService, ContentSelectionService>();
builder.Services.AddScoped<ContentPublishingService>();

builder.Services.AddHttpClient<ISourceHttpClient, ProductionSafeSourceHttpClient>()
    .ConfigurePrimaryHttpMessageHandler(sp =>
        new SsrfSafeHttpMessageHandler(sp.GetRequiredService<IIpAddressResolver>()));
builder.Services.AddHttpClient<KanunuSourceAdapter>()
    .ConfigurePrimaryHttpMessageHandler(sp =>
        new SsrfSafeHttpMessageHandler(sp.GetRequiredService<IIpAddressResolver>()));
builder.Services.AddHttpClient<SeventeenKSourceAdapter>()
    .ConfigureHttpClient(client => client.Timeout = TimeSpan.FromSeconds(20))
    .ConfigurePrimaryHttpMessageHandler(sp =>
        new SsrfSafeHttpMessageHandler(sp.GetRequiredService<IIpAddressResolver>()));
builder.Services.AddScoped<ISelectorEvaluator, RuleSelectorEvaluator>();
builder.Services.AddScoped<RuleAdapter>();
builder.Services.AddScoped<SourceCatalogService>();
builder.Services.AddScoped<CanonicalChapterMappingService>();
builder.Services.AddScoped<SourceContentService>();
builder.Services.AddScoped<ISourceAdapterFactory>(sp => new SourceAdapterFactory(
    sp.GetRequiredService<ISourceRepository>(),
    sp.GetRequiredService<RuleAdapter>(),
    sp.GetRequiredService<ISelectorEvaluator>(),
    [
        sp.GetRequiredService<KanunuSourceAdapter>(),
        sp.GetRequiredService<SeventeenKSourceAdapter>(),
    ]));
builder.Services.AddScoped<TocSyncTaskHandler>();
builder.Services.AddScoped<ContentFetchTaskHandler>();
builder.Services.AddScoped<ContentFetchChainService>();
builder.Services.AddScoped<IChainedContentPublisher, MappingContentPublisher>();
builder.Services.AddScoped<CompositeTaskExecutor>();
builder.Services.AddHostedService<TaskPollingService>();
builder.Services.AddHostedService<SourceSeedService>();
builder.Services.AddHostedService<OutboxRelayBackgroundService>();
builder.Services.AddHostedService<MessageRetentionBackgroundService>();
builder.Services.AddHostedService<AuditRetentionBackgroundService>();


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
/// 抓取 → 发布桥的宿主实现:经章节映射找到正典身份后调用内容发布服务。
/// 幂等性由 ContentPublishingService 的 CanonicalHash 判重保证;
/// 未映射返回 false(不算失败),基础设施/发布异常向上传播走任务重试。
/// </summary>
internal sealed class MappingContentPublisher(
    IChapterMappingRepository chapterMappings,
    ContentPublishingService publisher) : IChainedContentPublisher
{
    public async Task<bool> TryPublishAsync(
        string sourceId,
        string externalBookId,
        string externalChapterId,
        string rawContent,
        CancellationToken cancellationToken = default)
    {
        var mapping = await chapterMappings
            .FindAsync(sourceId, externalChapterId, cancellationToken)
            .ConfigureAwait(false);

        if (mapping is null)
        {
            return false;
        }

        var outcome = await publisher
            .PublishAsync(mapping.CanonicalBookId, mapping.CanonicalChapterId, sourceId, rawContent, cancellationToken)
            .ConfigureAwait(false);

        if (!outcome.IsSuccess)
        {
            throw new InvalidOperationException(
                $"publishing '{sourceId}/{externalChapterId}' failed: {string.Join("; ", outcome.Errors)}");
        }

        return true;
    }
}

/// <summary>
/// 轮询消费:领取可执行任务 → 执行 → 完成/失败落库。v1 串行消费,批量并发属后续优化。
/// </summary>
internal sealed class TaskPollingService(
    IServiceScopeFactory scopeFactory,
    TimeProvider clock,
    RetryPolicy retryPolicy,
    CrawlerFailureReporter failureReporter) : BackgroundService
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
        var now = clock.GetUtcNow();
        if (task.Status != CrawlerTaskStatus.Running)
        {
            failureReporter.Report(CrawlerFailureObservation.Create(
                task.Id,
                task.Payload.SourceId,
                task.Payload.Capability.ToString(),
                task.AttemptCount,
                task.MaxAttempts,
                CrawlerFailureDisposition.NotRunning,
                reason,
                now));
            return;
        }

        DateTimeOffset? nextAttemptAt = task.AttemptCount < task.MaxAttempts
            ? now + retryPolicy.DelayFor(task.AttemptCount)
            : null;
        task.Fail(now, nextAttemptAt);
        failureReporter.Report(CrawlerFailureObservation.Create(
            task.Id,
            task.Payload.SourceId,
            task.Payload.Capability.ToString(),
            task.AttemptCount,
            task.MaxAttempts,
            task.Status == CrawlerTaskStatus.DeadLettered
                ? CrawlerFailureDisposition.DeadLetter
                : CrawlerFailureDisposition.Retry,
            reason,
            now));
        if (task.Status == CrawlerTaskStatus.DeadLettered)
        {
            await tasks.AddDeadLetterAsync(
                DeadLetterTask.From(task, reason, now),
                stoppingToken).ConfigureAwait(false);
        }

        await tasks.SaveAsync(task, stoppingToken).ConfigureAwait(false);
    }
}

/// <summary>
/// 周期性删除已成功处理且超过保留期的消息；失败/待重试消息不会被清理。
/// </summary>
internal sealed class MessageRetentionBackgroundService(
    IServiceScopeFactory scopeFactory,
    MessageRetentionOptions options,
    TimeProvider clock) : BackgroundService
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(InitialDelay, stoppingToken).ConfigureAwait(false);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var retention = scope.ServiceProvider
                    .GetRequiredService<IMessageRetentionService>();
                var result = await retention
                    .CleanupAsync(options, stoppingToken)
                    .ConfigureAwait(false);
                Console.WriteLine(
                    $"message retention cleanup at {clock.GetUtcNow():O}: " +
                    $"outbox={result.OutboxDeletedCount}, inbox={result.InboxDeletedCount}.");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                Console.WriteLine(
                    $"message retention cleanup failed: {exception.GetType().Name}.");
            }

            await Task.Delay(Interval, stoppingToken).ConfigureAwait(false);
        }
    }
}
