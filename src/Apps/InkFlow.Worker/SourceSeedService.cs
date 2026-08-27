using InkFlow.Modules.Crawling.Domain;
using InkFlow.Modules.Sources.Application;
using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Sources.Adapters.Seeding;

/// <summary>
/// 规则型来源种子:启动时确保规则型站点(如 linovelib)的 Source 记录已登记。
/// 新增规则型书源 = 在此添加一条种子定义(纯配置,零代码);复杂站点则实现
/// ISourceAdapter 作为独立插件项目接入。
/// </summary>
internal sealed class SourceSeedService(IServiceScopeFactory scopeFactory) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var sources = scope.ServiceProvider.GetRequiredService<ISourceRepository>();

            if (await sources.GetAsync(LinovelibSourceDefinition.SourceId, stoppingToken).ConfigureAwait(false) is null)
            {
                var now = DateTimeOffset.UtcNow;
                var source = Source.Create(
                    LinovelibSourceDefinition.SourceId, "轻小说文库(linovelib)",
                    LinovelibSourceDefinition.BaseUrl, now);
                source.UpdateRuleDsl(LinovelibSourceDefinition.BuildRuleDsl(), now);
                await sources.AddAsync(source, stoppingToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Console.WriteLine($"source seed error: {ex.Message}");
        }
    }
}
