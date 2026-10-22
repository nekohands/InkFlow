using System.Data.Common;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DotNet.Testcontainers.Images;
using InkFlow.Modules.Billing.Application;
using InkFlow.Modules.Billing.Infrastructure.Persistence;
using InkFlow.Modules.Content.Application;
using InkFlow.Modules.Content.Domain;
using InkFlow.Modules.Content.Infrastructure.Persistence;
using InkFlow.Modules.Developers.Application;
using InkFlow.Modules.Developers.Domain;
using InkFlow.Modules.Developers.Infrastructure.Credentials;
using InkFlow.Modules.Developers.Infrastructure.Persistence;
using InkFlow.Modules.Library.Domain;
using InkFlow.Modules.Library.Infrastructure.Persistence;
using InkFlow.UnitTests;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using static InkFlow.UnitTests.DeveloperEndpointFixture;

// Isolated mapping/storage diagnostic: real authentication handler + persisted key, quota,
// catalog and policy. Identity eligibility, distributed rate counter, quota cache and audit
// sink are deterministic boundaries. Optional content reselection is excluded. Concurrency=1.
var output = args.FirstOrDefault() ?? "artifacts/developer/measurement.json";
await using var postgres = new PostgreSqlBuilder(new DockerImage("postgres:18-alpine")).Build();
await postgres.StartAsync();
var trace = new SqlTrace();
DbContextOptions<T> Options<T>() where T : DbContext => new DbContextOptionsBuilder<T>()
    .UseNpgsql(postgres.GetConnectionString()).AddInterceptors(trace).Options;
await using var developers = new DeveloperDbContext(Options<DeveloperDbContext>());
await using var billing = new BillingDbContext(Options<BillingDbContext>());
await using var library = new LibraryDbContext(Options<LibraryDbContext>());
await using var content = new ContentDbContext(Options<ContentDbContext>());
DbContext[] contexts = [developers, billing, library, content];
foreach (var db in contexts) await db.Database.MigrateAsync();
var clock = new FrozenClock();
var users = new EligibleUser();
var applications = new DeveloperApplicationService(new EfDeveloperApplicationRepository(developers),
    new EfDeveloperApiKeyRepository(developers), new DeveloperApiKeySecretGenerator(), users, clock);
var app = (await applications.CreateApplicationAsync(UserId, "Measurement")).Value!;
var key = (await applications.IssueKeyAsync(UserId, app.ApplicationId, "Measurement", 1)).Value!;
var entitlements = new EntitlementService(new EfPlanRepository(billing), new EfEntitlementAssignmentRepository(billing), users, clock);
var quota = new QuotaService(billing, entitlements, new NoCache(), clock);
var books = new EfCanonicalBookRepository(library);
var versions = new EfContentVersionRepository(content);
var policy = new ContentPolicyService(new EfContentPolicyRepository(content), clock);
await books.AddAsync(CanonicalBook.Rehydrate(BookId, "Stored book", "Author", Now, Now,
    [new CanonicalChapter(ChapterId, BookId, 0, "Chapter", Now)]));
await versions.AddAsync(ContentVersion.Rehydrate(Guid.Parse("00000000-0000-4000-8000-000000000006"),
    BookId, ChapterId, "fixture", "fixed-content-hash", "First\n\nSecond", 2, 100, true, Now));
await using var host = await StartAsync(builder =>
{
    builder.Services.AddSingleton<IDeveloperApiKeyValidator>(applications);
    builder.Services.AddSingleton<IQuotaService>(quota);
    builder.Services.AddSingleton(new CatalogQueryService(books, versions, policy));
});
host.UseActivity = false;
var shapes = new[]
{
    new Shape("search", "/search?q=Stored&limit=1", 200, 1),
    new Shape("list", "/books?limit=20", 200, 1),
    new Shape("book", $"/books/{BookId}", 200, 1),
    new Shape("chapters", $"/books/{BookId}/chapters", 200, 1),
    new Shape("content", $"/chapters/{ChapterId}/content", 200, 5),
    new Shape("invalid-key", "/books", 401, 0),
    new Shape("invalid-parameter", "/books?limit=0", 400, 0),
    new Shape("missing", "/chapters/00000000-0000-4000-8000-000000000099/content", 404, 5),
    new Shape("exceeded", "/books", 429, 0),
};
var samples = new List<Sample>();
for (var round = 0; round < 3; round++)
{
    foreach (var shape in shapes)
    {
        trace.Enabled = false;
        await billing.UsageLedger.ExecuteDeleteAsync();
        await billing.UsagePeriods.ExecuteDeleteAsync();
        foreach (var db in contexts) db.ChangeTracker.Clear();
        if (shape.Name == "exceeded")
        {
            var reserved = await quota.ReserveAsync(new(UserId, app.ApplicationId, key.Key.KeyId, "fixture.fill", 1000, "fixture"));
            if (reserved.Status != QuotaReservationStatus.Reserved) throw new InvalidOperationException("Unable to seed quota");
        }
        for (var index = -3; index < 30; index++)
        {
            foreach (var db in contexts) db.ChangeTracker.Clear();
            var beforeCount = await billing.UsageLedger.CountAsync();
            var beforeUnits = await billing.UsageLedger.SumAsync(entry => (long?)entry.Units) ?? 0;
            trace.Commands.Clear();
            trace.Enabled = true;
            var start = Stopwatch.GetTimestamp();
            using var response = await host.SendAsync("GET", "/api/developer/v1" + shape.Path,
                key: shape.Name == "invalid-key" ? "lf_dev_invalid-fixture" : key.RawKey);
            var body = await response.Content.ReadAsStringAsync();
            var elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            trace.Enabled = false;
            var ledgerCount = await billing.UsageLedger.CountAsync() - beforeCount;
            var ledgerUnits = (await billing.UsageLedger.SumAsync(entry => (long?)entry.Units) ?? 0) - beforeUnits;
            if ((int)response.StatusCode != shape.Status || ledgerUnits != shape.Units || ledgerCount != (shape.Units > 0 ? 1 : 0))
                throw new InvalidOperationException($"Unexpected response/ledger for {shape.Name}: {(int)response.StatusCode}/{ledgerCount}/{ledgerUnits}");
            if (shape.Units > 0 && !await billing.UsageLedger.AllAsync(entry => entry.TraceId == "rf05-trace"))
                throw new InvalidOperationException("TraceId was not forwarded");
            var headers = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var header in response.Headers.Concat(response.Content.Headers))
                if (header.Key is not ("Date" or "Server")) headers[header.Key] = string.Join(", ", header.Value);
            if (index >= 0) samples.Add(new(shape.Name, round, index, (int)response.StatusCode,
                headers, body, ledgerCount, ledgerUnits, trace.Commands.Count,
                Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", trace.Commands)))), elapsed));
            host.Audits.Clear();
        }
    }
}
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
await File.WriteAllTextAsync(output, JsonSerializer.Serialize(new
{
    schemaVersion = 1, database = "postgres:18-alpine", concurrency = 1, warmups = 3, rounds = 3,
    samplesPerRound = 30, clock = Now, catalogBooks = 1, catalogChapters = 1,
    boundaries = "fixed eligibility/rate counter; no-op quota cache; captured audit sink; no content reselection",
    samples
}, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"PASS: {samples.Count} observations written to {output}");

internal sealed record Shape(string Name, string Path, int Status, int Units);
internal sealed record Sample(string Shape, int Round, int Index, int Status, SortedDictionary<string, string> Headers,
    string Body, int LedgerCount, long LedgerUnits, int SqlCount, string SqlHash, double ElapsedMilliseconds);
internal sealed class FrozenClock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }
internal sealed class EligibleUser : IDeveloperUserStatusReader, IBillingUserStatusReader
{
    public Task<bool> IsActiveAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(userId == UserId);
    public Task<bool> IsActiveAdministratorAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(userId == UserId);
}
internal sealed class NoCache : IQuotaSnapshotCache
{
    public Task<QuotaSnapshot?> GetAsync(Guid userId, DateTimeOffset periodStart, CancellationToken cancellationToken = default) => Task.FromResult<QuotaSnapshot?>(null);
    public Task SetAsync(QuotaSnapshot snapshot, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task RemoveAsync(Guid userId, DateTimeOffset periodStart, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
internal sealed class SqlTrace : DbCommandInterceptor
{
    internal bool Enabled { get; set; }
    internal List<string> Commands { get; } = [];
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    { if (Enabled) Commands.Add(command.CommandText); return ValueTask.FromResult(result); }
    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    { if (Enabled) Commands.Add(command.CommandText); return ValueTask.FromResult(result); }
    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default)
    { if (Enabled) Commands.Add(command.CommandText); return ValueTask.FromResult(result); }
}
