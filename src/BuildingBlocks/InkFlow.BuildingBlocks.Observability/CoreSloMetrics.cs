using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Http;

namespace InkFlow.BuildingBlocks.Observability;

/// <summary>
/// Core SLO v1 的稳定服务面和目标。目标是测量契约，不代表没有时间窗口证据时已经达标。
/// </summary>
public static class CoreSloPolicy
{
    public const string Version = "core-slo-v1";

    public const string PublicApiSurface = "public_api";
    public const string LegadoApiSurface = "legado_api";
    public const string DeveloperApiSurface = "developer_api";
    public const string ReaderSurface = "reader";

    public const decimal AvailabilityTarget = 0.995m;
    public const double PublicApiLatencyP95Milliseconds = 750;
    public const double LegadoApiLatencyP95Milliseconds = 1_000;
    public const double DeveloperApiLatencyP95Milliseconds = 750;
    public const double ReaderLatencyP95Milliseconds = 1_000;

    /// <summary>
    /// Expected client outcomes, including authentication and rate-limit responses, do not
    /// consume the server availability budget. Server failures (5xx) are bad events.
    /// </summary>
    public static bool IsGoodAvailabilityStatus(int statusCode) =>
        statusCode is >= 100 and < 500;

    public static double LatencyP95TargetMilliseconds(string surface) => surface switch
    {
        PublicApiSurface => PublicApiLatencyP95Milliseconds,
        LegadoApiSurface => LegadoApiLatencyP95Milliseconds,
        DeveloperApiSurface => DeveloperApiLatencyP95Milliseconds,
        ReaderSurface => ReaderLatencyP95Milliseconds,
        _ => throw new ArgumentException("unknown Core SLO surface.", nameof(surface)),
    };

    public static bool IsKnownSurface(string? surface) => surface is
        PublicApiSurface or LegadoApiSurface or DeveloperApiSurface or ReaderSurface;

    /// <summary>
    /// Maps only stable service surfaces. Path values, identifiers, query strings and user
    /// identities never become metric attributes.
    /// </summary>
    public static bool TryGetSurface(PathString path, out string surface) =>
        TryGetSurface(path.Value, out surface);

    public static bool TryGetSurface(string? path, out string surface)
    {
        surface = string.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        if (IsPathOrDescendant(path, "/api/legado/v1") ||
            string.Equals(path, "/legado/book-source.json", StringComparison.OrdinalIgnoreCase))
        {
            surface = LegadoApiSurface;
            return true;
        }

        if (IsPathOrDescendant(path, "/api/developer/v1"))
        {
            surface = DeveloperApiSurface;
            return true;
        }

        if (IsPathOrDescendant(path, "/api/v1"))
        {
            surface = PublicApiSurface;
            return true;
        }

        if (IsPathOrDescendant(path, "/reader"))
        {
            surface = ReaderSurface;
            return true;
        }

        return false;
    }

    private static bool IsPathOrDescendant(string path, string prefix) =>
        string.Equals(path, prefix, StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Custom low-cardinality metrics used to calculate the Core SLO. Detailed route/path metrics
/// remain the responsibility of the standard ASP.NET Core OpenTelemetry instrumentation.
/// </summary>
public static class CoreSloMetrics
{
    public const string MeterName = "InkFlow.Core.Slo";
    public const string RequestsName = "inkflow.slo.requests";
    public const string RequestDurationName = "inkflow.slo.request.duration";
    public const string ServerErrorsName = "inkflow.slo.server.errors";

    private const string SurfaceTag = "inkflow.slo.surface";
    private const string OutcomeTag = "inkflow.slo.outcome";
    private const string GoodOutcome = "good";
    private const string BadOutcome = "bad";

    private static readonly Meter Meter = new(MeterName, CoreSloPolicy.Version);
    private static readonly Counter<long> Requests = Meter.CreateCounter<long>(
        RequestsName,
        unit: "{request}",
        description: "Core SLO request events by bounded service surface and outcome.");
    private static readonly Histogram<double> RequestDuration = Meter.CreateHistogram<double>(
        RequestDurationName,
        unit: "ms",
        description: "Core SLO request duration by bounded service surface and outcome.");
    private static readonly Counter<long> ServerErrors = Meter.CreateCounter<long>(
        ServerErrorsName,
        unit: "{error}",
        description: "Core SLO server error events by bounded service surface.");

    public static void Record(string surface, int statusCode, TimeSpan elapsed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(surface);
        if (!CoreSloPolicy.IsKnownSurface(surface))
        {
            throw new ArgumentException("unknown Core SLO surface.", nameof(surface));
        }

        if (statusCode is < 100 or > 599)
        {
            throw new ArgumentOutOfRangeException(nameof(statusCode));
        }

        var outcome = CoreSloPolicy.IsGoodAvailabilityStatus(statusCode)
            ? GoodOutcome
            : BadOutcome;
        var tags = new TagList();
        tags.Add(SurfaceTag, surface);
        tags.Add(OutcomeTag, outcome);

        Requests.Add(1, tags);
        RequestDuration.Record(Math.Max(0, elapsed.TotalMilliseconds), tags);
        if (outcome == BadOutcome)
        {
            var errorTags = new TagList();
            errorTags.Add(SurfaceTag, surface);
            ServerErrors.Add(1, errorTags);
        }
    }
}

/// <summary>
/// Records Core SLO events around selected user-facing surfaces. Health, admin static pages,
/// source internals and unknown paths are intentionally outside this SLI.
/// </summary>
public sealed class CoreSloMetricsMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!CoreSloPolicy.TryGetSurface(context.Request.Path, out var surface))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        var startedAt = Stopwatch.GetTimestamp();
        var failed = false;
        try
        {
            await next(context).ConfigureAwait(false);
        }
        catch
        {
            failed = true;
            throw;
        }
        finally
        {
            CoreSloMetrics.Record(
                surface,
                failed ? StatusCodes.Status500InternalServerError : context.Response.StatusCode,
                Stopwatch.GetElapsedTime(startedAt));
        }
    }
}
