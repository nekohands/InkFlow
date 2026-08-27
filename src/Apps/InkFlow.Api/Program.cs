// Public Content API:目录/内容端点只读(数据来自已落库正典数据),
// /search 端点是唯一的写侧入口——触发来源发现(幂等导入+匹配),随后仍从落库数据返回。
using System.Security.Claims;
using InkFlow.Api;
using InkFlow.BuildingBlocks.Application;
using InkFlow.BuildingBlocks.Observability;
using InkFlow.BuildingBlocks.Persistence;
using InkFlow.BuildingBlocks.Security;
using InkFlow.Modules.Content.Application;
using InkFlow.Modules.Content.Domain;
using InkFlow.Modules.Content.Infrastructure.Persistence;
using InkFlow.Modules.Crawling.Application;
using InkFlow.Modules.Crawling.Infrastructure.Persistence;
using InkFlow.Modules.Legado.Application;
using InkFlow.Modules.Identity.Application;
using InkFlow.Modules.Identity.Domain;
using InkFlow.Modules.Identity.Infrastructure.Authentication;
using InkFlow.Modules.Identity.Infrastructure.Credentials;
using InkFlow.Modules.Identity.Infrastructure.Persistence;
using InkFlow.Modules.Library.Application;
using InkFlow.Modules.Library.Domain;
using InkFlow.Modules.Library.Infrastructure.Persistence;
using InkFlow.Modules.Sources.Application;
using InkFlow.Modules.Sources.Domain;
using InkFlow.Modules.Sources.Infrastructure;
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
builder.Services.AddDbContext<IdentityDbContext>(options =>
    options.UseNpgsql(databaseConnectionString));
builder.Services.AddDbContext<CrawlingDbContext>(options =>
    options.UseNpgsql(databaseConnectionString));
builder.Services.AddScoped<LoggingAuditEventSink>();
builder.Services.AddScoped<PersistentAuditEventSink>();
builder.Services.AddScoped<IAuditEventSink, CompositeAuditEventSink>();

builder.Services.AddScoped<IUserRepository, EfUserRepository>();
builder.Services.AddScoped<IIdentitySessionRepository, EfIdentitySessionRepository>();
builder.Services.AddSingleton(IdentityOptions.FromConfiguration(builder.Configuration));
builder.Services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
builder.Services.AddSingleton<IOpaqueTokenGenerator, SecureOpaqueTokenGenerator>();
builder.Services.AddScoped<IIdentityService, IdentityService>();
builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = IdentityAuthenticationDefaults.Scheme;
        options.DefaultChallengeScheme = IdentityAuthenticationDefaults.Scheme;
    })
    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions,
        OpaqueBearerAuthenticationHandler>(IdentityAuthenticationDefaults.Scheme, _ => { });
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
        IdentityPolicies.ContentModeration,
        policy => policy.RequireRole(UserRole.Administrator.ToString()));
    options.AddPolicy(
        IdentityPolicies.SourceOperations,
        policy => policy.RequireRole(
            UserRole.Operator.ToString(),
            UserRole.Administrator.ToString()));
});

builder.Services.AddScoped<EfCrawlerTaskRepository>();
builder.Services.AddScoped<ICrawlerTaskRepository>(sp =>
    sp.GetRequiredService<EfCrawlerTaskRepository>());
builder.Services.AddScoped<ICrawlerTaskRepairRepository>(sp =>
    sp.GetRequiredService<EfCrawlerTaskRepository>());

builder.Services.AddDbContext<LibraryDbContext>(options =>
    options.UseNpgsql(databaseConnectionString));
builder.Services.AddDbContext<InkFlow.Modules.Sources.Infrastructure.Persistence.SourcesDbContext>(options =>
    options.UseNpgsql(databaseConnectionString));

builder.Services.AddScoped<ICanonicalBookRepository, EfCanonicalBookRepository>();
builder.Services.AddScoped<IMatchCandidateRepository, InkFlow.Modules.Library.Infrastructure.Persistence.EfMatchCandidateRepository>();
builder.Services.AddScoped<ISourceRepository, InkFlow.Modules.Sources.Infrastructure.Persistence.EfSourceRepository>();
builder.Services.AddScoped<ISourceBookRepository, InkFlow.Modules.Sources.Infrastructure.Persistence.EfSourceBookRepository>();
builder.Services.AddScoped<ISourceHealthRepository, InkFlow.Modules.Sources.Infrastructure.Persistence.EfSourceHealthRepository>();
builder.Services.AddSingleton(TimeProvider.System);
var sourceHealthOptions = SourceHealthOptions.FromConfiguration(builder.Configuration);
builder.Services.AddSingleton(sourceHealthOptions);
SourceHealthPolicy.Configure(sourceHealthOptions.ToParameters());
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
builder.Services.AddScoped<ISelectorEvaluator, CssSelectorEvaluator>();
builder.Services.AddScoped<RuleAdapter>();
builder.Services.AddScoped<ISourceAdapterFactory>(sp => new SourceAdapterFactory(
    sp.GetRequiredService<ISourceRepository>(),
    sp.GetRequiredService<RuleAdapter>(),
    sp.GetRequiredService<ISelectorEvaluator>(),
    [sp.GetRequiredService<InkFlow.Sources.Adapters.Kanunu8.KanunuSourceAdapter>()]));
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
builder.Services.AddScoped<CatalogQueryService>();
builder.Services.AddScoped<LegadoContractService>();

var app = builder.Build();

// 认证先于审计/限流，使审计 actor 与认证主体分桶均可用；health 不进入业务审计。
app.UseAuthentication();
app.UseMiddleware<RequestAuditMiddleware>();
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

var operationsRead = api.MapGroup("/admin")
    .RequireAuthorization(IdentityPolicies.OperationsRead);

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
    IOperationsCenterReader operations,
    CancellationToken ct) =>
{
    var snapshot = await operations.ReadAsync(
        limit ?? OperationsCenterReader.DefaultLimit,
        ct);
    return Results.Ok(snapshot);
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
    ISourceRepository sources,
    ISourceHealthOperations health,
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
    HttpContext httpContext,
    IAuditEventSink auditSink,
    TimeProvider clock,
    CancellationToken ct) =>
{
    if (!RepairEndpointResults.TryGetActor(principal, out var actorId))
    {
        return (IResult)Results.Unauthorized();
    }

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
    HttpContext httpContext,
    IAuditEventSink auditSink,
    TimeProvider clock,
    CancellationToken ct) =>
{
    if (!RepairEndpointResults.TryGetActor(principal, out var actorId))
    {
        return (IResult)Results.Unauthorized();
    }

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

// ---- Minimal Web Reader(服务端渲染 HTML)----

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
app.MapGet("/legado/book-source.json", (HttpContext http) =>
{
    var baseUrl = $"{http.Request.Scheme}://{http.Request.Host}";
    return Results.Text(
        LegadoBookSourceManifest.Generate(baseUrl),
        contentType: "application/json; charset=utf-8");
}).RequireRateLimiting(ApiRateLimitPolicies.LegadoPolicyName);

app.Run();

public partial class Program;
