using System.Diagnostics;
using InkFlow.Modules.Billing.Application;

namespace InkFlow.Api;

internal sealed class DeveloperEndpointLogCategory
{
}

internal static class DeveloperEndpointResults
{
    public static async Task<IResult?> ReserveAsync(
        IQuotaService quota,
        TimeProvider clock,
        ILogger logger,
        HttpContext httpContext,
        DeveloperCatalogEndpoints.DeveloperIdentity identity,
        string operation,
        long units,
        CancellationToken cancellationToken)
    {
        QuotaReservationResult result;
        try
        {
            result = await quota
                .ReserveAsync(
                    new QuotaReservationRequest(
                        identity.UserId,
                        identity.ApplicationId,
                        identity.ApiKeyId,
                        operation,
                        units,
                        Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "developer API quota reservation failed for {Operation}", operation);
            NoStore(httpContext);
            return Results.Json(
                new { error = "quota_unavailable" },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        switch (result.Status)
        {
            case QuotaReservationStatus.Reserved:
                return null;
            case QuotaReservationStatus.Forbidden:
                NoStore(httpContext);
                return Results.Json(
                    new { error = "developer_api_forbidden" },
                    statusCode: StatusCodes.Status403Forbidden);
            case QuotaReservationStatus.InvalidRequest:
                NoStore(httpContext);
                return Results.BadRequest(new { error = "invalid_request" });
            case QuotaReservationStatus.Exceeded:
            {
                var now = clock.GetUtcNow();
                var periodEnd = result.Snapshot?.PeriodEnd ?? now.AddMinutes(1);
                var retryAfter = Math.Max(1, (int)Math.Ceiling((periodEnd - now).TotalSeconds));
                httpContext.Response.Headers["Retry-After"] = retryAfter.ToString();
                NoStore(httpContext);
                return Results.Json(
                    new
                    {
                        error = "quota_exceeded",
                        periodEnd,
                        remainingUnits = result.Snapshot?.RemainingUnits ?? 0,
                    },
                    statusCode: StatusCodes.Status429TooManyRequests);
            }
            default:
                NoStore(httpContext);
                return Results.Json(
                    new { error = "quota_unavailable" },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    public static void NoStore(HttpContext context)
    {
        context.Response.Headers.CacheControl = "private, no-store";
    }
}
