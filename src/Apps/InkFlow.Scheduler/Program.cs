using InkFlow.BuildingBlocks.Application;
using InkFlow.BuildingBlocks.Observability;
using InkFlow.BuildingBlocks.Persistence;
using InkFlow.BuildingBlocks.Security;
// 定时调度宿主:周期性扫描已导入书目,为需要更新的书创建 Toc 同步任务。
using InkFlow.Modules.Crawling.Application;
using InkFlow.Modules.Crawling.Infrastructure.Persistence;
using InkFlow.Modules.Sources.Application;
using InkFlow.Modules.Sources.Domain;
using InkFlow.Modules.Sources.Infrastructure;
using InkFlow.Modules.Sources.Infrastructure.Persistence;
using InkFlow.Scheduler;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddInkFlowObservability("InkFlow.Scheduler");

var connectionString =
    builder.Configuration.GetConnectionString("Database")
    ?? "Host=localhost;Port=5432;Database=inkflow;Username=inkflow;Password=inkflow";

builder.Services.AddDbContext<SourcesDbContext>(o => o.UseNpgsql(connectionString));
builder.Services.AddDbContext<CrawlingDbContext>(o => o.UseNpgsql(connectionString));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ISourceCredentialProvider, ConfigurationSourceCredentialProvider>();
builder.Services.AddScoped<ITransactionalOutboxWriter, EfTransactionalOutboxWriter>();
var sourceHealthOptions = SourceHealthOptions.FromConfiguration(builder.Configuration);
SourceHealthPolicy.Configure(sourceHealthOptions.ToParameters());
builder.Services.AddSingleton(SourceRuleExecutionLimits.Default);
builder.Services.AddScoped<ICrawlerTaskRepository, EfCrawlerTaskRepository>();
builder.Services.AddScoped<ISourceBookRepository, EfSourceBookRepository>();
builder.Services.AddScoped<ISourceHealthRepository, EfSourceHealthRepository>();
builder.Services.AddScoped<ISourceRepository, InkFlow.Modules.Sources.Infrastructure.Persistence.EfSourceRepository>();
builder.Services.AddScoped<SourceHealthService>();
builder.Services.AddScoped<ISourceHealthReader>(sp =>
    sp.GetRequiredService<SourceHealthService>());
builder.Services.AddScoped<ISourceHealthRecorder>(sp =>
    sp.GetRequiredService<SourceHealthService>());
builder.Services.AddScoped<UpdateScanService>();
builder.Services.AddScoped<HealthProbeService>();
builder.Services.AddHttpClient<ISourceHttpClient, ProductionSafeSourceHttpClient>()
    .ConfigurePrimaryHttpMessageHandler(sp =>
        new SsrfSafeHttpMessageHandler(sp.GetRequiredService<IIpAddressResolver>()));
builder.Services.AddHttpClient<InkFlow.Sources.Adapters.Kanunu8.KanunuSourceAdapter>()
    .ConfigurePrimaryHttpMessageHandler(sp =>
        new SsrfSafeHttpMessageHandler(sp.GetRequiredService<IIpAddressResolver>()));
builder.Services.AddHttpClient<InkFlow.Sources.Adapters.SeventeenK.SeventeenKSourceAdapter>()
    .ConfigureHttpClient(client => client.Timeout = TimeSpan.FromSeconds(20))
    .ConfigurePrimaryHttpMessageHandler(sp =>
        new SsrfSafeHttpMessageHandler(sp.GetRequiredService<IIpAddressResolver>()));
builder.Services.AddSingleton<IIpAddressResolver, DnsIpAddressResolver>();
builder.Services.AddScoped<ISelectorEvaluator, InkFlow.Modules.Sources.Infrastructure.RuleSelectorEvaluator>();
builder.Services.AddScoped<RuleAdapter>();
builder.Services.AddScoped<ISourceAdapterFactory>(sp => new SourceAdapterFactory(
    sp.GetRequiredService<ISourceRepository>(),
    sp.GetRequiredService<RuleAdapter>(),
    sp.GetRequiredService<ISelectorEvaluator>(),
    [
        sp.GetRequiredService<InkFlow.Sources.Adapters.Kanunu8.KanunuSourceAdapter>(),
        sp.GetRequiredService<InkFlow.Sources.Adapters.SeventeenK.SeventeenKSourceAdapter>(),
    ]));
builder.Services.AddHostedService<UpdateScanBackgroundService>();
builder.Services.AddHostedService<HealthProbeBackgroundService>();

var app = builder.Build();

// compose healthcheck 依赖此端点。
app.MapGet("/health", () => Results.Json(new { status = "healthy", service = "InkFlow.Scheduler" }));

await app.RunAsync();
