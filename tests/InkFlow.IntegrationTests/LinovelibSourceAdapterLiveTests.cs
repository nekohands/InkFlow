using InkFlow.BuildingBlocks.Security;
using InkFlow.Modules.Sources.Application;
using InkFlow.Modules.Sources.Domain;
using InkFlow.Modules.Sources.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.IntegrationTests;

/// <summary>
/// linovelib 规则型来源的真实服务端链路验证。
/// 默认不触网；设置 INKFLOW_LIVE_TESTS=1 后执行 Search → BookInfo → TOC → Content。
/// </summary>
[TestClass]
public sealed class LinovelibSourceAdapterLiveTests
{
    [TestMethod]
    public async Task Live_Search_BookInfo_Toc_Content_Can_Be_Fetched_Through_RuleAdapter()
    {
        if (Environment.GetEnvironmentVariable("INKFLOW_LIVE_TESTS") != "1")
        {
            Assert.Inconclusive("set INKFLOW_LIVE_TESTS=1 to run live verification");
        }

        var now = DateTimeOffset.UtcNow;
        var source = Source.Rehydrate(
            LinovelibSourceDefinition.SourceId,
            "轻小说文库",
            LinovelibSourceDefinition.BaseUrl,
            LinovelibSourceDefinition.BuildRuleDsl(),
            now,
            now);
        var resolver = new DnsIpAddressResolver();
        using var http = new HttpClient(new SsrfSafeHttpMessageHandler(resolver))
        {
            Timeout = TimeSpan.FromSeconds(30),
        };
        var sourceHttp = new ProductionSafeSourceHttpClient(http, resolver);
        var selector = new CssSelectorEvaluator();
        var adapter = new RuleBasedSourceAdapter(
            source,
            new RuleAdapter(sourceHttp, selector),
            selector);

        var results = await adapter.SearchAsync("恶魔高校");

        Assert.IsTrue(results.Count > 0, "真实搜索应至少返回一个可解析书目");
        var selected = results[0];
        Assert.IsFalse(string.IsNullOrWhiteSpace(selected.ExternalBookId));

        var info = await adapter.GetBookInfoAsync(selected.ExternalBookId);
        Assert.IsNotNull(info, "真实书页应能解析书目元数据");
        Assert.IsFalse(string.IsNullOrWhiteSpace(info.Title));

        var toc = await adapter.GetTableOfContentsAsync(selected.ExternalBookId);
        Assert.IsTrue(toc.Count > 0, "真实书页应能解析目录条目");

        var content = await adapter.GetChapterContentAsync(toc[0].ExternalChapterId);
        Assert.IsFalse(string.IsNullOrWhiteSpace(content), "真实章节页应能解析正文");
        Assert.IsTrue(content.Length > 200, $"正文长度异常: {content.Length}");
    }
}
