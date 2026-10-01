using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace InkFlow.Modules.Identity.Application;

/// <summary>
/// Identity 会话/令牌事实的有界保留策略。过期删除只由受控 retention job 执行，
/// 认证路径只追加事实，不参与清理决策。
/// </summary>
public sealed class IdentityRetentionOptions
{
    public const string ConfigurationSectionName = "Identity:Retention";

    /// <summary>已过期会话/令牌的额外保留天数，用于事故取证与安全调查。</summary>
    public int GraceDays { get; init; } = 7;

    public int BatchSize { get; init; } = 500;

    public int MaxBatchesPerRun { get; init; } = 10;

    public TimeSpan Grace => TimeSpan.FromDays(GraceDays);

    /// <summary>从配置读取；缺失配置使用安全默认值，非法值快速失败。</summary>
    public static IdentityRetentionOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var section = configuration.GetSection(ConfigurationSectionName);
        var options = new IdentityRetentionOptions
        {
            GraceDays = ReadInt(section, nameof(GraceDays), 7),
            BatchSize = ReadInt(section, nameof(BatchSize), 500),
            MaxBatchesPerRun = ReadInt(section, nameof(MaxBatchesPerRun), 10),
        };
        options.Validate();
        return options;
    }

    public void Validate()
    {
        ValidateRange(GraceDays, 0, 365, nameof(GraceDays));
        ValidateRange(BatchSize, 1, 1_000, nameof(BatchSize));
        ValidateRange(MaxBatchesPerRun, 1, 100, nameof(MaxBatchesPerRun));
    }

    private static int ReadInt(IConfiguration section, string key, int defaultValue)
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

public sealed record IdentityRetentionResult(int DeletedSessions, int DeletedAccessTokens);

/// <summary>
/// 删除一批已过期且超过宽限期的 Identity 事实。
/// 实现必须保证只删除过期行，绝不触碰仍然活跃的会话或令牌。
/// </summary>
public interface IIdentityRetentionStore
{
    Task<IdentityRetentionResult> DeleteExpiredBatchAsync(
        DateTimeOffset sessionCutoff,
        DateTimeOffset accessTokenCutoff,
        int batchSize,
        CancellationToken cancellationToken = default);
}

public interface IIdentityRetentionService
{
    Task<IdentityRetentionResult> CleanupAsync(
        IdentityRetentionOptions options,
        CancellationToken cancellationToken = default);
}

/// <summary>有界执行 Identity retention，避免单次运行因历史积压占满数据库。</summary>
public sealed class IdentityRetentionService(
    IIdentityRetentionStore store,
    TimeProvider clock) : IIdentityRetentionService
{
    public async Task<IdentityRetentionResult> CleanupAsync(
        IdentityRetentionOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        var now = clock.GetUtcNow().ToUniversalTime();
        var cutoff = now - options.Grace;

        var total = new IdentityRetentionResult(0, 0);
        for (var batchNumber = 0; batchNumber < options.MaxBatchesPerRun; batchNumber++)
        {
            var deleted = await store
                .DeleteExpiredBatchAsync(cutoff, cutoff, options.BatchSize, cancellationToken)
                .ConfigureAwait(false);
            total = new IdentityRetentionResult(
                total.DeletedSessions + deleted.DeletedSessions,
                total.DeletedAccessTokens + deleted.DeletedAccessTokens);

            // 两个集合都未达到批大小上限时说明已排空，可以提前结束。
            if (deleted.DeletedSessions < options.BatchSize &&
                deleted.DeletedAccessTokens < options.BatchSize)
            {
                break;
            }
        }

        return total;
    }
}
