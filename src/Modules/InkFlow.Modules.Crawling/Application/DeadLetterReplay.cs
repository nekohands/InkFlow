using InkFlow.Modules.Crawling.Domain;

namespace InkFlow.Modules.Crawling.Application;

/// <summary>
/// 受控死信重放命令。操作者和理由是修复轨迹的一部分，理由不得携带凭据、Cookie 或正文。
/// </summary>
public sealed record DeadLetterReplayCommand
{
    public Guid DeadLetterId { get; }
    public string RequestedBy { get; }
    public string ReplayReason { get; }

    private DeadLetterReplayCommand(
        Guid deadLetterId,
        string requestedBy,
        string replayReason)
    {
        DeadLetterId = deadLetterId;
        RequestedBy = requestedBy;
        ReplayReason = replayReason;
    }

    public static DeadLetterReplayCommand Create(
        Guid deadLetterId,
        string requestedBy,
        string replayReason)
    {
        if (deadLetterId == Guid.Empty)
        {
            throw new ArgumentException("dead letter id must not be empty.", nameof(deadLetterId));
        }

        return new DeadLetterReplayCommand(
            deadLetterId,
            NormalizeRequired(requestedBy, nameof(requestedBy), 128),
            NormalizeRequired(replayReason, nameof(replayReason), 512));
    }

    private static string NormalizeRequired(string value, string parameterName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("replay field must not be empty.", parameterName);
        }

        var normalized = value.Trim().Replace('\r', ' ').Replace('\n', ' ');
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }
}

public enum DeadLetterReplayStatus
{
    Replayed,
    AlreadyReplayed,
    NotFound,
    OriginalTaskMissing,
    OriginalTaskNotDeadLettered,
}

public sealed record DeadLetterReplayResult(
    DeadLetterReplayStatus Status,
    Guid? ReplayTaskId = null)
{
    public bool IsSuccess => Status is
        DeadLetterReplayStatus.Replayed or DeadLetterReplayStatus.AlreadyReplayed;
}

/// <summary>
/// 死信修复的外部 seam。实现必须保持原死信事实，并以原子方式创建唯一重放任务。
/// </summary>
public interface ICrawlerTaskRepairRepository
{
    Task<DeadLetterReplayResult> ReplayDeadLetterAsync(
        DeadLetterReplayCommand command,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);
}
