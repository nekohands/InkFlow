// 定时调度宿主:周期性扫描已导入书目,为需要更新的书创建 Toc 同步任务。
using InkFlow.Modules.Crawling.Application;
using InkFlow.Modules.Crawling.Infrastructure.Persistence;
using InkFlow.Modules.Sources.Application;
using InkFlow.Modules.Sources.Domain;
using InkFlow.Modules.Sources.Infrastructure;
using InkFlow.Modules.Sources.Infrastructure.Persistence;
using InkFlow.BuildingBlocks.Application;
using InkFlow.BuildingBlocks.Security;
using InkFlow.BuildingBlocks.Observability;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddInkFlowObservability("InkFlow.Scheduler");

var connectionString =
    builder.Configuration.GetConnectionString("Database")
    ?? "Host=localhost;Port=5432;Database=inkflow;Username=inkflow;Password=inkflow";

builder.Services.AddDbContext<SourcesDbContext>(o => o.UseNpgsql(connectionString));
builder.Services.AddDbContext<CrawlingDbContext>(o => o.UseNpgsql(connectionString));
builder.Services.AddSingleton(TimeProvider.System);
var sourceHealthOptions = SourceHealthOptions.FromConfiguration(builder.Configuration);
SourceHealthPolicy.Configure(sourceHealthOptions.ToParameters());
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
builder.Services.AddSingleton<IIpAddressResolver, DnsIpAddressResolver>();
builder.Services.AddScoped<ISelectorEvaluator, InkFlow.Modules.Sources.Infrastructure.CssSelectorEvaluator>();
builder.Services.AddScoped<RuleAdapter>();
builder.Services.AddScoped<ISourceAdapterFactory>(sp => new SourceAdapterFactory(
    sp.GetRequiredService<ISourceRepository>(),
    sp.GetRequiredService<RuleAdapter>(),
    sp.GetRequiredService<ISelectorEvaluator>(),
    [sp.GetRequiredService<InkFlow.Sources.Adapters.Kanunu8.KanunuSourceAdapter>()]));
builder.Services.AddHostedService<UpdateScanBackgroundService>();
builder.Services.AddHostedService<HealthProbeBackgroundService>();

var app = builder.Build();

// compose healthcheck 依赖此端点。
app.MapGet("/health", () => Results.Json(new { status = "healthy", service = "InkFlow.Scheduler" }));

await app.RunAsync();

/// <summary>
/// 追更扫描后台服务:按固定间隔为已导入书目入队 Toc 同步任务(带活跃任务去重)。
/// Worker 消费任务完成"检测新章 → 抓取 → 落库 → 映射",全程无需人工干预。
/// </summary>
internal sealed class UpdateScanBackgroundService(
    IServiceScopeFactory scopeFactory) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 启动后先等一个间隔,避免与迁移/其他服务抢启动窗口。
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var scanner = ActivatorUtilities.CreateInstance<UpdateScanService>(scope.ServiceProvider);
                var count = await scanner.EnqueueTocScansAsync(stoppingToken).ConfigureAwait(false);
                Console.WriteLine($"update scan enqueued {count} toc task(s).");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Console.WriteLine($"update scan error: {ex.Message}");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }
}

/// <summary>
/// 健康巡检后台服务:周期性对冷却期已满的 Unhealthy 能力主动发起轻量探针
/// (Search/Toc),成败经既有健康上报裁定,让无自然流量的来源也能自动恢复。
/// </summary>
internal sealed class HealthProbeBackgroundService(
    IServiceScopeFactory scopeFactory) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var prober = scope.ServiceProvider.GetRequiredService<HealthProbeService>();
                var results = await prober.ProbeDueAsync(stoppingToken).ConfigureAwait(false);
                foreach (var result in results)
                {
                    Console.WriteLine(
                        $"health probe {result.SourceId}/{result.Capability}: " +
                        (result.Recovered ? "recovered." : $"still failing ({result.FailureReason})."));
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Console.WriteLine($"health probe error: {ex.Message}");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }
}
