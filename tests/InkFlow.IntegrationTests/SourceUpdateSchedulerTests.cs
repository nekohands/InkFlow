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
public sealed class SourceUpdateSchedulerTests
{
    [TestMethod]
    public async Task Due_source_book_is_scheduled_once_per_time_slot()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:18")
            .WithDatabase("inkflow").WithUsername("inkflow").WithPassword("inkflow-test").Build();
        await postgres.StartAsync();
        var services = new ServiceCollection();
        services.AddInkFlowPersistence(postgres.GetConnectionString());
        await using var provider = services.BuildServiceProvider();
        await provider.MigrateInkFlowAsync();

        await using var scope = provider.CreateAsyncScope();
        var sources = scope.ServiceProvider.GetRequiredService<SourcesDbContext>();
        var crawling = scope.ServiceProvider.GetRequiredService<CrawlingDbContext>();
        var now = new DateTimeOffset(2026, 8, 20, 16, 0, 0, TimeSpan.Zero);
        var sourceId = Guid.CreateVersion7();
        var ruleId = Guid.CreateVersion7();
        var bookId = Guid.CreateVersion7();
        sources.Sources.Add(new SourceRecord
        {
            Id = sourceId, Name = "Fixture", BaseUrl = "https://example.test", Status = "Active", HealthScore = 95,
            ActiveRuleVersionId = ruleId, CreatedAtUtc = now.AddDays(-1), UpdatedAtUtc = now.AddDays(-1)
        });
        sources.RuleVersions.Add(new SourceRuleVersionRecord
        {
            Id = ruleId, SourceId = sourceId, Version = 1, SchemaVersion = 1, Status = "Published", RuleJson = "{}",
            CreatedAtUtc = now.AddDays(-1), PublishedAtUtc = now.AddDays(-1)
        });
        sources.SourceBooks.Add(new SourceBookRecord
        {
            Id = bookId, SourceId = sourceId, ExternalId = "book-1", Url = "https://example.test/book/1",
            Title = "测试小说", Author = "作者", LastCheckedAtUtc = now.AddHours(-1),
            CreatedAtUtc = now.AddDays(-1), UpdatedAtUtc = now.AddHours(-1)
        });
        await sources.SaveChangesAsync();

        var scheduler = new SourceUpdateScheduler(sources, crawling);
        var first = await scheduler.ScheduleDueAsync(now, TimeSpan.FromMinutes(15));
        var second = await scheduler.ScheduleDueAsync(now.AddMinutes(1), TimeSpan.FromMinutes(15));

        Assert.AreEqual(1, first.Enqueued);
        Assert.AreEqual(0, second.Enqueued);
        Assert.AreEqual(1, await crawling.CrawlerTasks.CountAsync());
        var task = await crawling.CrawlerTasks.SingleAsync();
        var payload = RuleCrawlerTaskPayload.Deserialize(task.Payload);
        Assert.AreEqual(SourceOperation.Toc, payload.Operation);
        Assert.AreEqual(bookId, payload.SourceBookId);
        Assert.AreEqual(ruleId, payload.RuleVersionId);
    }

    [TestMethod]
    public async Task Unhealthy_or_inactive_sources_are_not_scheduled()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:18")
            .WithDatabase("inkflow").WithUsername("inkflow").WithPassword("inkflow-test").Build();
        await postgres.StartAsync();
        var services = new ServiceCollection();
        services.AddInkFlowPersistence(postgres.GetConnectionString());
        await using var provider = services.BuildServiceProvider();
        await provider.MigrateInkFlowAsync();

        await using var scope = provider.CreateAsyncScope();
        var sources = scope.ServiceProvider.GetRequiredService<SourcesDbContext>();
        var crawling = scope.ServiceProvider.GetRequiredService<CrawlingDbContext>();
        var now = DateTimeOffset.UtcNow;
        var sourceId = Guid.CreateVersion7();
        var ruleId = Guid.CreateVersion7();
        sources.Sources.Add(new SourceRecord
        {
            Id = sourceId, Name = "Broken", BaseUrl = "https://example.test", Status = "Active", HealthScore = 20,
            ActiveRuleVersionId = ruleId, CreatedAtUtc = now.AddDays(-1), UpdatedAtUtc = now
        });
        sources.RuleVersions.Add(new SourceRuleVersionRecord
        {
            Id = ruleId, SourceId = sourceId, Version = 1, SchemaVersion = 1, Status = "Published", RuleJson = "{}",
            CreatedAtUtc = now.AddDays(-1), PublishedAtUtc = now.AddDays(-1)
        });
        sources.SourceBooks.Add(new SourceBookRecord
        {
            Id = Guid.CreateVersion7(), SourceId = sourceId, ExternalId = "book-1", Url = "https://example.test/book/1",
            CreatedAtUtc = now.AddDays(-1), UpdatedAtUtc = now.AddDays(-1)
        });
        await sources.SaveChangesAsync();

        var scheduler = new SourceUpdateScheduler(sources, crawling);
        var result = await scheduler.ScheduleDueAsync(now, TimeSpan.FromMinutes(15));

        Assert.AreEqual(0, result.Enqueued);
        Assert.AreEqual(0, await crawling.CrawlerTasks.CountAsync());
    }
}
