using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace InkFlow.BuildingBlocks.Messaging;

/// <summary>
/// 已成功处理消息的保留策略。未处理的消息（包括失败后等待重试的消息）
/// 不会被 retention 清理删除。
/// </summary>
public sealed class MessageRetentionOptions
{
    public const string ConfigurationSectionName = "Messaging:Retention";

    public int OutboxRetentionDays { get; init; } = 30;

    public int InboxRetentionDays { get; init; } = 30;

    public int BatchSize { get; init; } = 500;

    public int MaxBatchesPerRun { get; init; } = 10;

    public TimeSpan OutboxRetention => TimeSpan.FromDays(OutboxRetentionDays);

    public TimeSpan InboxRetention => TimeSpan.FromDays(InboxRetentionDays);

    /// <summary>从配置读取；缺失配置使用安全默认值，非法值快速失败。</summary>
    public static MessageRetentionOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var section = configuration.GetSection(ConfigurationSectionName);
        var options = new MessageRetentionOptions
        {
            OutboxRetentionDays = ReadInt(
                section,
                nameof(OutboxRetentionDays),
                30),
            InboxRetentionDays = ReadInt(
                section,
                nameof(InboxRetentionDays),
                30),
            BatchSize = ReadInt(section, nameof(BatchSize), 500),
            MaxBatchesPerRun = ReadInt(section, nameof(MaxBatchesPerRun), 10),
        };
        options.Validate();
        return options;
    }

    public void Validate()
    {
        ValidateRange(OutboxRetentionDays, 1, 10 * 365, nameof(OutboxRetentionDays));
        ValidateRange(InboxRetentionDays, 1, 10 * 365, nameof(InboxRetentionDays));
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

public sealed record MessageRetentionBatchResult(
    int OutboxDeletedCount,
    int InboxDeletedCount);

public sealed record MessageRetentionResult(
    int OutboxDeletedCount,
    int InboxDeletedCount)
{
    public int TotalDeletedCount => OutboxDeletedCount + InboxDeletedCount;
}

/// <summary>
/// 删除一批已处理消息。实现必须保留未处理、失败待重试和仍在 lease 中的消息。
/// </summary>
public interface IMessageRetentionStore
{
    Task<MessageRetentionBatchResult> DeleteProcessedBatchAsync(
        DateTimeOffset outboxCutoff,
        DateTimeOffset inboxCutoff,
        int batchSize,
        CancellationToken cancellationToken = default);
}

public interface IMessageRetentionService
{
    Task<MessageRetentionResult> CleanupAsync(
        MessageRetentionOptions options,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 有界执行 retention 清理，避免单次运行因历史积压占满数据库。
/// </summary>
public sealed class MessageRetentionService(
    IMessageRetentionStore store,
    TimeProvider clock) : IMessageRetentionService
{
    public async Task<MessageRetentionResult> CleanupAsync(
        MessageRetentionOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        var now = clock.GetUtcNow().ToUniversalTime();
        var outboxCutoff = now - options.OutboxRetention;
        var inboxCutoff = now - options.InboxRetention;
        var total = new MessageRetentionResult(0, 0);

        for (var batchNumber = 0; batchNumber < options.MaxBatchesPerRun; batchNumber++)
        {
            var deleted = await store
                .DeleteProcessedBatchAsync(
                    outboxCutoff,
                    inboxCutoff,
                    options.BatchSize,
                    cancellationToken)
                .ConfigureAwait(false);
            total = new(
                total.OutboxDeletedCount + deleted.OutboxDeletedCount,
                total.InboxDeletedCount + deleted.InboxDeletedCount);

            if (deleted.OutboxDeletedCount < options.BatchSize &&
                deleted.InboxDeletedCount < options.BatchSize)
            {
                break;
            }
        }

        return total;
    }
}
