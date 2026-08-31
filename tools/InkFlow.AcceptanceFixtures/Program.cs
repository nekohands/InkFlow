using System.Text.Json;
using InkFlow.Modules.Content.Application;
using InkFlow.Modules.Content.Infrastructure.Persistence;
using InkFlow.Modules.Crawling.Domain;
using InkFlow.Modules.Crawling.Infrastructure.Persistence;
using InkFlow.Modules.Identity.Domain;
using InkFlow.Modules.Identity.Infrastructure.Persistence;
using InkFlow.Modules.Library.Domain;
using InkFlow.Modules.Library.Infrastructure.Persistence;
using InkFlow.Modules.Sources.Application;
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
const string FailoverSourceAId = "inkflow-failover-a";
const string FailoverSourceBId = "inkflow-failover-b";
const string FailoverBookTitle = "InkFlow Source Failover Fixture";
const string FailoverBookAuthor = "InkFlow Automation";
const string FailoverChapterTitle = "Failover Acceptance Chapter";
const string FailoverSourceABaseUrl = "https://inkflow-failover-a.invalid";
const string FailoverSourceBBaseUrl = "https://inkflow-failover-b.invalid";
const string FailoverSourceAContent = """
    <p>InkFlow failover source A marker. This authoritative fixture paragraph is intentionally long enough to represent a high quality published chapter and to make the selected source observable during the failover drill.</p>
    <p>The source A content remains available in canonical storage while its upstream capability is disabled, so the public Web and Legado readers can prove that reading does not synchronously depend on a live source.</p>
    <p>Restoring source A must make this version the preferred candidate again without changing the canonical book or chapter identity.</p>
    """;
const string FailoverSourceBContent = """
    <p>InkFlow failover source B marker. This secondary fixture remains readable when source A is disabled and provides a distinct canonical content version.</p>
    <p>The fallback response keeps the same canonical identifiers and is intentionally scored below source A so recovery can be observed deterministically.</p>
    """;

var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Database");
if (string.IsNullOrWhiteSpace(connectionString))
{
    return Fail("ConnectionStrings__Database is required.");
}

if (args.Length == 0)
{
    return Fail("usage: ensure-catalog | ensure-reader-catalog | ensure-failover-catalog | ensure-collection-control-runs | set-role <email> <operator|administrator> | disable-user <email>");
}

try
{
    return args[0] switch
    {
        "ensure-catalog" when args.Length == 1 =>
            await EnsureCatalogAsync(connectionString, publishReaderContent: false),
        "ensure-reader-catalog" when args.Length == 1 =>
            await EnsureCatalogAsync(connectionString, publishReaderContent: true),
        "ensure-failover-catalog" when args.Length == 1 =>
            await EnsureFailoverCatalogAsync(connectionString),
        "ensure-collection-control-runs" when args.Length == 1 =>
            await EnsureCollectionControlRunsAsync(connectionString),
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
    await EnsureSourceAsync(
        connectionString,
        FixtureSourceId,
        "InkFlow Acceptance Source",
        FixtureSourceBaseUrl,
        now);

    var (bookId, chapterId) = await EnsureCanonicalBookAsync(
        connectionString,
        FixtureBookTitle,
        FixtureBookAuthor,
        FixtureChapterTitle,
        now);

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

static async Task<int> EnsureFailoverCatalogAsync(string connectionString)
{
    var now = DateTimeOffset.UtcNow;
    await EnsureSourceAsync(
        connectionString,
        FailoverSourceAId,
        "InkFlow Failover Source A",
        FailoverSourceABaseUrl,
        now);
    await EnsureSourceAsync(
        connectionString,
        FailoverSourceBId,
        "InkFlow Failover Source B",
        FailoverSourceBBaseUrl,
        now);

    var (bookId, chapterId) = await EnsureCanonicalBookAsync(
        connectionString,
        FailoverBookTitle,
        FailoverBookAuthor,
        FailoverChapterTitle,
        now);

    await using var sourceDb = new SourcesDbContext(Options<SourcesDbContext>(connectionString));
    var health = new SourceHealthService(
        new EfSourceHealthRepository(sourceDb),
        TimeProvider.System);
    await health.EnableAsync(FailoverSourceAId, SourceCapability.Content);
    await health.EnableAsync(FailoverSourceBId, SourceCapability.Content);

    await using var contentDb = new ContentDbContext(Options<ContentDbContext>(connectionString));
    var versions = new EfContentVersionRepository(contentDb);
    var selector = new ContentSelectionService(
        versions,
        health,
        new EfContentSelectionDecisionRepository(contentDb),
        TimeProvider.System);
    var publisher = new ContentPublishingService(versions, selector);

    foreach (var (sourceId, content) in new[]
    {
        (FailoverSourceAId, FailoverSourceAContent),
        (FailoverSourceBId, FailoverSourceBContent),
    })
    {
        var outcome = await publisher.PublishAsync(bookId, chapterId, sourceId, content);
        if (!outcome.IsSuccess || outcome.Version is null)
        {
            throw new InvalidOperationException(
                $"failover fixture content for '{sourceId}' could not be published: {string.Join("; ", outcome.Errors)}");
        }
    }

    var selected = await selector.SelectCurrentAsync(chapterId);
    if (!selected.IsSuccess || selected.SelectedVersion?.SourceId != FailoverSourceAId)
    {
        throw new InvalidOperationException(
            $"failover fixture did not select source A initially: {selected.Evidence}");
    }

    Console.WriteLine(JsonSerializer.Serialize(new
    {
        sourceAId = FailoverSourceAId,
        sourceBId = FailoverSourceBId,
        bookId,
        chapterId,
    }));
    return 0;
}

static async Task EnsureSourceAsync(
    string connectionString,
    string sourceId,
    string displayName,
    string baseUrl,
    DateTimeOffset now)
{
    await using var sourceDb = new SourcesDbContext(Options<SourcesDbContext>(connectionString));
    var sources = new EfSourceRepository(sourceDb);
    var source = await sources.GetAsync(sourceId);
    if (source is null)
    {
        source = Source.Create(sourceId, displayName, baseUrl, now);
        source.UpdateRuleDsl(BuildFixtureRuleDsl(sourceId), now);
        await sources.AddAsync(source);
    }
    else if (source.RuleDsl is null)
    {
        source.UpdateRuleDsl(BuildFixtureRuleDsl(sourceId), now);
        await sources.SaveAsync(source);
    }
}

static async Task<(Guid BookId, Guid ChapterId)> EnsureCanonicalBookAsync(
    string connectionString,
    string title,
    string author,
    string chapterTitle,
    DateTimeOffset now)
{
    await using var libraryDb = new LibraryDbContext(Options<LibraryDbContext>(connectionString));
    var books = new EfCanonicalBookRepository(libraryDb);
    var book = await books.FindByTitleAuthorAsync(title, author);
    if (book is null)
    {
        book = CanonicalBook.Create(title, author, now);
        var chapter = book.AddChapter(0, chapterTitle, now);
        await books.AddAsync(book);
        return (book.Id, chapter.Id);
    }

    book = await books.GetAsync(book.Id)
        ?? throw new InvalidOperationException("acceptance fixture book disappeared while loading.");
    var existingChapter = book.Chapters.FirstOrDefault();
    if (existingChapter is null)
    {
        existingChapter = book.AddChapter(0, chapterTitle, now);
        await books.SaveAsync(book);
    }

    return (book.Id, existingChapter.Id);
}

static async Task<int> EnsureCollectionControlRunsAsync(string connectionString)
{
    var now = DateTimeOffset.UtcNow;
    await using var crawlingDb = new CrawlingDbContext(Options<CrawlingDbContext>(connectionString));
    var runs = new EfCollectionRunRepository(crawlingDb);
    var result = new Dictionary<string, Guid>(StringComparer.Ordinal);
    foreach (var action in new[] { "pause", "stop", "cancel", "resume" })
    {
        var externalBookId = $"control-{action}-{Guid.NewGuid():N}";
        var run = CollectionRun.Create(
            FixtureSourceId,
            externalBookId,
            $"{FixtureSourceBaseUrl}/book/{externalBookId}",
            now);
        await runs.AddAsync(run);
        result[action] = run.Id;
    }

    Console.WriteLine(JsonSerializer.Serialize(result));
    return 0;
}

static SourceRuleDsl BuildFixtureRuleDsl(string sourceId) => new("1", sourceId,
[
    new CapabilityRule(
        SourceCapability.BookInfo,
        RuleRequest.Get("/book/{bookId}"),
        [
            new RuleField("title", new RuleSelector(SelectorKind.Css, "h1"), null, []),
            new RuleField("author", new RuleSelector(SelectorKind.Css, ".author"), null, []),
        ]),
    new CapabilityRule(
        SourceCapability.Toc,
        RuleRequest.Get("/book/{bookId}/toc"),
        [],
        List: new RuleListBinding("a.chapter", "href", "/book/", "")),
    new CapabilityRule(
        SourceCapability.Content,
        RuleRequest.Get("/chapter/{chapterId}"),
        [new RuleField("content", new RuleSelector(SelectorKind.Css, "p"), null, [])]),
]);
