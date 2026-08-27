using InkFlow.Modules.Content.Application;
using InkFlow.Modules.Library.Application;
using InkFlow.Modules.Library.Domain;
using InkFlow.Modules.Reading.Application;
using InkFlow.Modules.Reading.Domain;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class ReadingStateTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 28, 16, 0, 0, TimeSpan.Zero);
    private static readonly Guid UserA = Guid.Parse("018f1b3a-9c0a-7b31-8a2e-0123456789ab");
    private static readonly Guid UserB = Guid.Parse("018f1b3a-9c0a-7b32-8a2e-0123456789ab");

    [TestMethod]
    public void Domain_Rejects_Invalid_Progress_And_Preference_Values()
    {
        var bookId = Guid.CreateVersion7();
        var chapterId = Guid.CreateVersion7();

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            ReadingProgress.Create(UserA, bookId, chapterId, -1, 10, T0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            ReadingProgress.Create(UserA, bookId, chapterId, 0, 101, T0));

        var preference = ReaderPreference.CreateDefault(UserA, T0);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            preference.Update(79, preference.LineHeightPercent, preference.Theme, T0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            preference.Update(preference.FontSizePercent, 241, preference.Theme, T0));
    }

    [TestMethod]
    public async Task User_State_Is_Isolated_And_Progress_Updates_Current_Chapter()
    {
        var book = CreateBook();
        var books = new InMemoryBooks(book);
        var repository = new InMemoryReadingRepository();
        var service = CreateService(repository, books);

        var added = await service.PutShelfAsync(UserA, book.Id, ShelfStatus.Reading);
        Assert.IsTrue(added.IsSuccess);

        var firstChapter = book.Chapters[0];
        var secondChapter = book.Chapters[1];
        var first = await service.SaveProgressAsync(
            UserA, book.Id, firstChapter.Id, paragraphIndex: 2, progressPercent: 25);
        Assert.IsTrue(first.IsSuccess);

        var second = await service.SaveProgressAsync(
            UserA, book.Id, secondChapter.Id, paragraphIndex: 4, progressPercent: 60);
        Assert.IsTrue(second.IsSuccess);
        Assert.AreEqual(secondChapter.Id, second.Value!.ChapterId);

        var shelf = await service.ListShelfAsync(UserA, 1_000);
        Assert.AreEqual(1, shelf.Count);
        Assert.AreEqual(secondChapter.Id, shelf[0].CurrentChapterId);
        Assert.AreEqual(60, shelf[0].ProgressPercent);

        var history = await service.ListHistoryAsync(UserA, 1_000);
        Assert.AreEqual(2, history.Count);
        CollectionAssert.Contains(
            history.Select(item => item.ChapterId).ToList(),
            secondChapter.Id);

        Assert.AreEqual(0, (await service.ListShelfAsync(UserB, 100)).Count);
        Assert.AreEqual(0, (await service.ListHistoryAsync(UserB, 100)).Count);
        Assert.IsNull(await service.GetProgressAsync(UserB, book.Id));
    }

    [TestMethod]
    public async Task Takedown_Hides_Book_And_Blocks_User_State_Writes()
    {
        var book = CreateBook();
        var books = new InMemoryBooks(book);
        var service = CreateService(
            new InMemoryReadingRepository(),
            books,
            new SelectivePolicy(book.Id));

        var shelf = await service.PutShelfAsync(UserA, book.Id, ShelfStatus.Reading);
        Assert.AreEqual(ReadingResultStatus.NotFound, shelf.Status);
        var progress = await service.SaveProgressAsync(
            UserA, book.Id, book.Chapters[0].Id, paragraphIndex: 0, progressPercent: 1);
        Assert.AreEqual(ReadingResultStatus.NotFound, progress.Status);
    }

    [TestMethod]
    public async Task Preferences_Return_Defaults_And_Allow_Validated_Partial_Update()
    {
        var repository = new InMemoryReadingRepository();
        var service = CreateService(repository, new InMemoryBooks(CreateBook()));

        var defaults = await service.GetPreferencesAsync(UserA);
        Assert.AreEqual(ReaderPreference.DefaultFontSizePercent, defaults.FontSizePercent);
        Assert.AreEqual(ReaderPreference.DefaultLineHeightPercent, defaults.LineHeightPercent);
        Assert.AreEqual(nameof(ReaderTheme.System), defaults.Theme);

        var updated = await service.UpdatePreferencesAsync(
            UserA,
            fontSizePercent: 120,
            lineHeightPercent: null,
            theme: ReaderTheme.Sepia);
        Assert.IsTrue(updated.IsSuccess);
        Assert.AreEqual(120, updated.Value!.FontSizePercent);
        Assert.AreEqual(ReaderPreference.DefaultLineHeightPercent, updated.Value.LineHeightPercent);
        Assert.AreEqual(nameof(ReaderTheme.Sepia), updated.Value.Theme);

        var invalid = await service.UpdatePreferencesAsync(UserA, null, null, null);
        Assert.AreEqual(ReadingResultStatus.InvalidRequest, invalid.Status);
        var otherUser = await service.GetPreferencesAsync(UserB);
        Assert.AreEqual(nameof(ReaderTheme.System), otherUser.Theme);
    }

    [TestMethod]
    public async Task List_Queries_Clamp_User_Requested_Limit()
    {
        var repository = new InMemoryReadingRepository();
        var service = CreateService(repository, new InMemoryBooks(CreateBook()));

        await service.ListShelfAsync(UserA, int.MaxValue);
        await service.ListHistoryAsync(UserA, int.MinValue);

        Assert.AreEqual(ReadingStateService.MaxPageSize, repository.LastShelfLimit);
        Assert.AreEqual(1, repository.LastHistoryLimit);
    }

    private static ReadingStateService CreateService(
        InMemoryReadingRepository repository,
        InMemoryBooks books,
        IContentPolicyReader? policy = null) =>
        new(repository, books, policy ?? new AllowAllPolicy(), new FixedClock(T0));

    private static CanonicalBook CreateBook()
    {
        var book = CanonicalBook.Create("测试书", "测试作者", T0);
        book.AddChapter(0, "第一章", T0);
        book.AddChapter(1, "第二章", T0.AddMinutes(1));
        return book;
    }

    private sealed class InMemoryReadingRepository : IReadingStateRepository
    {
        private readonly Dictionary<(Guid UserId, Guid BookId), BookshelfEntry> _shelf = [];
        private readonly Dictionary<(Guid UserId, Guid BookId), ReadingProgress> _progress = [];
        private readonly Dictionary<(Guid UserId, Guid BookId, Guid ChapterId), ReadingHistoryEntry> _history = [];
        private readonly Dictionary<Guid, ReaderPreference> _preferences = [];

        public int LastShelfLimit { get; private set; }
        public int LastHistoryLimit { get; private set; }

        public Task<BookshelfEntry?> GetShelfEntryAsync(Guid userId, Guid canonicalBookId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_shelf.GetValueOrDefault((userId, canonicalBookId)));

        public Task<IReadOnlyList<BookshelfEntry>> ListShelfAsync(Guid userId, int limit, CancellationToken cancellationToken = default)
        {
            LastShelfLimit = limit;
            return Task.FromResult<IReadOnlyList<BookshelfEntry>>(_shelf
                .Where(pair => pair.Key.UserId == userId)
                .Select(pair => pair.Value)
                .Take(limit)
                .ToList());
        }

        public Task UpsertShelfAsync(BookshelfEntry entry, CancellationToken cancellationToken = default)
        {
            _shelf[(entry.UserId, entry.CanonicalBookId)] = entry;
            return Task.CompletedTask;
        }

        public Task RemoveShelfAsync(Guid userId, Guid canonicalBookId, CancellationToken cancellationToken = default)
        {
            _shelf.Remove((userId, canonicalBookId));
            return Task.CompletedTask;
        }

        public Task<ReadingProgress?> GetProgressAsync(Guid userId, Guid canonicalBookId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_progress.GetValueOrDefault((userId, canonicalBookId)));

        public Task<ReadingHistoryEntry?> GetHistoryAsync(Guid userId, Guid canonicalBookId, Guid canonicalChapterId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_history.GetValueOrDefault((userId, canonicalBookId, canonicalChapterId)));

        public Task SaveProgressAsync(ReadingProgress progress, ReadingHistoryEntry history, CancellationToken cancellationToken = default)
        {
            _progress[(progress.UserId, progress.CanonicalBookId)] = progress;
            _history[(history.UserId, history.CanonicalBookId, history.CanonicalChapterId)] = history;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ReadingHistoryEntry>> ListHistoryAsync(Guid userId, int limit, CancellationToken cancellationToken = default)
        {
            LastHistoryLimit = limit;
            return Task.FromResult<IReadOnlyList<ReadingHistoryEntry>>(_history
                .Where(pair => pair.Key.UserId == userId)
                .OrderByDescending(pair => pair.Value.LastReadAt)
                .Select(pair => pair.Value)
                .Take(limit)
                .ToList());
        }

        public Task<ReaderPreference?> GetPreferencesAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_preferences.GetValueOrDefault(userId));

        public Task UpsertPreferencesAsync(ReaderPreference preference, CancellationToken cancellationToken = default)
        {
            _preferences[preference.UserId] = preference;
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryBooks(CanonicalBook book) : ICanonicalBookRepository
    {
        public Task AddAsync(CanonicalBook value, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<CanonicalBook?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<CanonicalBook?>(id == book.Id ? book : null);

        public Task<IReadOnlyList<CanonicalBook>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CanonicalBook>>([book]);

        public Task<CanonicalBook?> FindByTitleAuthorAsync(string title, string author, CancellationToken cancellationToken = default) =>
            Task.FromResult<CanonicalBook?>(null);

        public Task SaveAsync(CanonicalBook value, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class AllowAllPolicy : IContentPolicyReader
    {
        public Task<bool> IsTakedownAsync(Guid canonicalBookId, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }

    private sealed class SelectivePolicy(Guid takenDownBookId) : IContentPolicyReader
    {
        public Task<bool> IsTakedownAsync(Guid canonicalBookId, CancellationToken cancellationToken = default) =>
            Task.FromResult(canonicalBookId == takenDownBookId);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
