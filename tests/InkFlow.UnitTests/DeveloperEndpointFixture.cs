using System.Diagnostics;
using System.Net.Http.Json;
using System.Security.Claims;
using InkFlow.Api;
using InkFlow.BuildingBlocks.Security;
using InkFlow.Modules.Billing.Application;
using InkFlow.Modules.Content.Application;
using InkFlow.Modules.Content.Domain;
using InkFlow.Modules.Developers.Application;
using InkFlow.Modules.Developers.Domain;
using InkFlow.Modules.Developers.Infrastructure.Authentication;
using InkFlow.Modules.Identity.Application;
using InkFlow.Modules.Identity.Domain;
using InkFlow.Modules.Library.Application;
using InkFlow.Modules.Library.Domain;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace InkFlow.UnitTests;

// Real production registration, routes, authentication handlers and authorization policies.
// Only service/storage boundaries are replaced. No network, database or real credentials.
internal sealed class DeveloperEndpointFixture : IAsyncDisposable, IDeveloperApiKeyValidator,
    IQuotaService, IDeveloperApplicationService, IEntitlementService, IAuditEventSink,
    ICanonicalBookRepository, IContentVersionRepository, IContentPolicyReader, IDistributedRateLimitCounter
{
    internal static readonly Guid UserId = new("00000000-0000-4000-8000-000000000001");
    internal static readonly Guid ApplicationId = new("00000000-0000-4000-8000-000000000002");
    internal static readonly Guid KeyId = new("00000000-0000-4000-8000-000000000003");
    internal static readonly Guid BookId = new("00000000-0000-4000-8000-000000000004");
    internal static readonly Guid ChapterId = new("00000000-0000-4000-8000-000000000005");
    internal static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-31T23:59:58.500Z");
    internal const string RawKey = "lf_dev_fixture-only-secret";
    internal List<string> Calls { get; } = [];
    internal List<QuotaReservationRequest> Reservations { get; } = [];
    internal List<(AuditEvent Event, CancellationToken Token)> Audits { get; } = [];
    internal QuotaReservationStatus QuotaStatus { get; set; } = QuotaReservationStatus.Reserved;
    internal DeveloperOperationStatus CommandStatus { get; set; } = DeveloperOperationStatus.Success;
    internal EntitlementOperationStatus AssignmentStatus { get; set; } = EntitlementOperationStatus.Success;
    internal Exception? QuotaFailure { get; set; }
    internal Exception? AuditFailure { get; set; }
    internal bool Missing { get; set; }
    internal bool MissingEntitlement { get; set; }
    internal bool MissingSnapshot { get; set; }
    internal bool Takedown { get; set; }
    internal bool RateLimited { get; set; }
    internal bool UseActivity { get; set; } = true;
    internal int LastLimit { get; private set; }
    internal string LastTrace { get; private set; } = "";
    internal CancellationToken QuotaToken { get; private set; }
    internal WebApplication App { get; private set; } = null!;
    internal HttpClient Client { get; private set; } = null!;
    internal QuotaSnapshot Snapshot { get; set; } = new(UserId, "free", 1,
        DateTimeOffset.Parse("2026-10-01T00:00:00Z"), DateTimeOffset.Parse("2026-11-01T00:00:00Z"),
        1000, 997, 3, "quota-v1");

    internal static async Task<DeveloperEndpointFixture> StartAsync(Action<WebApplicationBuilder>? configure = null)
    {
        var fixture = new DeveloperEndpointFixture();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.AddInkFlowApiServices();
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton<TimeProvider>(new FixedClock());
        builder.Services.AddSingleton<IIdentityService>(new IdentityStub());
        builder.Services.AddSingleton<IDeveloperApiKeyValidator>(fixture);
        builder.Services.AddSingleton<IQuotaService>(fixture);
        builder.Services.AddSingleton<IDeveloperApplicationService>(fixture);
        builder.Services.AddSingleton<IEntitlementService>(fixture);
        builder.Services.AddSingleton<IAuditEventSink>(fixture);
        builder.Services.AddSingleton<IDistributedRateLimitCounter>(fixture);
        builder.Services.AddSingleton(new CatalogQueryService(fixture, fixture, fixture));
        configure?.Invoke(builder);
        fixture.App = builder.Build();
        var app = fixture.App;
        app.UseInkFlowExceptionHandler();
        app.Use(async (context, next) =>
        {
            var activity = Activity.Current;
            if (!fixture.UseActivity) Activity.Current = null;
            context.TraceIdentifier = "rf05-trace";
            fixture.LastTrace = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
            try { await next(context); }
            finally { Activity.Current = activity; }
        });
        app.UseAuthentication();
        app.UseMiddleware<RequestAuditMiddleware>();
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/api/developer/v1"))
            {
                var result = await context.AuthenticateAsync(DeveloperAuthenticationDefaults.Scheme);
                if (result.Succeeded) context.User = result.Principal!;
            }
            await next(context);
        });
        app.UseRateLimiter();
        app.UseAuthorization();
        var api = app.MapGroup("/api/v1").RequireRateLimiting(ApiRateLimitPolicies.PublicPolicyName);
        api.MapDeveloperManagementEndpoints();
        api.MapDeveloperAdministrationEndpoints();
        app.MapDeveloperCatalogEndpoints();
        await app.StartAsync();
        fixture.Client = app.GetTestClient();
        return fixture;
    }

    internal async Task<HttpResponseMessage> SendAsync(string method, string path, string? bearer = null,
        string? key = null, object? body = null)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (bearer is not null) request.Headers.Add("Authorization", "Bearer " + bearer);
        if (key is not null) request.Headers.Add(DeveloperAuthenticationDefaults.ApiKeyHeader, key);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await Client.SendAsync(request);
    }

    // Invoke the actual bound route without the exception middleware so cancellation is observable.
    internal async Task<DefaultHttpContext> InvokeAsync(string pattern, string method, CancellationToken token)
    {
        var endpoint = ((IEndpointRouteBuilder)App).DataSources.SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>().Single(endpoint => endpoint.RoutePattern.RawText?.TrimEnd('/') == pattern.TrimEnd('/') &&
                endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Contains(method));
        await using var scope = App.Services.CreateAsyncScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider, RequestAborted = token };
        context.Request.Method = method;
        context.Request.Path = pattern;
        context.Response.Body = new MemoryStream();
        context.User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, UserId.ToString()),
            new Claim("sub", UserId.ToString()),
            new Claim("client_id", ApplicationId.ToString()),
            new Claim("developer_api_key_id", KeyId.ToString())], "fixture"));
        await endpoint.RequestDelegate!(context);
        return context;
    }

    public Task<DeveloperKeyAuthentication?> ValidateAsync(string rawKey, CancellationToken cancellationToken = default)
    {
        Calls.Add("authenticate");
        return Task.FromResult(rawKey is "valid" or "scope-denied" or "empty-id"
            ? new DeveloperKeyAuthentication(rawKey == "empty-id" ? Guid.Empty : UserId, ApplicationId, KeyId,
                rawKey == "scope-denied" ? "other" : "catalog.read", DeveloperEnvironment.Production)
            : null);
    }

    public Task<QuotaReservationResult> ReserveAsync(QuotaReservationRequest request, CancellationToken cancellationToken = default)
    {
        Calls.Add("reserve");
        Reservations.Add(request);
        QuotaToken = cancellationToken;
        if (QuotaFailure is not null) throw QuotaFailure;
        return Task.FromResult(new QuotaReservationResult(QuotaStatus, MissingSnapshot ? null : Snapshot));
    }

    public Task<QuotaSnapshot?> GetSnapshotAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        Calls.Add("snapshot");
        if (QuotaFailure is not null) throw QuotaFailure;
        return Task.FromResult<QuotaSnapshot?>(MissingSnapshot ? null : Snapshot);
    }

    public ValueTask AppendAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        Audits.Add((auditEvent, cancellationToken));
        if (auditEvent.Action.Contains('.'))
        {
            Calls.Add("command-audit");
            if (AuditFailure is not null) throw AuditFailure;
        }
        return ValueTask.CompletedTask;
    }

    private static DeveloperApplicationView Application => new(ApplicationId, UserId, "Fixture app", DeveloperEnvironment.Production, Now, null);
    private static DeveloperApiKeyView Key => new(KeyId, ApplicationId, "Fixture key", "lf_dev_fixture", "catalog.read", DeveloperEnvironment.Production, Now, Now.AddDays(30), null, null);
    private static PlanView Plan => new("free", 1, "Free", 1000, "quota-v1", ["developer.catalog.read"]);
    private static EntitlementView Entitlement => new(UserId, Plan, Now);
    private Task<DeveloperOperationResult<T>> Command<T>(string operation, T value) where T : class
    {
        Calls.Add(operation);
        return Task.FromResult(new DeveloperOperationResult<T>(CommandStatus, CommandStatus == DeveloperOperationStatus.Success ? value : null));
    }

    public Task<DeveloperOperationResult<DeveloperApplicationView>> CreateApplicationAsync(Guid userId, string? name, CancellationToken cancellationToken = default) => Command("create", Application);
    public Task<IReadOnlyList<DeveloperApplicationView>> ListApplicationsAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DeveloperApplicationView>>([Application]);
    public Task<DeveloperOperationStatus> RevokeApplicationAsync(Guid userId, Guid applicationId, CancellationToken cancellationToken = default) { Calls.Add("revoke-app"); return Task.FromResult(CommandStatus); }
    public Task<DeveloperOperationResult<IssuedDeveloperApiKey>> IssueKeyAsync(Guid userId, Guid applicationId, string? name, int? expiresInDays, CancellationToken cancellationToken = default) => Command("issue", new IssuedDeveloperApiKey(Key, RawKey));
    public Task<IReadOnlyList<DeveloperApiKeyView>> ListKeysAsync(Guid userId, Guid applicationId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DeveloperApiKeyView>>([Key]);
    public Task<DeveloperOperationResult<IssuedDeveloperApiKey>> RotateKeyAsync(Guid userId, Guid applicationId, Guid keyId, int? expiresInDays, CancellationToken cancellationToken = default) => Command("rotate", new IssuedDeveloperApiKey(Key, RawKey));
    public Task<DeveloperOperationStatus> RevokeKeyAsync(Guid userId, Guid applicationId, Guid keyId, CancellationToken cancellationToken = default) { Calls.Add("revoke-key"); return Task.FromResult(CommandStatus); }
    public Task<IReadOnlyList<PlanView>> ListPlansAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PlanView>>([Plan]);
    public Task<EntitlementView?> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default) { Calls.Add("entitlement"); return Task.FromResult<EntitlementView?>(MissingEntitlement ? null : Entitlement); }
    public Task<EntitlementOperationResult> AssignAsync(Guid actorId, Guid userId, string? planCode, string? reason, CancellationToken cancellationToken = default) { Calls.Add("assign"); return Task.FromResult(new EntitlementOperationResult(AssignmentStatus, AssignmentStatus == EntitlementOperationStatus.Success ? Entitlement : null)); }

    private static CanonicalBook Book => CanonicalBook.Rehydrate(BookId, "Stored book", "Author", Now, Now, [new CanonicalChapter(ChapterId, BookId, 0, "Chapter", Now)]);
    public Task<CanonicalBook?> GetAsync(Guid id, CancellationToken cancellationToken = default) { Calls.Add("read-book"); return Task.FromResult(Missing ? null : Book); }
    public Task<IReadOnlyList<CanonicalBookSummary>> ListSummariesAsync(int limit, CancellationToken cancellationToken = default) { Calls.Add("read-list"); LastLimit = limit; return Task.FromResult<IReadOnlyList<CanonicalBookSummary>>(Missing ? [] : [new(BookId, Book.Title, Book.Author, 1)]); }
    public Task<IReadOnlyList<CanonicalBookSummary>> SearchSummariesAsync(string query, int limit, CancellationToken cancellationToken = default) { Calls.Add("read-search"); LastLimit = limit; return Task.FromResult<IReadOnlyList<CanonicalBookSummary>>(Missing ? [] : [new(BookId, Book.Title, Book.Author, 1)]); }
    public Task<bool> IsTakedownAsync(Guid canonicalBookId, CancellationToken cancellationToken = default) => Task.FromResult(Takedown);
    public Task<Guid?> GetCurrentCanonicalBookIdAsync(Guid canonicalChapterId, CancellationToken cancellationToken = default) { Calls.Add("read-content-id"); return Task.FromResult<Guid?>(Missing ? null : BookId); }
    public Task<ContentVersion?> GetCurrentForChapterAsync(Guid canonicalChapterId, CancellationToken cancellationToken = default) { Calls.Add("read-content"); return Task.FromResult<ContentVersion?>(ContentVersion.Rehydrate(Guid.Empty, BookId, ChapterId, "fixture", "hash", "First\n\nSecond", 2, 100, true, Now)); }
    public Task AddAsync(CanonicalBook book, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<IReadOnlyList<CanonicalBook>> ListAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<CanonicalBook?> FindByTitleAuthorAsync(string title, string author, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task SaveAsync(CanonicalBook book, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task AddAsync(ContentVersion version, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<ContentVersion?> FindByHashAsync(Guid canonicalChapterId, string canonicalHash, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<IReadOnlyList<ContentVersion>> ListForChapterAsync(Guid canonicalChapterId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<IReadOnlyList<ContentVersion>> ListCurrentForBookAsync(Guid canonicalBookId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task SetCurrentAsync(Guid chapterId, Guid versionId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public RateLimitCounterResult TryAcquire(string key, int permitCount, int permitLimit, TimeSpan window) => new(!RateLimited, 1, TimeSpan.FromSeconds(7));
    public ValueTask<RateLimitCounterResult> TryAcquireAsync(string key, int permitCount, int permitLimit, TimeSpan window, CancellationToken cancellationToken = default) => ValueTask.FromResult(TryAcquire(key, permitCount, permitLimit, window));
    public async ValueTask DisposeAsync() { Client.Dispose(); await App.DisposeAsync(); }

    private sealed class FixedClock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }

    private sealed class IdentityStub : IIdentityService
    {
        public Task<AuthenticatedIdentity?> ValidateAccessTokenAsync(string accessToken, CancellationToken cancellationToken = default) => Task.FromResult(accessToken is "reader" or "admin" or "empty-id"
            ? new AuthenticatedIdentity(accessToken == "empty-id" ? Guid.Empty : UserId, "fixture@example.test", accessToken == "admin" ? UserRole.Administrator : UserRole.Reader, KeyId) : null);
        public Task<IdentityOperationResult> RegisterAsync(string email, string password, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IdentityOperationResult> LoginAsync(string email, string password, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IdentityOperationResult> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task LogoutAsync(Guid sessionId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IdentityProfile?> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ProfileOperationResult> UpdateProfileAsync(Guid userId, string? displayName, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PasswordChangeOperationResult> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AvatarOperationResult> UploadAvatarAsync(Guid userId, Stream content, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IdentityAvatar?> GetAvatarAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
