using InkFlow.BuildingBlocks.Persistence;
using InkFlow.Modules.Crawling;
using InkFlow.Modules.Crawling.Orchestration;
using InkFlow.Modules.Sources.Rules;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Testcontainers.PostgreSql;

namespace InkFlow.IntegrationTests;

[TestClass]
public sealed class CrawlerIngestionPipelineTests
{
    [TestMethod]
    public async Task BookInfo_toc_content_fixture_execution_builds_readable_canonical_chain()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:18").WithDatabase("inkflow").WithUsername("inkflow").WithPassword("inkflow-test").Build();
        await postgres.StartAsync();
        var services = new ServiceCollection();
        services.AddSingleton(TimeProvider.System);
        services.AddInkFlowPersistence(postgres.GetConnectionString());
        await using var provider = services.BuildServiceProvider();
        await provider.MigrateInkFlowAsync();
        var now = DateTimeOffset.UtcNow;
        var sourceId = Guid.CreateVersion7();
        var ruleId = Guid.CreateVersion7();

        await using var scope = provider.CreateAsyncScope();
        var sources = scope.ServiceProvider.GetRequiredService<SourcesDbContext>();
        var library = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
        var crawling = scope.ServiceProvider.GetRequiredService<CrawlingDbContext>();
        var content = scope.ServiceProvider.GetRequiredService<ContentDbContext>();
        sources.Sources.Add(new SourceRecord { Id = sourceId, Name = "Fixture", BaseUrl = "https://example.test", HealthScore = 95, CreatedAtUtc = now, UpdatedAtUtc = now });
        sources.RuleVersions.Add(new SourceRuleVersionRecord { Id = ruleId, SourceId = sourceId, Version = 1, SchemaVersion = 1, Status = "Published", RuleJson = "{}", CreatedAtUtc = now, PublishedAtUtc = now });
        await sources.SaveChangesAsync();
        var processor = new CrawlerTaskProcessor(crawling, sources, library, content, TimeProvider.System);

        var bookPayload = new RuleCrawlerTaskPayload(sourceId, ruleId, SourceOperation.BookInfo, null, null, new Dictionary<string, string> { ["externalId"] = "book-1", ["bookUrl"] = "https://example.test/book/1" });
        var bookExecution = Execution("https://example.test/book/1", new Dictionary<string, string> { ["externalId"] = "book-1", ["title"] = "测试小说", ["author"] = "测试作者", ["description"] = "Fixture" });
        await processor.ApplyExecutionAsync(null, bookPayload, bookExecution, now);

        var sourceBook = await sources.SourceBooks.SingleAsync();
        var canonicalBook = await library.Books.SingleAsync();
        Assert.AreEqual(canonicalBook.Id, (await library.SourceBookMatches.SingleAsync()).BookId);
        var tocTask = await crawling.CrawlerTasks.SingleAsync(task => task.IdempotencyKey.StartsWith("toc:"));
        var tocPayload = RuleCrawlerTaskPayload.Deserialize(tocTask.Payload);

        var tocExecution = new SourceOperationExecution(200, new Uri("https://example.test/book/1/toc"), "toc-fixture", 11,
            new RuleExtractionResult([
                new Dictionary<string, string> { ["externalId"] = "c1", ["title"] = "第一章 开始", ["url"] = "https://example.test/chapter/1" },
                new Dictionary<string, string> { ["externalId"] = "c2", ["title"] = "第二章 继续", ["url"] = "https://example.test/chapter/2" }
            ]));
        await processor.ApplyExecutionAsync(null, tocPayload, tocExecution, now.AddSeconds(1));

        Assert.AreEqual(2, await sources.SourceChapters.CountAsync());
        Assert.AreEqual(2, await library.Chapters.CountAsync());
        Assert.AreEqual(2, await library.ChapterMappings.CountAsync());
        Assert.AreEqual(2, await crawling.CrawlerTasks.CountAsync(task => task.IdempotencyKey.StartsWith("content:")));

        var contentTask = await crawling.CrawlerTasks.Where(task => task.IdempotencyKey.StartsWith("content:")).OrderBy(task => task.CreatedAtUtc).FirstAsync();
        var contentPayload = RuleCrawlerTaskPayload.Deserialize(contentTask.Payload);
        var body = string.Join('\n', Enumerable.Repeat("这是一段用于集成测试的完整小说正文，应该形成可阅读的 Canonical Content。", 40));
        var contentExecution = Execution("https://example.test/chapter/1", new Dictionary<string, string> { ["content"] = body });
        await processor.ApplyExecutionAsync(null, contentPayload, contentExecution, now.AddSeconds(2));

        var selection = await content.ChapterSelections.SingleAsync();
        var version = await content.ContentVersions.SingleAsync(item => item.Id == selection.ContentVersionId);
        var blob = await content.ContentBlobs.SingleAsync(item => item.Id == version.BlobId);
        Assert.IsGreaterThan(50d, version.QualityScore);
        StringAssert.Contains(blob.InlineContent!, "Canonical Content");
    }

    private static SourceOperationExecution Execution(string url, IReadOnlyDictionary<string, string> row) =>
        new(200, new Uri(url), string.Join('|', row.Values), row.Values.Sum(value => value.Length), new RuleExtractionResult([row]));
}
