using InkFlow.Modules.Sources.Application;
using InkFlow.Modules.Sources.Domain;
using InkFlow.Modules.Sources.Infrastructure;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class SourceAdapterFactoryTests
{
    private static readonly DateTimeOffset T0 =
        new(2026, 8, 30, 7, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task Returns_registered_code_adapter_after_loading_source_repository()
    {
        var codeAdapter = new StubAdapter("trusted-code");
        var repository = new TrackingSourceRepository(
            Source.Create("trusted-code", "可信代码来源", "https://trusted-code.example", T0));
        var factory = CreateFactory(repository, [codeAdapter]);

        var result = await factory.GetAdapterAsync(codeAdapter.SourceId);

        Assert.AreSame(codeAdapter, result);
        Assert.AreEqual(1, repository.GetCallCount);
    }

    [TestMethod]
    public async Task Builds_rule_based_adapter_for_source_with_rule_document()
    {
        var source = CreateRuleSource();
        var repository = new TrackingSourceRepository(source);
        var factory = CreateFactory(repository);

        var result = await factory.GetAdapterAsync(source.Id);

        Assert.IsInstanceOfType<RuleBasedSourceAdapter>(result);
        Assert.AreEqual(1, repository.GetCallCount);
    }

    [TestMethod]
    public async Task Returns_null_for_missing_source_or_source_without_rule_document()
    {
        var repository = new TrackingSourceRepository(
            Source.Create("without-rule", "无规则来源", "https://example.com", T0));
        var factory = CreateFactory(repository);

        var withoutRule = await factory.GetAdapterAsync("without-rule");
        var missing = await factory.GetAdapterAsync("missing");

        Assert.IsNull(withoutRule);
        Assert.IsNull(missing);
        Assert.AreEqual(2, repository.GetCallCount);
    }

    [TestMethod]
    public async Task Does_Not_Return_An_Adapter_For_A_Disabled_Source()
    {
        var source = CreateRuleSource();
        source.Disable(T0.AddMinutes(1));
        var repository = new TrackingSourceRepository(source);
        var factory = CreateFactory(repository);

        var result = await factory.GetAdapterAsync(source.Id);

        Assert.IsNull(result);
    }

    private static SourceAdapterFactory CreateFactory(
        TrackingSourceRepository repository,
        IEnumerable<ISourceAdapter>? codeAdapters = null)
    {
        var selector = new RuleSelectorEvaluator();
        var ruleAdapter = new RuleAdapter(new NoopSourceHttpClient(), selector);
        return new SourceAdapterFactory(
            repository,
            ruleAdapter,
            selector,
            codeAdapters ?? []);
    }

    private static Source CreateRuleSource()
    {
        var source = Source.Create(
            LinovelibSourceDefinition.SourceId,
            "轻小说文库(linovelib)",
            LinovelibSourceDefinition.BaseUrl,
            T0);
        source.UpdateRuleDsl(LinovelibSourceDefinition.BuildRuleDsl(), T0);
        return source;
    }

    private sealed class TrackingSourceRepository(Source? source = null) : ISourceRepository
    {
        private readonly Source? _source = source;

        public int GetCallCount { get; private set; }

        public Task AddAsync(Source source, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<Source?> GetAsync(
            string sourceId,
            CancellationToken cancellationToken = default)
        {
            GetCallCount++;
            return Task.FromResult<Source?>(
                _source?.Id == sourceId ? _source : null);
        }

        public Task<IReadOnlyList<Source>> ListAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Source>>(
                _source is null ? [] : [_source]);

        public Task SaveAsync(Source source, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class NoopSourceHttpClient : ISourceHttpClient
    {
        public Task<SourceHttpResponse> SendAsync(
            SourceHttpRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new SourceHttpResponse(200, string.Empty));
    }

    private sealed class StubAdapter(string sourceId) : ISourceAdapter
    {
        public string SourceId { get; } = sourceId;

        public Task<IReadOnlyList<SourceSearchResult>> SearchAsync(
            string keyword,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SourceSearchResult>>([]);

        public Task<SourceBookInfo?> GetBookInfoAsync(
            string externalBookId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<SourceBookInfo?>(null);

        public Task<IReadOnlyList<SourceTocEntry>> GetTableOfContentsAsync(
            string externalBookId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SourceTocEntry>>([]);

        public Task<string?> GetChapterContentAsync(
            string externalChapterId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);
    }
}
