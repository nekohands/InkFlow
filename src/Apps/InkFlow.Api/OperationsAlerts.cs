using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace InkFlow.Api;

/// <summary>
/// Operations 告警阈值。告警读取是快照语义；外部通知、去重和历史留存不在本版本内。
/// </summary>
public sealed class OperationsAlertOptions
{
    public const string ConfigurationSectionName = "Operations:Alerts";

    public int DeadLetterCountThreshold { get; init; } = 1;
    public int UnavailableCapabilityCountThreshold { get; init; } = 1;
    public int ConsistencyIssueCountThreshold { get; init; } = 1;
    public int MaxReturnedAlerts { get; init; } = 100;

    public static OperationsAlertOptions FromConfiguration(
        IConfiguration configuration)
    {
        var section = configuration.GetSection(ConfigurationSectionName);
        var options = new OperationsAlertOptions
        {
            DeadLetterCountThreshold = ReadInt(
                section,
                nameof(DeadLetterCountThreshold),
                1),
            UnavailableCapabilityCountThreshold = ReadInt(
                section,
                nameof(UnavailableCapabilityCountThreshold),
                1),
            ConsistencyIssueCountThreshold = ReadInt(
                section,
                nameof(ConsistencyIssueCountThreshold),
                1),
            MaxReturnedAlerts = ReadInt(
                section,
                nameof(MaxReturnedAlerts),
                100),
        };
        options.Validate();
        return options;
    }

    public void Validate()
    {
        ValidateRange(DeadLetterCountThreshold, 1, 100_000, nameof(DeadLetterCountThreshold));
        ValidateRange(
            UnavailableCapabilityCountThreshold,
            1,
            100_000,
            nameof(UnavailableCapabilityCountThreshold));
        ValidateRange(
            ConsistencyIssueCountThreshold,
            1,
            100_000,
            nameof(ConsistencyIssueCountThreshold));
        ValidateRange(MaxReturnedAlerts, 1, 100, nameof(MaxReturnedAlerts));
    }

    private static int ReadInt(
        IConfigurationSection section,
        string key,
        int defaultValue)
    {
        var raw = section[key];
        if (string.IsNullOrWhiteSpace(raw))
        {
            return defaultValue;
        }

        if (!int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var value))
        {
            throw new InvalidOperationException(
                $"{ConfigurationSectionName}:{key} must be an integer.");
        }

        return value;
    }

    private static void ValidateRange(
        int value,
        int minimum,
        int maximum,
        string name)
    {
        if (value < minimum || value > maximum)
        {
            throw new InvalidOperationException(
                $"{ConfigurationSectionName}:{name} must be between {minimum} and {maximum}.");
        }
    }
}

public sealed record OperationsAlert(
    string Code,
    string Severity,
    string ResourceType,
    string ResourceId,
    string Message);

public sealed record OperationsAlertSnapshot(
    DateTimeOffset GeneratedAt,
    string Status,
    int TotalAlertCount,
    int ReturnedAlertCount,
    bool Truncated,
    IReadOnlyList<OperationsAlert> Alerts);

public interface IOperationsAlertReader
{
    Task<OperationsAlertSnapshot> ReadAsync(
        int limit,
        CancellationToken cancellationToken = default);

    Task<OperationsAlertSnapshot> ReadForSourcesAsync(
        int limit,
        IReadOnlySet<string> allowedSourceIds,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 将 Operations Center 的有界读模型转换为可轮询的告警快照。
/// 不执行修复、不发送通知，也不把原始异常或来源响应带入告警。
/// </summary>
public sealed class OperationsAlertReader(
    IOperationsCenterReader operations,
    IRateLimitStoreHealthReader rateLimitHealth,
    OperationsAlertOptions options,
    TimeProvider clock) : IOperationsAlertReader
{
    public const int DefaultLimit = 50;

    private readonly OperationsAlertOptions _options = ValidateOptions(options);

    public async Task<OperationsAlertSnapshot> ReadAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        return await ReadCoreAsync(limit, null, cancellationToken).ConfigureAwait(false);
    }

    public async Task<OperationsAlertSnapshot> ReadForSourcesAsync(
        int limit,
        IReadOnlySet<string> allowedSourceIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(allowedSourceIds);
        return await ReadCoreAsync(limit, allowedSourceIds, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<OperationsAlertSnapshot> ReadCoreAsync(
        int limit,
        IReadOnlySet<string>? allowedSourceIds,
        CancellationToken cancellationToken)
    {
        var boundedLimit = Math.Clamp(limit, 1, _options.MaxReturnedAlerts);
        var operationsSnapshot = allowedSourceIds is null
            ? await operations.ReadAsync(OperationsCenterReader.MaxLimit, cancellationToken)
                .ConfigureAwait(false)
            : await operations.ReadForSourcesAsync(
                    OperationsCenterReader.MaxLimit,
                    allowedSourceIds,
                    cancellationToken)
                .ConfigureAwait(false);
        var alerts = OperationsAlertEvaluator.Evaluate(
            operationsSnapshot,
            rateLimitHealth.GetSnapshot(),
            _options);
        var returnedAlerts = alerts.Take(boundedLimit).ToList();

        return new OperationsAlertSnapshot(
            clock.GetUtcNow(),
            operationsSnapshot.Status,
            alerts.Count,
            returnedAlerts.Count,
            returnedAlerts.Count < alerts.Count,
            returnedAlerts);
    }

    private static OperationsAlertOptions ValidateOptions(OperationsAlertOptions value)
    {
        ArgumentNullException.ThrowIfNull(value);
        value.Validate();
        return value;
    }
}

/// <summary>纯函数告警规则，便于在没有 PostgreSQL/Redis 的情况下回归。</summary>
public static class OperationsAlertEvaluator
{
    public static IReadOnlyList<OperationsAlert> Evaluate(
        OperationsCenterResponse snapshot,
        RateLimitStoreHealthSnapshot rateLimitHealth,
        OperationsAlertOptions options)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(rateLimitHealth);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        var alerts = new List<OperationsAlert>();
        if (!string.Equals(snapshot.Status, "ready", StringComparison.OrdinalIgnoreCase))
        {
            alerts.Add(new OperationsAlert(
                "operations_snapshot_partial",
                "warning",
                "operations",
                "operations-center",
                "one or more operations sections could not be read"));
        }

        AddSourceAlerts(snapshot, options, alerts);
        AddCrawlerAlerts(snapshot, options, alerts);
        AddConsistencyAlerts(snapshot, options, alerts);

        if (rateLimitHealth.Status == RateLimitStoreHealthStatus.Unavailable)
        {
            alerts.Add(new OperationsAlert(
                "rate_limit_store_unavailable",
                "critical",
                "infrastructure",
                "redis-rate-limit-store",
                "distributed rate-limit storage is unavailable; bounded local fallback may be active"));
        }

        return alerts
            .OrderBy(alert => SeverityRank(alert.Severity))
            .ThenBy(alert => alert.Code, StringComparer.Ordinal)
            .ThenBy(alert => alert.ResourceId, StringComparer.Ordinal)
            .ToList();
    }

    private static void AddSourceAlerts(
        OperationsCenterResponse snapshot,
        OperationsAlertOptions options,
        ICollection<OperationsAlert> alerts)
    {
        if (!string.Equals(snapshot.Sources.Status, "ready", StringComparison.OrdinalIgnoreCase))
        {
            alerts.Add(new OperationsAlert(
                "source_health_snapshot_unavailable",
                "critical",
                "operations-section",
                "sources",
                snapshot.Sources.Error ?? "source health could not be read"));
        }

        var unavailableCapabilities = snapshot.Sources.Data?
            .SelectMany(source => source.Capabilities)
            .Count(capability => !capability.IsAvailable) ?? 0;
        if (unavailableCapabilities >= options.UnavailableCapabilityCountThreshold)
        {
            alerts.Add(new OperationsAlert(
                "source_capabilities_unavailable",
                "critical",
                "source-health",
                "sources",
                $"{unavailableCapabilities} source capabilities are unavailable; " +
                $"threshold={options.UnavailableCapabilityCountThreshold}"));
        }
    }

    private static void AddCrawlerAlerts(
        OperationsCenterResponse snapshot,
        OperationsAlertOptions options,
        ICollection<OperationsAlert> alerts)
    {
        if (!string.Equals(snapshot.Crawler.Status, "ready", StringComparison.OrdinalIgnoreCase))
        {
            alerts.Add(new OperationsAlert(
                "crawler_snapshot_unavailable",
                "critical",
                "operations-section",
                "crawler",
                snapshot.Crawler.Error ?? "crawler state could not be read"));
            return;
        }

        var crawler = snapshot.Crawler.Data;
        if (crawler is null)
        {
            return;
        }

        if (crawler.ReturnedDeadLetterCount >= options.DeadLetterCountThreshold ||
            crawler.HasMoreDeadLetters)
        {
            alerts.Add(new OperationsAlert(
                "crawler_dead_letters_present",
                "critical",
                "crawler",
                "dead-letters",
                $"dead-letter tasks are present; returned={crawler.ReturnedDeadLetterCount}, " +
                $"hasMore={crawler.HasMoreDeadLetters}, threshold={options.DeadLetterCountThreshold}"));
        }
    }

    private static void AddConsistencyAlerts(
        OperationsCenterResponse snapshot,
        OperationsAlertOptions options,
        ICollection<OperationsAlert> alerts)
    {
        if (!string.Equals(snapshot.Consistency.Status, "ready", StringComparison.OrdinalIgnoreCase))
        {
            alerts.Add(new OperationsAlert(
                "consistency_snapshot_unavailable",
                "critical",
                "operations-section",
                "consistency",
                snapshot.Consistency.Error ?? "consistency report could not be read"));
            return;
        }

        var report = snapshot.Consistency.Data;
        if (report is not null &&
            report.TotalIssueCount >= options.ConsistencyIssueCountThreshold)
        {
            alerts.Add(new OperationsAlert(
                "consistency_issues_found",
                "critical",
                "consistency",
                "consistency-report",
                $"consistency issues detected; total={report.TotalIssueCount}, " +
                $"threshold={options.ConsistencyIssueCountThreshold}, truncated={report.Truncated}"));
        }
    }

    private static int SeverityRank(string severity) =>
        string.Equals(severity, "critical", StringComparison.OrdinalIgnoreCase) ? 0 : 1;
}
