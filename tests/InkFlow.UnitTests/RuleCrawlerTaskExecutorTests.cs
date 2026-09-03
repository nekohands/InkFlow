using InkFlow.Modules.Sources.Application;
using InkFlow.Modules.Crawling.Domain;
using InkFlow.Modules.Crawling.Infrastructure;
using InkFlow.Modules.Sources.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class RuleCrawlerTaskExecutorTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 25, 16, 0, 0, TimeSpan.Zero);

    private sealed class FakeSourceRepository(Source? source) : ISourceRepository
    {
        public Task AddAsync(Source source, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<Source?> GetAsync(string sourceId, CancellationToken cancellationToken = default)
            => Task.FromResult(source is not null && source.Id == sourceId ? source : null);
        public Task<IReadOnlyList<Source>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Source>>(source is null ? [] : [source]);
        public Task SaveAsync(Source source, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeHttpClient : ISourceHttpClient
    {
        public int CallCount { get; private set; }
        public Func<SourceHttpRequest, SourceHttpResponse> Responder { get; set; } =
            _ => new SourceHttpResponse(200, "<html><h1>标题</h1></html>");

        public Task<SourceHttpResponse> SendAsync(SourceHttpRequest request, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(Responder(request));
        }
    }

    private sealed class FixedCredentialProvider(SourceCredential credential) : ISourceCredentialProvider
    {
        public string? ReferenceId { get; private set; }
        public SourceCredentialOwnerScope? OwnerScope { get; private set; }

        public Task<SourceCredential?> ResolveAsync(
            SourceCredentialResolutionContext context,
            CancellationToken cancellationToken = default)
        {
            ReferenceId = context.CredentialReferenceId;
            OwnerScope = context.OwnerScope;
            return Task.FromResult<SourceCredential?>(credential);
        }
    }

    private sealed class PassthroughSelectorEvaluator : ISelectorEvaluator
    {
        public string? EvaluateFirst(string documentBody, RuleSelector selector, string? attributeName = null) =>
            selector.Expression.Contains("title") || selector.Expression.Contains("h1") ? "标题" : null;

        public IReadOnlyList<SelectorElementSnapshot> SelectAll(string documentBody, RuleSelector selector) => [];
    }

    private static Source SourceWithSearchRule(string? defaultCredentialReferenceId = null) =>
        Source.Rehydrate(
            "example-source",
            "示例来源",
            "https://books.example.com",
            new SourceRuleDsl("1", "example-source",
            [
                new CapabilityRule(
                    SourceCapability.Search,
                    RuleRequest.Get("/search?q={query}"),
                    [new RuleField("title", new RuleSelector(SelectorKind.Css, "h1.title"), null, [])]),
            ]),
            T0,
            T0,
            defaultCredentialReferenceId);

    private static CrawlerTask SearchTask() =>
        CrawlerTask.Create(
            new CrawlPayload("example-source", SourceCapability.Search,
                new Dictionary<string, string> { ["query"] = "剑来" }),
            maxAttempts: 3,
            T0);

    [TestMethod]
    public async Task Happy_Path_Executes_Rule_Via_Http()
    {
        var http = new FakeHttpClient();
        var executor = new RuleCrawlerTaskExecutor(
            new FakeSourceRepository(SourceWithSearchRule()),
            new RuleAdapter(http, new PassthroughSelectorEvaluator()));

        var outcome = await executor.ExecuteAsync(SearchTask());

        Assert.IsTrue(outcome.Succeeded, outcome.FailureReason);
        Assert.AreEqual(1, http.CallCount);
    }

    [TestMethod]
    public async Task Unknown_Source_Fails_Without_Hitting_Http()
    {
        var http = new FakeHttpClient();
        var executor = new RuleCrawlerTaskExecutor(
            new FakeSourceRepository(null),
            new RuleAdapter(http, new PassthroughSelectorEvaluator()));

        var outcome = await executor.ExecuteAsync(SearchTask());

        Assert.IsFalse(outcome.Succeeded);
        StringAssert.Contains(outcome.FailureReason!, "does not exist");
        Assert.AreEqual(0, http.CallCount);
    }

    [TestMethod]
    public async Task Source_Without_Rules_Fails()
    {
        var bare = Source.Create("bare-source", "无规则来源", "https://books.example.com", T0);
        var http = new FakeHttpClient();
        var executor = new RuleCrawlerTaskExecutor(
            new FakeSourceRepository(bare),
            new RuleAdapter(http, new PassthroughSelectorEvaluator()));

        var taskWithOtherSource = CrawlerTask.Create(
            new CrawlPayload("bare-source", SourceCapability.Search,
                new Dictionary<string, string> { ["query"] = "q" }),
            maxAttempts: 3, T0);

        var outcome = await executor.ExecuteAsync(taskWithOtherSource);

        Assert.IsFalse(outcome.Succeeded);
        StringAssert.Contains(outcome.FailureReason!, "no rule DSL");
        Assert.AreEqual(0, http.CallCount);
    }

    [TestMethod]
    public async Task Disabled_Source_Fails_Without_Hitting_Http()
    {
        var source = SourceWithSearchRule();
        source.Disable(T0.AddMinutes(1));
        var http = new FakeHttpClient();
        var executor = new RuleCrawlerTaskExecutor(
            new FakeSourceRepository(source),
            new RuleAdapter(http, new PassthroughSelectorEvaluator()));

        var outcome = await executor.ExecuteAsync(SearchTask());

        Assert.IsFalse(outcome.Succeeded);
        StringAssert.Contains(outcome.FailureReason!, "is disabled");
        Assert.AreEqual(0, http.CallCount);
    }

    [TestMethod]
    public async Task Missing_Capability_Rule_Fails()
    {
        var http = new FakeHttpClient();
        var executor = new RuleCrawlerTaskExecutor(
            new FakeSourceRepository(SourceWithSearchRule()),
            new RuleAdapter(http, new PassthroughSelectorEvaluator()));

        // 来源只装了 Search 规则，任务却要求 Content。
        var tocTask = CrawlerTask.Create(
            new CrawlPayload("example-source", SourceCapability.Content,
                new Dictionary<string, string> { ["chapterId"] = "1" }),
            maxAttempts: 3, T0);

        var outcome = await executor.ExecuteAsync(tocTask);

        Assert.IsFalse(outcome.Succeeded);
        StringAssert.Contains(outcome.FailureReason!, "declares no rule");
        Assert.AreEqual(0, http.CallCount);
    }

    [TestMethod]
    public async Task Rule_Failures_Are_Aggregated_Into_Outcome_Reason()
    {
        var http = new FakeHttpClient { Responder = _ => new SourceHttpResponse(500, "") };
        var executor = new RuleCrawlerTaskExecutor(
            new FakeSourceRepository(SourceWithSearchRule()),
            new RuleAdapter(http, new PassthroughSelectorEvaluator()));

        var outcome = await executor.ExecuteAsync(SearchTask());

        Assert.IsFalse(outcome.Succeeded);
        StringAssert.Contains(outcome.FailureReason!, "500");
    }

    [TestMethod]
    public async Task Task_Without_Explicit_Credential_Uses_Source_Default()
    {
        var http = new FakeHttpClient();
        var provider = new FixedCredentialProvider(SourceCredential.BearerToken("default-secret"));
        var executor = new RuleCrawlerTaskExecutor(
            new FakeSourceRepository(SourceWithSearchRule("platform-reader")),
            new RuleAdapter(
                http,
                new PassthroughSelectorEvaluator(),
                credentialProvider: provider));

        var outcome = await executor.ExecuteAsync(SearchTask());

        Assert.IsTrue(outcome.Succeeded, outcome.FailureReason);
        Assert.AreEqual("platform-reader", provider.ReferenceId);
        Assert.AreEqual(SourceCredentialOwnerKind.Platform, provider.OwnerScope!.Kind);
        Assert.IsNull(provider.OwnerScope.OwnerId);
    }

    [TestMethod]
    public async Task Task_Explicit_Credential_Overrides_Source_Default()
    {
        var http = new FakeHttpClient();
        var provider = new FixedCredentialProvider(SourceCredential.BearerToken("explicit-secret"));
        var executor = new RuleCrawlerTaskExecutor(
            new FakeSourceRepository(SourceWithSearchRule("platform-reader")),
            new RuleAdapter(
                http,
                new PassthroughSelectorEvaluator(),
                credentialProvider: provider));
        var task = CrawlerTask.Create(
            new CrawlPayload(
                "example-source",
                SourceCapability.Search,
                new Dictionary<string, string> { ["query"] = "剑来" },
                "user-reader"),
            maxAttempts: 3,
            T0);

        var outcome = await executor.ExecuteAsync(task);

        Assert.IsTrue(outcome.Succeeded, outcome.FailureReason);
        Assert.AreEqual("user-reader", provider.ReferenceId);
    }
}
