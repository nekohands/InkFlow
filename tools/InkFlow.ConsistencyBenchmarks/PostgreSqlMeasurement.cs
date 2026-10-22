using System.Data.Common;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using InkFlow.Api;
using InkFlow.ConsistencyFixtures;
using InkFlow.Modules.Content.Infrastructure.Persistence;
using InkFlow.Modules.Crawling.Infrastructure.Persistence;
using InkFlow.Modules.Library.Infrastructure.Persistence;
using InkFlow.Modules.Operations.Application;
using InkFlow.Modules.Sources.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using Testcontainers.PostgreSql;

internal static class PostgreSqlMeasurement
{
    public static async Task RunAsync(string output)
    {
        // Only an owned disposable container is accepted. Never seed a user-supplied database.
        await using var container = new PostgreSqlBuilder("postgres:18-alpine").Build();
        await container.StartAsync();
        var measurements = new List<object>();
        await using var admin = new NpgsqlConnection(container.GetConnectionString());
        await admin.OpenAsync();
        foreach (var size in new[] { 0, 1_000, 10_000, 100_000 })
        {
            var database = "fixture_" + size;
            await using (var create = new NpgsqlCommand($"CREATE DATABASE {database}", admin)) await create.ExecuteNonQueryAsync();
            var connection = new NpgsqlConnectionStringBuilder(container.GetConnectionString()) { Database = database }.ConnectionString;
            var trace = new QueryTrace();
            await using var library = new LibraryDbContext(Options<LibraryDbContext>(connection, trace));
            await using var sources = new SourcesDbContext(Options<SourcesDbContext>(connection, trace));
            await using var content = new ContentDbContext(Options<ContentDbContext>(connection, trace));
            await using var crawling = new CrawlingDbContext(Options<CrawlingDbContext>(connection, trace));
            foreach (var db in new DbContext[] { library, sources, content, crawling }) await db.Database.MigrateAsync();
            await SeedAsync(size, library, sources, content);
            await using var sql = new NpgsqlConnection(connection);
            await sql.OpenAsync();
            await using (var analyze = new NpgsqlCommand("ANALYZE", sql)) await analyze.ExecuteNonQueryAsync();
            var reader = new EfConsistencySnapshotReader(library, sources, content, crawling);
            var service = new ConsistencyCheckService(reader, new ConsistencyFixture.Clock(), ConsistencyDomainValues.Current);
            foreach (var corrupt in new[] { false, true })
            {
                if (corrupt && size > 0)
                {
                    // Cross-schema orphan references are possible; local duplicates remain protected by UNIQUE constraints.
                    await using var damage = new NpgsqlCommand("UPDATE content.versions SET \"CanonicalChapterId\" = md5(\"CanonicalChapterId\"::text)::uuid", sql);
                    await damage.ExecuteNonQueryAsync();
                    await using var analyze = new NpgsqlCommand("ANALYZE", sql);
                    await analyze.ExecuteNonQueryAsync();
                }
                trace.Queries.Clear();
                var snapshot = await reader.ReadAsync();
                var queries = trace.Queries.ToArray();
                if (queries.Length != 13) throw new InvalidOperationException($"Expected 13 metadata queries, got {queries.Length}.");
                var rows = typeof(ConsistencySnapshot).GetProperties().Where(p => p.Name != "Empty")
                    .ToDictionary(p => p.Name, p => ((System.Collections.ICollection)p.GetValue(snapshot)!).Count);
                var expectedRows = size == 0 ? 0 : 5L * size + 5;
                if (rows.Values.Sum(v => (long)v) != expectedRows) throw new InvalidOperationException("Fixture row count changed.");
                var expected = await service.CheckAsync();
                if ((!corrupt || size == 0) && expected.TotalIssueCount != 0) throw new InvalidOperationException("Healthy fixture has issues.");
                if (corrupt && expected.TotalIssueCount != 2 * size) throw new InvalidOperationException("Orphan count changed.");
                var completeHash = Hash(ConsistencyCheckValidator.Validate(snapshot, ConsistencyDomainValues.Current));
                var reportHash = Hash(expected);
                var plans = new List<JsonElement>();
                foreach (var query in queries)
                {
                    await using var explain = new NpgsqlCommand("EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON) " + query, sql);
                    using var json = JsonDocument.Parse((string)(await explain.ExecuteScalarAsync())!);
                    plans.Add(json.RootElement.Clone());
                }
                for (var round = 1; round <= 3; round++)
                {
                    for (var warmup = 0; warmup < 3; warmup++) await service.CheckAsync();
                    var samples = new List<object>();
                    var elapsed = new List<double>();
                    for (var sample = 0; sample < 30; sample++)
                    {
                        trace.Queries.Clear();
                        var before = GC.GetTotalAllocatedBytes(true);
                        var start = Stopwatch.GetTimestamp();
                        var actual = await service.CheckAsync();
                        var milliseconds = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                        var allocated = GC.GetTotalAllocatedBytes(true) - before;
                        if (Hash(actual) != reportHash || trace.Queries.Count != 13) throw new InvalidOperationException("Measured report or query count changed.");
                        samples.Add(new { milliseconds, allocated, sqlCount = trace.Queries.Count, processLifetimePeakBytes = Process.GetCurrentProcess().PeakWorkingSet64 });
                        elapsed.Add(milliseconds);
                    }
                    // Validate the entire issue list, including issues beyond the returned sample.
                    if (Hash(ConsistencyCheckValidator.Validate(await reader.ReadAsync(), ConsistencyDomainValues.Current)) != completeHash)
                        throw new InvalidOperationException("Complete issue set changed.");
                    elapsed.Sort();
                    measurements.Add(new { size, corrupt, round, completeHash, reportHash, expected.TotalIssueCount, rows, queries, plans, p50 = elapsed[14], p95 = elapsed[28], samples });
                    Console.WriteLine($"postgres size={size} corrupt={corrupt} round={round} p50={elapsed[14]:F3}ms p95={elapsed[28]:F3}ms issues={expected.TotalIssueCount}");
                }
            }
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        await File.WriteAllTextAsync(output, JsonSerializer.Serialize(new
        {
            mode = "postgres-end-to-end", database = admin.PostgreSqlVersion.ToString(),
            framework = RuntimeInformation.FrameworkDescription, os = RuntimeInformation.OSDescription,
            cpuCount = Environment.ProcessorCount, warmups = 3, samplesPerRound = 30, rounds = 3,
            concurrency = 1, cache = "warm after three full scans; no cache flush", measurements,
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static DbContextOptions<T> Options<T>(string connection, QueryTrace trace) where T : DbContext =>
        new DbContextOptionsBuilder<T>().UseNpgsql(connection).AddInterceptors(trace).Options;

    private static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));

    private static async Task SeedAsync(int size, LibraryDbContext library, SourcesDbContext sources, ContentDbContext content)
    {
        if (size == 0) return;
        var now = ConsistencyFixture.Now;
        library.Books.Add(new() { Id = ConsistencyFixture.Id(1), Title = "fixture", Author = "fixture", CreatedAt = now, UpdatedAt = now });
        foreach (var id in new[] { "a", "b" }) sources.Sources.Add(new() { Id = id, DisplayName = id, BaseUrl = "https://example.com", CreatedAt = now, UpdatedAt = now });
        sources.SourceBooks.AddRange(new SourceBookEntity { Id = ConsistencyFixture.Id(1), SourceId = "a", ExternalBookId = "book", Title = "fixture", Author = "fixture", CreatedAt = now, UpdatedAt = now }, new SourceBookEntity { Id = ConsistencyFixture.Id(2), SourceId = "b", ExternalBookId = "book", Title = "fixture", Author = "fixture", CreatedAt = now, UpdatedAt = now });
        await library.SaveChangesAsync();
        await sources.SaveChangesAsync();
        // Deterministic UUIDs match the rule-only fixture. SQL seed runs outside measured work.
        await library.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO library.chapters ("Id", "BookId", "ChapterIndex", "Title", "CreatedAt")
            SELECT (lpad(to_hex(i),8,'0') || '-0000-0000-0000-000000000000')::uuid,
              '00000001-0000-0000-0000-000000000000'::uuid, i, 'fixture', {now}
            FROM generate_series(1, {size}) i
            """);
        await sources.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO sources.source_chapters ("Id", "SourceBookId", "ExternalChapterId", "ChapterIndex", "Title")
            SELECT (lpad(to_hex(i),8,'0') || '-0000-0000-0000-000000000000')::uuid,
              (lpad(to_hex(i%2+1),8,'0') || '-0000-0000-0000-000000000000')::uuid,
              ((i+1)/2)::text, (i+1)/2, 'fixture' FROM generate_series(1, {size * 2}) i
            """);
        await content.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO content.versions ("Id", "CanonicalBookId", "CanonicalChapterId", "SourceId", "CanonicalHash", "CanonicalText", "ParagraphCount", "QualityScore", "QualityAlgorithmVersion", "QualityEvidence", "IsCurrent", "CreatedAt")
            SELECT (lpad(to_hex(i),8,'0') || '-0000-0000-0000-000000000000')::uuid,
              '00000001-0000-0000-0000-000000000000'::uuid,
              (lpad(to_hex((i+1)/2),8,'0') || '-0000-0000-0000-000000000000')::uuid,
              CASE WHEN i%2=1 THEN 'a' ELSE 'b' END, 'hash-' || i, repeat('x',64), 1, 80, 'fixture-v1', 'fixture', i%2=1, {now}
            FROM generate_series(1, {size * 2}) i
            """);
        library.ChangeTracker.Clear();
        sources.ChangeTracker.Clear();
    }

    private sealed class QueryTrace : DbCommandInterceptor
    {
        public List<string> Queries { get; } = [];
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Queries.Add(command.CommandText);
            return ValueTask.FromResult(result);
        }
    }
}
