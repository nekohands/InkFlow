using InkFlow.Modules.Library.Application;
using InkFlow.Modules.Library.Domain;
using InkFlow.Modules.Sources.Application;
using InkFlow.Modules.Sources.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class CanonicalBookMatchingServiceTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 26, 18, 0, 0, TimeSpan.Zero);

    private sealed class InMemoryBookRepository : ICanonicalBookRepository
    {
        public Dictionary<Guid, CanonicalBook> Store { get; } = new();

        public Task AddAsync(CanonicalBook book, CancellationToken cancellationToken = default)
        {
            Store[book.Id] = book;
            return Task.CompletedTask;
        }

        public Task<CanonicalBook?> GetAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(Store.TryGetValue(id, out var book) ? book : null);

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

    private sealed class InMemorySourceBookRepository : ISourceBookRepository
    {
        public SourceBook? Book { get; set; } =
            SourceBook.Create("example-source", "10001", "剑来", "烽火戏诸侯", T0);

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

    [TestMethod]
    public async Task First_Match_Creates_New_Canonical_Book_With_Confirmed_Candidate()
    {
        var books = new InMemoryBookRepository();
        var candidates = new InMemoryCandidateRepository();
        var service = new CanonicalBookMatchingService(new InMemorySourceBookRepository(), books, candidates);

        var outcome = await service.CreateOrMatchAsync("example-source", "10001");

        Assert.IsTrue(outcome.IsSuccess);
        Assert.IsTrue(outcome.NewlyCreated);
        Assert.AreEqual("剑来", outcome.Book!.Title);

        // 候选已确认且指向新正典书
        var candidate = await candidates.FindForSourceBookAsync("example-source", "10001");
        Assert.IsNotNull(candidate);
        Assert.AreEqual(MatchCandidateStatus.Confirmed, candidate.Status);
        Assert.AreEqual(outcome.Book.Id, candidate.CanonicalBookId);
    }

    [TestMethod]
    public async Task Second_Match_Is_Idempotent_Returning_Same_Book()
    {
        var books = new InMemoryBookRepository();
        var candidates = new InMemoryCandidateRepository();
        var service = new CanonicalBookMatchingService(new InMemorySourceBookRepository(), books, candidates);

        var first = await service.CreateOrMatchAsync("example-source", "10001");
        var second = await service.CreateOrMatchAsync("example-source", "10001");

        Assert.IsTrue(second.IsSuccess);
        Assert.IsFalse(second.NewlyCreated);
        Assert.AreEqual(first.Book!.Id, second.Book!.Id);
        Assert.AreEqual(1, books.Store.Count, "幂等匹配不得创建第二本正典书");
        Assert.AreEqual(1, candidates.Store.Count);
    }

    [TestMethod]
    public async Task Missing_Source_Book_Fails_Clearly()
    {
        var service = new CanonicalBookMatchingService(
            new InMemorySourceBookRepository { Book = null },
            new InMemoryBookRepository(),
            new InMemoryCandidateRepository());

        var outcome = await service.CreateOrMatchAsync("ghost-source", "999");

        Assert.IsFalse(outcome.IsSuccess);
        StringAssert.Contains(outcome.Errors[0], "does not exist");
    }
}
