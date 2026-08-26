using InkFlow.Modules.Sources.Application;
using InkFlow.Modules.Sources.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class SourceContentServiceTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 26, 15, 0, 0, TimeSpan.Zero);

    private sealed class InMemoryBookRepository : ISourceBookRepository
    {
        public SourceBook? Book { get; set; }
        public Task AddAsync(SourceBook book, CancellationToken cancellationToken = default)
        {
            Book = book;
            return Task.CompletedTask;
        }

        public Task<SourceBook?> GetAsync(string sourceId, string externalBookId, CancellationToken cancellationToken = default)
            => Task.FromResult(Book is not null && Book.SourceId == sourceId && Book.ExternalBookId == externalBookId ? Book : null);

        public Task<IReadOnlyList<SourceBook>> ListAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SourceBook>>(Book is null ? [] : [Book]);

        public Task SaveAsync(SourceBook book, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class InMemoryArtifactRepository : IFetchArtifactRepository
    {
        public List<FetchArtifact> Store { get; } = [];

        public Task AddAsync(FetchArtifact artifact, CancellationToken cancellationToken = default)
        {
            Store.Add(artifact);
            return Task.CompletedTask;
        }

        public Task<FetchArtifact?> GetLatestAsync(string sourceId, string externalChapterId, CancellationToken cancellationToken = default)
            => Task.FromResult<FetchArtifact?>(
                Store.Where(a => a.SourceId == sourceId && a.ExternalChapterId == externalChapterId)
                    .OrderByDescending(a => a.FetchedAt)
                    .FirstOrDefault());
    }

    private sealed class FakeHttpClient(string body) : ISourceHttpClient
    {
        public int CallCount { get; private set; }
        public string Body { get; set; } = body;

        public Task<SourceHttpResponse> SendAsync(SourceHttpRequest request, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(new SourceHttpResponse(200, Body));
        }
    }

    /// <summary>把 content 字段的 CSS 表达式映射到预设正文的最小求值器（值可变，模拟上游修订）。</summary>
    private sealed class ContentEvaluator(string? value) : ISelectorEvaluator
    {
        public string? Value { get; set; } = value;

        public string? EvaluateFirst(string documentBody, RuleSelector selector) => Value;
    }

    private static Source SourceWithContentRule() =>
        Source.Rehydrate(
            "example-source",
            "示例来源",
            "https://books.example.com",
            new SourceRuleDsl("1", "example-source",
            [
                new CapabilityRule(
                    SourceCapability.Content,
                    RuleRequest.Get("/chapter/{chapterId}"),
                    [new RuleField("content", new RuleSelector(SelectorKind.Css, "#content"), null, [])]),
            ]),
            T0,
            T0);

    private static (SourceContentService Service, FakeHttpClient Http,
        InMemoryArtifactRepository Artifacts, InMemoryBookRepository Books, ContentEvaluator Evaluator)
        CreateService(string chapterBody)
    {
        var http = new FakeHttpClient(chapterBody);
        var books = new InMemoryBookRepository
        {
            Book = SourceBook.Create("example-source", "10001", "剑来", "烽火戏诸侯", T0),
        };
        books.Book!.SyncChapters([("ch-001", "第一章")], T0);

        var artifacts = new InMemoryArtifactRepository();
        var evaluator = new ContentEvaluator(chapterBody);
        var service = new SourceContentService(
            new FakeSourceRepo(SourceWithContentRule()),
            books,
            artifacts,
            new RuleAdapter(http, evaluator));

        return (service, http, artifacts, books, evaluator);
    }

    [TestMethod]
    public async Task First_Fetch_Persists_Artifact()
    {
        var (service, http, artifacts, _, _) = CreateService("<p>第一章正文</p>");

        var outcome = await service.FetchChapterContentAsync("example-source", "10001", "ch-001", T0);

        Assert.IsTrue(outcome.IsSuccess, string.Join("; ", outcome.Errors));
        Assert.IsFalse(outcome.Unchanged);
        Assert.IsNotNull(outcome.Artifact);
        Assert.AreEqual(1, artifacts.Store.Count);
        Assert.AreEqual(1, http.CallCount);
    }

    [TestMethod]
    public async Task Same_Content_Second_Fetch_Is_Unchanged_And_Skips_Storage()
    {
        var (service, _, artifacts, _, _) = CreateService("<p>第一章正文</p>");
        await service.FetchChapterContentAsync("example-source", "10001", "ch-001", T0);

        var second = await service.FetchChapterContentAsync("example-source", "10001", "ch-001", T0.AddMinutes(5));

        Assert.IsTrue(second.IsSuccess);
        Assert.IsTrue(second.Unchanged, "内容未变应返回 Unchanged");
        Assert.AreEqual(1, artifacts.Store.Count, "未变的内容不应产生新的存储行");
    }

    [TestMethod]
    public async Task Changed_Content_Creates_New_Artifact()
    {
        var (service, http, artifacts, _, evaluator) = CreateService("<p>旧正文</p>");
        await service.FetchChapterContentAsync("example-source", "10001", "ch-001", T0);

        http.Body = "<p>修订后的正文</p>";
        evaluator.Value = "<p>修订后的正文</p>";
        var second = await service.FetchChapterContentAsync(
            "example-source", "10001", "ch-001", T0.AddDays(1));

        Assert.IsTrue(second.IsSuccess);
        Assert.IsFalse(second.Unchanged);
        Assert.AreNotEqual(artifacts.Store[0].RawHash, second.Artifact!.RawHash);
    }

    [TestMethod]
    public async Task Unknown_Chapter_Fails_Before_Any_Network_Call()
    {
        var (service, http, artifacts, _, _) = CreateService("<p>正文</p>");

        var outcome = await service.FetchChapterContentAsync("example-source", "10001", "ghost-chapter", T0);

        Assert.IsFalse(outcome.IsSuccess);
        StringAssert.Contains(outcome.Errors[0], "not part of book");
        Assert.AreEqual(0, http.CallCount);
        Assert.AreEqual(0, artifacts.Store.Count);
    }

    [TestMethod]
    public async Task Empty_Extraction_Is_Reported_As_Error()
    {
        var (service, _, _, _, _) = CreateService("");

        var outcome = await service.FetchChapterContentAsync("example-source", "10001", "ch-001", T0);

        Assert.IsFalse(outcome.IsSuccess);
        StringAssert.Contains(outcome.Errors[0], "'content'");
    }

    /// <summary>内存来源仓储。</summary>
    private sealed class FakeSourceRepo(Source? source) : ISourceRepository
    {
        public Task AddAsync(Source source, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<Source?> GetAsync(string sourceId, CancellationToken cancellationToken = default)
            => Task.FromResult(source is not null && source.Id == sourceId ? source : null);

        public Task SaveAsync(Source source, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
