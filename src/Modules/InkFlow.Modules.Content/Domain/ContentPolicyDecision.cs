namespace InkFlow.Modules.Content.Domain;

public enum ContentPolicyAction
{
    Takedown = 1,
    Restore = 2,
}

/// <summary>
/// 内容公开策略的不可变决策。策略状态由同一正典书的最新决策派生，历史只追加不覆盖。
/// </summary>
public sealed class ContentPolicyDecision
{
    public const int MaxActorIdLength = 256;
    public const int MaxReasonLength = 512;

    public Guid Id { get; private set; }
    public Guid CanonicalBookId { get; private set; }
    public ContentPolicyAction Action { get; private set; }
    public string ActorId { get; private set; } = null!;
    public string Reason { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }

    private ContentPolicyDecision() { }

    public static ContentPolicyDecision Create(
        Guid canonicalBookId,
        ContentPolicyAction action,
        string actorId,
        string reason,
        DateTimeOffset createdAt,
        Guid? id = null)
    {
        if (canonicalBookId == Guid.Empty)
        {
            throw new ArgumentException("canonicalBookId must not be empty.", nameof(canonicalBookId));
        }

        if (!Enum.IsDefined(action))
        {
            throw new ArgumentOutOfRangeException(nameof(action), "unsupported content policy action.");
        }

        return new ContentPolicyDecision
        {
            Id = id ?? Guid.CreateVersion7(),
            CanonicalBookId = canonicalBookId,
            Action = action,
            ActorId = NormalizeRequired(actorId, MaxActorIdLength, nameof(actorId)),
            Reason = NormalizeRequired(reason, MaxReasonLength, nameof(reason)),
            CreatedAt = createdAt,
        };
    }

    /// <summary>从持久化历史重建；不会改变决策内容。</summary>
    public static ContentPolicyDecision Rehydrate(
        Guid id,
        Guid canonicalBookId,
        ContentPolicyAction action,
        string actorId,
        string reason,
        DateTimeOffset createdAt) =>
        Create(canonicalBookId, action, actorId, reason, createdAt, id);

    private static string NormalizeRequired(string value, int maxLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("value must not be empty.", parameterName);
        }

        var normalized = value.Trim().Replace('\r', ' ').Replace('\n', ' ');
        if (normalized.Length == 0)
        {
            throw new ArgumentException("value must not be empty.", parameterName);
        }

        if (normalized.Length > maxLength)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                $"value must be at most {maxLength} characters.");
        }

        return normalized;
    }
}
