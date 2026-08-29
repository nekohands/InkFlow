using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace InkFlow.BuildingBlocks.Messaging;

/// <summary>
/// Worker 内部 PostgreSQL Outbox relay 的有界运行参数。
/// 该 relay 不引入未选定的外部消息代理，只负责把 Outbox 事实可靠地转入 Inbox 事实表。
/// </summary>
public sealed class OutboxRelayOptions
{
    public const string ConfigurationSectionName = "Messaging:Relay";

    public bool Enabled { get; init; } = true;

    public string OwnerPrefix { get; init; } = "inkflow-worker-relay";

    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(5);

    public TimeSpan StartupDelay { get; init; } = TimeSpan.FromSeconds(5);

    public TimeSpan LeaseDuration { get; init; } = TimeSpan.FromMinutes(2);

    public int BatchSize { get; init; } = 50;

    /// <summary>从配置读取；缺失配置使用安全默认值，非法值快速失败。</summary>
    public static OutboxRelayOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var section = configuration.GetSection(ConfigurationSectionName);
        var options = new OutboxRelayOptions
        {
            Enabled = ReadBool(section, nameof(Enabled), true),
            OwnerPrefix = ReadString(
                section,
                nameof(OwnerPrefix),
                "inkflow-worker-relay"),
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
        };
        options.Validate();
        return options;
    }

    public void Validate()
    {
        var normalizedPrefix = OwnerPrefix?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedPrefix) ||
            normalizedPrefix.Length > 60 ||
            normalizedPrefix.Any(char.IsControl))
        {
            throw new InvalidOperationException(
                $"{ConfigurationSectionName}:{nameof(OwnerPrefix)} must be 1-60 characters without control characters.");
        }

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
        ValidateRange(
            LeaseDuration,
            TimeSpan.FromMilliseconds(1),
            TimeSpan.FromHours(24),
            nameof(LeaseDuration));

        if (BatchSize is < 1 or > 100)
        {
            throw new InvalidOperationException(
                $"{ConfigurationSectionName}:{nameof(BatchSize)} must be between 1 and 100.");
        }
    }

    /// <summary>生成不重复的进程 owner；结果长度始终适配消息 lease 字段。</summary>
    public string CreateOwner(string instanceName)
    {
        Validate();
        var normalizedInstance = instanceName?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedInstance) ||
            normalizedInstance.Length > 32 ||
            normalizedInstance.Any(char.IsControl))
        {
            throw new ArgumentException(
                "relay instance name must be 1-32 characters without control characters.",
                nameof(instanceName));
        }

        return $"{OwnerPrefix.Trim()}-{normalizedInstance}-{Guid.CreateVersion7():N}";
    }

    public OutboxDispatcherOptions CreateDispatcherOptions(string owner)
    {
        Validate();
        return new OutboxDispatcherOptions
        {
            Owner = owner,
            LeaseDuration = LeaseDuration,
            BatchSize = BatchSize,
        };
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

    private static string ReadString(
        IConfiguration section,
        string key,
        string defaultValue) =>
        section[key] ?? defaultValue;

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
