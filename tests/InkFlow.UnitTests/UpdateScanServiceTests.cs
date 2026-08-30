using InkFlow.Modules.Crawling.Application;
using InkFlow.Modules.Crawling.Domain;
using InkFlow.Modules.Sources.Application;
using InkFlow.Modules.Sources.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class UpdateScanServiceTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 27, 21, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task Unavailable_Source_Is_Skipped_By_Update_Scan()
    {
        var sourceBooks = new InMemorySourceBookRepository(
            SourceBook.Create("offline-source", "book-1", "离线书", "作者", T0),
            SourceBook.Create("healthy-source", "book-2", "在线书", "作者", T0));
        var tasks = new InMemoryTaskRepository();
        var health = new InMemoryHealthReader("offline-source");
        var scanner = new UpdateScanService(
            sourceBooks,
            tasks,
            new FixedClock(T0),
            health);

        var count = await scanner.EnqueueTocScansAsync();

        Assert.AreEqual(1, count);
        Assert.AreEqual("healthy-source", tasks.Store.Single().Payload.SourceId);
    }

    private sealed class InMemorySourceBookRepository(params SourceBook[] books) : ISourceBookRepository
    {
        private readonly IReadOnlyList<SourceBook> _books = books;

        public Task AddAsync(SourceBook book, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<SourceBook?> GetAsync(
            string sourceId,
            string externalBookId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<SourceBook?>(_books.SingleOrDefault(book =>
                book.SourceId == sourceId && book.ExternalBookId == externalBookId));

        public Task<IReadOnlyList<SourceBook>> ListAllAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_books);

        public Task SaveAsync(SourceBook book, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class InMemoryTaskRepository : ICrawlerTaskRepository
    {
        public List<CrawlerTask> Store { get; } = [];

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
            CancellationToken cancellationToken = default) =>
            Task.FromResult<CrawlerTask?>(null);

        public Task<CrawlerTask?> TryLeaseAsync(
            Guid taskId,
            DateTimeOffset now,
            string owner,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<CrawlerTask?>(null);

        public Task SaveAsync(CrawlerTask task, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<CrawlerTask>> FindLeasableAsync(
            DateTimeOffset now,
            int limit,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CrawlerTask>>([]);

        public Task AddDeadLetterAsync(
            DeadLetterTask deadLetter,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<DeadLetterTask>> ListDeadLettersAsync(
            int limit,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DeadLetterTask>>([]);

        public Task<bool> HasActiveTaskAsync(
            string sourceId,
            SourceCapability capability,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> HasConflictingTaskAsync(
            string sourceId,
            SourceCapability capability,
            string variableName,
            string variableValue,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }

    private sealed class InMemoryHealthReader(params string[] unavailable) : ISourceHealthReader
    {
        private readonly HashSet<string> _unavailable = unavailable.ToHashSet(StringComparer.Ordinal);

        public Task<bool> IsAvailableAsync(
            string sourceId,
            SourceCapability capability,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(!_unavailable.Contains(sourceId));
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
