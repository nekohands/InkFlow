using System.Text;
using InkFlow.Modules.Content.Application;
using InkFlow.Modules.Content.Domain;
using InkFlow.Modules.Content.Infrastructure.Persistence;
using InkFlow.Modules.Library.Application;
using InkFlow.Modules.Library.Domain;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class BookPackageServiceTests
{
    private static readonly DateTimeOffset T0 =
        new(2026, 8, 31, 10, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task Package_Snapshot_Uses_One_Consistent_Current_Version_Read()
    {
        var book = CanonicalBook.Create("测试书", "作者", T0);
        var firstChapter = book.AddChapter(0, "第一章", T0);
        var secondChapter = book.AddChapter(1, "第二章", T0.AddMinutes(1));
        var firstVersion = NewVersion(book.Id, firstChapter.Id, "第一章版本 A");
        var secondVersion = NewVersion(book.Id, secondChapter.Id, "第二章版本 A");
        var versions = new SnapshotVersionRepository(firstVersion, secondVersion);
        var jobs = new InMemoryPackageJobRepository();
        var builder = new CapturingBuilder();
        var root = Path.Combine(Path.GetTempPath(), $"inkflow-package-test-{Guid.NewGuid():N}");
        var artifacts = new FileBookPackageArtifactStore(new BookPackageOptions(
            root,
            MaxChapters: 100,
            MaxPackageBytes: 1_000_000,
            Retention: TimeSpan.FromDays(7),
            LeaseDuration: TimeSpan.FromMinutes(10)));
        var service = new BookPackageService(
            jobs,
            new InMemoryBookRepository(book),
            versions,
            new AllowAllPolicy(),
            builder,
            artifacts,
            new BookPackageOptions(
                root,
                MaxChapters: 100,
                MaxPackageBytes: 1_000_000,
                Retention: TimeSpan.FromDays(7),
                LeaseDuration: TimeSpan.FromMinutes(10)),
            new FixedClock(T0));

        try
        {
            var created = await service.CreateAsync(book.Id, BookPackageFormat.Txt);
            Assert.IsTrue(created.IsSuccess);

            var job = jobs.Items.Single();
            job.Lease("package-test", T0, TimeSpan.FromMinutes(10));
            await service.ProcessAsync(job);

            Assert.AreEqual(BookPackageJobStatus.Completed, job.Status);
            Assert.IsNotNull(builder.Document);
            CollectionAssert.AreEqual(
                new[] { firstVersion.Id, secondVersion.Id },
                builder.Document!.Chapters.Select(chapter => chapter.ContentVersionId).ToArray());
            CollectionAssert.AreEqual(
                new[] { firstVersion.CanonicalText, secondVersion.CanonicalText },
                builder.Document.Chapters.Select(chapter => chapter.CanonicalText).ToArray());
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [TestMethod]
    public async Task Process_Drops_Stale_Lease_Without_Marking_Job_Failed()
    {
        var book = CanonicalBook.Create("租约书", "作者", T0);
        var chapter = book.AddChapter(0, "第一章", T0);
        var version = NewVersion(book.Id, chapter.Id, "正文");
        var jobs = new InMemoryPackageJobRepository { RejectLeasedSaves = true };
        var builder = new CapturingBuilder();
        var root = Path.Combine(Path.GetTempPath(), $"inkflow-package-lease-test-{Guid.NewGuid():N}");
        var options = new BookPackageOptions(
            root,
            MaxChapters: 100,
            MaxPackageBytes: 1_000_000,
            Retention: TimeSpan.FromDays(7),
            LeaseDuration: TimeSpan.FromMinutes(10));
        var artifacts = new FileBookPackageArtifactStore(options);
        var service = new BookPackageService(
            jobs,
            new InMemoryBookRepository(book),
            new SnapshotVersionRepository(version),
            new AllowAllPolicy(),
            builder,
            artifacts,
            options,
            new FixedClock(T0));

        try
        {
            var created = await service.CreateAsync(book.Id, BookPackageFormat.Epub);
            Assert.IsTrue(created.IsSuccess);

            var job = jobs.Items.Single();
            job.Lease("package-test", T0, options.LeaseDuration);
            await service.ProcessAsync(job);

            Assert.AreEqual(BookPackageJobStatus.Running, job.Status);
            Assert.IsNull(builder.Document);
            Assert.IsFalse(File.Exists(artifacts.GetTemporaryPath(job.Id, job.AttemptCount)));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [TestMethod]
    public async Task Process_Does_Not_Persist_Builder_Exception_Details()
    {
        const string marker = "package-secret-marker";
        var book = CanonicalBook.Create("失败书", "作者", T0);
        var chapter = book.AddChapter(0, "第一章", T0);
        var version = NewVersion(book.Id, chapter.Id, "正文");
        var jobs = new InMemoryPackageJobRepository();
        var root = Path.Combine(Path.GetTempPath(), $"inkflow-package-test-{Guid.NewGuid():N}");
        var options = new BookPackageOptions(
            root,
            MaxChapters: 100,
            MaxPackageBytes: 1_000_000,
            Retention: TimeSpan.FromDays(7),
            LeaseDuration: TimeSpan.FromMinutes(10));
        var artifacts = new FileBookPackageArtifactStore(options);
        var service = new BookPackageService(
            jobs,
            new InMemoryBookRepository(book),
            new SnapshotVersionRepository(version),
            new AllowAllPolicy(),
            new ThrowingBuilder(marker),
            artifacts,
            options,
            new FixedClock(T0));

        try
        {
            var created = await service.CreateAsync(book.Id, BookPackageFormat.Txt);
            Assert.IsTrue(created.IsSuccess);

            var job = jobs.Items.Single();
            job.Lease("package-test", T0, options.LeaseDuration);
            await service.ProcessAsync(job);

            Assert.AreEqual(BookPackageJobStatus.Queued, job.Status);
            Assert.AreEqual("package generation failed.", job.FailureReason);
            Assert.IsFalse(job.FailureReason!.Contains(marker, StringComparison.Ordinal));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static ContentVersion NewVersion(Guid bookId, Guid chapterId, string text) =>
        ContentVersion.Create(
            bookId,
            chapterId,
            "source-a",
            ContentNormalizer.Normalize($"<p>{text}</p>"),
            T0);

    private sealed class CapturingBuilder : IBookPackageBuilder
    {
        public BookPackageDocument? Document { get; private set; }

        public async Task BuildAsync(
            BookPackageDocument document,
            BookPackageFormat format,
            Stream output,
            Func<int, Task> progress,
            CancellationToken cancellationToken = default)
        {
            Document = document;
            await progress(document.Chapters.Count);
            var bytes = Encoding.UTF8.GetBytes(string.Join(
                "\n",
                document.Chapters.Select(chapter => chapter.CanonicalText)));
            await output.WriteAsync(bytes, cancellationToken);
        }
    }

    private sealed class ThrowingBuilder(string marker) : IBookPackageBuilder
    {
        public Task BuildAsync(
            BookPackageDocument document,
            BookPackageFormat format,
            Stream output,
            Func<int, Task> progress,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(marker);
    }

    private sealed class SnapshotVersionRepository(params ContentVersion[] versions)
        : IContentVersionRepository
    {
        private readonly IReadOnlyList<ContentVersion> _versions = versions;

        public Task AddAsync(ContentVersion version, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ContentVersion?> FindByHashAsync(
            Guid canonicalChapterId,
            string canonicalHash,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ContentVersion?>(_versions.FirstOrDefault(version =>
                version.CanonicalChapterId == canonicalChapterId &&
                version.CanonicalHash == canonicalHash));

        public Task<IReadOnlyList<ContentVersion>> ListForChapterAsync(
            Guid canonicalChapterId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ContentVersion>>(_versions
                .Where(version => version.CanonicalChapterId == canonicalChapterId)
                .ToList());

        public Task<ContentVersion?> GetCurrentForChapterAsync(
            Guid canonicalChapterId,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(
                "package snapshot must read current versions at book scope.");

        public Task<IReadOnlyList<ContentVersion>> ListCurrentForBookAsync(
            Guid canonicalBookId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ContentVersion>>(_versions
                .Where(version => version.CanonicalBookId == canonicalBookId)
                .ToList());

        public Task<Guid?> GetCurrentCanonicalBookIdAsync(
            Guid canonicalChapterId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Guid?>(_versions
                .FirstOrDefault(version => version.CanonicalChapterId == canonicalChapterId)
                ?.CanonicalBookId);

        public Task SetCurrentAsync(
            Guid chapterId,
            Guid versionId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class InMemoryBookRepository(CanonicalBook book) : ICanonicalBookRepository
    {
        public Task AddAsync(CanonicalBook value, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<CanonicalBook?> GetAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<CanonicalBook?>(id == book.Id ? book : null);

        public Task<IReadOnlyList<CanonicalBook>> ListAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CanonicalBook>>([book]);

        public Task<CanonicalBook?> FindByTitleAuthorAsync(
            string title,
            string author,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<CanonicalBook?>(null);

        public Task SaveAsync(CanonicalBook value, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class InMemoryPackageJobRepository : IBookPackageJobRepository
    {
        public List<BookPackageJob> Items { get; } = [];

        public bool RejectLeasedSaves { get; set; }

        public Task AddAsync(BookPackageJob job, CancellationToken cancellationToken = default)
        {
            Items.Add(job);
            return Task.CompletedTask;
        }

        public Task<BookPackageJob?> GetAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<BookPackageJob?>(Items.SingleOrDefault(job => job.Id == id));

        public Task<BookPackageJob?> TryLeaseAsync(
            DateTimeOffset now,
            string owner,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken = default)
        {
            var job = Items.FirstOrDefault(candidate => candidate.IsLeasable(now));
            if (job is null)
            {
                return Task.FromResult<BookPackageJob?>(null);
            }

            job.Lease(owner, now, leaseDuration);
            return Task.FromResult<BookPackageJob?>(job);
        }

        public Task SaveAsync(BookPackageJob job, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<bool> SaveLeasedAsync(
            BookPackageJob job,
            string leaseOwner,
            int leaseAttempt,
            DateTimeOffset now,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(!RejectLeasedSaves);

        public Task<IReadOnlyList<BookPackageJob>> ListExpiredAsync(
            DateTimeOffset now,
            int limit,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<BookPackageJob>>(Items
                .Where(job => job.Status == BookPackageJobStatus.Completed && job.ExpiresAt <= now)
                .Take(limit)
                .ToList());
    }

    private sealed class AllowAllPolicy : IContentPolicyReader
    {
        public Task<bool> IsTakedownAsync(
            Guid canonicalBookId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
