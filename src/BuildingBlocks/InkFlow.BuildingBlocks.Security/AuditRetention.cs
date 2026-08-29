using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace InkFlow.BuildingBlocks.Security;

/// <summary>
/// 审计事实的有界保留策略。过期删除仅由受控 retention job 执行；普通审计写入路径仍只追加。
/// </summary>
public sealed class AuditRetentionOptions
{
    public const string ConfigurationSectionName = "Audit:Retention";

    public int RetentionDays { get; init; } = 365;

    public int BatchSize { get; init; } = 500;

    public int MaxBatchesPerRun { get; init; } = 10;

    public TimeSpan Retention => TimeSpan.FromDays(RetentionDays);

    /// <summary>从配置读取；缺失配置使用安全默认值，非法值快速失败。</summary>
    public static AuditRetentionOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var section = configuration.GetSection(ConfigurationSectionName);
        var options = new AuditRetentionOptions
        {
            RetentionDays = ReadInt(section, nameof(RetentionDays), 365),
            BatchSize = ReadInt(section, nameof(BatchSize), 500),
            MaxBatchesPerRun = ReadInt(section, nameof(MaxBatchesPerRun), 10),
        };
        options.Validate();
        return options;
    }

    public void Validate()
    {
        ValidateRange(RetentionDays, 1, 10 * 365, nameof(RetentionDays));
        ValidateRange(BatchSize, 1, 1_000, nameof(BatchSize));
        ValidateRange(MaxBatchesPerRun, 1, 100, nameof(MaxBatchesPerRun));
    }

    private static int ReadInt(
        IConfiguration section,
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

    private static void ValidateRange(int value, int minimum, int maximum, string name)
    {
        if (value < minimum || value > maximum)
        {
            throw new InvalidOperationException(
                $"{ConfigurationSectionName}:{name} must be between {minimum} and {maximum}.");
        }
    }
}

public sealed record AuditRetentionResult(int DeletedCount);

/// <summary>删除一批早于 cutoff 的审计事实；实现必须服从数据库的受控 retention 删除边界。</summary>
public interface IAuditRetentionStore
{
    Task<int> DeleteExpiredBatchAsync(
        DateTimeOffset cutoff,
        int batchSize,
        CancellationToken cancellationToken = default);
}

public interface IAuditRetentionService
{
    Task<AuditRetentionResult> CleanupAsync(
        AuditRetentionOptions options,
        CancellationToken cancellationToken = default);
}

/// <summary>有界执行审计 retention，避免单次运行因历史积压占满数据库。</summary>
public sealed class AuditRetentionService(
    IAuditRetentionStore store,
    TimeProvider clock) : IAuditRetentionService
{
    public async Task<AuditRetentionResult> CleanupAsync(
        AuditRetentionOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        var cutoff = clock.GetUtcNow().ToUniversalTime() - options.Retention;
        var deletedCount = 0;
        for (var batchNumber = 0; batchNumber < options.MaxBatchesPerRun; batchNumber++)
        {
            var deleted = await store
                .DeleteExpiredBatchAsync(cutoff, options.BatchSize, cancellationToken)
                .ConfigureAwait(false);
            deletedCount += deleted;

            if (deleted < options.BatchSize)
            {
                break;
            }
        }

        return new AuditRetentionResult(deletedCount);
    }
}
