namespace InkFlow.Modules.Identity.Domain;

/// <summary>短期访问令牌事实；原始 opaque token 只在签发响应中出现一次。</summary>
public sealed class AccessToken
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid SessionId { get; private set; }
    public string TokenHash { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    private AccessToken() { }

    public static AccessToken Create(
        Guid userId,
        Guid sessionId,
        string tokenHash,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            UserId = ValidateId(userId, nameof(userId)),
            SessionId = ValidateId(sessionId, nameof(sessionId)),
            TokenHash = ValidateHash(tokenHash),
            CreatedAt = createdAt,
            ExpiresAt = expiresAt > createdAt
                ? expiresAt
                : throw new ArgumentException("access token expiry must be after creation time.", nameof(expiresAt)),
        };

    public static AccessToken Rehydrate(
        Guid id,
        Guid userId,
        Guid sessionId,
        string tokenHash,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt,
        DateTimeOffset? revokedAt) =>
        new()
        {
            Id = id,
            UserId = userId,
            SessionId = sessionId,
            TokenHash = tokenHash,
            CreatedAt = createdAt,
            ExpiresAt = expiresAt,
            RevokedAt = revokedAt,
        };

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;

    public void Revoke(DateTimeOffset now) => RevokedAt ??= now;

    private static Guid ValidateId(Guid value, string name) =>
        value == Guid.Empty
            ? throw new ArgumentException("identity id must not be empty.", name)
            : value;

    private static string ValidateHash(string value) =>
        string.IsNullOrWhiteSpace(value) || value.Length > 128
            ? throw new ArgumentException("token hash must be non-empty and at most 128 characters.", nameof(value))
            : value;
}
