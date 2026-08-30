using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace InkFlow.BuildingBlocks.Messaging;

/// <summary>
/// Outbox 的传输适配端口。具体实现可以对接消息代理、HTTP 或其他受治理的传输，
/// 但必须接受重复投递；Dispatcher 只在发布调用成功后确认 Outbox。
/// </summary>
public interface IIntegrationMessagePublisher
{
    Task PublishAsync(
        OutboxMessageRecord message,
        CancellationToken cancellationToken = default);
}

/// <summary>按消息类型处理已领取的 Inbox 消息。</summary>
public interface IIntegrationMessageHandler
{
    string MessageType { get; }

    Task HandleAsync(
        IntegrationMessage message,
        CancellationToken cancellationToken = default);
}

/// <summary>为 Inbox Consumer 解析稳定消息类型对应的处理器。</summary>
public interface IIntegrationMessageHandlerResolver
{
    IIntegrationMessageHandler? Resolve(string messageType);

    IReadOnlyCollection<string> MessageTypes { get; }
}

/// <summary>发布失败后的有界重试延迟策略。</summary>
public interface IMessageRetryPolicy
{
    TimeSpan DelayFor(int attemptCount);
}

public sealed class OutboxDispatcherOptions
{
    public required string Owner { get; init; }

    public TimeSpan LeaseDuration { get; init; } = TimeSpan.FromMinutes(2);

    public int BatchSize { get; init; } = 50;

    public IMessageRetryPolicy RetryPolicy { get; init; } = new ExponentialMessageRetryPolicy();

    internal void Validate()
    {
        MessageExecutionValidation.ValidateOwner(Owner);
        MessageExecutionValidation.ValidateLease(LeaseDuration);
        MessageExecutionValidation.ValidateBatchSize(BatchSize);
        ArgumentNullException.ThrowIfNull(RetryPolicy);
    }
}

public sealed class InboxConsumerOptions
{
    public const string ConfigurationSectionName = "Messaging:Inbox";

    public required string Owner { get; init; }

    public bool Enabled { get; init; } = true;

    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(5);

    public TimeSpan StartupDelay { get; init; } = TimeSpan.FromSeconds(5);

    public TimeSpan LeaseDuration { get; init; } = TimeSpan.FromMinutes(2);

    public int BatchSize { get; init; } = 50;

    /// <summary>单条 Inbox 消息允许执行 Handler 的最大次数，包含当前领取。</summary>
    public int MaxAttempts { get; init; } = 5;

    /// <summary>Handler 失败后的有界重试延迟策略。</summary>
    public IMessageRetryPolicy RetryPolicy { get; init; } = new ExponentialMessageRetryPolicy();

    /// <summary>从配置读取；缺失配置使用安全默认值，非法值快速失败。</summary>
    public static InboxConsumerOptions FromConfiguration(
        IConfiguration configuration,
        string owner)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var section = configuration.GetSection(ConfigurationSectionName);
        var options = new InboxConsumerOptions
        {
            Owner = owner,
            Enabled = ReadBool(section, nameof(Enabled), true),
            PollInterval = ReadTimeSpan(
                section,
                nameof(PollInterval),
                TimeSpan.FromSeconds(5)),
            StartupDelay = ReadTimeSpan(
                section,
                nameof(StartupDelay),
                TimeSpan.FromSeconds(5)),
            LeaseDuration = ReadTimeSpan(
                section,
                nameof(LeaseDuration),
                TimeSpan.FromMinutes(2)),
            BatchSize = ReadInt(section, nameof(BatchSize), 50),
            MaxAttempts = ReadInt(section, nameof(MaxAttempts), 5),
        };
        options.Validate();
        return options;
    }

    public void Validate()
    {
        MessageExecutionValidation.ValidateOwner(Owner);
        MessageExecutionValidation.ValidateLease(LeaseDuration);
        ValidateRange(
            PollInterval,
            TimeSpan.FromMilliseconds(100),
            TimeSpan.FromMinutes(5),
            nameof(PollInterval));
        ValidateRange(
            StartupDelay,
            TimeSpan.Zero,
            TimeSpan.FromMinutes(5),
            nameof(StartupDelay));
        if (BatchSize is < 1 or > 100)
        {
            throw new InvalidOperationException(
                $"{ConfigurationSectionName}:{nameof(BatchSize)} must be between 1 and 100.");
        }

        if (MaxAttempts is < 1 or > 100)
        {
            throw new InvalidOperationException(
                $"{ConfigurationSectionName}:{nameof(MaxAttempts)} must be between 1 and 100.");
        }

        ArgumentNullException.ThrowIfNull(RetryPolicy);
    }

    private static bool ReadBool(
        IConfiguration section,
        string key,
        bool defaultValue)
    {
        var raw = section[key];
        if (string.IsNullOrWhiteSpace(raw))
        {
            return defaultValue;
        }

        if (!bool.TryParse(raw, out var value))
        {
            throw new InvalidOperationException(
                $"{ConfigurationSectionName}:{key} must be a boolean.");
        }

        return value;
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

    private static TimeSpan ReadTimeSpan(
        IConfiguration section,
        string key,
        TimeSpan defaultValue)
    {
        var raw = section[key];
        if (string.IsNullOrWhiteSpace(raw))
        {
            return defaultValue;
        }

        if (!TimeSpan.TryParse(raw, CultureInfo.InvariantCulture, out var value))
        {
            throw new InvalidOperationException(
                $"{ConfigurationSectionName}:{key} must be a valid duration.");
        }

        return value;
    }

    private static void ValidateRange(
        TimeSpan value,
        TimeSpan minimum,
        TimeSpan maximum,
        string name)
    {
        if (value < minimum || value > maximum)
        {
            throw new InvalidOperationException(
                $"{ConfigurationSectionName}:{name} must be between {minimum} and {maximum}.");
        }
    }
}

public sealed record OutboxDispatchResult(
    int ClaimedCount,
    int PublishedCount,
    int FailedCount);

public enum InboxConsumeStatus
{
    Processed,
    AlreadyProcessed,
    AlreadyInProgress,
    RetryScheduled,
    DeadLettered,
    NoHandler,
    Failed,
}

public sealed record InboxConsumeResult(
    Guid MessageId,
    InboxConsumeStatus Status,
    int AttemptCount,
    string? FailureCode = null);

public interface IOutboxDispatcher
{
    Task<OutboxDispatchResult> DispatchOnceAsync(
        CancellationToken cancellationToken = default);
}

public interface IIntegrationMessageConsumer
{
    Task<InboxConsumeResult> ConsumeAsync(
        IntegrationMessage message,
        CancellationToken cancellationToken = default);

    Task<InboxConsumeResult> ConsumeClaimedAsync(
        InboxMessageRecord message,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 默认的确定性指数退避。宿主如需抖动，可通过 IMessageRetryPolicy 注入自己的有界策略。
/// </summary>
public sealed class ExponentialMessageRetryPolicy : IMessageRetryPolicy
{
    public ExponentialMessageRetryPolicy(
        TimeSpan? baseDelay = null,
        TimeSpan? maxDelay = null)
    {
        BaseDelay = baseDelay ?? TimeSpan.FromSeconds(5);
        MaxDelay = maxDelay ?? TimeSpan.FromHours(1);
        if (BaseDelay <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(baseDelay));
        }

        if (MaxDelay < BaseDelay)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDelay));
        }
    }

    public TimeSpan BaseDelay { get; }

    public TimeSpan MaxDelay { get; }

    public TimeSpan DelayFor(int attemptCount)
    {
        if (attemptCount < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(attemptCount),
                "attempt count starts at 1.");
        }

        var exponent = Math.Min(attemptCount - 1, 30);
        var multiplier = 1L << exponent;
        var maxTicks = MaxDelay.Ticks;
        var baseTicks = BaseDelay.Ticks;
        var delayTicks = baseTicks > maxTicks / multiplier
            ? maxTicks
            : baseTicks * multiplier;
        return TimeSpan.FromTicks(Math.Min(delayTicks, maxTicks));
    }
}

public sealed class IntegrationMessageHandlerRegistry : IIntegrationMessageHandlerResolver
{
    private readonly IReadOnlyDictionary<string, IIntegrationMessageHandler> _handlers;
    private readonly IReadOnlyCollection<string> _messageTypes;

    public IntegrationMessageHandlerRegistry(IEnumerable<IIntegrationMessageHandler> handlers)
    {
        ArgumentNullException.ThrowIfNull(handlers);

        var resolved = new Dictionary<string, IIntegrationMessageHandler>(StringComparer.Ordinal);
        foreach (var handler in handlers)
        {
            ArgumentNullException.ThrowIfNull(handler);
            var messageType = handler.MessageType?.Trim();
            if (string.IsNullOrWhiteSpace(messageType) ||
                messageType.Length > IntegrationMessage.MaxMessageTypeLength ||
                messageType.Any(char.IsControl))
            {
                throw new ArgumentException(
                    "integration message handler type is invalid.",
                    nameof(handlers));
            }

            if (!resolved.TryAdd(messageType, handler))
            {
                throw new InvalidOperationException(
                    $"duplicate integration message handler: {messageType}.");
            }
        }

        _handlers = resolved;
        _messageTypes = resolved.Keys.ToArray();
    }

    public IReadOnlyCollection<string> MessageTypes => _messageTypes;

    public IIntegrationMessageHandler? Resolve(string messageType) =>
        _handlers.TryGetValue(messageType, out var handler) ? handler : null;
}

public static class MessageFailureCodes
{
    public const string PublishFailed = "publish_failed";
    public const string HandlerNotRegistered = "handler_not_registered";
    public const string HandlerFailed = "handler_failed";
    public const string AttemptsExhausted = "attempts_exhausted";
}

internal static class MessageExecutionValidation
{
    public static void ValidateOwner(string owner)
    {
        if (string.IsNullOrWhiteSpace(owner) || owner.Trim().Length > 128 ||
            owner.Any(char.IsControl))
        {
            throw new ArgumentException("message owner is invalid.", nameof(owner));
        }
    }

    public static void ValidateLease(TimeSpan leaseDuration)
    {
        if (leaseDuration <= TimeSpan.Zero || leaseDuration > TimeSpan.FromHours(24))
        {
            throw new ArgumentOutOfRangeException(nameof(leaseDuration));
        }
    }

    public static void ValidateBatchSize(int batchSize)
    {
        if (batchSize is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(batchSize));
        }
    }

    public static void ValidateRetryDelay(TimeSpan retryDelay)
    {
        if (retryDelay < TimeSpan.Zero || retryDelay > TimeSpan.FromDays(7))
        {
            throw new ArgumentOutOfRangeException(nameof(retryDelay));
        }
    }
}
