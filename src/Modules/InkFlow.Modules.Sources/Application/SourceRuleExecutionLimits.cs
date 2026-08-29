namespace InkFlow.Modules.Sources.Application;

/// <summary>
/// A bounded execution policy for the RuleAdapter, including its optional next-link pagination.
/// The values are deliberately finite: a source rule must not be able to consume an
/// unbounded amount of network, CPU, or memory even when its input is untrusted.
/// </summary>
public sealed record SourceRuleExecutionLimits
{
    public const int DefaultMaxRequests = 8;
    public const int DefaultMaxBytes = 2 * 1024 * 1024;
    public const int DefaultMaxResultSize = 512 * 1024;
    public static readonly TimeSpan DefaultMaxExecutionTime = TimeSpan.FromSeconds(20);
    public static readonly TimeSpan DefaultMaxRegexTime = TimeSpan.FromSeconds(2);

    public int MaxRequests { get; init; } = DefaultMaxRequests;
    public int MaxBytes { get; init; } = DefaultMaxBytes;
    public TimeSpan MaxExecutionTime { get; init; } = DefaultMaxExecutionTime;
    public TimeSpan MaxRegexTime { get; init; } = DefaultMaxRegexTime;
    public int MaxResultSize { get; init; } = DefaultMaxResultSize;

    public static SourceRuleExecutionLimits Default { get; } = new();

    public void Validate()
    {
        ValidateRange(MaxRequests, 0, 32, nameof(MaxRequests));
        ValidateRange(MaxBytes, 1, 16 * 1024 * 1024, nameof(MaxBytes));
        ValidateRange(MaxResultSize, 1, 16 * 1024 * 1024, nameof(MaxResultSize));
        ValidateDuration(MaxExecutionTime, TimeSpan.FromMilliseconds(1), TimeSpan.FromMinutes(5), nameof(MaxExecutionTime));
        ValidateDuration(MaxRegexTime, TimeSpan.FromMilliseconds(1), DefaultMaxRegexTime, nameof(MaxRegexTime));
    }

    private static void ValidateRange(int value, int minimum, int maximum, string name)
    {
        if (value < minimum || value > maximum)
        {
            throw new ArgumentOutOfRangeException(
                name,
                value,
                $"{name} must be between {minimum} and {maximum}.");
        }
    }

    private static void ValidateDuration(
        TimeSpan value,
        TimeSpan minimum,
        TimeSpan maximum,
        string name)
    {
        if (value < minimum || value > maximum)
        {
            throw new ArgumentOutOfRangeException(
                name,
                value,
                $"{name} must be between {minimum} and {maximum}.");
        }
    }
}
