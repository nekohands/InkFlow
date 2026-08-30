using System.Text.Json;
using InkFlow.Modules.Content.Application;
using InkFlow.Modules.Content.Infrastructure.Persistence;
using InkFlow.Modules.Identity.Domain;
using InkFlow.Modules.Identity.Infrastructure.Persistence;
using InkFlow.Modules.Library.Domain;
using InkFlow.Modules.Library.Infrastructure.Persistence;
using InkFlow.Modules.Sources.Domain;
using InkFlow.Modules.Sources.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

const string FixtureSourceId = "inkflow-acceptance";
const string FixtureBookTitle = "InkFlow Runtime Acceptance Fixture";
const string FixtureBookAuthor = "InkFlow Automation";
const string FixtureChapterTitle = "Automated Acceptance Chapter";
const string FixtureSourceBaseUrl = "https://inkflow-acceptance.invalid";
const string FixtureReaderContent = """
    <p>这一章用于 InkFlow 的非阅读 App 自动化验收。</p>
    <p>正文来自已发布的 Canonical Content，阅读页应展示已落库内容。</p>
    <p>滚动、阅读进度和历史记录由浏览器自动化链路验证。</p>
    """;

var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Database");
if (string.IsNullOrWhiteSpace(connectionString))
{
    return Fail("ConnectionStrings__Database is required.");
}

if (args.Length == 0)
{
    return Fail("usage: ensure-catalog | ensure-reader-catalog | set-role <email> <operator|administrator> | disable-user <email>");
}

try
{
    return args[0] switch
    {
        "ensure-catalog" when args.Length == 1 =>
            await EnsureCatalogAsync(connectionString, publishReaderContent: false),
        "ensure-reader-catalog" when args.Length == 1 =>
            await EnsureCatalogAsync(connectionString, publishReaderContent: true),
        "set-role" when args.Length == 3 =>
            await SetRoleAsync(connectionString, args[1], args[2]),
        "disable-user" when args.Length == 2 =>
            await DisableUserAsync(connectionString, args[1]),
        _ => Fail("invalid arguments.")
    };
}
catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or DbUpdateException)
{
    return Fail(ex.Message);
}

static int Fail(string message)
{
    Console.Error.WriteLine($"InkFlow.AcceptanceFixtures: {message}");
    return 2;
}

static DbContextOptions<TContext> Options<TContext>(string connectionString)
    where TContext : DbContext =>
    new DbContextOptionsBuilder<TContext>()
        .UseNpgsql(connectionString)
        .Options;

static async Task<int> SetRoleAsync(
    string connectionString,
    string email,
    string rawRole)
{
    var role = Enum.Parse<UserRole>(rawRole, ignoreCase: true);
    if (role is UserRole.Reader)
    {
        return Fail("acceptance fixture roles must be Operator or Administrator.");
    }

    var normalizedEmail = UserEmailAddress.Normalize(email);
    await using var db = new IdentityDbContext(Options<IdentityDbContext>(connectionString));
    var users = new EfUserRepository(db);
    var current = await users.FindByNormalizedEmailAsync(normalizedEmail)
        ?? throw new InvalidOperationException("acceptance fixture user was not found.");
    var updated = User.Rehydrate(
        current.Id,
        current.Email,
        current.NormalizedEmail,
        current.PasswordHash,
        role,
        UserStatus.Active,
        current.CreatedAt,
        DateTimeOffset.UtcNow);
    await users.SaveAsync(updated);

    Console.WriteLine(JsonSerializer.Serialize(new
    {
        userId = updated.Id,
        role = updated.Role.ToString(),
        status = updated.Status.ToString(),
    }));
    return 0;
}

static async Task<int> DisableUserAsync(string connectionString, string email)
{
    var normalizedEmail = UserEmailAddress.Normalize(email);
    await using var db = new IdentityDbContext(Options<IdentityDbContext>(connectionString));
    var users = new EfUserRepository(db);
    var current = await users.FindByNormalizedEmailAsync(normalizedEmail)
        ?? throw new InvalidOperationException("acceptance fixture user was not found.");
    current.Disable(DateTimeOffset.UtcNow);
    await users.SaveAsync(current);
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        userId = current.Id,
        status = current.Status.ToString(),
    }));
    return 0;
}

static async Task<int> EnsureCatalogAsync(string connectionString, bool publishReaderContent)
{
    var now = DateTimeOffset.UtcNow;
    await using (var sourceDb = new SourcesDbContext(Options<SourcesDbContext>(connectionString)))
    {
        var sources = new EfSourceRepository(sourceDb);
        var source = await sources.GetAsync(FixtureSourceId);
        if (source is null)
        {
            source = Source.Create(
                FixtureSourceId,
                "InkFlow Acceptance Source",
                FixtureSourceBaseUrl,
                now);
            await sources.AddAsync(source);
        }
    }

    Guid bookId;
    Guid chapterId;
    await using (var libraryDb = new LibraryDbContext(Options<LibraryDbContext>(connectionString)))
    {
        var books = new EfCanonicalBookRepository(libraryDb);
        var book = await books.FindByTitleAuthorAsync(FixtureBookTitle, FixtureBookAuthor);
        if (book is null)
        {
            book = CanonicalBook.Create(FixtureBookTitle, FixtureBookAuthor, now);
            var chapter = book.AddChapter(0, FixtureChapterTitle, now);
            await books.AddAsync(book);
            bookId = book.Id;
            chapterId = chapter.Id;
        }
        else
        {
            book = await books.GetAsync(book.Id)
                ?? throw new InvalidOperationException("acceptance fixture book disappeared while loading.");
            var existingChapter = book.Chapters.FirstOrDefault();
            if (existingChapter is null)
            {
                existingChapter = book.AddChapter(0, FixtureChapterTitle, now);
                await books.SaveAsync(book);
            }

            bookId = book.Id;
            chapterId = existingChapter.Id;
        }
    }

    if (publishReaderContent)
    {
        await using var contentDb = new ContentDbContext(Options<ContentDbContext>(connectionString));
        var publisher = new ContentPublishingService(new EfContentVersionRepository(contentDb));
        var outcome = await publisher.PublishAsync(
            bookId,
            chapterId,
            FixtureSourceId,
            FixtureReaderContent);
        if (!outcome.IsSuccess || outcome.Version is null)
        {
            throw new InvalidOperationException(
                $"acceptance reader content could not be published: {string.Join("; ", outcome.Errors)}");
        }
    }

    Console.WriteLine(JsonSerializer.Serialize(new
    {
        sourceId = FixtureSourceId,
        bookId,
        chapterId,
    }));
    return 0;
}
