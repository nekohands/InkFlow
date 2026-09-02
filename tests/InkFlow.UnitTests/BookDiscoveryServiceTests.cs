using InkFlow.Modules.Crawling.Application;
using InkFlow.Modules.Library.Application;
using InkFlow.Modules.Library.Domain;
using InkFlow.Modules.Sources.Application;
using InkFlow.Modules.Sources.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.UnitTests;

/// <summary>
/// 来源搜索发现编排验证:多源命中归并到同一正典书、不健康/无适配器来源跳过、
/// 单源失败隔离为警告、重复发现幂等、空查询零触达。全部内存执行,零真实网络流量。
/// </summary>
[TestClass]
public sealed class BookDiscoveryServiceTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 29, 10, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task Hits_From_Two_Sources_Merge_Into_One_Canonical_Book()
    {
        // 两个来源返回同名同作者的命中 → v1 匹配挂接到同一本正典书。
        var sourceBooks = new InMemorySourceBooks();
        var canonicalRepo = new InMemoryCanonicalRepo();
        var candidates = new InMemoryCandidates();
        var harness = CreateHarness(
            sourceBooks, canonicalRepo, candidates,
            new SourceSpec("src-a", [new SourceSearchResult("a-1", "剑来", "烽火戏诸侯")]),
            new SourceSpec("src-b", [new SourceSearchResult("b-9", "剑来", "烽火戏诸侯")]));

        var outcome = await harness.Service.DiscoverAsync("剑来");

        Assert.AreEqual(0, outcome.Warnings.Count, string.Join("; ", outcome.Warnings));
        var book = outcome.Books.Single();
        Assert.AreEqual(canonicalRepo.Store.Single().Id, book.CanonicalBookId);
        CollectionAssert.AreEquivalent(new[] { "src-a", "src-b" }, book.SourceIds.ToList());
        Assert.IsFalse(book.AlreadyInLibrary, "本次会话新建的正典书不是库内旧书");
        Assert.AreEqual(2, sourceBooks.Store.Count, "两个来源各自幂等导入了一本来源书");
        Assert.AreEqual(
            2, candidates.Store.Count(c => c.Status == MatchCandidateStatus.Confirmed),
            "每个来源各自有一条 Confirmed 候选指向同一正典书");
    }

    [TestMethod]
    public async Task Unavailable_Search_Capability_Skips_Source_With_Warning()
    {
        var harness = CreateHarness(
            new InMemorySourceBooks(), new InMemoryCanonicalRepo(), new InMemoryCandidates(),
            new SourceSpec("dead-source", [new SourceSearchResult("d-1", "不见", "作者")]));
        harness.Health.Unavailable.Add(("dead-source", SourceCapability.Search));

        var outcome = await harness.Service.DiscoverAsync("任何词");

        Assert.AreEqual(0, outcome.Books.Count);
        StringAssert.Contains(outcome.Warnings.Single(), "dead-source");
        StringAssert.Contains(outcome.Warnings.Single(), "unavailable");
    }

    [TestMethod]
    public async Task Source_Exception_Is_Isolated_As_Safe_Warning()
    {
        var sourceBooks = new InMemorySourceBooks();
        var harness = CreateHarness(
            sourceBooks, new InMemoryCanonicalRepo(), new InMemoryCandidates(),
            new SourceSpec(
                "broken-source",
                SearchThrows: new InvalidOperationException(
                    "network down; internal-path=/srv/inkflow; connection-detail=redacted")),
            new SourceSpec("good-source", [new SourceSearchResult("g-1", "玉簟秋", "灵希")]));

        var outcome = await harness.Service.DiscoverAsync("玉簟秋");

        Assert.AreEqual(1, outcome.Books.Count, "单源失败不得影响其他来源的命中");
        Assert.AreEqual(1, outcome.Warnings.Count);
        StringAssert.Contains(outcome.Warnings[0],
            "search: source 'broken-source' failed; retry later.");
        Assert.IsFalse(outcome.Warnings[0].Contains("network down"));
        Assert.IsFalse(outcome.Warnings[0].Contains("internal-path"));
        Assert.AreEqual(1, sourceBooks.Store.Count, "失败来源不应留下半成品导入");
    }

    [TestMethod]
    public async Task Source_Timeout_Is_Isolated_As_Safe_Warning()
    {
        var sourceBooks = new InMemorySourceBooks();
        var harness = CreateHarness(
            sourceBooks, new InMemoryCanonicalRepo(), new InMemoryCandidates(),
            new SourceSpec("timeout-source", SearchThrows: new TaskCanceledException()),
            new SourceSpec("good-source", [new SourceSearchResult("g-1", "玉簟秋", "灵希")]));

        var outcome = await harness.Service.DiscoverAsync("玉簟秋");

        Assert.AreEqual(1, outcome.Books.Count, "来源超时不得影响其他来源的命中");
        Assert.AreEqual(1, outcome.Warnings.Count);
        StringAssert.Contains(outcome.Warnings[0],
            "search: source 'timeout-source' failed; retry later.");
        Assert.AreEqual(1, sourceBooks.Store.Count, "超时来源不应留下半成品导入");
    }

    [TestMethod]
    public async Task Discovery_Exception_After_Search_Is_Isolated_As_Warning()
    {
        var sourceBooks = new ThrowingSourceBooks("broken-source");
        var sources = new InMemorySources();
        var broken = Source.Create("broken-source", "来源 broken-source",
            "https://broken-source.example.com", T0);
        var good = Source.Create("good-source", "来源 good-source",
            "https://good-source.example.com", T0);
        sources.Store.Add(broken);
        sources.Store.Add(good);

        var service = BuildService(
            sources,
            sourceBooks,
            new InMemoryCanonicalRepo(),
            new InMemoryCandidates(),
            ("broken-source", new RecordingAdapter(
                "broken-source",
                [new SourceSearchResult("b-1", "坏来源书", "作者")])),
            ("good-source", new RecordingAdapter(
                "good-source",
                [new SourceSearchResult("g-1", "玉簟秋", "灵希")])));

        var outcome = await service.DiscoverAsync("玉簟秋");

        Assert.AreEqual(1, outcome.Books.Count,
            "导入阶段的单来源异常不得影响其他来源的命中");
        Assert.AreEqual("玉簟秋", outcome.Books[0].Title);
        Assert.AreEqual(1, outcome.Warnings.Count);
        StringAssert.Contains(outcome.Warnings[0],
            "discovery: source 'broken-source' failed; retry later.");
        Assert.IsFalse(outcome.Warnings[0].Contains("source repository unavailable"));
        Assert.IsFalse(outcome.Warnings[0].Contains("internal-path"));
    }

    [TestMethod]
    public async Task Source_Without_Adapter_Is_Skipped()
    {
        var harness = CreateHarness(
            new InMemorySourceBooks(), new InMemoryCanonicalRepo(), new InMemoryCandidates(),
            new SourceSpec("ghost-source"));

        var outcome = await harness.Service.DiscoverAsync("词");

        Assert.AreEqual(0, outcome.Books.Count);
        StringAssert.Contains(outcome.Warnings.Single(), "no usable adapter");
    }

    [TestMethod]
    public async Task Repeated_Discovery_Is_Idempotent()
    {
        var sourceBooks = new InMemorySourceBooks();
        var canonicalRepo = new InMemoryCanonicalRepo();
        var harness = CreateHarness(
            sourceBooks, canonicalRepo, new InMemoryCandidates(),
            new SourceSpec("src-a", [new SourceSearchResult("a-1", "剑来", "烽火戏诸侯")]));

        var first = await harness.Service.DiscoverAsync("剑来");
        var second = await harness.Service.DiscoverAsync("剑来");

        Assert.AreEqual(first.Books.Single().CanonicalBookId, second.Books.Single().CanonicalBookId);
        Assert.IsTrue(second.Books.Single().AlreadyInLibrary, "第二次发现同一本书应标记为库内旧书");
        Assert.AreEqual(1, canonicalRepo.Store.Count, "重复发现不得产生第二本正典书");
        Assert.AreEqual(1, sourceBooks.Store.Count, "BookInfo upsert 幂等");
    }

    [TestMethod]
    public async Task Empty_Query_Returns_Empty_Without_Touching_Sources()
    {
        var probe = new RecordingAdapter("probe-source", []);
        var sources = new InMemorySources();
        sources.Store.Add(Source.Create("probe-source", "探针来源",
            "https://probe.example.com", T0));

        var service = BuildService(
            sources, new InMemorySourceBooks(), new InMemoryCanonicalRepo(), new InMemoryCandidates(),
            ("probe-source", probe));

        var outcome = await service.DiscoverAsync("   ");

        Assert.AreEqual(0, outcome.Books.Count);
        Assert.AreEqual(0, outcome.Warnings.Count);
        Assert.AreEqual(0, probe.CallCount, "空查询不得触达任何来源");
    }

    private sealed record SourceSpec(
        string Id,
        IReadOnlyList<SourceSearchResult>? Hits = null,
        Exception? SearchThrows = null);

    private static Harness CreateHarness(
        InMemorySourceBooks sourceBooks,
        InMemoryCanonicalRepo canonicalRepo,
        InMemoryCandidates candidates,
        params SourceSpec[] specs)
    {
        var sources = new InMemorySources();
        var adapterEntries = new List<(string SourceId, ISourceAdapter? Adapter)>();

        foreach (var spec in specs)
        {
            sources.Store.Add(Source.Create(spec.Id, $"来源 {spec.Id}",
                $"https://{spec.Id}.example.com", T0));

            // Hits 与 SearchThrows 均缺省 = 工厂无法提供适配器的场景。
            ISourceAdapter? adapter =
                (spec.Hits is null && spec.SearchThrows is null)
                    ? null
                    : new RecordingAdapter(spec.Id, spec.Hits ?? [], spec.SearchThrows);
            adapterEntries.Add((spec.Id, adapter));
        }

        var health = new SettableHealthReader();
        var service = BuildService(sources, sourceBooks, canonicalRepo, candidates, adapterEntries.ToArray(), health);

        return new Harness(service, candidates, health);
    }

    private static BookDiscoveryService BuildService(
        ISourceRepository sources,
        ISourceBookRepository sourceBooks,
        ICanonicalBookRepository canonicalRepo,
        IMatchCandidateRepository candidates,
        params (string SourceId, ISourceAdapter? Adapter)[] adapterEntries) =>
        BuildService(sources, sourceBooks, canonicalRepo, candidates, adapterEntries, health: null);

    private static BookDiscoveryService BuildService(
        ISourceRepository sources,
        ISourceBookRepository sourceBooks,
        ICanonicalBookRepository canonicalRepo,
        IMatchCandidateRepository candidates,
        (string SourceId, ISourceAdapter? Adapter)[] adapterEntries,
        SettableHealthReader? health)
    {
        var catalog = new SourceCatalogService(
            new KeyedAdapterFactory(adapterEntries), sourceBooks, TimeProvider.System);
        var matching = new CanonicalBookMatchingService(sourceBooks, canonicalRepo, candidates);

        return new BookDiscoveryService(
            sources,
            new KeyedAdapterFactory(adapterEntries),
            catalog,
            matching,
            health);
    }

    private sealed record Harness(
        BookDiscoveryService Service,
        InMemoryCandidates Candidates,
        SettableHealthReader Health);

    private sealed class SettableHealthReader : ISourceHealthReader
    {
        public HashSet<(string SourceId, SourceCapability Capability)> Unavailable { get; } = [];

        public Task<bool> IsAvailableAsync(
            string sourceId,
            SourceCapability capability,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(!Unavailable.Contains((sourceId, capability)));
    }

    /// <summary>记录搜索调用的适配器替身。</summary>
    private sealed class RecordingAdapter : ISourceAdapter
    {
        private readonly IReadOnlyList<SourceSearchResult> _results;
        private readonly Exception? _searchThrows;

        public RecordingAdapter(string sourceId, IReadOnlyList<SourceSearchResult> results, Exception? searchThrows = null)
        {
            SourceId = sourceId;
            _results = results;
            _searchThrows = searchThrows;
        }

        public string SourceId { get; }

        public int CallCount { get; private set; }

        public Task<IReadOnlyList<SourceSearchResult>> SearchAsync(
            string keyword,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            if (_searchThrows is not null)
            {
                throw _searchThrows;
            }

            return Task.FromResult(_results);
        }

        public Task<SourceBookInfo?> GetBookInfoAsync(
            string externalBookId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<SourceBookInfo?>(
                _results.FirstOrDefault(r => r.ExternalBookId == externalBookId) is { } hit
                    ? new SourceBookInfo(hit.Title, hit.Author)
                    : null);

        public Task<IReadOnlyList<SourceTocEntry>> GetTableOfContentsAsync(
            string externalBookId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SourceTocEntry>>([]);

        public Task<string?> GetChapterContentAsync(
            string externalChapterId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);
    }

    private sealed class KeyedAdapterFactory(
        IReadOnlyList<(string SourceId, ISourceAdapter? Adapter)> entries) : ISourceAdapterFactory
    {
        public Task<ISourceAdapter?> GetAdapterAsync(
            string sourceId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(entries.FirstOrDefault(e => e.SourceId == sourceId).Adapter);
    }

    private sealed class InMemorySources : ISourceRepository
    {
        public List<Source> Store { get; } = [];

        public Task AddAsync(Source source, CancellationToken cancellationToken = default)
        {
            Store.Add(source);
            return Task.CompletedTask;
        }

        public Task<Source?> GetAsync(string sourceId, CancellationToken cancellationToken = default) =>
            Task.FromResult<Source?>(Store.FirstOrDefault(s => s.Id == sourceId));

        public Task<IReadOnlyList<Source>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Source>>(Store.ToList());

        public Task SaveAsync(Source source, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class InMemorySourceBooks : ISourceBookRepository
    {
        public Dictionary<(string SourceId, string ExternalId), SourceBook> Store { get; } = [];

        public Task AddAsync(SourceBook book, CancellationToken cancellationToken = default)
        {
            Store[(book.SourceId, book.ExternalBookId)] = book;
            return Task.CompletedTask;
        }

        public Task<SourceBook?> GetAsync(
            string sourceId,
            string externalBookId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Store.TryGetValue((sourceId, externalBookId), out var book) ? book : null);

        public Task<IReadOnlyList<SourceBook>> ListAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SourceBook>>(Store.Values.ToList());

        public Task SaveAsync(SourceBook book, CancellationToken cancellationToken = default)
        {
            Store[(book.SourceId, book.ExternalBookId)] = book;
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingSourceBooks(string failingSourceId) : ISourceBookRepository
    {
        private readonly InMemorySourceBooks _inner = new();

        public Task AddAsync(SourceBook book, CancellationToken cancellationToken = default) =>
            _inner.AddAsync(book, cancellationToken);

        public Task<SourceBook?> GetAsync(
            string sourceId,
            string externalBookId,
            CancellationToken cancellationToken = default) =>
            sourceId == failingSourceId
                ? throw new InvalidOperationException(
                    "source repository unavailable; internal-path=/var/lib/inkflow")
                : _inner.GetAsync(sourceId, externalBookId, cancellationToken);

        public Task<IReadOnlyList<SourceBook>> ListAllAsync(CancellationToken cancellationToken = default) =>
            _inner.ListAllAsync(cancellationToken);

        public Task SaveAsync(SourceBook book, CancellationToken cancellationToken = default) =>
            _inner.SaveAsync(book, cancellationToken);
    }

    private sealed class InMemoryCanonicalRepo : ICanonicalBookRepository
    {
        public List<CanonicalBook> Store { get; } = [];

        public Task AddAsync(CanonicalBook book, CancellationToken cancellationToken = default)
        {
            Store.Add(book);
            return Task.CompletedTask;
        }

        public Task<CanonicalBook?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<CanonicalBook?>(Store.FirstOrDefault(b => b.Id == id));

        public Task<IReadOnlyList<CanonicalBook>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CanonicalBook>>(Store.ToList());

        public Task<CanonicalBook?> FindByTitleAuthorAsync(
            string title,
            string author,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<CanonicalBook?>(
                Store.FirstOrDefault(b => b.Title == title.Trim() && b.Author == author.Trim()));

        public Task SaveAsync(CanonicalBook book, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class InMemoryCandidates : IMatchCandidateRepository
    {
        public List<MatchCandidate> Store { get; } = [];

        public Task AddAsync(MatchCandidate candidate, CancellationToken cancellationToken = default)
        {
            Store.Add(candidate);
            return Task.CompletedTask;
        }

        public Task<MatchCandidate?> FindForSourceBookAsync(
            string sourceId,
            string externalBookId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<MatchCandidate?>(
                Store.FirstOrDefault(c =>
                    c.SourceId == sourceId && c.ExternalBookId == externalBookId));
    }
}
