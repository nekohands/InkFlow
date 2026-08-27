using System.Text;
using InkFlow.BuildingBlocks.Security;
using InkFlow.Sources.Adapters.Kanunu8;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.IntegrationTests;

/// <summary>
/// 努努书坊(kanunu8.com)真实网络验证。
/// 默认跳过(Inconclusive);设置环境变量 INKFLOW_LIVE_TESTS=1 后执行真实抓取。
/// 用途:Phase 1A "接入真实 Official Source" 验收证据采集。
/// </summary>
[TestClass]
public sealed class KanunuSourceAdapterLiveTests
{
    private static (KanunuSourceAdapter Adapter, HttpClient Client) CreateLiveAdapter()
    {
        Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        var resolver = new DnsIpAddressResolver();
        var client = new HttpClient(new SsrfSafeHttpMessageHandler(resolver));
        return (new KanunuSourceAdapter(client, resolver), client);
    }

    [TestMethod]
    public async Task Live_BookInfo_Can_Be_Fetched_From_Kanunu8()
    {
        if (Environment.GetEnvironmentVariable("INKFLOW_LIVE_TESTS") != "1")
        {
            return; // live tests opt-in only (INKFLOW_LIVE_TESTS=1)
        }

        var (adapter, client) = CreateLiveAdapter();
        using (client)
        {
            var info = await adapter.GetBookInfoAsync("book/3441");

            Assert.IsNotNull(info, "真实书页应能解析出书目元数据");
            Assert.IsFalse(string.IsNullOrWhiteSpace(info.Title));
            Console.WriteLine($"live book info: title={info.Title}, author={info.Author}");
        }
    }

    [TestMethod]
    public async Task Live_Toc_Can_Be_Fetched_From_Kanunu8()
    {
        if (Environment.GetEnvironmentVariable("INKFLOW_LIVE_TESTS") != "1")
        {
            return; // live tests opt-in only (INKFLOW_LIVE_TESTS=1)
        }

        var (adapter, client) = CreateLiveAdapter();
        using (client)
        {
            var toc = await adapter.GetTableOfContentsAsync("book/3441");

            Assert.IsTrue(toc.Count > 0, "真实书页应解析出目录条目");
            Console.WriteLine($"live toc entries: {toc.Count}, first={toc[0].ExternalChapterId} {toc[0].Title}");
        }
    }

    [TestMethod]
    public async Task Live_Chapter_Content_Can_Be_Fetched_From_Kanunu8()
    {
        if (Environment.GetEnvironmentVariable("INKFLOW_LIVE_TESTS") != "1")
        {
            return; // live tests opt-in only (INKFLOW_LIVE_TESTS=1)
        }

        var (adapter, client) = CreateLiveAdapter();
        using (client)
        {
            var toc = await adapter.GetTableOfContentsAsync("book/3441");
            Assert.IsTrue(toc.Count > 0);

            var content = await adapter.GetChapterContentAsync(toc[0].ExternalChapterId);
            Assert.IsNotNull(content, "真实章节页应解析出正文段落");
            Assert.IsTrue(content.Length > 200, $"正文长度异常: {content.Length}");
            Console.WriteLine($"live chapter content length: {content.Length}, preview: {content[..Math.Min(80, content.Length)]}...");
        }
    }
}
