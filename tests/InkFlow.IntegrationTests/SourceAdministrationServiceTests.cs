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
public sealed class SourceAdministrationServiceTests
{
    private const string ValidRuleJson = """
        {
          "schemaVersion": 1,
          "name": "Fixture",
          "baseUrl": "https://example.test",
          "capabilities": "BookInfo, Toc, Content, Update",
          "budget": {
            "maxRequests": 8,
            "maxBytes": 5242880,
            "maxRedirects": 5,
            "maxDepth": 8,
            "maxExecutionTimeMs": 10000,
            "maxRegexTimeMs": 250,
            "maxResultSize": 5000
          },
          "bookInfo": {
            "request": { "method": "GET", "url": "{{bookUrl}}" },
            "fields": {
              "externalId": { "kind": "Css", "expression": "meta[name='book-id']", "attribute": "content" },
              "title": { "kind": "Css", "expression": "h1" }
            }
          },
          "toc": {
            "request": { "method": "GET", "url": "{{bookUrl}}/chapters" },
            "fields": {
              "title": { "kind": "Css", "expression": ".chapter-title" },
              "url": { "kind": "Css", "expression": "a", "attribute": "href" }
            },
            "multiple": true
          },
          "content": {
            "request": { "method": "GET", "url": "{{chapterUrl}}" },
            "fields": {
              "content": { "kind": "Css", "expression": ".content" }
            }
          },
          "update": {
            "request": { "method": "GET", "url": "{{bookUrl}}/chapters" },
            "fields": {
              "title": { "kind": "Css", "expression": ".chapter-title" },
              "url": { "kind": "Css", "expression": "a", "attribute": "href" }
            },
            "multiple": true
          }
        }
        """;

    [TestMethod]
    public async Task Admin_flow_creates_source_versions_rules_and_enqueues_import_idempotently()
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
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 8, 21, 0, 0, 0, TimeSpan.Zero));
        var service = new SourceAdministrationService(sources, crawling, clock);

        var created = await service.CreateSourceAsync("Fixture", "https://example.test", "Official");
        Assert.AreEqual("Active", created.Status);
        Assert.IsTrue(service.ValidateRule(ValidRuleJson).IsValid);

        var firstPublish = await service.PublishRuleAsync(created.Id, ValidRuleJson);
        Assert.IsNotNull(firstPublish.Rule);
        Assert.AreEqual(1, firstPublish.Rule.Version);

        var secondPublish = await service.PublishRuleAsync(created.Id, ValidRuleJson);
        Assert.IsNotNull(secondPublish.Rule);
        Assert.AreEqual(2, secondPublish.Rule.Version);
        Assert.AreEqual(1, await sources.RuleVersions.CountAsync(rule => rule.Status == "Published"));
        Assert.AreEqual(1, await sources.RuleVersions.CountAsync(rule => rule.Status == "Superseded"));

        var firstImport = await service.EnqueueBookImportAsync(created.Id, "https://example.test/book/42", "42");
        var secondImport = await service.EnqueueBookImportAsync(created.Id, "https://example.test/book/42", "42");
        Assert.IsTrue(firstImport.Enqueued);
        Assert.IsFalse(secondImport.Enqueued);
        Assert.AreEqual(firstImport.TaskId, secondImport.TaskId);
        Assert.AreEqual(1, await crawling.CrawlerTasks.CountAsync());

        var task = await crawling.CrawlerTasks.AsNoTracking().SingleAsync();
        var payload = RuleCrawlerTaskPayload.Deserialize(task.Payload);
        Assert.AreEqual(SourceOperation.BookInfo, payload.Operation);
        Assert.AreEqual(secondPublish.Rule.Id, payload.RuleVersionId);
        Assert.AreEqual("https://example.test/book/42", payload.Variables["bookUrl"]);
    }

    [TestMethod]
    public async Task Publish_rejects_rule_from_different_origin()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:18")
            .WithDatabase("inkflow").WithUsername("inkflow").WithPassword("inkflow-test").Build();
        await postgres.StartAsync();
        var services = new ServiceCollection();
        services.AddInkFlowPersistence(postgres.GetConnectionString());
        await using var provider = services.BuildServiceProvider();
        await provider.MigrateInkFlowAsync();

        await using var scope = provider.CreateAsyncScope();
        var service = new SourceAdministrationService(
            scope.ServiceProvider.GetRequiredService<SourcesDbContext>(),
            scope.ServiceProvider.GetRequiredService<CrawlingDbContext>(),
            TimeProvider.System);
        var source = await service.CreateSourceAsync("Fixture", "https://example.test", "Official");
        var mismatched = ValidRuleJson.Replace("https://example.test", "https://other.test", StringComparison.Ordinal);

        var result = await service.PublishRuleAsync(source.Id, mismatched);

        Assert.IsNull(result.Rule);
        Assert.IsTrue(result.Errors.Any(error => error.Code == "RULE_BASE_URL_SOURCE_MISMATCH"));
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
