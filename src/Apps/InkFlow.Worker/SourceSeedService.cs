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

            if (await sources.GetAsync(Linovelib.SourceId, stoppingToken).ConfigureAwait(false) is null)
            {
                var now = DateTimeOffset.UtcNow;
                var source = Source.Create(
                    Linovelib.SourceId, "轻小说文库(linovelib)",
                    Linovelib.BaseUrl, now);
                source.UpdateRuleDsl(Linovelib.BuildRuleDsl(), now);
                await sources.AddAsync(source, stoppingToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Console.WriteLine($"source seed error: {ex.Message}");
        }
    }

    internal static class Linovelib
    {
        public const string SourceId = "linovelib";
        public const string BaseUrl = "https://www.linovelib.com";

        public static SourceRuleDsl BuildRuleDsl() => new("1", SourceId,
        [
            new CapabilityRule(
                SourceCapability.BookInfo,
                RuleRequest.Get("/novel/{bookId}.html"),
                [
                    new RuleField("title",
                        new RuleSelector(SelectorKind.Css, "meta[property='og:novel:book_name']"),
                        null, [], Attribute: "content"),
                    new RuleField("author",
                        new RuleSelector(SelectorKind.Css, "meta[property='og:novel:author']"),
                        null, [], Attribute: "content"),
                ]),
            new CapabilityRule(
                SourceCapability.Toc,
                RuleRequest.Get("/novel/{bookId}/catalog"),
                [],
                List: new RuleListBinding(
                    ItemsSelector: "ul li a[href*='/novel/']",
                    ExternalIdAttribute: "href",
                    IdPrefixToStrip: string.Empty,
                    IdSuffixToStrip: ".html")),
            new CapabilityRule(
                SourceCapability.Content,
                RuleRequest.Get("/novel/{chapterId}.html"),
                [new RuleField("content", new RuleSelector(SelectorKind.Css, "p"), null, [])]),
        ]);
    }
}
