using InkFlow.Modules.Sources.Application;
using InkFlow.Modules.Sources.Domain;
using InkFlow.Modules.Sources.Infrastructure;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class LinovelibSourceDefinitionTests
{
    [TestMethod]
    public void Defines_Search_And_Validates_All_Capabilities()
    {
        var dsl = LinovelibSourceDefinition.BuildRuleDsl();

        Assert.AreEqual(LinovelibSourceDefinition.SourceId, dsl.SourceId);
        Assert.AreEqual(4, dsl.Rules.Count);
        Assert.AreEqual(0, SourceRuleDslValidator.Validate(dsl).Count);

        var search = dsl.Rules.Single(rule => rule.Capability == SourceCapability.Search);
        Assert.AreEqual(RuleHttpMethod.Post, search.Request.Method);
        Assert.AreEqual("/S6/", search.Request.PathTemplate);
        Assert.AreEqual("{key}", search.Request.Form["searchkey"]);
        Assert.AreEqual("/novel/", search.List!.IdPrefixToStrip);
        Assert.AreEqual(".html", search.List.IdSuffixToStrip);
    }

    [TestMethod]
    public async Task Search_Uses_Site_Form_And_Returns_Normalized_Book_Id()
    {
        var source = CreateSource();
        var http = new FixtureHttpClient(
            """<ul><li><a href="/novel/book-123.html">示例书</a></li></ul>""");
        var selector = new CssSelectorEvaluator();
        var adapter = new RuleBasedSourceAdapter(source, new RuleAdapter(http, selector), selector);

        var results = await adapter.SearchAsync("剑 来");

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual("book-123", results[0].ExternalBookId);
        Assert.AreEqual("示例书", results[0].Title);
        Assert.AreEqual(RuleHttpMethod.Post, http.LastRequest!.Method);
        Assert.AreEqual("/S6/", new Uri(http.LastRequest.Url).AbsolutePath);
        Assert.AreEqual("searchkey=%E5%89%91%20%E6%9D%A5", http.LastRequest.FormBody);
    }

    [TestMethod]
    public async Task Toc_Strips_Novel_Path_Before_Content_Path_Is_Built()
    {
        var source = CreateSource();
        var http = new FixtureHttpClient(
            """<ul><li><a href="/novel/book-123/1.html">第一章</a></li><li><a href="/novel/book-123/2.html">第二章</a></li></ul>""");
        var selector = new CssSelectorEvaluator();
        var adapter = new RuleBasedSourceAdapter(source, new RuleAdapter(http, selector), selector);

        var entries = await adapter.GetTableOfContentsAsync("book-123");

        Assert.AreEqual(2, entries.Count);
        Assert.AreEqual("book-123/1", entries[0].ExternalChapterId);
        Assert.AreEqual("book-123/2", entries[1].ExternalChapterId);
        Assert.AreEqual(0, entries[0].Index);
        Assert.AreEqual(1, entries[1].Index);
    }

    [TestMethod]
    public async Task Search_Result_Over_Max_Size_Fails_Closed()
    {
        var source = CreateSource();
        var http = new FixtureHttpClient(
            """<ul><li><a href="/novel/book-123.html">123456</a></li></ul>""");
        var selector = new CssSelectorEvaluator();
        var limits = new SourceRuleExecutionLimits { MaxResultSize = 5 };
        var adapter = new RuleBasedSourceAdapter(
            source,
            new RuleAdapter(http, selector, limits),
            selector);

        var results = await adapter.SearchAsync("剑 来");

        Assert.AreEqual(0, results.Count, "an oversized list result must not expose partial results");
    }

    private static Source CreateSource()
    {
        var source = Source.Create(
            LinovelibSourceDefinition.SourceId,
            "轻小说文库(linovelib)",
            LinovelibSourceDefinition.BaseUrl,
            DateTimeOffset.UtcNow);
        source.UpdateRuleDsl(LinovelibSourceDefinition.BuildRuleDsl(), DateTimeOffset.UtcNow);
        return source;
    }

    private sealed class FixtureHttpClient(string body) : ISourceHttpClient
    {
        public SourceHttpRequest? LastRequest { get; private set; }

        public Task<SourceHttpResponse> SendAsync(
            SourceHttpRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(new SourceHttpResponse(200, body));
        }
    }
}
