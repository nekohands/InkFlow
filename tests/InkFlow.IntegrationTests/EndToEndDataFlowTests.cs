using System.Text;
using InkFlow.Modules.Content.Application;
using InkFlow.Modules.Content.Domain;
using InkFlow.Modules.Crawling.Application;
using InkFlow.Modules.Crawling.Domain;
using InkFlow.Modules.Library.Application;
using InkFlow.Modules.Library.Domain;
using InkFlow.Modules.Sources.Application;
using InkFlow.Modules.Sources.Domain;
using InkFlow.Modules.Sources.Infrastructure;
using InkFlow.Sources.Adapters.Kanunu8;
using InkFlow.BuildingBlocks.Security;
using InkFlow.BuildingBlocks.Observability;
using Microsoft.Extensions.Logging.Abstractions;
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


        public Task<CanonicalBook?> FindByTitleAuthorAsync(string title, string author, CancellationToken cancellationToken = default)
            => Task.FromResult<CanonicalBook?>(null);

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
        var resolver = new DnsIpAddressResolver();
        using var safeHandler = new SsrfSafeHttpMessageHandler(resolver);
        using var http = new HttpClient(safeHandler);
        var sourceHttp = new ProductionSafeSourceHttpClient(http, resolver);
        var kanunu = new KanunuSourceAdapter(http, resolver);

        var ruleAdapter = new RuleAdapter(sourceHttp, new CssSelectorEvaluator());
        var factory = new SourceAdapterFactory(
            new NullSourceRepository(),
            ruleAdapter,
            new CssSelectorEvaluator(),
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
        var query = new CatalogQueryService(canonicalRepo, versions, new AllowAllContentPolicyReader());

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

    [TestMethod]
    public async Task Live_Scheduler_And_Worker_Complete_Current_Source_Content_Chain()
    {
        if (Environment.GetEnvironmentVariable("INKFLOW_LIVE_TESTS") != "1")
        {
            return; // live tests opt-in only (INKFLOW_LIVE_TESTS=1)
        }

        // ---- 组合根:真实 HTTP 适配器 + 内存持久层 ----
        Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        var resolver = new DnsIpAddressResolver();
        using var safeHandler = new SsrfSafeHttpMessageHandler(resolver);
        using var http = new HttpClient(safeHandler);
        var sourceHttp = new ProductionSafeSourceHttpClient(http, resolver);
        var kanunu = new KanunuSourceAdapter(http, resolver);

        var ruleAdapter = new RuleAdapter(sourceHttp, new CssSelectorEvaluator());
        var factory = new SourceAdapterFactory(
            new NullSourceRepository(),
            ruleAdapter,
            new CssSelectorEvaluator(),
            [kanunu]);

        var sourceBooks = new InMemorySourceBooks();
        var canonicalRepo = new InMemoryCanonicalRepo();
        var candidates = new InMemoryCandidateRepository();
        var mappings = new InMemoryMappingRepository();
        var versions = new InMemoryVersions();
        var artifacts = new InMemoryArtifacts();
        var tasks = new InMemoryCrawlerTasks();

        var catalog = new SourceCatalogService(factory, sourceBooks, TimeProvider.System);
        var matching = new CanonicalBookMatchingService(sourceBooks, canonicalRepo, candidates);
        var chapterMapping = new CanonicalChapterMappingService(sourceBooks, candidates, canonicalRepo, mappings);
        var contentService = new SourceContentService(factory, sourceBooks, artifacts, TimeProvider.System);
        var publishing = new ContentPublishingService(versions);
        var query = new CatalogQueryService(canonicalRepo, versions, new AllowAllContentPolicyReader());
        var contentChain = new ContentFetchChainService(sourceBooks, artifacts, tasks, TimeProvider.System);
        var tocHandler = new TocSyncTaskHandler(catalog, chapterMapping, contentChain);
        var contentHandler = new ContentFetchTaskHandler(
            contentService,
            new MappingContentPublisher(mappings, publishing));
        var failureReporter = new CrawlerFailureReporter(
            Array.Empty<ICrawlerFailureSink>(),
            NullLogger<CrawlerFailureReporter>.Instance);
        var leaseService = new CrawlerLeaseService(TimeProvider.System);
        var tocProcessor = new CrawlerTaskProcessor(
            tocHandler,
            tasks,
            TimeProvider.System,
            new RetryPolicy { BaseDelay = TimeSpan.FromMilliseconds(1), MaxDelay = TimeSpan.FromMilliseconds(1) },
            failureReporter);
        var contentProcessor = new CrawlerTaskProcessor(
            contentHandler,
            tasks,
            TimeProvider.System,
            new RetryPolicy { BaseDelay = TimeSpan.FromMilliseconds(1), MaxDelay = TimeSpan.FromMilliseconds(1) },
            failureReporter);

        const string externalBookId = "book/3441";

        // 1. 首次导入建立来源书目和正典映射，后续扫描完全由 Scheduler/Worker 链路驱动。
        var import = await catalog.ImportBookInfoAsync(KanunuSourceAdapter.SourceIdValue, externalBookId);
        Assert.IsTrue(import.IsSuccess, string.Join("; ", import.Errors));
        var initialToc = await catalog.SyncChaptersAsync(KanunuSourceAdapter.SourceIdValue, externalBookId);
        Assert.IsTrue(initialToc.IsSuccess, string.Join("; ", initialToc.Errors));
        Assert.IsTrue(initialToc.Book!.Chapters.Count >= 10);

        var match = await matching.CreateOrMatchAsync(KanunuSourceAdapter.SourceIdValue, externalBookId);
        Assert.IsTrue(match.IsSuccess, string.Join("; ", match.Errors));
        var initialMapping = await chapterMapping
            .SyncChapterMappingAsync(KanunuSourceAdapter.SourceIdValue, externalBookId);
        Assert.IsTrue(initialMapping.IsSuccess, string.Join("; ", initialMapping.Errors));
        Assert.AreEqual(initialToc.Book.Chapters.Count, initialMapping.NewlyMappedCount);

        // 2. Scheduler 入队，Worker 通过真实 TocSyncTaskHandler 完成重扫和正文联动入队。
        var updateScan = new UpdateScanService(sourceBooks, tasks, TimeProvider.System);
        Assert.AreEqual(1, await updateScan.EnqueueTocScansAsync());
        var tocTask = tasks.Store.Single(task => task.Payload.Capability == SourceCapability.Toc);
        Assert.IsTrue(leaseService.TryLease(tocTask, "live-source-test-worker"));
        await tocProcessor.ProcessAsync(tocTask);
        Assert.AreEqual(CrawlerTaskStatus.Completed, tocTask.Status);

        var contentTasks = tasks.Store
            .Where(task => task.Payload.Capability == SourceCapability.Content)
            .ToList();
        Assert.AreEqual(initialToc.Book.Chapters.Count, contentTasks.Count);
        Assert.AreEqual(
            contentTasks.Count,
            contentTasks.Select(task => task.Payload.Variables["chapterId"]).Distinct().Count(),
            "Toc sync should enqueue one Content task per unfetched chapter.");

        // 3. 周期重扫在正文任务尚未完成时也必须幂等，不能重复制造 Content 任务。
        Assert.AreEqual(1, await updateScan.EnqueueTocScansAsync());
        var repeatedTocTask = tasks.Store
            .Where(task => task.Payload.Capability == SourceCapability.Toc)
            .Single(task => task.Status == CrawlerTaskStatus.Pending);
        Assert.IsTrue(leaseService.TryLease(repeatedTocTask, "live-source-test-worker"));
        await tocProcessor.ProcessAsync(repeatedTocTask);
        Assert.AreEqual(CrawlerTaskStatus.Completed, repeatedTocTask.Status);
        Assert.AreEqual(
            contentTasks.Count,
            tasks.Store.Count(task => task.Payload.Capability == SourceCapability.Content),
            "An in-flight Content task must block duplicate chain enqueueing.");

        // 4. Worker 消费一条真实正文任务，经 FetchArtifact 和 ContentVersion 发布后可由公共查询读取。
        var firstContentTask = contentTasks[0];
        Assert.IsTrue(leaseService.TryLease(firstContentTask, "live-source-test-worker"));
        await contentProcessor.ProcessAsync(firstContentTask);
        Assert.AreEqual(CrawlerTaskStatus.Completed, firstContentTask.Status);
        Assert.AreEqual(1, artifacts.Store.Count);
        Assert.AreEqual(1, versions.Store.Count);

        var firstChapterId = mappings.Store
            .First(mapping => mapping.ExternalChapterId == firstContentTask.Payload.Variables["chapterId"])
            .CanonicalChapterId;
        var readable = await query.GetChapterContentAsync(firstChapterId);
        Assert.IsNotNull(readable, "真实正文经 Worker 发布后应可经公共查询读取");
        Assert.IsTrue(readable.Paragraphs.Count > 0);
        StringAssert.Contains(readable.Paragraphs[0], "时值金陵六月");

        Console.WriteLine(
            $"live scheduler/content chain: source={KanunuSourceAdapter.SourceIdValue}, " +
            $"chapters={initialToc.Book.Chapters.Count}, " +
            $"contentTasks={contentTasks.Count}, published={versions.Store.Count}");
    }

    [TestMethod]
    public async Task Automated_Scheduler_Discovers_New_Chapter_And_Publishes_Content()
    {
        // Deterministic equivalent of the periodic scheduler/worker path. The
        // live-source test above remains opt-in, while this test always runs so
        // a release gate can prove the update chain without a third-party site.
        var adapter = new MutableUpdateSourceAdapter();
        var sourceBooks = new InMemorySourceBooks();
        var canonicalRepo = new InMemoryCanonicalRepo();
        var candidates = new InMemoryCandidateRepository();
        var mappings = new InMemoryMappingRepository();
        var versions = new InMemoryVersions();
        var artifacts = new InMemoryArtifacts();
        var tasks = new InMemoryCrawlerTasks();

        var factory = new FixedAdapterFactory(adapter);
        var catalog = new SourceCatalogService(factory, sourceBooks, TimeProvider.System);
        var matching = new CanonicalBookMatchingService(sourceBooks, canonicalRepo, candidates);
        var chapterMapping = new CanonicalChapterMappingService(
            sourceBooks, candidates, canonicalRepo, mappings);
        var contentService = new SourceContentService(
            factory, sourceBooks, artifacts, TimeProvider.System);
        var publishing = new ContentPublishingService(versions);
        var query = new CatalogQueryService(
            canonicalRepo, versions, new AllowAllContentPolicyReader());
        var contentChain = new ContentFetchChainService(
            sourceBooks, artifacts, tasks, TimeProvider.System);
        var tocHandler = new TocSyncTaskHandler(catalog, chapterMapping, contentChain);
        var contentHandler = new ContentFetchTaskHandler(
            contentService,
            new MappingContentPublisher(mappings, publishing));
        var failureReporter = new CrawlerFailureReporter(
            Array.Empty<ICrawlerFailureSink>(),
            NullLogger<CrawlerFailureReporter>.Instance);
        var leaseService = new CrawlerLeaseService(TimeProvider.System);
        var tocProcessor = new CrawlerTaskProcessor(
            tocHandler,
            tasks,
            TimeProvider.System,
            new RetryPolicy { BaseDelay = TimeSpan.FromMilliseconds(1), MaxDelay = TimeSpan.FromMilliseconds(1) },
            failureReporter);
        var contentProcessor = new CrawlerTaskProcessor(
            contentHandler,
            tasks,
            TimeProvider.System,
            new RetryPolicy { BaseDelay = TimeSpan.FromMilliseconds(1), MaxDelay = TimeSpan.FromMilliseconds(1) },
            failureReporter);

        var import = await catalog.ImportBookInfoAsync(
            MutableUpdateSourceAdapter.SourceIdValue,
            MutableUpdateSourceAdapter.ExternalBookId);
        Assert.IsTrue(import.IsSuccess, string.Join("; ", import.Errors));
        var initialToc = await catalog.SyncChaptersAsync(
            MutableUpdateSourceAdapter.SourceIdValue,
            MutableUpdateSourceAdapter.ExternalBookId);
        Assert.IsTrue(initialToc.IsSuccess, string.Join("; ", initialToc.Errors));
        Assert.AreEqual(2, initialToc.Book!.Chapters.Count);

        var match = await matching.CreateOrMatchAsync(
            MutableUpdateSourceAdapter.SourceIdValue,
            MutableUpdateSourceAdapter.ExternalBookId);
        Assert.IsTrue(match.IsSuccess, string.Join("; ", match.Errors));
        var initialMapping = await chapterMapping.SyncChapterMappingAsync(
            MutableUpdateSourceAdapter.SourceIdValue,
            MutableUpdateSourceAdapter.ExternalBookId);
        Assert.IsTrue(initialMapping.IsSuccess, string.Join("; ", initialMapping.Errors));
        Assert.AreEqual(2, initialMapping.NewlyMappedCount);

        var updateScan = new UpdateScanService(sourceBooks, tasks, TimeProvider.System);

        // First scheduled scan establishes the durable content baseline.
        Assert.AreEqual(1, await updateScan.EnqueueTocScansAsync());
        var firstTocTask = tasks.Store.Single(task =>
            task.Payload.Capability == SourceCapability.Toc &&
            task.Status == CrawlerTaskStatus.Pending);
        Assert.IsTrue(leaseService.TryLease(firstTocTask, "automated-update-worker"));
        await tocProcessor.ProcessAsync(firstTocTask);
        Assert.AreEqual(CrawlerTaskStatus.Completed, firstTocTask.Status);

        var initialContentTasks = tasks.Store
            .Where(task => task.Payload.Capability == SourceCapability.Content)
            .ToList();
        Assert.AreEqual(2, initialContentTasks.Count);
        foreach (var contentTask in initialContentTasks)
        {
            Assert.IsTrue(leaseService.TryLease(contentTask, "automated-update-worker"));
            await contentProcessor.ProcessAsync(contentTask);
            Assert.AreEqual(CrawlerTaskStatus.Completed, contentTask.Status);
        }

        Assert.AreEqual(2, artifacts.Store.Count);
        Assert.AreEqual(2, versions.Store.Count);

        // The source changes between periodic scans. No database edit is used
        // to inject the chapter: only the source response changes.
        adapter.AddNextChapter();

        Assert.AreEqual(1, await updateScan.EnqueueTocScansAsync());
        var secondTocTask = tasks.Store.Single(task =>
            task.Payload.Capability == SourceCapability.Toc &&
            task.Status == CrawlerTaskStatus.Pending);
        Assert.IsTrue(leaseService.TryLease(secondTocTask, "automated-update-worker"));
        await tocProcessor.ProcessAsync(secondTocTask);
        Assert.AreEqual(CrawlerTaskStatus.Completed, secondTocTask.Status);

        Assert.AreEqual(3, sourceBooks.Book!.Chapters.Count);
        Assert.AreEqual(3, canonicalRepo.Book!.Chapters.Count);
        Assert.AreEqual(3, mappings.Store.Count);

        var newContentTask = tasks.Store.Single(task =>
            task.Payload.Capability == SourceCapability.Content &&
            task.Payload.Variables["chapterId"] == MutableUpdateSourceAdapter.NewChapterId &&
            task.Status == CrawlerTaskStatus.Pending);
        Assert.AreEqual("new", newContentTask.Payload.Variables["reason"]);
        Assert.IsTrue(leaseService.TryLease(newContentTask, "automated-update-worker"));
        await contentProcessor.ProcessAsync(newContentTask);
        Assert.AreEqual(CrawlerTaskStatus.Completed, newContentTask.Status);

        Assert.AreEqual(3, artifacts.Store.Count);
        Assert.AreEqual(3, versions.Store.Count);
        var newMapping = mappings.Store.Single(mapping =>
            mapping.ExternalChapterId == MutableUpdateSourceAdapter.NewChapterId);
        var readable = await query.GetChapterContentAsync(newMapping.CanonicalChapterId);
        Assert.IsNotNull(readable);
        StringAssert.Contains(readable.Paragraphs[0], "第三章由自动更新发现");

        // A subsequent scan is still allowed to re-check the source, but must
        // not duplicate content work for already fetched chapters.
        Assert.AreEqual(1, await updateScan.EnqueueTocScansAsync());
        var repeatedTocTask = tasks.Store.Single(task =>
            task.Payload.Capability == SourceCapability.Toc &&
            task.Status == CrawlerTaskStatus.Pending);
        Assert.IsTrue(leaseService.TryLease(repeatedTocTask, "automated-update-worker"));
        await tocProcessor.ProcessAsync(repeatedTocTask);
        Assert.AreEqual(CrawlerTaskStatus.Completed, repeatedTocTask.Status);
        Assert.AreEqual(3, tasks.Store.Count(task => task.Payload.Capability == SourceCapability.Content));
        Assert.AreEqual(4, adapter.TocCallCount);
        Assert.AreEqual(3, adapter.ContentCallCount);
    }

    // ---- 内存实现 ----

    private sealed class NullSourceRepository : ISourceRepository
    {
        public Task AddAsync(Source source, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<Source?> GetAsync(string sourceId, CancellationToken cancellationToken = default)
            => Task.FromResult<Source?>(null);
        public Task<IReadOnlyList<Source>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Source>>([]);
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

        public Task<CanonicalBook?> FindByTitleAuthorAsync(string title, string author, CancellationToken cancellationToken = default)
            => Task.FromResult(Book is not null && Book.Title == title && Book.Author == author ? Book : null);
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
        public Task<IReadOnlyList<ContentVersion>> ListCurrentForBookAsync(
            Guid canonicalBookId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ContentVersion>>(Store
                .Where(v => v.CanonicalBookId == canonicalBookId &&
                    _current.TryGetValue((v.CanonicalChapterId, v.CanonicalHash), out var cur) && cur)
                .ToList());
        public Task<Guid?> GetCurrentCanonicalBookIdAsync(Guid canonicalChapterId, CancellationToken cancellationToken = default)
            => Task.FromResult<Guid?>(Store.LastOrDefault(v =>
                v.CanonicalChapterId == canonicalChapterId &&
                _current.TryGetValue((canonicalChapterId, v.CanonicalHash), out var cur) && cur)?.CanonicalBookId);
        public Task SetCurrentAsync(Guid chapterId, Guid versionId, CancellationToken cancellationToken = default)
        {
            foreach (var v in Store.Where(v => v.CanonicalChapterId == chapterId))
            {
                _current[(chapterId, v.CanonicalHash)] = v.Id == versionId;
            }
            return Task.CompletedTask;
        }
    }

    private sealed class AllowAllContentPolicyReader : IContentPolicyReader
    {
        public Task<bool> IsTakedownAsync(
            Guid canonicalBookId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
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
        public Task<IReadOnlySet<string>> ListFetchedExternalChapterIdsAsync(
            string sourceId,
            IEnumerable<string> externalChapterIds,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlySet<string>>(
                new HashSet<string>(
                    Store.Where(a => a.SourceId == sourceId).Select(a => a.ExternalChapterId),
                    StringComparer.Ordinal));

        public Task<IReadOnlySet<string>> ListRecentlyFetchedExternalChapterIdsAsync(
            string sourceId,
            IEnumerable<string> externalChapterIds,
            DateTimeOffset since,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlySet<string>>(
                new HashSet<string>(
                    Store.Where(a => a.SourceId == sourceId && a.FetchedAt >= since)
                        .Select(a => a.ExternalChapterId),
                    StringComparer.Ordinal));
    }

    private sealed class MappingContentPublisher(
        InMemoryMappingRepository mappings,
        ContentPublishingService publishing) : IChainedContentPublisher
    {
        public async Task<bool> TryPublishAsync(
            string sourceId,
            string externalBookId,
            string externalChapterId,
            string rawContent,
            CancellationToken cancellationToken = default)
        {
            var mapping = await mappings
                .FindAsync(sourceId, externalChapterId, cancellationToken)
                .ConfigureAwait(false);
            if (mapping is null)
            {
                return false;
            }

            var outcome = await publishing
                .PublishAsync(
                    mapping.CanonicalBookId,
                    mapping.CanonicalChapterId,
                    sourceId,
                    rawContent,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!outcome.IsSuccess)
            {
                throw new InvalidOperationException(
                    $"live content publish failed: {string.Join("; ", outcome.Errors)}");
            }

            return true;
        }
    }

    private sealed class MutableUpdateSourceAdapter : ISourceAdapter
    {
        public const string SourceIdValue = "inkflow-update-fixture";
        public const string ExternalBookId = "update-book";
        public const string NewChapterId = "update-chapter-3";

        private readonly List<SourceTocEntry> _chapters =
        [
            new("update-chapter-1", 0, "第一章"),
            new("update-chapter-2", 1, "第二章"),
        ];

        public string SourceId => SourceIdValue;
        public int TocCallCount { get; private set; }
        public int ContentCallCount { get; private set; }

        public void AddNextChapter()
        {
            _chapters.Add(new SourceTocEntry(NewChapterId, 2, "第三章"));
        }

        public Task<IReadOnlyList<SourceSearchResult>> SearchAsync(
            string keyword,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SourceSearchResult>>([]);

        public Task<SourceBookInfo?> GetBookInfoAsync(
            string externalBookId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<SourceBookInfo?>(new("自动更新验收书", "InkFlow Automation"));

        public Task<IReadOnlyList<SourceTocEntry>> GetTableOfContentsAsync(
            string externalBookId,
            CancellationToken cancellationToken = default)
        {
            TocCallCount++;
            return Task.FromResult<IReadOnlyList<SourceTocEntry>>(_chapters.ToArray());
        }

        public Task<string?> GetChapterContentAsync(
            string externalChapterId,
            CancellationToken cancellationToken = default)
        {
            ContentCallCount++;
            var content = externalChapterId switch
            {
                "update-chapter-1" => "<p>第一章初始正文</p>",
                "update-chapter-2" => "<p>第二章初始正文</p>",
                NewChapterId => "<p>第三章由自动更新发现</p>",
                _ => null,
            };
            return Task.FromResult(content);
        }
    }

    private sealed class FixedAdapterFactory(ISourceAdapter adapter) : ISourceAdapterFactory
    {
        public Task<ISourceAdapter?> GetAdapterAsync(
            string sourceId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ISourceAdapter?>(
                adapter.SourceId == sourceId ? adapter : null);
    }

    private sealed class InMemoryCrawlerTasks : ICrawlerTaskRepository
    {
        private static readonly CrawlerTaskStatus[] BlockingStatuses =
        [
            CrawlerTaskStatus.Pending,
            CrawlerTaskStatus.Leased,
            CrawlerTaskStatus.Running,
            CrawlerTaskStatus.DeadLettered,
        ];

        public List<CrawlerTask> Store { get; } = [];
        public List<DeadLetterTask> DeadLetters { get; } = [];

        public Task AddAsync(CrawlerTask task, CancellationToken cancellationToken = default)
        {
            Store.Add(task);
            return Task.CompletedTask;
        }

        public Task<CrawlerTask?> GetAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<CrawlerTask?>(Store.SingleOrDefault(task => task.Id == id));

        public Task<CrawlerTask?> TryLeaseAsync(
            DateTimeOffset now,
            string owner,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken = default)
        {
            var task = Store.FirstOrDefault(candidate => candidate.IsLeasable(now));
            if (task is null)
            {
                return Task.FromResult<CrawlerTask?>(null);
            }

            task.Lease(owner, now, leaseDuration);
            return Task.FromResult<CrawlerTask?>(task);
        }

        public Task<CrawlerTask?> TryLeaseAsync(
            Guid taskId,
            DateTimeOffset now,
            string owner,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken = default)
        {
            var task = Store.SingleOrDefault(candidate => candidate.Id == taskId);
            if (task is null || !task.IsLeasable(now))
            {
                return Task.FromResult<CrawlerTask?>(null);
            }

            task.Lease(owner, now, leaseDuration);
            return Task.FromResult<CrawlerTask?>(task);
        }

        public Task SaveAsync(CrawlerTask task, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<CrawlerTask>> FindLeasableAsync(
            DateTimeOffset now,
            int limit,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CrawlerTask>>(
                Store.Where(task => task.IsLeasable(now)).Take(limit).ToList());

        public Task AddDeadLetterAsync(
            DeadLetterTask deadLetter,
            CancellationToken cancellationToken = default)
        {
            DeadLetters.Add(deadLetter);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<DeadLetterTask>> ListDeadLettersAsync(
            int limit,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DeadLetterTask>>(DeadLetters.Take(limit).ToList());

        public Task<bool> HasActiveTaskAsync(
            string sourceId,
            SourceCapability capability,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Store.Any(task =>
                task.Payload.SourceId == sourceId &&
                task.Payload.Capability == capability &&
                task.Status is CrawlerTaskStatus.Pending or CrawlerTaskStatus.Leased or CrawlerTaskStatus.Running));

        public Task<bool> HasConflictingTaskAsync(
            string sourceId,
            SourceCapability capability,
            string variableName,
            string variableValue,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Store.Any(task =>
                task.Payload.SourceId == sourceId &&
                task.Payload.Capability == capability &&
                BlockingStatuses.Contains(task.Status) &&
                task.Payload.Variables.TryGetValue(variableName, out var value) &&
                value == variableValue));
    }
}
