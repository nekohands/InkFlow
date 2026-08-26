using System.Text;
using InkFlow.Modules.Content.Application;
using InkFlow.Modules.Content.Domain;
using InkFlow.Modules.Crawling.Application;
using InkFlow.Modules.Library.Application;
using InkFlow.Modules.Library.Domain;
using InkFlow.Modules.Sources.Application;
using InkFlow.Modules.Sources.Domain;
using InkFlow.Modules.Sources.Infrastructure;
using InkFlow.BuildingBlocks.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.IntegrationTests;

/// <summary>
/// 端到端数据流编排验证(真实站点 + 内存持久层):
/// 导入书目 → 同步目录 → 匹配正典书 → 章节映射 → 正文发布 → 公共查询。
/// 验证各模块服务在真实数据上的编排正确性;持久化正确性由 Testcontainers 测试覆盖。
/// 默认跳过,设置 INKFLOW_LIVE_TESTS=1 后执行。
/// </summary>
[TestClass]
public sealed class EndToEndDataFlowTests
{
    private sealed class InMemoryBookRepository : ICanonicalBookRepository
    {
        public Dictionary<Guid, CanonicalBook> Store { get; } = [];
        public Task AddAsync(CanonicalBook book, CancellationToken cancellationToken = default)
        {
            Store[book.Id] = book;
            return Task.CompletedTask;
        }
        public Task<CanonicalBook?> GetAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(Store.TryGetValue(id, out var b) ? b : null);
        public Task<IReadOnlyList<CanonicalBook>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CanonicalBook>>(Store.Values.ToList());
        public Task SaveAsync(CanonicalBook book, CancellationToken cancellationToken = default)
        {
            Store[book.Id] = book;
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryCandidateRepository : IMatchCandidateRepository
    {
        public List<MatchCandidate> Store { get; } = [];
        public Task AddAsync(MatchCandidate candidate, CancellationToken cancellationToken = default)
        {
            Store.Add(candidate);
            return Task.CompletedTask;
        }
        public Task<MatchCandidate?> FindForSourceBookAsync(string sourceId, string externalBookId, CancellationToken cancellationToken = default)
            => Task.FromResult<MatchCandidate?>(
                Store.FirstOrDefault(c => c.SourceId == sourceId && c.ExternalBookId == externalBookId));
    }

    private sealed class InMemoryMappingRepository : IChapterMappingRepository
    {
        public List<ChapterMapping> Store { get; } = [];
        public Task AddAsync(ChapterMapping mapping, CancellationToken cancellationToken = default)
        {
            Store.Add(mapping);
            return Task.CompletedTask;
        }
        public Task<ChapterMapping?> FindAsync(string sourceId, string externalChapterId, CancellationToken cancellationToken = default)
            => Task.FromResult<ChapterMapping?>(
                Store.FirstOrDefault(m => m.SourceId == sourceId && m.ExternalChapterId == externalChapterId));
    }

    [TestMethod]
    public async Task Full_Pipeline_From_Live_Source_To_Public_Query()
    {
        if (Environment.GetEnvironmentVariable("INKFLOW_LIVE_TESTS") != "1")
        {
            Assert.Inconclusive("set INKFLOW_LIVE_TESTS=1 to run live verification");
        }

        // ---- 组合根:真实 HTTP 适配器 + 内存持久层 ----
        Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        var http = new HttpClient();
        var resolver = new DnsIpAddressResolver();
        var sourceHttp = new ProductionSafeSourceHttpClient(http, resolver);
        var kanunu = new KanunuSourceAdapter(http, resolver);

        var ruleAdapter = new RuleAdapter(sourceHttp, new CssSelectorEvaluator());
        var factory = new SourceAdapterFactory(
            new NullSourceRepository(),
            ruleAdapter,
            [kanunu]);

        var sourceBooks = new InMemorySourceBooks();
        var canonicalRepo = new InMemoryCanonicalRepo();
        var candidates = new InMemoryCandidateRepository();
        var mappings = new InMemoryMappingRepository();
        var versions = new InMemoryVersions();
        var artifacts = new InMemoryArtifacts();

        var catalog = new SourceCatalogService(factory, sourceBooks, TimeProvider.System);
        var matching = new CanonicalBookMatchingService(sourceBooks, canonicalRepo, candidates);
        var chapterMapping = new CanonicalChapterMappingService(sourceBooks, candidates, canonicalRepo, mappings);
        var contentService = new SourceContentService(factory, sourceBooks, artifacts, TimeProvider.System);
        var publishing = new ContentPublishingService(versions);
        var query = new CatalogQueryService(canonicalRepo, versions);

        const string externalBookId = "book/3441";

        // ---- 1. 导入书目元数据(BookInfo 能力)----
        var import = await catalog.ImportBookInfoAsync(KanunuSourceAdapter.SourceIdValue, externalBookId);
        Assert.IsTrue(import.IsSuccess, string.Join("; ", import.Errors));
        Assert.AreEqual("玉簟秋", import.Book!.Title);

        // ---- 2. 同步目录(Toc 能力)----
        var tocSync = await catalog.SyncChaptersAsync(KanunuSourceAdapter.SourceIdValue, externalBookId);
        Assert.IsTrue(tocSync.IsSuccess, string.Join("; ", tocSync.Errors));
        Assert.IsTrue(tocSync.Book!.Chapters.Count >= 10);

        // ---- 3. 匹配正典书 ----
        var match = await matching.CreateOrMatchAsync(KanunuSourceAdapter.SourceIdValue, externalBookId);
        Assert.IsTrue(match.IsSuccess);
        Assert.IsTrue(match.NewlyCreated);
        Assert.AreEqual("玉簟秋", match.Book!.Title);

        // ---- 4. 章节映射(来源章节 → 正典章节)----
        var mapping = await chapterMapping.SyncChapterMappingAsync(KanunuSourceAdapter.SourceIdValue, externalBookId);
        Assert.IsTrue(mapping.IsSuccess);
        Assert.AreEqual(tocSync.Book.Chapters.Count, mapping.NewlyMappedCount);

        // ---- 5. 发布第一章正文(Content 规范化 + 版本选优)----
        var firstTocEntry = tocSync.Book.Chapters[0];
        var raw = await kanunu.GetChapterContentAsync(firstTocEntry.ExternalChapterId);
        Assert.IsNotNull(raw);

        var canonicalChapterId = mappings.Store
            .First(m => m.ExternalChapterId == firstTocEntry.ExternalChapterId)
            .CanonicalChapterId;

        var published = await publishing.PublishAsync(
            match.Book.Id, canonicalChapterId,
            KanunuSourceAdapter.SourceIdValue, raw);
        Assert.IsTrue(published.IsSuccess, string.Join("; ", published.Errors));

        // ---- 6. 公共查询视角:书目可见、当前版本可读 ----
        var allBooks = await query.ListBooksAsync();
        Assert.AreEqual(1, allBooks.Count(b => b.Title == "玉簟秋"));

        var detail = await query.GetBookAsync(match.Book!.Id);
        Assert.IsNotNull(detail);
        Assert.IsTrue(detail.Chapters.Count > 0);

        var readable = await query.GetChapterContentAsync(canonicalChapterId);
        Assert.IsNotNull(readable, "已发布版本应可经公共查询读取");
        Assert.IsTrue(readable.Paragraphs.Count > 0);
        StringAssert.Contains(readable.Paragraphs[0], "时值金陵六月");
    }

    // ---- 内存实现 ----

    private sealed class NullSourceRepository : ISourceRepository
    {
        public Task AddAsync(Source source, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<Source?> GetAsync(string sourceId, CancellationToken cancellationToken = default)
            => Task.FromResult<Source?>(null);
        public Task SaveAsync(Source source, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class InMemorySourceBooks : ISourceBookRepository
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
        public Task SaveAsync(SourceBook book, CancellationToken cancellationToken = default)
        {
            Book = book;
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryCanonicalRepo : ICanonicalBookRepository
    {
        public CanonicalBook? Book { get; set; }
        public Task AddAsync(CanonicalBook book, CancellationToken cancellationToken = default)
        {
            Book = book;
            return Task.CompletedTask;
        }
        public Task<CanonicalBook?> GetAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(Book is not null && Book.Id == id ? Book : null);
        public Task<IReadOnlyList<CanonicalBook>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CanonicalBook>>(Book is null ? [] : [Book]);
        public Task SaveAsync(CanonicalBook book, CancellationToken cancellationToken = default)
        {
            Book = book;
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryVersions : IContentVersionRepository
    {
        public List<ContentVersion> Store { get; } = [];
        private readonly Dictionary<(Guid ChapterId, string Hash), bool> _current = [];

        public Task AddAsync(ContentVersion version, CancellationToken cancellationToken = default)
        {
            Store.Add(version);
            _current[(version.CanonicalChapterId, version.CanonicalHash)] = false;
            return Task.CompletedTask;
        }
        public Task<ContentVersion?> FindByHashAsync(Guid canonicalChapterId, string canonicalHash, CancellationToken cancellationToken = default)
            => Task.FromResult<ContentVersion?>(
                Store.FirstOrDefault(v => v.CanonicalChapterId == canonicalChapterId && v.CanonicalHash == canonicalHash));
        public Task<IReadOnlyList<ContentVersion>> ListForChapterAsync(Guid canonicalChapterId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ContentVersion>>(
                Store.Where(v => v.CanonicalChapterId == canonicalChapterId).ToList());
        public Task<ContentVersion?> GetCurrentForChapterAsync(Guid canonicalChapterId, CancellationToken cancellationToken = default)
            => Task.FromResult<ContentVersion?>(
                Store.LastOrDefault(v => v.CanonicalChapterId == canonicalChapterId &&
                    _current.TryGetValue((canonicalChapterId, v.CanonicalHash), out var cur) && cur));
        public Task SetCurrentAsync(Guid chapterId, Guid versionId, CancellationToken cancellationToken = default)
        {
            foreach (var v in Store.Where(v => v.CanonicalChapterId == chapterId))
            {
                _current[(chapterId, v.CanonicalHash)] = v.Id == versionId;
            }
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryArtifacts : IFetchArtifactRepository
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
}
