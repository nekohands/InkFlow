using InkFlow.Modules.Sources.Application;
using InkFlow.Modules.Sources.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class SourceCatalogServiceTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 26, 11, 0, 0, TimeSpan.Zero);

    /// <summary>内存书目仓储：模拟持久化语义（Add 后 Get 可见，Save 覆盖）。</summary>
    private sealed class InMemoryBookRepository : ISourceBookRepository
    {
        public Dictionary<(string SourceId, string ExternalId), SourceBook> Store { get; } = new();

        public Task AddAsync(SourceBook book, CancellationToken cancellationToken = default)
        {
            Store[(book.SourceId, book.ExternalBookId)] = book;
            return Task.CompletedTask;
        }

        public Task<SourceBook?> GetAsync(string sourceId, string externalBookId, CancellationToken cancellationToken = default)
            => Task.FromResult(Store.TryGetValue((sourceId, externalBookId), out var book) ? book : null);

        public Task<IReadOnlyList<SourceBook>> ListAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SourceBook>>(Store.Values.ToList());

        public Task SaveAsync(SourceBook book, CancellationToken cancellationToken = default)
        {
            Store[(book.SourceId, book.ExternalBookId)] = book;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeHttpClient : ISourceHttpClient
    {
        public string Body { get; set; } = "";
        public int CallCount { get; private set; }

        public Task<SourceHttpResponse> SendAsync(SourceHttpRequest request, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(new SourceHttpResponse(200, Body));
        }
    }

    /// <summary>把 CSS 表达式映射到预设字段值的最小求值器。</summary>
    private sealed class FieldEvaluator(IReadOnlyDictionary<string, string> fields) : ISelectorEvaluator
    {
        public string? EvaluateFirst(string documentBody, RuleSelector selector) =>
            fields.TryGetValue(selector.Expression, out var value) ? value : null;
    }

    [TestMethod]
    public async Task ImportBookInfo_Creates_Then_Updates_The_Same_Book()
    {
        var repo = new InMemoryBookRepository();
        var http = new FakeHttpClient { Body = "<html/>" };
        var sources = new FakeSourceRepo(SourceWithRules());
        var service = new SourceCatalogService(sources, repo, new RuleAdapter(http,
            new FieldEvaluator(new Dictionary<string, string>
            {
                ["h1.book-title"] = "剑来",
                ["span.author"] = "烽火戏诸侯",
            })));

        // 首次导入：创建
        var first = await service.ImportBookInfoAsync("example-source", "10001", T0);
        Assert.IsTrue(first.IsSuccess, string.Join("; ", first.Errors));
        Assert.AreEqual(1, repo.Store.Count);
        Assert.AreEqual("剑来", first.Book!.Title);

        // 二次导入：更新同一条来源视图，不新建
        var second = await service.ImportBookInfoAsync("example-source", "10001", T0.AddMinutes(5));

        Assert.IsTrue(second.IsSuccess);
        Assert.AreEqual(first.Book!.Id, second.Book!.Id);
        Assert.AreEqual(1, repo.Store.Count);
    }

    [TestMethod]
    public async Task SyncChapters_Parses_Toc_Block_And_Is_Idempotent()
    {
        var repo = new InMemoryBookRepository();
        var importHttp = new FakeHttpClient { Body = "<html/>" };
        var sources = new FakeSourceRepo(SourceWithRules());

        var importService = new SourceCatalogService(sources, repo, new RuleAdapter(importHttp,
            new FieldEvaluator(new Dictionary<string, string>
            {
                ["h1.book-title"] = "剑来",
                ["span.author"] = "烽火戏诸侯",
            })));
        await importService.ImportBookInfoAsync("example-source", "10001", T0);

        var tocHttp = new FakeHttpClient
        {
            Body = "<html/>",
        };
        var tocService = new SourceCatalogService(sources, repo, new RuleAdapter(tocHttp,
            new FieldEvaluator(new Dictionary<string, string>
            {
                ["#toc"] = "ch-001\t第一章 惊蛰\nch-002\t第二章 开门\n\n坏行\nch-003\t第三章",
            })));

        var result = await tocService.SyncChaptersAsync("example-source", "10001", T0.AddMinutes(1));
        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors));
        Assert.AreEqual(3, result.Book!.Chapters.Count);
        Assert.AreEqual("ch-001", result.Book.Chapters[0].ExternalChapterId);
        Assert.AreEqual(2, result.Book.Chapters[2].Index);

        // 幂等：重复同步无新增
        var again = await tocService.SyncChaptersAsync("example-source", "10001", T0.AddMinutes(2));
        Assert.IsTrue(again.IsSuccess);
        Assert.AreEqual(3, again.Book!.Chapters.Count);
    }

    [TestMethod]
    public async Task SyncChapters_Before_Import_Fails_Clearly()
    {
        var repo = new InMemoryBookRepository();
        var sources = new FakeSourceRepo(SourceWithRules());
        // Toc 规则成功返回目录块，但该书从未被 BookInfo 导入。
        var service = new SourceCatalogService(sources, repo,
            new RuleAdapter(new FakeHttpClient { Body = "<html/>" },
                new FieldEvaluator(new Dictionary<string, string> { ["#toc"] = "c1\t第一章\n" })));

        var outcome = await service.SyncChaptersAsync("example-source", "unknown-book", T0);

        Assert.IsFalse(outcome.IsSuccess);
        StringAssert.Contains(outcome.Errors[0], "must be imported");
    }

    [TestMethod]
    public async Task Unknown_Source_Fails_Without_Hitting_Http()
    {
        var repo = new InMemoryBookRepository();
        var http = new FakeHttpClient();
        var service = new SourceCatalogService(new FakeSourceRepo(null), repo,
            new RuleAdapter(http, new FieldEvaluator(new Dictionary<string, string>())));

        var outcome = await service.ImportBookInfoAsync("ghost-source", "1", T0);

        Assert.IsFalse(outcome.IsSuccess);
        StringAssert.Contains(outcome.Errors[0], "does not exist");
        Assert.AreEqual(0, http.CallCount);
    }

    /// <summary>内存来源仓储。</summary>
    private sealed class FakeSourceRepo(Source? source) : ISourceRepository
    {
        public Source? Source { get; set; } = source;

        public Task AddAsync(Source source, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<Source?> GetAsync(string sourceId, CancellationToken cancellationToken = default)
            => Task.FromResult(Source is not null && Source.Id == sourceId ? Source : null);

        public Task SaveAsync(Source source, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private static Source SourceWithRules() =>
        Source.Rehydrate(
            "example-source",
            "示例来源",
            "https://books.example.com",
            new SourceRuleDsl("1", "example-source",
            [
                new CapabilityRule(
                    SourceCapability.BookInfo,
                    RuleRequest.Get("/book/{bookId}"),
                    [
                        new RuleField("title", new RuleSelector(SelectorKind.Css, "h1.book-title"), null, []),
                        new RuleField("author", new RuleSelector(SelectorKind.Css, "span.author"), null, []),
                    ]),
                new CapabilityRule(
                    SourceCapability.Toc,
                    RuleRequest.Get("/toc/{bookId}"),
                    [new RuleField("chapters", new RuleSelector(SelectorKind.Css, "#toc"), null, [])]),
            ]),
            T0,
            T0);
}
