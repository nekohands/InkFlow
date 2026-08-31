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
var workerOwner = relayOptions.CreateOwner(Environment.MachineName);
builder.Services.AddSingleton(relayOptions);
builder.Services.AddSingleton(relayOptions.CreateDispatcherOptions(
    workerOwner));
builder.Services.AddSingleton(
    InboxConsumerOptions.FromConfiguration(builder.Configuration, workerOwner));
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
builder.Services.AddScoped<IntegrationMessageHandlerRegistry>();
builder.Services.AddScoped<IIntegrationMessageHandlerResolver>(sp =>
    sp.GetRequiredService<IntegrationMessageHandlerRegistry>());
builder.Services.AddScoped<IIntegrationMessageConsumer, IntegrationMessageConsumer>();
builder.Services.AddScoped<IInboxConsumerPump, InboxConsumerPump>();
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
builder.Services.AddScoped<ICollectionRunRepository, EfCollectionRunRepository>();
builder.Services.AddScoped<ISourceRepository, EfSourceRepository>();
builder.Services.AddScoped<ISourceBookRepository, EfSourceBookRepository>();
builder.Services.AddScoped<IFetchArtifactRepository, EfFetchArtifactRepository>();
builder.Services.AddScoped<ISourceHealthRepository, EfSourceHealthRepository>();
builder.Services.AddScoped<SourceHealthService>();
builder.Services.AddScoped<ISourceHealthReader>(sp =>
    sp.GetRequiredService<SourceHealthService>());
builder.Services.AddScoped<ISourceHealthRecorder>(sp =>
    sp.GetRequiredService<SourceHealthService>());
builder.Services.AddScoped<ICanonicalBookRepository, EfCanonicalBookRepository>();
builder.Services.AddSingleton<RetryPolicy>();
builder.Services.AddSingleton<ICrawlerFailureSink, LoggingCrawlerFailureSink>();
builder.Services.AddSingleton<ICrawlerFailureSink, OpenTelemetryCrawlerFailureSink>();
builder.Services.AddSingleton<CrawlerFailureReporter>();
builder.Services.AddScoped<IMatchCandidateRepository, EfMatchCandidateRepository>();
builder.Services.AddScoped<IChapterMappingRepository, EfChapterMappingRepository>();
builder.Services.AddScoped<IContentVersionRepository, EfContentVersionRepository>();
builder.Services.AddScoped<IContentPolicyRepository, EfContentPolicyRepository>();
builder.Services.AddScoped<ContentPolicyService>();
builder.Services.AddScoped<IContentPolicyReader>(sp =>
    sp.GetRequiredService<ContentPolicyService>());
builder.Services.AddScoped<IContentSelectionDecisionRepository, EfContentSelectionDecisionRepository>();
builder.Services.AddScoped<IContentSelectionService, ContentSelectionService>();
builder.Services.AddScoped<ContentPublishingService>();
builder.Services.AddSingleton(BookPackageOptions.FromEnvironment());
builder.Services.AddSingleton<IBookPackageBuilder, BookPackageBuilder>();
builder.Services.AddSingleton<IBookPackageArtifactStore, FileBookPackageArtifactStore>();
builder.Services.AddScoped<IBookPackageJobRepository, EfBookPackageJobRepository>();
builder.Services.AddScoped<BookPackageService>();

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
builder.Services.AddScoped<CanonicalBookMatchingService>();
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
builder.Services.AddScoped<SourceBookUrlResolver>();
builder.Services.AddScoped<CollectionRunService>();
builder.Services.AddScoped<BookInfoSyncTaskHandler>();
builder.Services.AddScoped<TocSyncTaskHandler>();
builder.Services.AddScoped<ContentFetchTaskHandler>();
builder.Services.AddScoped<ContentFetchChainService>();
builder.Services.AddScoped<IChainedContentPublisher, MappingContentPublisher>();
builder.Services.AddScoped<CompositeTaskExecutor>();
builder.Services.AddScoped<ICrawlerTaskExecutor>(sp =>
    sp.GetRequiredService<CompositeTaskExecutor>());
builder.Services.AddScoped<ICrawlerTaskProcessor, CrawlerTaskProcessor>();
builder.Services.AddScoped<IIntegrationMessageHandler, CrawlerTaskCreatedMessageHandler>();
builder.Services.AddHostedService<TaskPollingService>();
builder.Services.AddHostedService<BookPackagePollingService>();
builder.Services.AddHostedService<SourceSeedService>();
builder.Services.AddHostedService<OutboxRelayBackgroundService>();
builder.Services.AddHostedService<InboxConsumerBackgroundService>();
builder.Services.AddHostedService<MessageRetentionBackgroundService>();
builder.Services.AddHostedService<AuditRetentionBackgroundService>();


var app = builder.Build();

// compose healthcheck 依赖此端点。
app.MapGet("/health", () => Results.Json(new { status = "healthy", service = "InkFlow.Worker" }));

await app.RunAsync();

/// <summary>按能力分派的执行器组合根。</summary>
internal sealed class CompositeTaskExecutor(
    BookInfoSyncTaskHandler bookInfoHandler,
    TocSyncTaskHandler tocHandler,
    ContentFetchTaskHandler contentHandler) : ICrawlerTaskExecutor
{
    public Task<CrawlOutcome> ExecuteAsync(CrawlerTask task, CancellationToken cancellationToken = default) =>
        task.Payload.Capability switch
        {
            SourceCapability.BookInfo => bookInfoHandler.ExecuteAsync(task, cancellationToken),
            SourceCapability.Toc => tocHandler.ExecuteAsync(task, cancellationToken),
            SourceCapability.Content => contentHandler.ExecuteAsync(task, cancellationToken),
            _ => Task.FromResult(CrawlOutcome.Fail($"capability {task.Payload.Capability} has no handler.")),
        };
}

/// <summary>
/// 书籍包后台消费：按数据库租约串行生成，文件保留由同一循环定期清理。
/// </summary>
internal sealed class BookPackagePollingService(
    IServiceScopeFactory scopeFactory,
    TimeProvider clock,
    BookPackageOptions options) : BackgroundService
{
    private readonly string _owner = $"package-worker-{Environment.MachineName}"[..Math.Min(
        128,
        $"package-worker-{Environment.MachineName}".Length)];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var packages = scope.ServiceProvider.GetRequiredService<IBookPackageJobRepository>();
                var service = scope.ServiceProvider.GetRequiredService<BookPackageService>();
                var now = clock.GetUtcNow();
                var job = await packages
                    .TryLeaseAsync(now, _owner, options.LeaseDuration, stoppingToken)
                    .ConfigureAwait(false);
                if (job is not null)
                {
                    await service.ProcessAsync(job, stoppingToken).ConfigureAwait(false);
                    continue;
                }

                await service.ExpireOldAsync(stoppingToken).ConfigureAwait(false);
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception)
            {
                Console.Error.WriteLine("book package worker iteration failed.");
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }
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
    TimeProvider clock) : BackgroundService
{
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
                        CrawlerTaskExecutionDefaults.Owner,
                        CrawlerTaskExecutionDefaults.LeaseDuration,
                        stoppingToken)
                    .ConfigureAwait(false);
                if (task is not null)
                {
                    var processor = scope.ServiceProvider
                        .GetRequiredService<ICrawlerTaskProcessor>();
                    await processor
                        .ProcessAsync(task, stoppingToken)
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
                Console.WriteLine("polling failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
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
