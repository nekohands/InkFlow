namespace InkFlow.Modules.Identity.Domain;

/// <summary>Legado 个人令牌目前只授予读取已发布内容的能力，后续能力通过新增 scope 扩展。</summary>
[Flags]
public enum LegadoTokenScope
{
    None = 0,
    Read = 1,
}

/// <summary>
/// 独立于 Web Access/Refresh Token 的 Legado 个人令牌事实。
/// 原始令牌只在签发响应中出现一次，数据库只保存前缀和不可逆摘要。
/// </summary>
public sealed class LegadoAccessToken
{
    public const string TokenPrefix = "lf_lgd_";
    public const int MaxNameLength = 64;
    public const int MaxPrefixLength = 32;
    public const int MaxHashLength = 128;

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Name { get; private set; } = null!;
    public string Prefix { get; private set; } = null!;
    public string TokenHash { get; private set; } = null!;
    public LegadoTokenScope Scope { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    private LegadoAccessToken() { }

    public static LegadoAccessToken Create(
        Guid userId,
        string name,
        string prefix,
        string tokenHash,
        LegadoTokenScope scope,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt,
        Guid? id = null)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("user id must not be empty.", nameof(userId));
        }

        if (expiresAt <= createdAt)
        {
            throw new ArgumentException("Legado token expiry must be after creation time.", nameof(expiresAt));
        }

        if (!Enum.IsDefined(scope) || scope == LegadoTokenScope.None)
        {
            throw new ArgumentOutOfRangeException(nameof(scope), "unsupported Legado token scope.");
        }

        return new LegadoAccessToken
        {
            Id = id ?? Guid.CreateVersion7(),
            UserId = userId,
            Name = NormalizeRequired(name, MaxNameLength, nameof(name)),
            Prefix = NormalizeRequired(prefix, MaxPrefixLength, nameof(prefix)),
            TokenHash = NormalizeRequired(tokenHash, MaxHashLength, nameof(tokenHash)),
            Scope = scope,
            CreatedAt = createdAt,
            ExpiresAt = expiresAt,
        };
    }

    public static LegadoAccessToken Rehydrate(
        Guid id,
        Guid userId,
        string name,
        string prefix,
        string tokenHash,
        LegadoTokenScope scope,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt,
        DateTimeOffset? revokedAt) =>
        new()
        {
            Id = id,
            UserId = userId,
            Name = name,
            Prefix = prefix,
            TokenHash = tokenHash,
            Scope = scope,
            CreatedAt = createdAt,
            ExpiresAt = expiresAt,
            RevokedAt = revokedAt,
        };

    public bool IsActive(DateTimeOffset now) =>
        RevokedAt is null && ExpiresAt > now;

    public bool HasScope(LegadoTokenScope requiredScope) =>
        requiredScope != LegadoTokenScope.None && (Scope & requiredScope) == requiredScope;

    public void Revoke(DateTimeOffset now) => RevokedAt ??= now;

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
