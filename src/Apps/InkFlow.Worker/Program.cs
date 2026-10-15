// 后台任务宿主:轮询 crawler.tasks 队列,按能力分派给对应处理器。
// 普通阅读路径不经过这里——Worker 只负责"检测更新 → 抓取 → 落库"的写侧链路。
using InkFlow.BuildingBlocks.Application;
using InkFlow.BuildingBlocks.Messaging;
using InkFlow.BuildingBlocks.Observability;
using InkFlow.BuildingBlocks.Persistence;
using InkFlow.BuildingBlocks.Security;
using InkFlow.Modules.Content.Application;
using InkFlow.Modules.Content.Domain;
using InkFlow.Modules.Content.Infrastructure.Persistence;
using InkFlow.Modules.Crawling.Application;
using InkFlow.Modules.Crawling.Domain;
using InkFlow.Modules.Crawling.Infrastructure.Persistence;
using InkFlow.Modules.Identity.Application;
using InkFlow.Modules.Identity.Infrastructure.Persistence;
using InkFlow.Modules.Library.Application;
using InkFlow.Modules.Library.Infrastructure.Persistence;
using InkFlow.Modules.Sources.Application;
using InkFlow.Modules.Sources.Domain;
using InkFlow.Modules.Sources.Infrastructure;
using InkFlow.Modules.Sources.Infrastructure.Persistence;
using InkFlow.Sources.Adapters.Kanunu8;
using InkFlow.Sources.Adapters.Seeding;
using InkFlow.Sources.Adapters.SeventeenK;
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
builder.Services.AddDbContext<IdentityDbContext>(o => o.UseNpgsql(connectionString));

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
builder.Services.AddSingleton(
    IdentityRetentionOptions.FromConfiguration(builder.Configuration));
builder.Services.AddScoped<EfIdentityRetentionStore>();
builder.Services.AddScoped<IIdentityRetentionStore>(sp =>
    sp.GetRequiredService<EfIdentityRetentionStore>());
builder.Services.AddScoped<IIdentityRetentionService, IdentityRetentionService>();
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
builder.Services.AddHostedService<IdentityRetentionBackgroundService>();


var app = builder.Build();

// compose healthcheck 依赖此端点。
app.MapGet("/health", () => Results.Json(new { status = "healthy", service = "InkFlow.Worker" }));

await app.RunAsync();
