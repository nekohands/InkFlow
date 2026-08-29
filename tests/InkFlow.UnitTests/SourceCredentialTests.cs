using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using InkFlow.Modules.Sources.Application;
using InkFlow.Modules.Sources.Domain;
using InkFlow.Modules.Sources.Infrastructure;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class SourceCredentialTests
{
    private const string BaseUrl = "https://books.example.com";

    private sealed class RecordingHttpClient : ISourceHttpClient
    {
        public List<SourceHttpRequest> Requests { get; } = [];
        public Func<SourceHttpRequest, SourceHttpResponse> Responder { get; set; } =
            _ => new SourceHttpResponse(200, "<html/>");

        public Task<SourceHttpResponse> SendAsync(
            SourceHttpRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(Responder(request));
        }
    }

    private sealed class FixedCredentialProvider(SourceCredential? credential) : ISourceCredentialProvider
    {
        public int CallCount { get; private set; }
        public string? SourceId { get; private set; }
        public string? ReferenceId { get; private set; }
        public SourceCredentialOwnerScope? OwnerScope { get; private set; }

        public Task<SourceCredential?> ResolveAsync(
            SourceCredentialResolutionContext context,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            SourceId = context.SourceId;
            ReferenceId = context.CredentialReferenceId;
            OwnerScope = context.OwnerScope;
            return Task.FromResult(credential);
        }
    }

    private sealed class ThrowingCredentialProvider(string secret) : ISourceCredentialProvider
    {
        public Task<SourceCredential?> ResolveAsync(
            SourceCredentialResolutionContext context,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException($"provider failure: {secret}");
    }

    private sealed class HangingCredentialProvider : ISourceCredentialProvider
    {
        private readonly TaskCompletionSource<SourceCredential?> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<SourceCredential?> ResolveAsync(
            SourceCredentialResolutionContext context,
            CancellationToken cancellationToken = default) =>
            _completion.Task;
    }

    private sealed class PassthroughSelectorEvaluator : ISelectorEvaluator
    {
        public string? EvaluateFirst(
            string documentBody,
            RuleSelector selector,
            string? attributeName = null) =>
            selector.Expression == "next" && documentBody.Contains("next", StringComparison.Ordinal)
                ? "yes"
                : null;

        public IReadOnlyList<SelectorElementSnapshot> SelectAll(
            string documentBody,
            RuleSelector selector) => [];
    }

    private static CapabilityRule SingleRequestRule(
        IReadOnlyDictionary<string, string>? headers = null) =>
        new(
            SourceCapability.Search,
            new RuleRequest(
                RuleHttpMethod.Get,
                "/search",
                headers ?? new Dictionary<string, string>(),
                new Dictionary<string, string>(),
                new Dictionary<string, string>()),
            []);

    [TestMethod]
    public async Task Bearer_Reference_Is_Injected_At_Http_Seam_Without_Entering_Variables()
    {
        var http = new RecordingHttpClient();
        var provider = new FixedCredentialProvider(SourceCredential.BearerToken("bearer-secret"));
        var adapter = new RuleAdapter(
            http,
            new PassthroughSelectorEvaluator(),
            credentialProvider: provider);

        var result = await adapter.ExecuteAsync(
            SingleRequestRule(),
            BaseUrl,
            executionContext: new SourceExecutionContext("example-source", "reader"));

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors));
        Assert.AreEqual(1, provider.CallCount);
        Assert.AreEqual("example-source", provider.SourceId);
        Assert.AreEqual("reader", provider.ReferenceId);
        Assert.AreEqual("Bearer bearer-secret", http.Requests.Single().Headers["Authorization"]);
        Assert.AreEqual(0, result.Values.Count);
    }

    [TestMethod]
    public async Task Basic_And_Api_Key_Credentials_Use_Only_Their_Typed_Header_Forms()
    {
        var basicHttp = new RecordingHttpClient();
        var basic = new RuleAdapter(
            basicHttp,
            new PassthroughSelectorEvaluator(),
            credentialProvider: new FixedCredentialProvider(
                SourceCredential.BasicAuthentication("reader", "password")));

        var basicResult = await basic.ExecuteAsync(
            SingleRequestRule(),
            BaseUrl,
            executionContext: new SourceExecutionContext("example-source", "basic"));

        Assert.IsTrue(basicResult.IsSuccess, string.Join("; ", basicResult.Errors));
        Assert.AreEqual("Basic cmVhZGVyOnBhc3N3b3Jk", basicHttp.Requests.Single().Headers["Authorization"]);

        var apiKeyHttp = new RecordingHttpClient();
        var apiKey = new RuleAdapter(
            apiKeyHttp,
            new PassthroughSelectorEvaluator(),
            credentialProvider: new FixedCredentialProvider(
                SourceCredential.ApiKeyHeader("X-Api-Key", "api-secret")));

        var apiKeyResult = await apiKey.ExecuteAsync(
            SingleRequestRule(),
            BaseUrl,
            executionContext: new SourceExecutionContext("example-source", "api"));

        Assert.IsTrue(apiKeyResult.IsSuccess, string.Join("; ", apiKeyResult.Errors));
        Assert.AreEqual("api-secret", apiKeyHttp.Requests.Single().Headers["X-Api-Key"]);

        Assert.ThrowsExactly<ArgumentException>(() =>
            SourceCredential.ApiKeyHeader("aUtHoRiZaTiOn", "api-secret"));
    }

    [TestMethod]
    public async Task Credential_Header_Conflicts_Fail_Closed_Before_Http()
    {
        var http = new RecordingHttpClient();
        var adapter = new RuleAdapter(
            http,
            new PassthroughSelectorEvaluator(),
            credentialProvider: new FixedCredentialProvider(SourceCredential.BearerToken("secret")));

        var result = await adapter.ExecuteAsync(
            SingleRequestRule(new Dictionary<string, string>
            {
                ["Authorization"] = "static-value",
            }),
            BaseUrl,
            executionContext: new SourceExecutionContext("example-source", "reader"));

        Assert.IsFalse(result.IsSuccess);
        Assert.IsTrue(result.Errors.Any(error => error.Contains("credential header conflicts")));
        Assert.AreEqual(0, http.Requests.Count);

        var caseVariantHttp = new RecordingHttpClient();
        var caseVariantAdapter = new RuleAdapter(
            caseVariantHttp,
            new PassthroughSelectorEvaluator(),
            credentialProvider: new FixedCredentialProvider(SourceCredential.BearerToken("secret")));
        var caseVariant = await caseVariantAdapter.ExecuteAsync(
            SingleRequestRule(new Dictionary<string, string>
            {
                ["authorization"] = "static-value",
            }),
            BaseUrl,
            executionContext: new SourceExecutionContext("example-source", "reader"));

        Assert.IsFalse(caseVariant.IsSuccess);
        Assert.IsTrue(caseVariant.Errors.Any(error => error.Contains("credential header conflicts")));
        Assert.AreEqual(0, caseVariantHttp.Requests.Count);
    }

    [TestMethod]
    public async Task Invalid_Reference_And_Missing_Provider_Fail_Closed_Without_Resolving_Or_Sending()
    {
        var invalidHttp = new RecordingHttpClient();
        var invalidProvider = new FixedCredentialProvider(SourceCredential.BearerToken("secret"));
        var invalidAdapter = new RuleAdapter(
            invalidHttp,
            new PassthroughSelectorEvaluator(),
            credentialProvider: invalidProvider);

        var invalid = await invalidAdapter.ExecuteAsync(
            SingleRequestRule(),
            BaseUrl,
            executionContext: new SourceExecutionContext("example-source", "../secret"));

        Assert.IsFalse(invalid.IsSuccess);
        Assert.IsTrue(invalid.Errors.Any(error => error.Contains("credential reference")));
        Assert.AreEqual(0, invalidProvider.CallCount);
        Assert.AreEqual(0, invalidHttp.Requests.Count);

        var missingProviderHttp = new RecordingHttpClient();
        var missingProviderAdapter = new RuleAdapter(
            missingProviderHttp,
            new PassthroughSelectorEvaluator());

        var missingProvider = await missingProviderAdapter.ExecuteAsync(
            SingleRequestRule(),
            BaseUrl,
            executionContext: new SourceExecutionContext("example-source", "reader"));

        Assert.IsFalse(missingProvider.IsSuccess);
        Assert.IsTrue(missingProvider.Errors.Any(error => error.Contains("provider is unavailable")));
        Assert.AreEqual(0, missingProviderHttp.Requests.Count);
    }

    [TestMethod]
    public async Task Provider_Failure_Does_Not_Expose_Secret_Text()
    {
        const string secret = "provider-secret-that-must-not-escape";
        var http = new RecordingHttpClient();
        var adapter = new RuleAdapter(
            http,
            new PassthroughSelectorEvaluator(),
            credentialProvider: new ThrowingCredentialProvider(secret));

        var result = await adapter.ExecuteAsync(
            SingleRequestRule(),
            BaseUrl,
            executionContext: new SourceExecutionContext("example-source", "reader"));

        Assert.IsFalse(result.IsSuccess);
        Assert.IsTrue(result.Errors.Any(error => error.Contains("resolution failed")));
        Assert.IsFalse(result.Errors.Any(error => error.Contains(secret)));
        Assert.AreEqual(0, http.Requests.Count);
    }

    [TestMethod]
    public async Task Credential_Provider_Resolution_Uses_The_Execution_Time_Budget()
    {
        var http = new RecordingHttpClient();
        var adapter = new RuleAdapter(
            http,
            new PassthroughSelectorEvaluator(),
            new SourceRuleExecutionLimits
            {
                MaxExecutionTime = TimeSpan.FromMilliseconds(30),
            },
            new HangingCredentialProvider());

        var result = await adapter.ExecuteAsync(
            SingleRequestRule(),
            BaseUrl,
            executionContext: new SourceExecutionContext("example-source", "reader"));

        Assert.IsFalse(result.IsSuccess);
        Assert.IsTrue(result.Errors.Any(error => error.Contains("resolution timed out")));
        Assert.AreEqual(0, http.Requests.Count);
    }

    [TestMethod]
    public async Task Credential_Header_Is_Carried_Through_Bounded_Page_Number_Requests()
    {
        var http = new RecordingHttpClient
        {
            Responder = request => request.Url.EndsWith("page=1", StringComparison.Ordinal)
                ? new SourceHttpResponse(200, "next")
                : new SourceHttpResponse(200, "done"),
        };
        var rule = new CapabilityRule(
            SourceCapability.Search,
            new RuleRequest(
                RuleHttpMethod.Get,
                "/search",
                new Dictionary<string, string>(),
                new Dictionary<string, string> { ["page"] = "1" },
                new Dictionary<string, string>()),
            [],
            List: new RuleListBinding("a.book", "href", "/book/", string.Empty),
            Pagination: new RulePagination(
                new RuleSelector(SelectorKind.Css, "next"),
                "href",
                MaxPages: 2)
            {
                Mode = RulePaginationMode.PageNumber,
                ParameterName = "page",
                StartPage = 1,
                PageStep = 1,
            });
        var adapter = new RuleAdapter(
            http,
            new PassthroughSelectorEvaluator(),
            credentialProvider: new FixedCredentialProvider(SourceCredential.BearerToken("secret")));

        var result = await adapter.ExecuteAsync(
            rule,
            BaseUrl,
            executionContext: new SourceExecutionContext("example-source", "reader"));

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors));
        Assert.AreEqual(2, http.Requests.Count);
        Assert.IsTrue(http.Requests.All(request =>
            request.Headers.TryGetValue("Authorization", out var value) &&
            value == "Bearer secret"));
    }

    [TestMethod]
    public async Task Rule_Based_Source_Adapter_Uses_Execution_Context_For_Credentials()
    {
        var rule = new CapabilityRule(
            SourceCapability.Search,
            RuleRequest.Get("/search"),
            [],
            List: new RuleListBinding("a.book", "href", "/book/", string.Empty));
        var source = Source.Rehydrate(
            "example-source",
            "示例来源",
            BaseUrl,
            new SourceRuleDsl("1", "example-source", [rule]),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
        var http = new RecordingHttpClient();
        var adapter = new RuleBasedSourceAdapter(
            source,
            new RuleAdapter(
                http,
                new PassthroughSelectorEvaluator(),
                credentialProvider: new FixedCredentialProvider(
                    SourceCredential.BearerToken("adapter-secret"))),
            new PassthroughSelectorEvaluator());

        var results = await adapter.SearchAsync(
            "keyword",
            default,
            new SourceExecutionContext("example-source", "reader"));

        Assert.AreEqual(0, results.Count);
        Assert.AreEqual("Bearer adapter-secret", http.Requests.Single().Headers["Authorization"]);
    }

    [TestMethod]
    public async Task Rule_Based_Source_Adapter_Uses_Source_Default_When_Context_Reference_Is_Absent()
    {
        var rule = new CapabilityRule(
            SourceCapability.Search,
            RuleRequest.Get("/search"),
            [],
            List: new RuleListBinding("a.book", "href", "/book/", string.Empty));
        var source = Source.Rehydrate(
            "example-source",
            "示例来源",
            BaseUrl,
            new SourceRuleDsl("1", "example-source", [rule]),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            defaultCredentialReferenceId: "platform-reader");
        var provider = new FixedCredentialProvider(SourceCredential.BearerToken("default-secret"));
        var http = new RecordingHttpClient();
        var adapter = new RuleBasedSourceAdapter(
            source,
            new RuleAdapter(
                http,
                new PassthroughSelectorEvaluator(),
                credentialProvider: provider),
            new PassthroughSelectorEvaluator());

        var results = await adapter.SearchAsync("keyword");

        Assert.AreEqual(0, results.Count);
        Assert.AreEqual("platform-reader", provider.ReferenceId);
        Assert.AreEqual("Bearer default-secret", http.Requests.Single().Headers["Authorization"]);
    }

    [TestMethod]
    public async Task Source_Default_Reference_Always_Uses_Platform_Owner_Scope()
    {
        var rule = new CapabilityRule(
            SourceCapability.Search,
            RuleRequest.Get("/search"),
            [],
            List: new RuleListBinding("a.book", "href", "/book/", string.Empty));
        var source = Source.Rehydrate(
            "example-source",
            "示例来源",
            BaseUrl,
            new SourceRuleDsl("1", "example-source", [rule]),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            defaultCredentialReferenceId: "platform-reader");
        var provider = new FixedCredentialProvider(SourceCredential.BearerToken("default-secret"));
        var http = new RecordingHttpClient();
        var adapter = new RuleBasedSourceAdapter(
            source,
            new RuleAdapter(
                http,
                new PassthroughSelectorEvaluator(),
                credentialProvider: provider),
            new PassthroughSelectorEvaluator());

        var results = await adapter.SearchAsync(
            "keyword",
            default,
            new SourceExecutionContext(
                "example-source",
                null,
                SourceCredentialOwnerScope.ForUser(
                    Guid.Parse("0198f1b3-a0ca-7b23-8a2e-0123456789ab"))));

        Assert.AreEqual(0, results.Count);
        Assert.AreEqual("platform-reader", provider.ReferenceId);
        Assert.AreEqual(SourceCredentialOwnerKind.Platform, provider.OwnerScope!.Kind);
        Assert.IsNull(provider.OwnerScope.OwnerId);
    }

    [TestMethod]
    public async Task Explicit_Credential_Reference_Overrides_Source_Default()
    {
        var rule = new CapabilityRule(
            SourceCapability.Search,
            RuleRequest.Get("/search"),
            [],
            List: new RuleListBinding("a.book", "href", "/book/", string.Empty));
        var source = Source.Rehydrate(
            "example-source",
            "示例来源",
            BaseUrl,
            new SourceRuleDsl("1", "example-source", [rule]),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            defaultCredentialReferenceId: "platform-reader");
        var provider = new FixedCredentialProvider(SourceCredential.BearerToken("explicit-secret"));
        var http = new RecordingHttpClient();
        var adapter = new RuleBasedSourceAdapter(
            source,
            new RuleAdapter(
                http,
                new PassthroughSelectorEvaluator(),
                credentialProvider: provider),
            new PassthroughSelectorEvaluator());

        var results = await adapter.SearchAsync(
            "keyword",
            default,
            new SourceExecutionContext(
                "example-source",
                "user-reader",
                SourceCredentialOwnerScope.ForUser(
                    Guid.Parse("0198f1b3-a0ca-7b23-8a2e-0123456789ab"))));

        Assert.AreEqual(0, results.Count);
        Assert.AreEqual("user-reader", provider.ReferenceId);
        Assert.AreEqual(SourceCredentialOwnerKind.User, provider.OwnerScope!.Kind);
        Assert.AreEqual("Bearer explicit-secret", http.Requests.Single().Headers["Authorization"]);
    }

    [TestMethod]
    public async Task Configuration_Provider_Is_Source_And_Reference_Bounded()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SourceCredentials:example-source:reader:Type"] = "bearer",
                ["SourceCredentials:example-source:reader:Secret"] = "config-secret",
            })
            .Build();
        var provider = new ConfigurationSourceCredentialProvider(configuration);

        var credential = await provider.ResolveAsync(
            new SourceCredentialResolutionContext(
                "example-source",
                "reader",
                SourceCredentialOwnerScope.Platform));
        Assert.IsNotNull(credential);

        var http = new RecordingHttpClient();
        var adapter = new RuleAdapter(
            http,
            new PassthroughSelectorEvaluator(),
            credentialProvider: provider);
        var execution = await adapter.ExecuteAsync(
            SingleRequestRule(),
            BaseUrl,
            executionContext: new SourceExecutionContext("example-source", "reader"));
        Assert.IsTrue(execution.IsSuccess, string.Join("; ", execution.Errors));
        Assert.AreEqual("Bearer config-secret", http.Requests.Single().Headers["Authorization"]);

        var wrongSource = await provider.ResolveAsync(
            new SourceCredentialResolutionContext(
                "other-source",
                "reader",
                SourceCredentialOwnerScope.Platform));
        Assert.IsNull(wrongSource);

        var invalidReference = await provider.ResolveAsync(
            new SourceCredentialResolutionContext(
                "example-source",
                "reader:other",
                SourceCredentialOwnerScope.Platform));
        Assert.IsNull(invalidReference);

        var secretNotInToString = SourceCredential.BearerToken("config-secret");
        Assert.IsFalse(secretNotInToString.ToString().Contains("config-secret", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task Rule_Execution_Preserves_Explicit_User_Owner_Scope()
    {
        var ownerId = Guid.Parse("0198f1b3-a0ca-7b23-8a2e-0123456789ab");
        var provider = new FixedCredentialProvider(SourceCredential.BearerToken("user-secret"));
        var http = new RecordingHttpClient();
        var adapter = new RuleAdapter(
            http,
            new PassthroughSelectorEvaluator(),
            credentialProvider: provider);

        var result = await adapter.ExecuteAsync(
            SingleRequestRule(),
            BaseUrl,
            executionContext: new SourceExecutionContext(
                "example-source",
                "reader",
                SourceCredentialOwnerScope.ForUser(ownerId)));

        Assert.IsTrue(result.IsSuccess, string.Join("; ", result.Errors));
        Assert.IsNotNull(provider.OwnerScope);
        Assert.AreEqual(SourceCredentialOwnerKind.User, provider.OwnerScope.Kind);
        Assert.AreEqual(ownerId, provider.OwnerScope.OwnerId);
    }

    [TestMethod]
    public async Task Invalid_Owner_Scope_Fails_Before_Provider_And_Http()
    {
        var provider = new FixedCredentialProvider(SourceCredential.BearerToken("secret"));
        var http = new RecordingHttpClient();
        var adapter = new RuleAdapter(
            http,
            new PassthroughSelectorEvaluator(),
            credentialProvider: provider);

        var result = await adapter.ExecuteAsync(
            SingleRequestRule(),
            BaseUrl,
            executionContext: new SourceExecutionContext(
                "example-source",
                "reader",
                new SourceCredentialOwnerScope(SourceCredentialOwnerKind.User, Guid.Empty)));

        Assert.IsFalse(result.IsSuccess);
        StringAssert.Contains(string.Join("; ", result.Errors), "owner scope");
        Assert.AreEqual(0, provider.CallCount);
        Assert.AreEqual(0, http.Requests.Count);
    }

    [TestMethod]
    public async Task Configuration_Provider_Rejects_NonPlatform_Owner_Scope()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SourceCredentials:example-source:reader:Type"] = "bearer",
                ["SourceCredentials:example-source:reader:Secret"] = "config-secret",
            })
            .Build();
        var provider = new ConfigurationSourceCredentialProvider(configuration);

        var credential = await provider.ResolveAsync(
            new SourceCredentialResolutionContext(
                "example-source",
                "reader",
                SourceCredentialOwnerScope.ForUser(
                    Guid.Parse("0198f1b3-a0ca-7b23-8a2e-0123456789ab"))));

        Assert.IsNull(credential);
    }

    [TestMethod]
    public void Owner_Scope_Requires_A_Stable_Identity_And_Resolution_Context_Is_Validated()
    {
        Assert.Throws<ArgumentException>(() => SourceCredentialOwnerScope.ForUser(Guid.Empty));
        Assert.Throws<ArgumentException>(
            () => SourceCredentialOwnerScope.ForOrganization(Guid.Empty));

        var organizationId = Guid.Parse("0198f1b3-a0ca-7b23-8a2e-1123456789ab");
        var organizationScope = SourceCredentialOwnerScope.ForOrganization(organizationId);
        Assert.AreEqual(SourceCredentialOwnerKind.Organization, organizationScope.Kind);
        Assert.AreEqual(organizationId, organizationScope.OwnerId);
        Assert.IsTrue(
            new SourceCredentialResolutionContext(
                "example-source",
                "reader",
                organizationScope).IsValid);
        Assert.IsFalse(
            new SourceCredentialResolutionContext(
                "example-source",
                "reader",
                null).IsValid);
        Assert.IsFalse(
            new SourceCredentialResolutionContext(
                "example/source",
                "reader",
                SourceCredentialOwnerScope.Platform).IsValid);
    }

    [TestMethod]
    public async Task Source_Adapter_Context_Rejects_Credentials_For_Unsupported_Code_Adapters()
    {
        ISourceAdapter adapter = new UnsupportedAdapter();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            adapter.SearchAsync(
                "keyword",
                default,
                executionContext: new SourceExecutionContext("code-source", "reader")));
    }

    private sealed class UnsupportedAdapter : ISourceAdapter
    {
        public string SourceId => "code-source";

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
