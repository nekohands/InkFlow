namespace InkFlow.Modules.Identity.Domain;

/// <summary>
/// Refresh 会话事实。原始 refresh token 永不落库；只有不可逆摘要进入数据库。
/// </summary>
public sealed class RefreshSession
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string RefreshTokenHash { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public Guid? ReplacedBySessionId { get; private set; }

    private RefreshSession() { }

    public static RefreshSession Create(
        Guid userId,
        string refreshTokenHash,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            UserId = ValidateUserId(userId),
            RefreshTokenHash = ValidateTokenHash(refreshTokenHash),
            CreatedAt = createdAt,
            ExpiresAt = ValidateExpiry(createdAt, expiresAt),
        };

    public static RefreshSession Rehydrate(
        Guid id,
        Guid userId,
        string refreshTokenHash,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt,
        DateTimeOffset? revokedAt,
        Guid? replacedBySessionId) =>
        new()
        {
            Id = id,
            UserId = userId,
            RefreshTokenHash = refreshTokenHash,
            CreatedAt = createdAt,
            ExpiresAt = expiresAt,
            RevokedAt = revokedAt,
            ReplacedBySessionId = replacedBySessionId,
        };

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;

    public void ReplaceWith(Guid replacementSessionId, DateTimeOffset now)
    {
        if (replacementSessionId == Guid.Empty)
        {
            throw new ArgumentException("replacement session id must not be empty.", nameof(replacementSessionId));
        }

        RevokedAt ??= now;
        ReplacedBySessionId ??= replacementSessionId;
    }

    public void Revoke(DateTimeOffset now) => RevokedAt ??= now;

    private static Guid ValidateUserId(Guid userId) =>
        userId == Guid.Empty
            ? throw new ArgumentException("user id must not be empty.", nameof(userId))
            : userId;

    private static string ValidateTokenHash(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 128)
        {
            throw new ArgumentException("token hash must be non-empty and at most 128 characters.", nameof(value));
        }

        return value;
    }

    private static DateTimeOffset ValidateExpiry(DateTimeOffset createdAt, DateTimeOffset expiresAt) =>
        expiresAt <= createdAt
            ? throw new ArgumentException("session expiry must be after creation time.", nameof(expiresAt))
            : expiresAt;
}
