using InkFlow.BuildingBlocks.Persistence;
using InkFlow.Modules.Crawling.Orchestration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Testcontainers.PostgreSql;

namespace InkFlow.IntegrationTests;

[TestClass]
public sealed class SourceDebuggerServiceTests
{
    [TestMethod]
    public async Task Debugger_rejects_different_origin_before_network_execution()
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
        var now = DateTimeOffset.UtcNow;
        var sourceId = Guid.CreateVersion7();
        sources.Sources.Add(new SourceRecord
        {
            Id = sourceId,
            Name = "Fixture",
            BaseUrl = "https://example.test",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        await sources.SaveChangesAsync();

        var rule = ValidBookInfoRule("https://other.test", "https://other.test/book/1");
        var result = await new SourceDebuggerService(sources).DebugAsync(sourceId, "BookInfo", rule, null);

        Assert.IsFalse(result.Executed);
        Assert.IsTrue(result.ValidationErrors.Any(error => error.Code == "RULE_BASE_URL_SOURCE_MISMATCH"));
    }

    [TestMethod]
    public async Task Debugger_keeps_private_address_blocked_by_safe_http_boundary()
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
        var now = DateTimeOffset.UtcNow;
        var sourceId = Guid.CreateVersion7();
        sources.Sources.Add(new SourceRecord
        {
            Id = sourceId,
            Name = "PrivateTarget",
            BaseUrl = "http://127.0.0.1",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        await sources.SaveChangesAsync();

        var rule = ValidBookInfoRule("http://127.0.0.1", "http://127.0.0.1/book/1");
        var result = await new SourceDebuggerService(sources).DebugAsync(sourceId, "BookInfo", rule, null);

        Assert.IsFalse(result.Executed);
        Assert.AreEqual("RULE_DEBUG_EXECUTION_FAILED", result.ErrorCode);
        StringAssert.Contains(result.ErrorMessage ?? string.Empty, "blocked", StringComparison.OrdinalIgnoreCase);
    }

    private static string ValidBookInfoRule(string baseUrl, string requestUrl) => $$"""
        {
          "schemaVersion": 1,
          "name": "Fixture",
          "baseUrl": "{{baseUrl}}",
          "capabilities": "BookInfo",
          "budget": {
            "maxRequests": 2,
            "maxBytes": 1048576,
            "maxRedirects": 2,
            "maxDepth": 4,
            "maxExecutionTimeMs": 2000,
            "maxRegexTimeMs": 100,
            "maxResultSize": 100
          },
          "bookInfo": {
            "request": { "method": "GET", "url": "{{requestUrl}}" },
            "fields": {
              "title": { "kind": "Css", "expression": "h1" }
            }
          }
        }
        """;
}
