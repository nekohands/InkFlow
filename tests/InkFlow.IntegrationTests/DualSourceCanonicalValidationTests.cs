using InkFlow.Modules.Content.Application;
using InkFlow.Modules.Content.Domain;
using InkFlow.Modules.Library.Application;
using InkFlow.Modules.Library.Domain;
using InkFlow.Modules.Sources.Application;
using InkFlow.Modules.Sources.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.IntegrationTests;

/// <summary>
/// Phase 1B 双来源验证：使用确定性 Official Source 夹具，验证跨源身份复用、章节对齐和质量选优。
/// 不触网、不依赖 Docker；真实来源与 Legado 真机验收另行执行。
/// </summary>
[TestClass]
public sealed class DualSourceCanonicalValidationTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 27, 16, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task Two_SourceBooks_Reuse_CanonicalBook_And_CanonicalChapter_Identities()
    {
        var sourceBooks = new InMemorySourceBookRepository();
        var canonicalBooks = new InMemoryCanonicalBookRepository();
        var candidates = new InMemoryMatchCandidateRepository();
        var mappings = new InMemoryChapterMappingRepository();
        var catalog = new SourceCatalogService(
            new FixtureAdapterFactory(
                new FixtureOfficialSourceAdapter(
                    "official-a",
                    new SourceBookInfo("同一本书", "同一作者"),
                    [
                        new SourceTocEntry("a-ch-1", 0, "第一章"),
                        new SourceTocEntry("a-ch-2", 1, "第二章"),
                    ]),
                new FixtureOfficialSourceAdapter(
                    "official-b",
                    new SourceBookInfo("同 一本书", "同 一作者"),
                    [
                        new SourceTocEntry("b-ch-1", 0, "第 一 章"),
                        new SourceTocEntry("b-ch-2", 1, "第 二 章"),
                    ])),
            sourceBooks,
            TimeProvider.System);

        await ImportAndSyncAsync(catalog, "official-a", "book-a");
        await ImportAndSyncAsync(catalog, "official-b", "book-b");

        var matching = new CanonicalBookMatchingService(sourceBooks, canonicalBooks, candidates);
        var firstMatch = await matching.CreateOrMatchAsync("official-a", "book-a");
        var secondMatch = await matching.CreateOrMatchAsync("official-b", "book-b");

        Assert.IsTrue(firstMatch.IsSuccess);
        Assert.IsTrue(secondMatch.IsSuccess);
        Assert.IsTrue(firstMatch.NewlyCreated);
        Assert.IsFalse(secondMatch.NewlyCreated, "同名同作者的第二来源应复用正典书");
        Assert.AreEqual(firstMatch.Book!.Id, secondMatch.Book!.Id);
        Assert.AreEqual(1, canonicalBooks.Store.Count);
        Assert.AreEqual(2, candidates.Store.Count);

        var chapterMapping = new CanonicalChapterMappingService(
            sourceBooks, candidates, canonicalBooks, mappings);
        var firstMapping = await chapterMapping.SyncChapterMappingAsync("official-a", "book-a");
        var secondMapping = await chapterMapping.SyncChapterMappingAsync("official-b", "book-b");

        Assert.AreEqual(2, firstMapping.NewlyMappedCount);
        Assert.AreEqual(2, secondMapping.NewlyMappedCount);
        Assert.AreEqual(2, canonicalBooks.Store.Values.Single().Chapters.Count,
            "第二来源不得为相同逻辑章节创建重复 CanonicalChapter");
        Assert.AreEqual(4, mappings.Store.Count);

        var firstSourceMappings = mappings.Store
            .Where(m => m.SourceId == "official-a")
            .OrderBy(m => m.ExternalChapterId)
            .ToList();
        var secondSourceMappings = mappings.Store
            .Where(m => m.SourceId == "official-b")
            .OrderBy(m => m.ExternalChapterId)
            .ToList();

        Assert.AreEqual(firstSourceMappings[0].CanonicalChapterId, secondSourceMappings[0].CanonicalChapterId);
        Assert.AreEqual(firstSourceMappings[1].CanonicalChapterId, secondSourceMappings[1].CanonicalChapterId);
        Assert.IsTrue(secondSourceMappings.All(m =>
            m.AlignmentAlgorithmVersion == ChapterAlignmentAlgorithm.Version));
        Assert.IsTrue(secondSourceMappings.All(m => m.AlignmentEvidence.Contains("normalized-title")));
        Assert.IsTrue(mappings.Store
            .GroupBy(m => m.CanonicalChapterId)
            .All(group => group.Count() == 2),
            "每个正典章节都应拥有两个来源章节候选");
    }

    [TestMethod]
    public async Task Lower_Quality_Second_Source_Does_Not_Replace_Selected_Content()
    {
        var canonicalBooks = new InMemoryCanonicalBookRepository();
        var book = CanonicalBook.Create("同一本书", "同一作者", T0);
        var chapter = book.AddChapter(0, "第一章", T0);
        await canonicalBooks.AddAsync(book);

        var versions = new InMemoryVersionRepository();
        var sourceHealth = new InMemorySourceHealthReader();
        var decisions = new InMemoryDecisionRepository();
        var selector = new ContentSelectionService(
            versions,
            sourceHealth,
            decisions,
            new FixedClock(T0.AddMinutes(10)));
        var publishing = new ContentPublishingService(versions, selector);
        var good = await publishing.PublishAsync(
            book.Id,
            chapter.Id,
            "official-a",
            RichContent());
        var degraded = await publishing.PublishAsync(
            book.Id,
            chapter.Id,
            "official-b",
            "<p>截断正文</p>");

        Assert.IsTrue(good.IsSuccess);
        Assert.IsTrue(degraded.IsSuccess);
        Assert.AreEqual(2, versions.Store.Count);
        var degradedCandidate = versions.Store.Single(version => version.SourceId == "official-b");
        Assert.IsTrue(
            good.Version!.QualityScore > degradedCandidate.QualityScore,
            $"good score={good.Version.QualityScore}, degraded score={degradedCandidate.QualityScore}");
        Assert.AreEqual(QualityEngine.AlgorithmVersion, good.Version.QualityAlgorithmVersion);
        StringAssert.Contains(good.Version.QualityEvidence, "paragraphs=3");
        Assert.AreEqual(QualityEngine.AlgorithmVersion, degradedCandidate.QualityAlgorithmVersion);
        Assert.AreEqual(good.Version.Id, versions.CurrentVersionId,
            "低质量来源不得替换已选中的高质量正文");

        sourceHealth.Unavailable.Add("official-a");
        var failover = await selector.SelectCurrentAsync(chapter.Id);
        Assert.IsTrue(failover.IsSuccess, string.Join("; ", failover.Errors));
        Assert.IsTrue(failover.Changed);
        Assert.IsFalse(failover.UsedFallback);
        Assert.AreEqual(degradedCandidate.Id, failover.SelectedVersion!.Id,
            "高质量来源不可用时应切换到仍可用的第二来源");
        StringAssert.Contains(failover.Evidence, "excludedSources=official-a");
        Assert.AreEqual(degradedCandidate.Id, decisions.Store.Last().SelectedVersionId);

        var query = new CatalogQueryService(canonicalBooks, versions, new AllowAllContentPolicyReader());
        var readable = await query
            .GetChapterContentAsync(chapter.Id);
        Assert.IsNotNull(readable, "来源暂时不可用时，阅读路径仍应读取已选 Canonical Content");
        Assert.AreEqual(
            degradedCandidate.CanonicalText,
            string.Join("\n\n", readable!.Paragraphs));

        sourceHealth.Unavailable.Remove("official-a");
        var recovery = await selector.SelectCurrentAsync(chapter.Id);
        Assert.IsTrue(recovery.IsSuccess, string.Join("; ", recovery.Errors));
        Assert.IsTrue(recovery.Changed);
        Assert.AreEqual(good.Version.Id, recovery.SelectedVersion!.Id,
            "来源恢复后应重新进入可用候选");
    }

    private static async Task ImportAndSyncAsync(
        SourceCatalogService catalog, string sourceId, string externalBookId)
    {
        var imported = await catalog.ImportBookInfoAsync(sourceId, externalBookId);
        Assert.IsTrue(imported.IsSuccess, string.Join("; ", imported.Errors));
        var synced = await catalog.SyncChaptersAsync(sourceId, externalBookId);
        Assert.IsTrue(synced.IsSuccess, string.Join("; ", synced.Errors));
    }

    private static string RichContent() =>
        $"<p>{new string('字', 120)}</p>" +
        $"<p>{new string('字', 120)}</p>" +
        $"<p>{new string('字', 120)}</p>";

    private sealed class InMemorySourceHealthReader : ISourceHealthReader
    {
        public HashSet<string> Unavailable { get; } = new(StringComparer.Ordinal);

        public Task<bool> IsAvailableAsync(
            string sourceId,
            SourceCapability capability,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(!Unavailable.Contains(sourceId));
    }

    private sealed class InMemoryDecisionRepository : IContentSelectionDecisionRepository
    {
        public List<ContentSelectionDecision> Store { get; } = [];

        public Task AddAsync(
            ContentSelectionDecision decision,
            CancellationToken cancellationToken = default)
        {
            Store.Add(decision);
            return Task.CompletedTask;
        }

        public Task<ContentSelectionDecision?> GetLatestAsync(
            Guid canonicalChapterId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ContentSelectionDecision?>(Store
                .Where(decision => decision.CanonicalChapterId == canonicalChapterId)
                .OrderByDescending(decision => decision.CreatedAt)
                .FirstOrDefault());
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FixtureOfficialSourceAdapter(
        string sourceId,
        SourceBookInfo info,
        IReadOnlyList<SourceTocEntry> toc) : ISourceAdapter
    {
        public string SourceId => sourceId;

        public Task<IReadOnlyList<SourceSearchResult>> SearchAsync(
            string keyword, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SourceSearchResult>>([]);

        public Task<SourceBookInfo?> GetBookInfoAsync(
            string externalBookId, CancellationToken cancellationToken = default) =>
            Task.FromResult<SourceBookInfo?>(info);

        public Task<IReadOnlyList<SourceTocEntry>> GetTableOfContentsAsync(
            string externalBookId, CancellationToken cancellationToken = default) =>
            Task.FromResult(toc);

        public Task<string?> GetChapterContentAsync(
            string externalChapterId, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>("<p>fixture content</p>");
    }

    private sealed class FixtureAdapterFactory : ISourceAdapterFactory
    {
        private readonly IReadOnlyDictionary<string, ISourceAdapter> _adapters;

        public FixtureAdapterFactory(params ISourceAdapter[] adapters) =>
            _adapters = adapters.ToDictionary(a => a.SourceId, StringComparer.Ordinal);

        public Task<ISourceAdapter?> GetAdapterAsync(
            string sourceId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_adapters.TryGetValue(sourceId, out var adapter) ? adapter : null);
    }

    private sealed class InMemorySourceBookRepository : ISourceBookRepository
    {
        public Dictionary<(string SourceId, string ExternalBookId), SourceBook> Store { get; } = [];

        public Task AddAsync(SourceBook book, CancellationToken cancellationToken = default)
        {
            Store[(book.SourceId, book.ExternalBookId)] = book;
            return Task.CompletedTask;
        }

        public Task<SourceBook?> GetAsync(
            string sourceId, string externalBookId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Store.TryGetValue((sourceId, externalBookId), out var book) ? book : null);

        public Task<IReadOnlyList<SourceBook>> ListAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SourceBook>>(Store.Values.ToList());

        public Task SaveAsync(SourceBook book, CancellationToken cancellationToken = default)
        {
            Store[(book.SourceId, book.ExternalBookId)] = book;
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryCanonicalBookRepository : ICanonicalBookRepository
    {
        public Dictionary<Guid, CanonicalBook> Store { get; } = [];

        public Task AddAsync(CanonicalBook book, CancellationToken cancellationToken = default)
        {
            Store[book.Id] = book;
            return Task.CompletedTask;
        }

        public Task<CanonicalBook?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Store.TryGetValue(id, out var book) ? book : null);

        public Task<IReadOnlyList<CanonicalBook>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CanonicalBook>>(Store.Values.ToList());

        public Task<CanonicalBook?> FindByTitleAuthorAsync(
            string title, string author, CancellationToken cancellationToken = default)
        {
            var normalizedTitle = Normalize(title);
            var normalizedAuthor = Normalize(author);
            return Task.FromResult<CanonicalBook?>(Store.Values.FirstOrDefault(book =>
                Normalize(book.Title) == normalizedTitle && Normalize(book.Author) == normalizedAuthor));
        }

        public Task SaveAsync(CanonicalBook book, CancellationToken cancellationToken = default)
        {
            Store[book.Id] = book;
            return Task.CompletedTask;
        }

        private static string Normalize(string value) =>
            string.Concat(value.Where(c => !char.IsWhiteSpace(c))).ToLowerInvariant();
    }

    private sealed class InMemoryMatchCandidateRepository : IMatchCandidateRepository
    {
        public Dictionary<(string SourceId, string ExternalBookId), MatchCandidate> Store { get; } = [];

        public Task AddAsync(MatchCandidate candidate, CancellationToken cancellationToken = default)
        {
            Store[(candidate.SourceId, candidate.ExternalBookId)] = candidate;
            return Task.CompletedTask;
        }

        public Task<MatchCandidate?> FindForSourceBookAsync(
            string sourceId, string externalBookId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Store.TryGetValue((sourceId, externalBookId), out var candidate) ? candidate : null);
    }

    private sealed class InMemoryChapterMappingRepository : IChapterMappingRepository
    {
        public List<ChapterMapping> Store { get; } = [];

        public Task AddAsync(ChapterMapping mapping, CancellationToken cancellationToken = default)
        {
            Store.Add(mapping);
            return Task.CompletedTask;
        }

        public Task<ChapterMapping?> FindAsync(
            string sourceId, string externalChapterId, CancellationToken cancellationToken = default) =>
            Task.FromResult<ChapterMapping?>(Store.FirstOrDefault(mapping =>
                mapping.SourceId == sourceId && mapping.ExternalChapterId == externalChapterId));
    }

    private sealed class InMemoryVersionRepository : IContentVersionRepository
    {
        public List<ContentVersion> Store { get; } = [];
        public Guid? CurrentVersionId { get; private set; }

        public Task AddAsync(ContentVersion version, CancellationToken cancellationToken = default)
        {
            Store.Add(version);
            return Task.CompletedTask;
        }

        public Task<ContentVersion?> FindByHashAsync(
            Guid canonicalChapterId, string canonicalHash, CancellationToken cancellationToken = default) =>
            Task.FromResult<ContentVersion?>(Store.FirstOrDefault(version =>
                version.CanonicalChapterId == canonicalChapterId && version.CanonicalHash == canonicalHash));

        public Task<IReadOnlyList<ContentVersion>> ListForChapterAsync(
            Guid canonicalChapterId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ContentVersion>>(Store
                .Where(version => version.CanonicalChapterId == canonicalChapterId)
                .ToList());

        public Task<ContentVersion?> GetCurrentForChapterAsync(
            Guid canonicalChapterId, CancellationToken cancellationToken = default) =>
            Task.FromResult<ContentVersion?>(Store.FirstOrDefault(version =>
                version.CanonicalChapterId == canonicalChapterId && version.IsCurrent));

        public Task<IReadOnlyList<ContentVersion>> ListCurrentForBookAsync(
            Guid canonicalBookId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ContentVersion>>(Store
                .Where(version => version.CanonicalBookId == canonicalBookId && version.IsCurrent)
                .ToList());

        public Task<Guid?> GetCurrentCanonicalBookIdAsync(
            Guid canonicalChapterId, CancellationToken cancellationToken = default) =>
            Task.FromResult<Guid?>(Store.FirstOrDefault(version =>
                version.CanonicalChapterId == canonicalChapterId && version.IsCurrent)?.CanonicalBookId);

        public Task SetCurrentAsync(
            Guid chapterId, Guid versionId, CancellationToken cancellationToken = default)
        {
            for (var i = 0; i < Store.Count; i++)
            {
                var version = Store[i];
                Store[i] = ContentVersion.Rehydrate(
                    version.Id,
                    version.CanonicalBookId,
                    version.CanonicalChapterId,
                    version.SourceId,
                    version.CanonicalHash,
                    version.CanonicalText,
                    version.ParagraphCount,
                    version.QualityScore,
                    version.Id == versionId,
                    version.CreatedAt,
                    version.QualityAlgorithmVersion,
                    version.QualityEvidence);
            }

            CurrentVersionId = versionId;
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
}
