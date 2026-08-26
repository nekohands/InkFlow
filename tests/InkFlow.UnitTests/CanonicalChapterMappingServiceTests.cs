using InkFlow.Modules.Library.Application;
using InkFlow.Modules.Library.Domain;
using InkFlow.Modules.Sources.Application;
using InkFlow.Modules.Sources.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class CanonicalChapterMappingServiceTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 26, 20, 0, 0, TimeSpan.Zero);

    private sealed class InMemorySourceBookRepository(SourceBook? book) : ISourceBookRepository
    {
        public Task AddAsync(SourceBook book, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<SourceBook?> GetAsync(string sourceId, string externalBookId, CancellationToken cancellationToken = default)
            => Task.FromResult(book is not null && book.SourceId == sourceId && book.ExternalBookId == externalBookId ? book : null);
        public Task SaveAsync(SourceBook book, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class InMemoryCandidateRepository(MatchCandidate? candidate) : IMatchCandidateRepository
    {
        public Task AddAsync(MatchCandidate candidate, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<MatchCandidate?> FindForSourceBookAsync(string sourceId, string externalBookId, CancellationToken cancellationToken = default)
            => Task.FromResult(candidate);
    }

    private sealed class InMemoryCanonicalRepo : ICanonicalBookRepository
    {
        public CanonicalBook? Book { get; set; }
        public int SaveCount { get; private set; }

        public Task AddAsync(CanonicalBook book, CancellationToken cancellationToken = default)
        {
            Book = book;
            return Task.CompletedTask;
        }

        public Task<CanonicalBook?> GetAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(Book is not null && Book.Id == id ? Book : null);

        public Task SaveAsync(CanonicalBook book, CancellationToken cancellationToken = default)
        {
            SaveCount++;
            Book = book;
            return Task.CompletedTask;
        }
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

    private static (SourceBook SourceBook, CanonicalBook Canonical, IMatchCandidateRepository Candidates) Seed()
    {
        var sourceBook = SourceBook.Create("example-source", "10001", "剑来", "烽火戏诸侯", T0);
        sourceBook.SyncChapters([("c1", "第一章"), ("c2", "第二章")], T0);

        var canonical = CanonicalBook.Create("剑来", "烽火戏诸侯", T0);
        var candidate = MatchCandidate.Confirm(canonical.Id, "example-source", "10001", T0);

        return (sourceBook, canonical, new InMemoryCandidateRepository(candidate));
    }

    [TestMethod]
    public async Task First_Sync_Creates_Stable_Chapters_And_Mappings()
    {
        var (sourceBook, canonical, candidates) = Seed();
        var sourceBooks = new InMemorySourceBookRepository(sourceBook);
        var canonicalRepo = new InMemoryCanonicalRepo { Book = canonical };
        var mappings = new InMemoryMappingRepository();
        var service = new CanonicalChapterMappingService(sourceBooks, candidates, canonicalRepo, mappings);

        var outcome = await service.SyncChapterMappingAsync("example-source", "10001");

        Assert.IsTrue(outcome.IsSuccess, string.Join("; ", outcome.Errors));
        Assert.AreEqual(2, outcome.NewlyMappedCount);
        Assert.AreEqual(2, canonical.Chapters.Count);
        Assert.AreEqual(2, mappings.Store.Count);

        // 映射指向稳定 ID：来源章节 → 正典章节一一对应。
        var m1 = await mappings.FindAsync("example-source", "c1");
        Assert.IsNotNull(m1);
        Assert.AreEqual(canonical.Chapters[0].Id, m1.CanonicalChapterId);
    }

    [TestMethod]
    public async Task Second_Sync_Is_Idempotent()
    {
        var (sourceBook, canonical, candidates) = Seed();
        var sourceBooks = new InMemorySourceBookRepository(sourceBook);
        var canonicalRepo = new InMemoryCanonicalRepo { Book = canonical };
        var mappings = new InMemoryMappingRepository();
        var service = new CanonicalChapterMappingService(sourceBooks, candidates, canonicalRepo, mappings);

        await service.SyncChapterMappingAsync("example-source", "10001");
        var firstIds = canonical.Chapters.Select(c => c.Id).ToList();

        var second = await service.SyncChapterMappingAsync("example-source", "10001");

        Assert.IsTrue(second.IsSuccess);
        Assert.AreEqual(0, second.NewlyMappedCount);
        CollectionAssert.AreEquivalent(
            firstIds.ToList(), canonical.Chapters.Select(c => c.Id).ToList());
    }

    [TestMethod]
    public async Task Incremental_Chapters_Get_Appended_On_Later_Sync()
    {
        var (sourceBook, canonical, candidates) = Seed();
        var sourceBooks = new InMemorySourceBookRepository(sourceBook);
        var canonicalRepo = new InMemoryCanonicalRepo { Book = canonical };
        var mappings = new InMemoryMappingRepository();
        var service = new CanonicalChapterMappingService(sourceBooks, candidates, canonicalRepo, mappings);

        await service.SyncChapterMappingAsync("example-source", "10001");

        // 来源新增一章后再次同步:只有新章被映射,旧映射不动。
        sourceBook.SyncChapters([("c3", "第三章")], T0.AddMinutes(5));
        var outcome = await service.SyncChapterMappingAsync("example-source", "10001");

        Assert.IsTrue(outcome.IsSuccess);
        Assert.AreEqual(1, outcome.NewlyMappedCount);
        Assert.AreEqual(3, canonical.Chapters.Count);
        Assert.AreEqual(3, mappings.Store.Count);
    }

    [TestMethod]
    public async Task Missing_Book_Match_Fails_Clearly()
    {
        var sourceBook = SourceBook.Create("example-source", "10001", "剑来", "作者", T0);
        var service = new CanonicalChapterMappingService(
            new InMemorySourceBookRepository(sourceBook),
            new InMemoryCandidateRepository(null),
            new InMemoryCanonicalRepo(),
            new InMemoryMappingRepository());

        var outcome = await service.SyncChapterMappingAsync("example-source", "10001");

        Assert.IsFalse(outcome.IsSuccess);
        StringAssert.Contains(outcome.Errors[0], "no confirmed canonical match");
    }
}
