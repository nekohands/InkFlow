using InkFlow.Modules.Library.Application;
using InkFlow.Modules.Library.Domain;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class PrivateLibraryServiceTests
{
    private static readonly Guid UserId = Guid.Parse("01908d2a-2d44-7b3b-9ec2-123456789abc");
    private static readonly DateTimeOffset T0 = new(2026, 8, 28, 19, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task Create_And_Update_Use_Authenticated_User_Scope()
    {
        var repository = new InMemoryPrivateBookRepository();
        var service = new PrivateLibraryService(repository, new FixedClock(T0));

        var created = await service.CreateAsync(UserId, "书", null);
        Assert.AreEqual(PrivateLibraryResultStatus.Success, created.Status);
        Assert.IsNotNull(created.Value);

        var updated = await service.UpdateAsync(
            UserId,
            created.Value!.PrivateBookId,
            "新书名",
            "作者");

        Assert.AreEqual(PrivateLibraryResultStatus.Success, updated.Status);
        Assert.AreEqual("新书名", updated.Value!.Title);
        Assert.AreEqual(UserId, repository.LastReadUserId);
    }

    [TestMethod]
    public async Task Other_User_Cannot_Read_Update_Or_Delete_Book()
    {
        var repository = new InMemoryPrivateBookRepository();
        var service = new PrivateLibraryService(repository, new FixedClock(T0));
        var created = await service.CreateAsync(UserId, "私有书", null);
        var bookId = created.Value!.PrivateBookId;
        var otherUser = Guid.CreateVersion7();

        Assert.IsNull(await service.GetAsync(otherUser, bookId));
        var updated = await service.UpdateAsync(otherUser, bookId, "越权", null);
        Assert.AreEqual(PrivateLibraryResultStatus.NotFound, updated.Status);
        Assert.AreEqual(
            PrivateLibraryResultStatus.NotFound,
            await service.DeleteAsync(otherUser, bookId));
        Assert.AreEqual("私有书", (await service.GetAsync(UserId, bookId))!.Title);
    }

    [TestMethod]
    public async Task List_Clamps_Page_Size_And_Invalid_User_Cannot_Query()
    {
        var repository = new InMemoryPrivateBookRepository();
        var service = new PrivateLibraryService(repository, new FixedClock(T0));
        await service.CreateAsync(UserId, "第一本", null);
        await service.CreateAsync(UserId, "第二本", null);

        var books = await service.ListAsync(UserId, int.MaxValue);
        Assert.AreEqual(2, books.Count);
        Assert.AreEqual(0, (await service.ListAsync(Guid.Empty, 10)).Count);
        Assert.AreEqual(PrivateLibraryResultStatus.NotFound,
            await service.DeleteAsync(UserId, Guid.Empty));
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class InMemoryPrivateBookRepository : IPrivateBookRepository
    {
        private readonly Dictionary<(Guid UserId, Guid BookId), PrivateBook> _books = [];

        public Guid LastReadUserId { get; private set; }

        public Task AddAsync(PrivateBook book, CancellationToken cancellationToken = default)
        {
            _books[(book.UserId, book.Id)] = book;
            return Task.CompletedTask;
        }

        public Task<PrivateBook?> GetAsync(
            Guid userId,
            Guid privateBookId,
            CancellationToken cancellationToken = default)
        {
            LastReadUserId = userId;
            _books.TryGetValue((userId, privateBookId), out var book);
            return Task.FromResult(book);
        }

        public Task<IReadOnlyList<PrivateBook>> ListAsync(
            Guid userId,
            int limit,
            CancellationToken cancellationToken = default)
        {
            LastReadUserId = userId;
            IReadOnlyList<PrivateBook> result = _books
                .Where(pair => pair.Key.UserId == userId)
                .Select(pair => pair.Value)
                .Take(limit)
                .ToList();
            return Task.FromResult(result);
        }

        public Task<bool> SaveAsync(
            PrivateBook book,
            CancellationToken cancellationToken = default)
        {
            if (!_books.ContainsKey((book.UserId, book.Id)))
            {
                return Task.FromResult(false);
            }

            _books[(book.UserId, book.Id)] = book;
            return Task.FromResult(true);
        }

        public Task<bool> DeleteAsync(
            Guid userId,
            Guid privateBookId,
            CancellationToken cancellationToken = default)
        {
            LastReadUserId = userId;
            return Task.FromResult(_books.Remove((userId, privateBookId)));
        }
    }
}
