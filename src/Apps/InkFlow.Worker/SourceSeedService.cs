using InkFlow.Modules.Crawling.Domain;
using InkFlow.Modules.Sources.Application;
using InkFlow.Modules.Sources.Domain;
using InkFlow.Sources.Adapters.Kanunu8;
using InkFlow.Sources.Adapters.SeventeenK;

namespace InkFlow.Sources.Adapters.Seeding;

/// <summary>
/// 内置 Official Source 种子:启动时确保规则型和代码型来源的 Source 记录已登记。
/// 新增规则型书源 = 在此添加一条种子定义(纯配置,零代码);复杂站点则实现
/// ISourceAdapter 作为独立插件项目接入。已有记录不会被种子覆盖。
/// </summary>
internal sealed class SourceSeedService(IServiceScopeFactory scopeFactory) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var sources = scope.ServiceProvider.GetRequiredService<ISourceRepository>();

            foreach (var seed in CreateSeeds())
            {
                if (await sources.GetAsync(seed.Id, stoppingToken).ConfigureAwait(false) is not null)
                {
                    continue;
                }

                var now = DateTimeOffset.UtcNow;
                var source = Source.Create(seed.Id, seed.DisplayName, seed.BaseUrl, now);
                if (seed.RuleDsl is not null)
                {
                    source.UpdateRuleDsl(seed.RuleDsl, now);
                }

                await sources.AddAsync(source, stoppingToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Console.WriteLine("source seed failed.");
        }
    }

    private static IReadOnlyList<SourceSeed> CreateSeeds() =>
    [
        new(
            LinovelibSourceDefinition.SourceId,
            "轻小说文库(linovelib)",
            LinovelibSourceDefinition.BaseUrl,
            LinovelibSourceDefinition.BuildRuleDsl()),
        new(
            KanunuSourceAdapter.SourceIdValue,
            KanunuSourceAdapter.DisplayNameValue,
            KanunuSourceAdapter.BaseUrlValue,
            null),
        new(
            SeventeenKSourceAdapter.SourceIdValue,
            SeventeenKSourceAdapter.DisplayNameValue,
            SeventeenKSourceAdapter.BaseUrlValue,
            null),
    ];

    private sealed record SourceSeed(
        string Id,
        string DisplayName,
        string BaseUrl,
        SourceRuleDsl? RuleDsl);
}
