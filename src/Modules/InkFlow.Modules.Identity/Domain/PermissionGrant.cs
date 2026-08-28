namespace InkFlow.Modules.Identity.Domain;

/// <summary>
/// 面向单一资源的显式授权。撤销只写入时间，不删除历史记录。
/// </summary>
public sealed class PermissionGrant
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Permission { get; private set; } = null!;
    public string ResourceType { get; private set; } = null!;
    public string ResourceId { get; private set; } = null!;
    public Guid GrantedBy { get; private set; }
    public DateTimeOffset GrantedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    public bool IsActive => RevokedAt is null;

    private PermissionGrant() { }

    public static PermissionGrant Create(
        Guid userId,
        string permission,
        string resourceType,
        string resourceId,
        Guid grantedBy,
        DateTimeOffset grantedAt) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            UserId = RequireId(userId, nameof(userId)),
            Permission = Normalize(permission, nameof(permission), 128),
            ResourceType = Normalize(resourceType, nameof(resourceType), 64),
            ResourceId = Normalize(resourceId, nameof(resourceId), 256),
            GrantedBy = RequireId(grantedBy, nameof(grantedBy)),
            GrantedAt = grantedAt,
        };

    public static PermissionGrant Rehydrate(
        Guid id,
        Guid userId,
        string permission,
        string resourceType,
        string resourceId,
        Guid grantedBy,
        DateTimeOffset grantedAt,
        DateTimeOffset? revokedAt) =>
        new()
        {
            Id = id,
            UserId = userId,
            Permission = permission,
            ResourceType = resourceType,
            ResourceId = resourceId,
            GrantedBy = grantedBy,
            GrantedAt = grantedAt,
            RevokedAt = revokedAt,
        };

    public void Revoke(DateTimeOffset revokedAt) => RevokedAt ??= revokedAt;

    private static Guid RequireId(Guid value, string parameterName) =>
        value == Guid.Empty
            ? throw new ArgumentException("identifier must not be empty.", parameterName)
            : value;

    private static string Normalize(string value, string parameterName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("value must not be empty.", parameterName);
        }

        var normalized = value.Trim();
        if (normalized.Length > maxLength ||
            normalized.Any(character => char.IsWhiteSpace(character) || char.IsControl(character)))
        {
            throw new ArgumentException(
                $"value must be at most {maxLength} characters and contain no whitespace or control characters.",
                parameterName);
        }

        return normalized;
    }
}
