using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using InkFlow.Modules.Crawling.Orchestration;

namespace InkFlow.Api;

public static class AdminEndpoints
{
    private const string AdminKeyHeader = "X-InkFlow-Admin-Key";

    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/v1");
        group.MapGet("/sources", ListSourcesAsync);
        group.MapPost("/sources", CreateSourceAsync);
        group.MapPost("/rules/validate", ValidateRuleAsync);
        group.MapPost("/sources/{sourceId:guid}/rules/publish", PublishRuleAsync);
        group.MapPost("/sources/{sourceId:guid}/imports", ImportBookAsync);
        group.MapPost("/sources/{sourceId:guid}/debug/{operation}", DebugSourceAsync);
        return endpoints;
    }

    private static async Task<IResult> ListSourcesAsync(
        HttpContext context,
        IConfiguration configuration,
        SourceAdministrationService service,
        CancellationToken cancellationToken)
    {
        if (RequireAdmin(context, configuration) is { } failure)
        {
            return failure;
        }
        return Results.Ok(await service.ListSourcesAsync(cancellationToken));
    }

    private static async Task<IResult> CreateSourceAsync(
        CreateSourceRequest request,
        HttpContext context,
        IConfiguration configuration,
        SourceAdministrationService service,
        CancellationToken cancellationToken)
    {
        if (RequireAdmin(context, configuration) is { } failure)
        {
            return failure;
        }
        try
        {
            var created = await service.CreateSourceAsync(request.Name, request.BaseUrl, request.Kind, cancellationToken);
            return Results.Created($"/api/admin/v1/sources/{created.Id}", created);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = "SOURCE_INVALID", message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return Results.Conflict(new { error = "SOURCE_CONFLICT", message = exception.Message });
        }
    }

    private static Task<IResult> ValidateRuleAsync(
        RuleDocumentRequest request,
        HttpContext context,
        IConfiguration configuration,
        SourceAdministrationService service)
    {
        if (RequireAdmin(context, configuration) is { } failure)
        {
            return Task.FromResult(failure);
        }
        var report = service.ValidateRule(request.Rule.GetRawText());
        return Task.FromResult<IResult>(Results.Ok(report));
    }

    private static async Task<IResult> PublishRuleAsync(
        Guid sourceId,
        RuleDocumentRequest request,
        HttpContext context,
        IConfiguration configuration,
        SourceAdministrationService service,
        CancellationToken cancellationToken)
    {
        if (RequireAdmin(context, configuration) is { } failure)
        {
            return failure;
        }
        try
        {
            var result = await service.PublishRuleAsync(sourceId, request.Rule.GetRawText(), cancellationToken);
            if (result.Rule is null)
            {
                return Results.BadRequest(new { error = "RULE_INVALID", errors = result.Errors });
            }
            return Results.Ok(result.Rule);
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound(new { error = "SOURCE_NOT_FOUND" });
        }
    }

    private static async Task<IResult> ImportBookAsync(
        Guid sourceId,
        ImportBookRequest request,
        HttpContext context,
        IConfiguration configuration,
        SourceAdministrationService service,
        CancellationToken cancellationToken)
    {
        if (RequireAdmin(context, configuration) is { } failure)
        {
            return failure;
        }
        try
        {
            var result = await service.EnqueueBookImportAsync(sourceId, request.BookUrl, request.ExternalId, cancellationToken);
            return Results.Accepted($"/api/admin/v1/crawler/tasks/{result.TaskId}", result);
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound(new { error = "SOURCE_NOT_FOUND" });
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = "IMPORT_INVALID", message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return Results.Conflict(new { error = "IMPORT_NOT_READY", message = exception.Message });
        }
    }

    private static async Task<IResult> DebugSourceAsync(
        Guid sourceId,
        string operation,
        DebugSourceRequest request,
        HttpContext context,
        IConfiguration configuration,
        SourceDebuggerService debugger,
        CancellationToken cancellationToken)
    {
        if (RequireAdmin(context, configuration) is { } failure)
        {
            return failure;
        }
        try
        {
            var result = await debugger.DebugAsync(
                sourceId,
                operation,
                request.Rule.GetRawText(),
                request.Variables,
                cancellationToken);
            if (result.ValidationErrors.Count > 0)
            {
                return Results.BadRequest(result);
            }
            if (!result.Executed)
            {
                return Results.Json(result, statusCode: StatusCodes.Status502BadGateway);
            }
            return Results.Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound(new { error = "SOURCE_NOT_FOUND" });
        }
    }

    private static IResult? RequireAdmin(HttpContext context, IConfiguration configuration)
    {
        var expected = configuration["Admin:BootstrapKey"];
        if (string.IsNullOrWhiteSpace(expected))
        {
            return Results.Problem(
                title: "Admin bootstrap key is not configured.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        if (!context.Request.Headers.TryGetValue(AdminKeyHeader, out var provided)
            || string.IsNullOrWhiteSpace(provided))
        {
            return Results.Unauthorized();
        }
        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        var providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(provided.ToString()));
        return CryptographicOperations.FixedTimeEquals(expectedHash, providedHash)
            ? null
            : Results.Unauthorized();
    }

    public sealed record CreateSourceRequest(string Name, string BaseUrl, string? Kind);
    public sealed record RuleDocumentRequest(JsonElement Rule);
    public sealed record ImportBookRequest(string BookUrl, string? ExternalId);
    public sealed record DebugSourceRequest(JsonElement Rule, IReadOnlyDictionary<string, string>? Variables);
}
