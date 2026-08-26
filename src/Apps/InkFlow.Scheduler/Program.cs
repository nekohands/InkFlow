// 定时调度宿主:周期性扫描已导入书目,为需要更新的书创建 Toc 同步任务。
using InkFlow.Modules.Crawling.Application;
using InkFlow.Modules.Crawling.Infrastructure.Persistence;
using InkFlow.Modules.Sources.Application;
using InkFlow.Modules.Sources.Domain;
using InkFlow.Modules.Sources.Infrastructure.Persistence;
using InkFlow.BuildingBlocks.Security;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString =
    builder.Configuration.GetConnectionString("Database")
    ?? "Host=localhost;Port=5432;Database=inkflow;Username=inkflow;Password=inkflow";

builder.Services.AddDbContext<SourcesDbContext>(o => o.UseNpgsql(connectionString));
builder.Services.AddDbContext<CrawlingDbContext>(o => o.UseNpgsql(connectionString));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ICrawlerTaskRepository, EfCrawlerTaskRepository>();
builder.Services.AddScoped<ISourceBookRepository, EfSourceBookRepository>();
builder.Services.AddScoped<UpdateScanService>();
builder.Services.AddHostedService<UpdateScanBackgroundService>();

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
