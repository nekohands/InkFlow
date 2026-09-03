using InkFlow.Modules.Crawling.Application;
using InkFlow.Modules.Sources.Application;
using InkFlow.Modules.Sources.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class SourceBookUrlResolverTests
{
    [TestMethod]
    public async Task Resolves_Only_A_Registered_Source_And_Adapter_Path()
    {
        var resolver = CreateResolver();

        var result = await resolver.ResolveAsync("https://books.example.com/novel/42.html");

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual("books", result.SourceId);
        Assert.AreEqual("42", result.ExternalBookId);
        Assert.AreEqual("https://books.example.com/novel/42.html", result.NormalizedUrl);
    }

    [TestMethod]
    public async Task Rejects_Query_Credentials_And_Unknown_Hosts()
    {
        var resolver = CreateResolver();

        var query = await resolver.ResolveAsync("https://books.example.com/novel/42.html?x=1");
        var credentials = await resolver.ResolveAsync("https://user:password@books.example.com/novel/42.html");
        var unknown = await resolver.ResolveAsync("https://other.example.com/novel/42.html");

        Assert.AreEqual("source-url.query-not-allowed", query.ErrorCode);
        Assert.AreEqual("source-url.credentials", credentials.ErrorCode);
        Assert.AreEqual("source-url.unresolved", unknown.ErrorCode);
    }

    [TestMethod]
    public async Task Rejects_Path_That_Is_Not_An_Exact_Book_Page()
    {
        var resolver = CreateResolver();

        var result = await resolver.ResolveAsync("https://books.example.com/novel/42/catalog");

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual("source-url.unresolved", result.ErrorCode);
    }

    [TestMethod]
    public async Task Disabled_Source_Is_Not_Eligible_For_Direct_Url_Collection()
    {
        var source = Source.Create(
            "books",
            "Books",
            "https://books.example.com",
            DateTimeOffset.UtcNow);
        source.Disable(DateTimeOffset.UtcNow);
        var resolver = new SourceBookUrlResolver(
            new SingleSourceRepository(source),
            new FixedAdapterFactory(new TestAdapter()));

        var result = await resolver.ResolveAsync("https://books.example.com/novel/42.html");

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual("source-url.unresolved", result.ErrorCode);
    }

    private static SourceBookUrlResolver CreateResolver()
    {
        var source = Source.Create(
            "books",
            "Books",
            "https://books.example.com",
            DateTimeOffset.UtcNow);
        var repository = new SingleSourceRepository(source);
        return new SourceBookUrlResolver(repository, new FixedAdapterFactory(new TestAdapter()));
    }

    private sealed class SingleSourceRepository(Source source) : ISourceRepository
    {
        public Task AddAsync(Source value, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<Source?> GetAsync(string sourceId, CancellationToken cancellationToken = default) =>
            Task.FromResult<Source?>(source.Id == sourceId ? source : null);
        public Task<IReadOnlyList<Source>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Source>>([source]);
        public Task SaveAsync(Source value, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FixedAdapterFactory(ISourceAdapter adapter) : ISourceAdapterFactory
    {
        public Task<ISourceAdapter?> GetAdapterAsync(string sourceId, CancellationToken cancellationToken = default) =>
            Task.FromResult<ISourceAdapter?>(adapter.SourceId == sourceId ? adapter : null);
    }

    private sealed class TestAdapter : ISourceAdapter
    {
        public string SourceId => "books";

        public bool TryResolveBookUrl(Uri url, out string externalBookId)
        {
            externalBookId = string.Empty;
            const string prefix = "/novel/";
            const string suffix = ".html";
            if (!url.AbsolutePath.StartsWith(prefix, StringComparison.Ordinal) ||
                !url.AbsolutePath.EndsWith(suffix, StringComparison.Ordinal))
            {
                return false;
            }

            var candidate = url.AbsolutePath[prefix.Length..^suffix.Length];
            if (candidate.Length == 0 || candidate.Any(character => character is '/' or '\\'))
            {
                return false;
            }

            externalBookId = candidate;
            return true;
        }

        public Task<IReadOnlyList<SourceSearchResult>> SearchAsync(string keyword, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SourceSearchResult>>([]);
        public Task<SourceBookInfo?> GetBookInfoAsync(string externalBookId, CancellationToken cancellationToken = default) =>
            Task.FromResult<SourceBookInfo?>(null);
        public Task<IReadOnlyList<SourceTocEntry>> GetTableOfContentsAsync(string externalBookId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SourceTocEntry>>([]);
        public Task<string?> GetChapterContentAsync(string externalChapterId, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);
    }
}
