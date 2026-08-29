// Public Content API:目录/内容端点只读(数据来自已落库正典数据),
// /search 端点是唯一的写侧入口——触发来源发现(幂等导入+匹配),随后仍从落库数据返回。
using System.Security.Claims;
using System.Text.Json;
using InkFlow.Api;
using InkFlow.BuildingBlocks.Application;
using InkFlow.BuildingBlocks.Observability;
using InkFlow.BuildingBlocks.Persistence;
using InkFlow.BuildingBlocks.Security;
using InkFlow.Modules.Content.Application;
using InkFlow.Modules.Content.Domain;
using InkFlow.Modules.Content.Infrastructure.Persistence;
using InkFlow.Modules.Billing.Application;
using InkFlow.Modules.Billing.Infrastructure.Persistence;
using InkFlow.Modules.Billing.Infrastructure;
using InkFlow.Modules.Operations.Application;
using InkFlow.Modules.Operations.Infrastructure.Persistence;
using InkFlow.Modules.Crawling.Application;
using InkFlow.Modules.Crawling.Infrastructure.Persistence;
using InkFlow.Modules.Legado.Application;
using InkFlow.Modules.Identity.Application;
using InkFlow.Modules.Identity.Domain;
using InkFlow.Modules.Identity.Infrastructure.Authentication;
using InkFlow.Modules.Identity.Infrastructure.Credentials;
using InkFlow.Modules.Identity.Infrastructure.Persistence;
using InkFlow.Modules.Developers.Application;
using InkFlow.Modules.Developers.Domain;
using InkFlow.Modules.Developers.Infrastructure.Authentication;
using InkFlow.Modules.Developers.Infrastructure.Credentials;
using InkFlow.Modules.Developers.Infrastructure.Persistence;
using InkFlow.Modules.Library.Application;
using InkFlow.Modules.Library.Domain;
using InkFlow.Modules.Library.Infrastructure.Persistence;
using InkFlow.Modules.Reading.Application;
using InkFlow.Modules.Reading.Infrastructure.Persistence;
using InkFlow.Modules.Sources.Application;
using InkFlow.Modules.Sources.Domain;
using InkFlow.Modules.Sources.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddInkFlowObservability("InkFlow.Api");
builder.Services.AddInkFlowApiRateLimiting(
    ApiRateLimitOptions.FromConfiguration(builder.Configuration));

// 来源发现按需使用老站编码(kanunu8 GB18030 等)。
System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

var databaseConnectionString =
    builder.Configuration.GetConnectionString("Database")
    ?? "Host=localhost;Port=5432;Database=inkflow;Username=inkflow;Password=inkflow";

builder.Services.AddDbContext<AuditDbContext>(options =>
    options.UseNpgsql(databaseConnectionString));
builder.Services.AddDbContextFactory<OperationsDbContext>(options =>
    options.UseNpgsql(databaseConnectionString));
builder.Services.AddDbContext<IdentityDbContext>(options =>
    options.UseNpgsql(databaseConnectionString));
builder.Services.AddDbContext<DeveloperDbContext>(options =>
    options.UseNpgsql(databaseConnectionString));
builder.Services.AddDbContext<BillingDbContext>(options =>
    options.UseNpgsql(databaseConnectionString));
builder.Services.AddDbContext<CrawlingDbContext>(options =>
    options.UseNpgsql(databaseConnectionString));
builder.Services.AddScoped<ITransactionalOutboxWriter, EfTransactionalOutboxWriter>();
builder.Services.AddScoped<LoggingAuditEventSink>();
builder.Services.AddScoped<PersistentAuditEventSink>();
builder.Services.AddScoped<IAuditEventSink, CompositeAuditEventSink>();
builder.Services.AddScoped<IAuditEventReader, EfAuditEventReader>();

builder.Services.AddScoped<IUserRepository, EfUserRepository>();
builder.Services.AddScoped<IIdentitySessionRepository, EfIdentitySessionRepository>();
builder.Services.AddScoped<ILegadoAccessTokenRepository, EfLegadoAccessTokenRepository>();
builder.Services.AddScoped<IResourcePermissionRepository, EfResourcePermissionRepository>();
builder.Services.AddSingleton(IdentityOptions.FromConfiguration(builder.Configuration));
builder.Services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
builder.Services.AddSingleton<IOpaqueTokenGenerator, SecureOpaqueTokenGenerator>();
builder.Services.AddScoped<IIdentityService, IdentityService>();
builder.Services.AddScoped<ILegadoAccessTokenService, LegadoAccessTokenService>();
builder.Services.AddScoped<IResourcePermissionService, ResourcePermissionService>();
builder.Services.AddScoped<IResourceAuthorizationService, ResourceAuthorizationService>();
builder.Services.AddScoped<DeveloperUserStatusReader>();
builder.Services.AddScoped<IDeveloperUserStatusReader>(sp =>
    sp.GetRequiredService<DeveloperUserStatusReader>());
builder.Services.AddScoped<IBillingUserStatusReader>(sp =>
    sp.GetRequiredService<DeveloperUserStatusReader>());
builder.Services.AddScoped<EfDeveloperApplicationRepository>();
builder.Services.AddScoped<IDeveloperApplicationRepository>(sp =>
    sp.GetRequiredService<EfDeveloperApplicationRepository>());
builder.Services.AddScoped<EfDeveloperApiKeyRepository>();
builder.Services.AddScoped<IDeveloperApiKeyRepository>(sp =>
    sp.GetRequiredService<EfDeveloperApiKeyRepository>());
builder.Services.AddSingleton<IDeveloperApiKeySecretGenerator, DeveloperApiKeySecretGenerator>();
builder.Services.AddScoped<DeveloperApplicationService>();
builder.Services.AddScoped<IDeveloperApplicationService>(sp =>
    sp.GetRequiredService<DeveloperApplicationService>());
builder.Services.AddScoped<IDeveloperApiKeyValidator>(sp =>
    sp.GetRequiredService<DeveloperApplicationService>());
builder.Services.AddScoped<IPlanRepository, EfPlanRepository>();
builder.Services.AddScoped<IEntitlementAssignmentRepository, EfEntitlementAssignmentRepository>();
builder.Services.AddScoped<IEntitlementService, EntitlementService>();
builder.Services.AddScoped<IQuotaSnapshotCache, RedisQuotaSnapshotCache>();
builder.Services.AddScoped<IQuotaService, QuotaService>();
builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = IdentityAuthenticationDefaults.Scheme;
        options.DefaultChallengeScheme = IdentityAuthenticationDefaults.Scheme;
    })
    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions,
        OpaqueBearerAuthenticationHandler>(IdentityAuthenticationDefaults.Scheme, _ => { })
    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions,
        LegadoTokenAuthenticationHandler>(IdentityAuthenticationDefaults.LegadoScheme, _ => { })
    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions,
        DeveloperApiKeyAuthenticationHandler>(DeveloperAuthenticationDefaults.Scheme, _ => { });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(
        IdentityPolicies.CrawlerRepair,
        policy => policy.RequireRole(
            UserRole.Operator.ToString(),
            UserRole.Administrator.ToString()));
    options.AddPolicy(
        IdentityPolicies.OperationsRead,
        policy => policy.RequireRole(
            UserRole.Operator.ToString(),
            UserRole.Administrator.ToString()));
    options.AddPolicy(
        IdentityPolicies.AuditRead,
        policy => policy.RequireRole(
            UserRole.Operator.ToString(),
            UserRole.Administrator.ToString()));
    options.AddPolicy(
        IdentityPolicies.ContentModeration,
        policy => policy.RequireRole(UserRole.Administrator.ToString()));
    options.AddPolicy(
        IdentityPolicies.SourceOperations,
        policy => policy.RequireRole(
            UserRole.Operator.ToString(),
            UserRole.Administrator.ToString()));
    options.AddPolicy(
        IdentityPolicies.PermissionManagement,
        policy => policy.RequireRole(UserRole.Administrator.ToString()));
    options.AddPolicy(
        IdentityPolicies.CommercialManagement,
        policy => policy.RequireRole(UserRole.Administrator.ToString()));
    options.AddPolicy(
        IdentityPolicies.LegadoRead,
        policy => policy
            .AddAuthenticationSchemes(IdentityAuthenticationDefaults.LegadoScheme)
            .RequireAuthenticatedUser()
            .RequireClaim(IdentityAuthenticationDefaults.LegadoScopeClaim, "read"));
    options.AddPolicy(
        DeveloperEndpointPolicies.CatalogRead,
        policy => policy
            .AddAuthenticationSchemes(DeveloperAuthenticationDefaults.Scheme)
            .RequireAuthenticatedUser()
            .RequireClaim(DeveloperAuthenticationDefaults.ScopeClaim, DeveloperApiScopes.CatalogRead)
            .RequireClaim(DeveloperAuthenticationDefaults.EnvironmentClaim, "production"));
});

builder.Services.AddScoped<EfCrawlerTaskRepository>();
builder.Services.AddScoped<ICrawlerTaskRepository>(sp =>
    sp.GetRequiredService<EfCrawlerTaskRepository>());
builder.Services.AddScoped<ICrawlerTaskRepairRepository>(sp =>
    sp.GetRequiredService<EfCrawlerTaskRepository>());

builder.Services.AddDbContext<LibraryDbContext>(options =>
    options.UseNpgsql(databaseConnectionString));
builder.Services.AddDbContext<ReadingDbContext>(options =>
    options.UseNpgsql(databaseConnectionString));
builder.Services.AddDbContext<InkFlow.Modules.Sources.Infrastructure.Persistence.SourcesDbContext>(options =>
    options.UseNpgsql(databaseConnectionString));

builder.Services.AddScoped<ICanonicalBookRepository, EfCanonicalBookRepository>();
builder.Services.AddScoped<IPrivateBookRepository, EfPrivateBookRepository>();
builder.Services.AddScoped<IPrivateLibraryService, PrivateLibraryService>();
builder.Services.AddScoped<IPrivateBookImportParser, PrivateBookImportParser>();
builder.Services.AddScoped<IPrivateLibraryContentService, PrivateLibraryContentService>();
builder.Services.AddScoped<IMatchCandidateRepository, InkFlow.Modules.Library.Infrastructure.Persistence.EfMatchCandidateRepository>();
builder.Services.AddScoped<ISourceRepository, InkFlow.Modules.Sources.Infrastructure.Persistence.EfSourceRepository>();
builder.Services.AddScoped<ISourceBookRepository, InkFlow.Modules.Sources.Infrastructure.Persistence.EfSourceBookRepository>();
builder.Services.AddScoped<ISourceHealthRepository, InkFlow.Modules.Sources.Infrastructure.Persistence.EfSourceHealthRepository>();
builder.Services.AddSingleton(TimeProvider.System);
var sourceHealthOptions = SourceHealthOptions.FromConfiguration(builder.Configuration);
builder.Services.AddSingleton(sourceHealthOptions);
SourceHealthPolicy.Configure(sourceHealthOptions.ToParameters());
builder.Services.AddSingleton(SourceRuleExecutionLimits.Default);
builder.Services.AddScoped<SourceHealthService>();
builder.Services.AddScoped<ISourceHealthReader>(sp => sp.GetRequiredService<SourceHealthService>());
builder.Services.AddScoped<ISourceHealthOperations>(sp => sp.GetRequiredService<SourceHealthService>());

// 规则型/代码型适配器组合根(与 Worker 同源):健康感知由 BookDiscoveryService 内部执行。
builder.Services.AddSingleton<IIpAddressResolver, DnsIpAddressResolver>();
builder.Services.AddHttpClient<ISourceHttpClient, ProductionSafeSourceHttpClient>()
    .ConfigurePrimaryHttpMessageHandler(sp =>
        new SsrfSafeHttpMessageHandler(sp.GetRequiredService<IIpAddressResolver>()));
builder.Services.AddHttpClient<InkFlow.Sources.Adapters.Kanunu8.KanunuSourceAdapter>()
    .ConfigurePrimaryHttpMessageHandler(sp =>
        new SsrfSafeHttpMessageHandler(sp.GetRequiredService<IIpAddressResolver>()));
builder.Services.AddHttpClient<InkFlow.Sources.Adapters.SeventeenK.SeventeenKSourceAdapter>()
    .ConfigureHttpClient(client => client.Timeout = TimeSpan.FromSeconds(20))
    .ConfigurePrimaryHttpMessageHandler(sp =>
        new SsrfSafeHttpMessageHandler(sp.GetRequiredService<IIpAddressResolver>()));
builder.Services.AddScoped<ISelectorEvaluator, RuleSelectorEvaluator>();
builder.Services.AddScoped<RuleAdapter>();
builder.Services.AddScoped<ISourceAdapterFactory>(sp => new SourceAdapterFactory(
    sp.GetRequiredService<ISourceRepository>(),
    sp.GetRequiredService<RuleAdapter>(),
    sp.GetRequiredService<ISelectorEvaluator>(),
    [
        sp.GetRequiredService<InkFlow.Sources.Adapters.Kanunu8.KanunuSourceAdapter>(),
        sp.GetRequiredService<InkFlow.Sources.Adapters.SeventeenK.SeventeenKSourceAdapter>(),
    ]));
builder.Services.AddScoped<SourceCatalogService>();
builder.Services.AddScoped<CanonicalBookMatchingService>();
builder.Services.AddScoped<BookDiscoveryService>();

var connectionStringForContent =
    builder.Configuration.GetConnectionString("Database")
    ?? "Host=localhost;Port=5432;Database=inkflow;Username=inkflow;Password=inkflow";
builder.Services.AddScoped<InkFlow.Modules.Content.Infrastructure.Persistence.ContentDbContext>(_ =>
    new InkFlow.Modules.Content.Infrastructure.Persistence.ContentDbContext(
        new DbContextOptionsBuilder<InkFlow.Modules.Content.Infrastructure.Persistence.ContentDbContext>()
            .UseNpgsql(connectionStringForContent)
            .Options));
builder.Services.AddScoped<InkFlow.Modules.Content.Application.IContentVersionRepository,
    InkFlow.Modules.Content.Infrastructure.Persistence.EfContentVersionRepository>();
builder.Services.AddScoped<InkFlow.Modules.Content.Application.IContentPolicyRepository,
    InkFlow.Modules.Content.Infrastructure.Persistence.EfContentPolicyRepository>();
builder.Services.AddScoped<InkFlow.Modules.Content.Application.ContentPolicyService>();
builder.Services.AddScoped<InkFlow.Modules.Content.Application.IContentPolicyService>(sp =>
    sp.GetRequiredService<InkFlow.Modules.Content.Application.ContentPolicyService>());
builder.Services.AddScoped<InkFlow.Modules.Content.Application.IContentPolicyReader>(sp =>
    sp.GetRequiredService<InkFlow.Modules.Content.Application.ContentPolicyService>());
builder.Services.AddScoped<IConsistencySnapshotReader, EfConsistencySnapshotReader>();
builder.Services.AddScoped<IConsistencyCheckService, ConsistencyCheckService>();
builder.Services.AddScoped<IOperationsCenterReader, OperationsCenterReader>();
builder.Services.AddSingleton(
    OperationsAlertOptions.FromConfiguration(builder.Configuration));
builder.Services.AddScoped<IOperationsAlertReader, OperationsAlertReader>();
builder.Services.AddScoped<IOperationsAlertHistoryRepository, EfOperationsAlertHistoryRepository>();
builder.Services.AddScoped<IReadingStateRepository, EfReadingStateRepository>();
builder.Services.AddScoped<IReadingStateService, ReadingStateService>();
builder.Services.AddScoped<CatalogQueryService>();
builder.Services.AddScoped<LegadoContractService>();
builder.Services.AddSingleton<ILegadoRuleGenerator, LegadoRuleGenerator>();

var app = builder.Build();

// 认证先于审计/限流，使审计 actor 与认证主体分桶均可用；health 不进入业务审计。
app.UseAuthentication();
app.UseMiddleware<RequestAuditMiddleware>();
app.Use(async (context, next) =>
{
    // Developer API has a dedicated key scheme. Authenticate it before the
    // rate-limit partition is selected so valid keys are bucketed by key ID;
    // missing/invalid keys remain IP-bucketed and are then rejected by policy.
    if (context.Request.Path.StartsWithSegments("/api/developer/v1"))
    {
        var result = await context
            .AuthenticateAsync(DeveloperAuthenticationDefaults.Scheme)
            .ConfigureAwait(false);
        if (result.Succeeded && result.Principal is not null)
        {
            context.User = result.Principal;
        }
    }

    await next().ConfigureAwait(false);
});
app.UseMiddleware<CoreSloMetricsMiddleware>();
app.UseRateLimiter();
app.UseAuthorization();

app.MapGet("/health", () => Results.Json(new { status = "healthy", service = "InkFlow.Api" }));

var api = app.MapGroup("/api/v1")
    .RequireRateLimiting(ApiRateLimitPolicies.PublicPolicyName);

var auth = api.MapGroup("/auth");

auth.MapPost("/register", async (
    RegisterRequest request,
    IIdentityService identity,
    TimeProvider clock,
    CancellationToken ct) =>
{
    var result = await identity.RegisterAsync(
        request.Email ?? string.Empty,
        request.Password ?? string.Empty,
        ct);
    return AuthEndpointResults.FromIdentityResult(result, clock);
});

auth.MapPost("/login", async (
    LoginRequest request,
    IIdentityService identity,
    TimeProvider clock,
    CancellationToken ct) =>
{
    var result = await identity.LoginAsync(
        request.Email ?? string.Empty,
        request.Password ?? string.Empty,
        ct);
    return AuthEndpointResults.FromIdentityResult(result, clock);
});

auth.MapPost("/refresh", async (
    RefreshRequest request,
    IIdentityService identity,
    TimeProvider clock,
    CancellationToken ct) =>
{
    var result = await identity.RefreshAsync(request.RefreshToken ?? string.Empty, ct);
    return AuthEndpointResults.FromIdentityResult(result, clock);
});

auth.MapPost("/logout", async (
    ClaimsPrincipal principal,
    IIdentityService identity,
    CancellationToken ct) =>
{
    var rawSessionId = principal.FindFirstValue(IdentityAuthenticationDefaults.SessionIdClaim);
    if (!Guid.TryParse(rawSessionId, out var sessionId))
    {
        return Results.Unauthorized();
    }

    await identity.LogoutAsync(sessionId, ct);
    return Results.NoContent();
}).RequireAuthorization();

auth.MapGet("/me", (ClaimsPrincipal principal) =>
    AuthEndpointResults.Current(principal)).RequireAuthorization();

// ---- Personal Legado Token 管理(使用 Web Access Token,令牌只签发一次)----

var personalLegadoTokens = api.MapGroup("/me/legado")
    .RequireAuthorization();

personalLegadoTokens.MapPost("/tokens", async (
    CreateLegadoTokenRequest? request,
    ClaimsPrincipal principal,
    ILegadoAccessTokenService tokens,
    ILegadoRuleGenerator legadoRuleGenerator,
    HttpContext httpContext,
    IAuditEventSink auditSink,
    TimeProvider clock,
    CancellationToken ct) =>
{
    if (!LegadoTokenEndpointResults.TryGetUserId(principal, out var userId))
    {
        return (IResult)Results.Unauthorized();
    }

    var result = await tokens.IssueAsync(userId, request?.Name, ct).ConfigureAwait(false);
    if (!result.IsSuccess || result.Issue is null)
    {
        return LegadoTokenEndpointResults.FromIssueFailure(result.Status);
    }

    var baseUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}";
    using var document = JsonDocument.Parse(
        legadoRuleGenerator.Generate(baseUrl, result.Issue.RawToken));
    var response = LegadoTokenEndpointResults.ToIssueResponse(
        result.Issue,
        document.RootElement.Clone());
    return LegadoTokenEndpointResults.Issue(
        response,
        httpContext,
        auditSink,
        clock,
        ct);
});

personalLegadoTokens.MapGet("/tokens", async (
    ClaimsPrincipal principal,
    ILegadoAccessTokenService tokens,
    CancellationToken ct) =>
{
    if (!LegadoTokenEndpointResults.TryGetUserId(principal, out var userId))
    {
        return (IResult)Results.Unauthorized();
    }

    var values = await tokens.ListAsync(userId, ct).ConfigureAwait(false);
    return Results.Ok(values.Select(LegadoTokenEndpointResults.ToResponse));
});

personalLegadoTokens.MapDelete("/tokens/{tokenId:guid}", async (
    Guid tokenId,
    ClaimsPrincipal principal,
    ILegadoAccessTokenService tokens,
    HttpContext httpContext,
    IAuditEventSink auditSink,
    TimeProvider clock,
    CancellationToken ct) =>
{
    if (!LegadoTokenEndpointResults.TryGetUserId(principal, out var userId))
    {
        return (IResult)Results.Unauthorized();
    }

    var status = await tokens.RevokeAsync(userId, tokenId, ct).ConfigureAwait(false);
    if (status != LegadoTokenResultStatus.Success)
    {
        return LegadoTokenEndpointResults.FromRevokeStatus(status);
    }

    return LegadoTokenEndpointResults.Revoke(
        tokenId,
        principal,
        httpContext,
        auditSink,
        clock,
        ct);
});

// ---- 用户阅读状态(用户数据严格按认证主体隔离)----

var reading = api.MapGroup("/me/reading")
    .RequireAuthorization();

api.MapPrivateLibraryEndpoints();
app.MapDeveloperEndpoints(api);

reading.MapGet("/shelf", async (
    int? limit,
    ClaimsPrincipal principal,
    IReadingStateService state,
    CancellationToken ct) =>
{
    if (!ReadingEndpointResults.TryGetUserId(principal, out var userId))
    {
        return Results.Unauthorized();
    }

    return Results.Ok(await state
        .ListShelfAsync(userId, limit ?? ReadingStateService.DefaultPageSize, ct)
        .ConfigureAwait(false));
});

reading.MapPut("/shelf/{bookId:guid}", async (
    Guid bookId,
    ShelfStatusRequest? request,
    ClaimsPrincipal principal,
    IReadingStateService state,
    CancellationToken ct) =>
{
    if (!ReadingEndpointResults.TryGetUserId(principal, out var userId))
    {
        return Results.Unauthorized();
    }

    if (!ReadingEndpointResults.TryParseShelfStatus(request?.Status, out var status))
    {
        return Results.BadRequest(new { error = "invalid_request" });
    }

    var result = await state.PutShelfAsync(userId, bookId, status, ct).ConfigureAwait(false);
    return ReadingEndpointResults.FromResult(result);
});

reading.MapDelete("/shelf/{bookId:guid}", async (
    Guid bookId,
    ClaimsPrincipal principal,
    IReadingStateService state,
    CancellationToken ct) =>
{
    if (!ReadingEndpointResults.TryGetUserId(principal, out var userId))
    {
        return Results.Unauthorized();
    }

    var status = await state.RemoveShelfAsync(userId, bookId, ct).ConfigureAwait(false);
    return ReadingEndpointResults.FromStatus(status);
});

reading.MapGet("/history", async (
    int? limit,
    ClaimsPrincipal principal,
    IReadingStateService state,
    CancellationToken ct) =>
{
    if (!ReadingEndpointResults.TryGetUserId(principal, out var userId))
    {
        return Results.Unauthorized();
    }

    return Results.Ok(await state
        .ListHistoryAsync(userId, limit ?? ReadingStateService.DefaultPageSize, ct)
        .ConfigureAwait(false));
});

reading.MapGet("/progress/{bookId:guid}", async (
    Guid bookId,
    ClaimsPrincipal principal,
    IReadingStateService state,
    CancellationToken ct) =>
{
    if (!ReadingEndpointResults.TryGetUserId(principal, out var userId))
    {
        return Results.Unauthorized();
    }

    var progress = await state.GetProgressAsync(userId, bookId, ct).ConfigureAwait(false);
    return progress is null ? Results.NotFound() : Results.Ok(progress);
});

reading.MapPut("/progress/{bookId:guid}", async (
    Guid bookId,
    ReadingProgressRequest? request,
    ClaimsPrincipal principal,
    IReadingStateService state,
    CancellationToken ct) =>
{
    if (!ReadingEndpointResults.TryGetUserId(principal, out var userId))
    {
        return Results.Unauthorized();
    }

    if (request is null)
    {
        return Results.BadRequest(new { error = "invalid_request" });
    }

    var result = await state.SaveProgressAsync(
        userId,
        bookId,
        request.ChapterId,
        request.ParagraphIndex,
        request.ProgressPercent,
        ct).ConfigureAwait(false);
    return ReadingEndpointResults.FromResult(result);
});

reading.MapGet("/preferences", async (
    ClaimsPrincipal principal,
    IReadingStateService state,
    CancellationToken ct) =>
{
    if (!ReadingEndpointResults.TryGetUserId(principal, out var userId))
    {
        return Results.Unauthorized();
    }

    return Results.Ok(await state.GetPreferencesAsync(userId, ct).ConfigureAwait(false));
});

reading.MapPut("/preferences", async (
    ReaderPreferenceRequest? request,
    ClaimsPrincipal principal,
    IReadingStateService state,
    CancellationToken ct) =>
{
    if (!ReadingEndpointResults.TryGetUserId(principal, out var userId))
    {
        return Results.Unauthorized();
    }

    if (!ReadingEndpointResults.TryParseTheme(request?.Theme, out var theme))
    {
        return Results.BadRequest(new { error = "invalid_request" });
    }

    var result = await state.UpdatePreferencesAsync(
        userId,
        request?.FontSizePercent,
        request?.LineHeightPercent,
        theme,
        ct).ConfigureAwait(false);
    return ReadingEndpointResults.FromResult(result);
});

var operationsRead = api.MapGroup("/admin")
    .RequireAuthorization(IdentityPolicies.OperationsRead);

var auditRead = api.MapGroup("/admin")
    .RequireAuthorization(IdentityPolicies.AuditRead);

auditRead.MapGet("/audit/events", async (
    string? from,
    string? to,
    string? action,
    string? outcome,
    string? actorId,
    string? cursor,
    int? limit,
    IAuditEventReader auditReader,
    CancellationToken ct) =>
{
    if (!AuditEndpointResults.TryCreateQuery(
            from,
            to,
            action,
            outcome,
            actorId,
            cursor,
            limit,
            out var query,
            out var error))
    {
        return (IResult)Results.BadRequest(new { error });
    }

    try
    {
        var page = await auditReader.QueryAsync(query!, ct).ConfigureAwait(false);
        return Results.Ok(AuditEndpointResults.ToResponse(page));
    }
    catch (Exception) when (!ct.IsCancellationRequested)
    {
        return Results.Json(
            new { error = "audit_unavailable" },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

operationsRead.MapGet("/crawler/dead-letters", async (
    int? limit,
    ICrawlerTaskRepository tasks,
    CancellationToken ct) =>
{
    var boundedLimit = Math.Clamp(limit ?? 50, 1, 100);
    var deadLetters = await tasks.ListDeadLettersAsync(boundedLimit, ct);
    return Results.Ok(deadLetters);
});

operationsRead.MapGet("/consistency", async (
    IConsistencyCheckService consistency,
    CancellationToken ct) =>
{
    var report = await consistency.CheckAsync(ct);
    return Results.Ok(report);
});

operationsRead.MapGet("/operations/overview", async (
    int? limit,
    ClaimsPrincipal principal,
    IOperationsCenterReader operations,
    IResourceAuthorizationService authorization,
    CancellationToken ct) =>
{
    if (!ResourcePermissionEndpointResults.TryGetIdentity(
            principal,
            out var userId,
            out var role))
    {
        return (IResult)Results.Unauthorized();
    }

    var boundedLimit = limit ?? OperationsCenterReader.DefaultLimit;
    var snapshot = role == UserRole.Administrator
        ? await operations.ReadAsync(boundedLimit, ct).ConfigureAwait(false)
        : await operations.ReadForSourcesAsync(
                boundedLimit,
                await authorization.ListAllowedResourceIdsAsync(
                    userId,
                    role,
                    IdentityPermissions.SourceRead,
                    IdentityResourceTypes.Source,
                    ct).ConfigureAwait(false),
                ct)
            .ConfigureAwait(false);
    return Results.Ok(snapshot);
});

operationsRead.MapGet("/operations/alerts", async (
    int? limit,
    ClaimsPrincipal principal,
    IOperationsAlertReader alerts,
    IResourceAuthorizationService authorization,
    CancellationToken ct) =>
{
    if (!ResourcePermissionEndpointResults.TryGetIdentity(
            principal,
            out var userId,
            out var role))
    {
        return (IResult)Results.Unauthorized();
    }

    var boundedLimit = limit ?? OperationsAlertReader.DefaultLimit;
    var snapshot = role == UserRole.Administrator
        ? await alerts.ReadAsync(boundedLimit, ct).ConfigureAwait(false)
        : await alerts.ReadForSourcesAsync(
                boundedLimit,
                await authorization.ListAllowedResourceIdsAsync(
                    userId,
                    role,
                    IdentityPermissions.SourceRead,
                    IdentityResourceTypes.Source,
                    ct).ConfigureAwait(false),
                ct)
            .ConfigureAwait(false);
    return Results.Ok(snapshot);
});

operationsRead.MapGet("/operations/alerts/history", async (
    int? limit,
    string? cursor,
    ClaimsPrincipal principal,
    IOperationsAlertHistoryRepository history,
    CancellationToken ct) =>
{
    if (!ResourcePermissionEndpointResults.TryGetIdentity(
            principal,
            out _,
            out var role))
    {
        return (IResult)Results.Unauthorized();
    }

    // History is a platform-wide incident view. Operators receive source-scoped
    // snapshots, so only administrators may query this unfiltered history.
    if (role != UserRole.Administrator)
    {
        return (IResult)Results.Forbid();
    }

    if (!OperationsAlertHistoryEndpointResults.TryCreateQuery(
            limit,
            cursor,
            out var boundedLimit,
            out var before,
            out var error))
    {
        return (IResult)Results.BadRequest(new { error });
    }

    try
    {
        var page = await history.QueryAsync(boundedLimit, before, ct).ConfigureAwait(false);
        return Results.Ok(OperationsAlertHistoryEndpointResults.ToResponse(page));
    }
    catch (Exception) when (!ct.IsCancellationRequested)
    {
        return Results.Json(
            new { error = "operations_alert_history_unavailable" },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

var repair = api.MapGroup("/admin")
    .RequireAuthorization(IdentityPolicies.CrawlerRepair);

repair.MapPost("/crawler/dead-letters/{deadLetterId:guid}/replay", async (
    Guid deadLetterId,
    ReplayDeadLetterRequest request,
    ClaimsPrincipal principal,
    ICrawlerTaskRepairRepository repairRepository,
    HttpContext httpContext,
    IAuditEventSink auditSink,
    TimeProvider clock,
    CancellationToken ct) =>
{
    if (!RepairEndpointResults.TryGetActor(principal, out var actorId))
    {
        return (IResult)Results.Unauthorized();
    }

    DeadLetterReplayCommand command;
    try
    {
        command = DeadLetterReplayCommand.Create(
            deadLetterId,
            actorId,
            request.Reason ?? string.Empty);
    }
    catch (ArgumentException)
    {
        return (IResult)Results.BadRequest(new { error = "invalid_replay_request" });
    }

    var result = await repairRepository.ReplayDeadLetterAsync(command, clock.GetUtcNow(), ct);
    return RepairEndpointResults.Replay(
        result,
        deadLetterId,
        actorId,
        command.ReplayReason,
        httpContext,
        auditSink,
        clock,
        ct);
});

var sourceOperationsRead = api.MapGroup("/admin/sources")
    .RequireAuthorization(IdentityPolicies.OperationsRead);

sourceOperationsRead.MapGet("/{sourceId}/health", async (
    string sourceId,
    ClaimsPrincipal principal,
    ISourceRepository sources,
    ISourceHealthOperations health,
    IResourceAuthorizationService authorization,
    CancellationToken ct) =>
{
    if (!SourceHealthEndpointResults.IsValidSourceId(sourceId))
    {
        return (IResult)Results.BadRequest(new { error = "invalid_source_id" });
    }

    if (await sources.GetAsync(sourceId, ct).ConfigureAwait(false) is null)
    {
        return (IResult)Results.NotFound(new { error = "source_not_found" });
    }

    if (!ResourcePermissionEndpointResults.TryGetIdentity(
            principal,
            out var userId,
            out var role))
    {
        return (IResult)Results.Unauthorized();
    }

    if (!await authorization.CanAccessAsync(
            userId,
            role,
            IdentityPermissions.SourceRead,
            IdentityResourceTypes.Source,
            sourceId,
            ct).ConfigureAwait(false))
    {
        return (IResult)Results.Forbid();
    }

    var rows = await health.ListForSourceAsync(sourceId, ct).ConfigureAwait(false);
    return Results.Ok(rows.Select(SourceHealthEndpointResults.ToResponse));
});

var sourceOperations = api.MapGroup("/admin/sources")
    .RequireAuthorization(IdentityPolicies.SourceOperations);

sourceOperations.MapPost("/{sourceId}/health/{rawCapability}/disable", async (
    string sourceId,
    string rawCapability,
    SourceHealthCommandRequest? request,
    ClaimsPrincipal principal,
    ISourceRepository sources,
    ISourceHealthOperations health,
    IResourceAuthorizationService authorization,
    HttpContext httpContext,
    IAuditEventSink auditSink,
    TimeProvider clock,
    CancellationToken ct) =>
{
    if (!ResourcePermissionEndpointResults.TryGetIdentity(
            principal,
            out var actorUserId,
            out var actorRole))
    {
        return (IResult)Results.Unauthorized();
    }
    var actorId = actorUserId.ToString("D");

    if (!SourceHealthEndpointResults.IsValidSourceId(sourceId) ||
        !SourceHealthEndpointResults.TryParseCapability(rawCapability, out var capability) ||
        request is null ||
        !SourceHealthEndpointResults.TryNormalizeReason(request.Reason, out var reason))
    {
        return (IResult)Results.BadRequest(new { error = "invalid_source_health_request" });
    }

    if (await sources.GetAsync(sourceId, ct).ConfigureAwait(false) is null)
    {
        return (IResult)Results.NotFound(new { error = "source_not_found" });
    }

    if (!await authorization.CanAccessAsync(
            actorUserId,
            actorRole,
            IdentityPermissions.SourceManage,
            IdentityResourceTypes.Source,
            sourceId,
            ct).ConfigureAwait(false))
    {
        return (IResult)Results.Forbid();
    }

    var updated = await health.DisableAsync(sourceId, capability, reason, ct).ConfigureAwait(false);
    return SourceHealthEndpointResults.Command(
        updated,
        SourceHealthCommandAction.Disable,
        actorId,
        reason,
        httpContext,
        auditSink,
        clock,
        ct);
});

sourceOperations.MapPost("/{sourceId}/health/{rawCapability}/enable", async (
    string sourceId,
    string rawCapability,
    SourceHealthCommandRequest? request,
    ClaimsPrincipal principal,
    ISourceRepository sources,
    ISourceHealthOperations health,
    IResourceAuthorizationService authorization,
    HttpContext httpContext,
    IAuditEventSink auditSink,
    TimeProvider clock,
    CancellationToken ct) =>
{
    if (!ResourcePermissionEndpointResults.TryGetIdentity(
            principal,
            out var actorUserId,
            out var actorRole))
    {
        return (IResult)Results.Unauthorized();
    }
    var actorId = actorUserId.ToString("D");

    if (!SourceHealthEndpointResults.IsValidSourceId(sourceId) ||
        !SourceHealthEndpointResults.TryParseCapability(rawCapability, out var capability) ||
        request is null ||
        !SourceHealthEndpointResults.TryNormalizeReason(request.Reason, out var reason))
    {
        return (IResult)Results.BadRequest(new { error = "invalid_source_health_request" });
    }

    if (await sources.GetAsync(sourceId, ct).ConfigureAwait(false) is null)
    {
        return (IResult)Results.NotFound(new { error = "source_not_found" });
    }

    if (!await authorization.CanAccessAsync(
            actorUserId,
            actorRole,
            IdentityPermissions.SourceManage,
            IdentityResourceTypes.Source,
            sourceId,
            ct).ConfigureAwait(false))
    {
        return (IResult)Results.Forbid();
    }

    var updated = await health.EnableAsync(sourceId, capability, ct).ConfigureAwait(false);
    return SourceHealthEndpointResults.Command(
        updated,
        SourceHealthCommandAction.Enable,
        actorId,
        reason,
        httpContext,
        auditSink,
        clock,
        ct);
});

var sourcePermissionManagement = api.MapGroup("/admin/sources")
    .RequireAuthorization(IdentityPolicies.PermissionManagement);

sourcePermissionManagement.MapGet("/{sourceId}/permissions", async (
    string sourceId,
    int? limit,
    ClaimsPrincipal principal,
    ISourceRepository sources,
    IResourcePermissionService permissions,
    CancellationToken ct) =>
{
    if (!SourceHealthEndpointResults.IsValidSourceId(sourceId))
    {
        return (IResult)Results.BadRequest(new { error = "invalid_source_id" });
    }

    if (await sources.GetAsync(sourceId, ct).ConfigureAwait(false) is null)
    {
        return (IResult)Results.NotFound(new { error = "source_not_found" });
    }

    if (!ResourcePermissionEndpointResults.TryGetIdentity(
            principal,
            out var actorId,
            out var actorRole))
    {
        return (IResult)Results.Unauthorized();
    }

    var result = await permissions.ListAsync(
        actorId,
        actorRole,
        IdentityResourceTypes.Source,
        sourceId,
        limit ?? ResourcePermissionService.MaxListLimit,
        ct).ConfigureAwait(false);
    return result.Status switch
    {
        ResourcePermissionListStatus.Success =>
            Results.Ok(result.Grants),
        ResourcePermissionListStatus.ActorNotAllowed =>
            Results.Forbid(),
        _ => Results.BadRequest(new { error = "invalid_permission_request" }),
    };
});

sourcePermissionManagement.MapPost("/{sourceId}/permissions", async (
    string sourceId,
    SourcePermissionGrantRequest? request,
    ClaimsPrincipal principal,
    ISourceRepository sources,
    IResourcePermissionService permissions,
    HttpContext httpContext,
    IAuditEventSink auditSink,
    TimeProvider clock,
    CancellationToken ct) =>
{
    if (!SourceHealthEndpointResults.IsValidSourceId(sourceId) ||
        request is null ||
        !ResourcePermissionEndpointResults.TryNormalizeReason(request.Reason, out var reason))
    {
        return (IResult)Results.BadRequest(new { error = "invalid_permission_request" });
    }

    if (await sources.GetAsync(sourceId, ct).ConfigureAwait(false) is null)
    {
        return (IResult)Results.NotFound(new { error = "source_not_found" });
    }

    if (!ResourcePermissionEndpointResults.TryGetIdentity(
            principal,
            out var actorId,
            out var actorRole))
    {
        return (IResult)Results.Unauthorized();
    }

    var result = await permissions.GrantAsync(
        actorId,
        actorRole,
        request.UserId,
        IdentityResourceTypes.Source,
        sourceId,
        request.Permission,
        ct).ConfigureAwait(false);
    return ResourcePermissionEndpointResults.AuditedOperation(
        result,
        "identity.resource_permission.grant",
        sourceId,
        actorId,
        reason,
        httpContext,
        auditSink,
        clock,
        ct);
});

sourcePermissionManagement.MapDelete("/{sourceId}/permissions/{grantId:guid}", async (
    string sourceId,
    Guid grantId,
    string? reason,
    ClaimsPrincipal principal,
    ISourceRepository sources,
    IResourcePermissionService permissions,
    HttpContext httpContext,
    IAuditEventSink auditSink,
    TimeProvider clock,
    CancellationToken ct) =>
{
    if (!SourceHealthEndpointResults.IsValidSourceId(sourceId) ||
        !ResourcePermissionEndpointResults.TryNormalizeReason(reason, out var normalizedReason))
    {
        return (IResult)Results.BadRequest(new { error = "invalid_permission_request" });
    }

    if (await sources.GetAsync(sourceId, ct).ConfigureAwait(false) is null)
    {
        return (IResult)Results.NotFound(new { error = "source_not_found" });
    }

    if (!ResourcePermissionEndpointResults.TryGetIdentity(
            principal,
            out var actorId,
            out var actorRole))
    {
        return (IResult)Results.Unauthorized();
    }

    var result = await permissions.RevokeAsync(
        actorId,
        actorRole,
        grantId,
        IdentityResourceTypes.Source,
        sourceId,
        ct).ConfigureAwait(false);
    return ResourcePermissionEndpointResults.AuditedOperation(
        result,
        "identity.resource_permission.revoke",
        sourceId,
        actorId,
        normalizedReason,
        httpContext,
        auditSink,
        clock,
        ct);
});

var contentPolicy = api.MapGroup("/admin/content")
    .RequireAuthorization(IdentityPolicies.ContentModeration);

contentPolicy.MapGet("/takedowns", async (
    int? limit,
    IContentPolicyService policy,
    CancellationToken ct) =>
{
    var boundedLimit = Math.Clamp(limit ?? 50, 1, ContentPolicyService.MaxListLimit);
    var statuses = await policy.ListAsync(
        takenDownOnly: true,
        limit: boundedLimit,
        cancellationToken: ct);
    return Results.Ok(statuses);
});

contentPolicy.MapPost("/takedowns", async (
    ContentPolicyTakedownRequest? request,
    ClaimsPrincipal principal,
    ICanonicalBookRepository books,
    IContentPolicyService policy,
    HttpContext httpContext,
    IAuditEventSink auditSink,
    TimeProvider clock,
    CancellationToken ct) =>
{
    if (!RepairEndpointResults.TryGetActor(principal, out var actorId))
    {
        return (IResult)Results.Unauthorized();
    }

    if (request is null || request.BookId == Guid.Empty || string.IsNullOrWhiteSpace(request.Reason))
    {
        return Results.BadRequest(new { error = "book_id_and_reason_required" });
    }

    if (request.Reason.Trim().Length > ContentPolicyDecision.MaxReasonLength)
    {
        return Results.BadRequest(new { error = "reason_too_long" });
    }

    if (await books.GetAsync(request.BookId, ct) is null)
    {
        return Results.NotFound();
    }

    var reason = request.Reason.Trim();
    var result = await policy.TakedownAsync(request.BookId, actorId, reason, ct);
    return ContentPolicyEndpointResults.Command(
        result,
        ContentPolicyAction.Takedown,
        actorId,
        reason,
        httpContext,
        auditSink,
        clock,
        ct);
});

contentPolicy.MapPost("/takedowns/{bookId:guid}/restore", async (
    Guid bookId,
    ContentPolicyRestoreRequest? request,
    ClaimsPrincipal principal,
    ICanonicalBookRepository books,
    IContentPolicyService policy,
    HttpContext httpContext,
    IAuditEventSink auditSink,
    TimeProvider clock,
    CancellationToken ct) =>
{
    if (!RepairEndpointResults.TryGetActor(principal, out var actorId))
    {
        return (IResult)Results.Unauthorized();
    }

    if (request is null || bookId == Guid.Empty || string.IsNullOrWhiteSpace(request.Reason))
    {
        return Results.BadRequest(new { error = "book_id_and_reason_required" });
    }

    if (request.Reason.Trim().Length > ContentPolicyDecision.MaxReasonLength)
    {
        return Results.BadRequest(new { error = "reason_too_long" });
    }

    if (await books.GetAsync(bookId, ct) is null)
    {
        return Results.NotFound();
    }

    var reason = request.Reason.Trim();
    var result = await policy.RestoreAsync(bookId, actorId, reason, ct);
    return ContentPolicyEndpointResults.Command(
        result,
        ContentPolicyAction.Restore,
        actorId,
        reason,
        httpContext,
        auditSink,
        clock,
        ct);
});

api.MapGet("/books", async (CatalogQueryService catalog, CancellationToken ct) =>
{
    var books = await catalog.ListBooksAsync(ct);
    return Results.Ok(books);
});

// 来源搜索发现:幂等导入 + v1 匹配后返回归并结果(落库数据)。
api.MapGet("/search", async (
    string q,
    BookDiscoveryService discovery,
    IContentPolicyReader policy,
    CancellationToken ct) =>
{
    var outcome = await discovery.DiscoverAsync(q ?? string.Empty, ct);
    var visibleBooks = new List<DiscoveredBook>(outcome.Books.Count);
    foreach (var book in outcome.Books)
    {
        if (!await policy.IsTakedownAsync(book.CanonicalBookId, ct))
        {
            visibleBooks.Add(book);
        }
    }

    return Results.Ok(new { books = visibleBooks, warnings = outcome.Warnings });
});

api.MapGet("/books/{bookId:guid}", async (Guid bookId, CatalogQueryService catalog, CancellationToken ct) =>
{
    var book = await catalog.GetBookAsync(bookId, ct);
    return book is null ? Results.NotFound() : Results.Ok(book);
});

api.MapGet("/chapters/{chapterId:guid}/content",
    async (Guid chapterId, CatalogQueryService catalog, CancellationToken ct) =>
{
    var content = await catalog.GetChapterContentAsync(chapterId, ct);
    return content is null ? Results.NotFound() : Results.Ok(content);
});

// ---- Legado v1 契约(阅读 3.0 接入)----

var legado = app.MapGroup("/api/legado/v1")
    .RequireRateLimiting(ApiRateLimitPolicies.LegadoPolicyName);

legado.MapGet("/search", async (string q, BookDiscoveryService discovery, LegadoContractService legadoService, CancellationToken ct) =>
{
    // 先经来源发现把命中的新书幂等导入并匹配正典身份,再从落库数据返回
    // Legado DTO——契约形态保持稳定,冷启动搜索从此可发现未入库书目。
    await discovery.DiscoverAsync(q ?? string.Empty, ct);
    var results = await legadoService.SearchAsync(q ?? string.Empty, ct);
    return Results.Json(new { data = results });
});

legado.MapGet("/books/{bookId:guid}", async (Guid bookId, LegadoContractService legadoService, CancellationToken ct) =>
{
    var book = await legadoService.GetBookAsync(bookId, ct);
    return book is null ? Results.NotFound() : Results.Json(book);
});

legado.MapGet("/books/{bookId:guid}/chapters",
    async (Guid bookId, LegadoContractService legadoService, CancellationToken ct) =>
{
    var toc = await legadoService.GetTocAsync(bookId, ct);
    return toc is null ? Results.NotFound() : Results.Json(new { data = toc });
});

legado.MapGet("/chapters/{chapterId:guid}",
    async (Guid chapterId, LegadoContractService legadoService, CancellationToken ct) =>
{
    var content = await legadoService.GetChapterContentAsync(chapterId, ct);
    return content is null ? Results.NotFound() : Results.Json(content);
});

// ---- Personal Legado v1 契约(通过 X-InkFlow-Legado-Token 认证)----

var personalLegado = app.MapGroup(LegadoRoutePrefixes.Personal)
    .RequireRateLimiting(ApiRateLimitPolicies.LegadoPolicyName)
    .RequireAuthorization(IdentityPolicies.LegadoRead);

personalLegado.MapGet("/search", async (
    string q,
    BookDiscoveryService discovery,
    LegadoContractService legadoService,
    CancellationToken ct) =>
{
    await discovery.DiscoverAsync(q ?? string.Empty, ct);
    var results = await legadoService
        .SearchAsync(q ?? string.Empty, LegadoRoutePrefixes.Personal, ct)
        .ConfigureAwait(false);
    return Results.Json(new { data = results });
});

personalLegado.MapGet("/books/{bookId:guid}", async (
    Guid bookId,
    LegadoContractService legadoService,
    CancellationToken ct) =>
{
    var book = await legadoService
        .GetBookAsync(bookId, LegadoRoutePrefixes.Personal, ct)
        .ConfigureAwait(false);
    return book is null ? Results.NotFound() : Results.Json(book);
});

personalLegado.MapGet("/books/{bookId:guid}/chapters", async (
    Guid bookId,
    LegadoContractService legadoService,
    CancellationToken ct) =>
{
    var toc = await legadoService
        .GetTocAsync(bookId, LegadoRoutePrefixes.Personal, ct)
        .ConfigureAwait(false);
    return toc is null ? Results.NotFound() : Results.Json(new { data = toc });
});

personalLegado.MapGet("/chapters/{chapterId:guid}", async (
    Guid chapterId,
    LegadoContractService legadoService,
    CancellationToken ct) =>
{
    var content = await legadoService
        .GetChapterContentAsync(chapterId, LegadoRoutePrefixes.Personal, ct)
        .ConfigureAwait(false);
    return content is null ? Results.NotFound() : Results.Json(content);
});

// ---- Web Reader / PWA(服务端渲染 HTML + 渐进增强)----

app.MapGet("/reader/manifest.webmanifest", () =>
    Results.Text(
        ReaderHtml.PwaManifest(),
        contentType: "application/manifest+json; charset=utf-8"))
    .RequireRateLimiting(ApiRateLimitPolicies.PublicPolicyName);

app.MapGet("/reader/sw.js", () =>
    Results.Text(
        ReaderHtml.ServiceWorker(),
        contentType: "application/javascript; charset=utf-8"))
    .RequireRateLimiting(ApiRateLimitPolicies.PublicPolicyName);

app.MapGet("/reader/icon-192.svg", () =>
    Results.Text(ReaderHtml.PwaIcon(), contentType: "image/svg+xml; charset=utf-8"))
    .RequireRateLimiting(ApiRateLimitPolicies.PublicPolicyName);

app.MapGet("/reader/icon-512.svg", () =>
    Results.Text(ReaderHtml.PwaIcon(), contentType: "image/svg+xml; charset=utf-8"))
    .RequireRateLimiting(ApiRateLimitPolicies.PublicPolicyName);

app.MapGet("/reader/offline", () =>
    Results.Content(
        ReaderHtml.OfflinePage(),
        contentType: "text/html; charset=utf-8"))
    .RequireRateLimiting(ApiRateLimitPolicies.PublicPolicyName);

app.MapGet("/reader/account", () =>
    Results.Content(
        ReaderHtml.AccountPage(),
        contentType: "text/html; charset=utf-8"))
    .RequireRateLimiting(ApiRateLimitPolicies.PublicPolicyName);

app.MapGet("/reader/shelf", () =>
    Results.Content(
        ReaderHtml.ShelfPage(),
        contentType: "text/html; charset=utf-8"))
    .RequireRateLimiting(ApiRateLimitPolicies.PublicPolicyName);

app.MapGet("/reader/history", () =>
    Results.Content(
        ReaderHtml.HistoryPage(),
        contentType: "text/html; charset=utf-8"))
    .RequireRateLimiting(ApiRateLimitPolicies.PublicPolicyName);

// 运维页面本身只返回静态壳；快照与修复命令仍由前端通过受保护 API 获取。
app.MapGet("/admin/operations", () =>
    Results.Content(
        ReaderHtml.OperationsPage(),
        contentType: "text/html; charset=utf-8"))
    .RequireRateLimiting(ApiRateLimitPolicies.PublicPolicyName);

app.MapGet("/reader", async (string? q, CatalogQueryService catalog, BookDiscoveryService discovery, CancellationToken ct) =>
{
    var query = q?.Trim() ?? string.Empty;
    var searched = query.Length > 0;

    // 非空搜索先走来源发现(幂等导入+匹配,失败隔离为逐源 warning);
    // 发现环节整体异常也不阻断页面——降级为"结果可能不完整"提示后仍从
    // 落库正典数据过滤返回(阅读路径零实时抓取)。
    var sourceDegraded = false;
    if (searched)
    {
        try
        {
            var outcome = await discovery.DiscoverAsync(query, ct);
            sourceDegraded = outcome.Warnings.Count > 0;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sourceDegraded = true;
        }
    }

    var books = await catalog.SearchBooksAsync(query, ct);
    return Results.Content(
        ReaderHtml.BookListPage(books, searched ? query : null, searched, sourceDegraded),
        contentType: "text/html; charset=utf-8");
}).RequireRateLimiting(ApiRateLimitPolicies.PublicPolicyName);

app.MapGet("/reader/books/{bookId:guid}",
    async (Guid bookId, CatalogQueryService catalog, CancellationToken ct) =>
{
    var book = await catalog.GetBookAsync(bookId, ct);
    return book is null
        ? Results.Content(ReaderHtml.BookListPage([], null), "text/html; charset=utf-8", statusCode: 404)
        : Results.Content(
            ReaderHtml.BookDetailPage(book), contentType: "text/html; charset=utf-8");
});

app.MapGet("/reader/read/{chapterId:guid}",
    async (Guid chapterId, CatalogQueryService catalog, CancellationToken ct) =>
{
    var content = await catalog.GetChapterContentAsync(chapterId, ct);
    if (content is null)
    {
        return Results.Content(
            "<!DOCTYPE html><html lang=\"zh-CN\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\"><title>未找到</title></head><body><main><p role=\"status\">该章节尚未发布内容。</p><p><a href=\"/reader\">返回书目</a></p></main></body></html>",
            "text/html; charset=utf-8",
            statusCode: 404);
    }

    var book = await catalog.GetBookAsync(content.BookId, ct);
    var chapters = book?.Chapters ?? [];

    (Guid ChapterId, string Title)? previous = null;
    (Guid ChapterId, string Title)? next = null;
    for (var i = 0; i < chapters.Count; i++)
    {
        if (chapters[i].ChapterId != chapterId)
        {
            continue;
        }

        previous = i > 0 ? (chapters[i - 1].ChapterId, chapters[i - 1].Title) : null;
        next = i + 1 < chapters.Count ? (chapters[i + 1].ChapterId, chapters[i + 1].Title) : null;
        break;
    }

    return Results.Content(
        ReaderHtml.ChapterPage(content, previous, next, content.BookId, book?.Title ?? string.Empty),
        contentType: "text/html; charset=utf-8");
});

// 书源清单:由代码生成,baseUrl 取请求自身的 scheme+host。
app.MapGet("/legado/book-source.json", (HttpContext http, ILegadoRuleGenerator legadoRuleGenerator) =>
{
    var baseUrl = $"{http.Request.Scheme}://{http.Request.Host}";
    return Results.Text(
        legadoRuleGenerator.Generate(baseUrl),
        contentType: "application/json; charset=utf-8");
}).RequireRateLimiting(ApiRateLimitPolicies.LegadoPolicyName);

app.Run();

public partial class Program;
