namespace InkFlow.Modules.Identity.Infrastructure.Persistence;

public static class IdentitySchema
{
    public const string Name = "identity";
}

public sealed class UserEntity
{
    public Guid Id { get; set; }
    public string Email { get; set; } = null!;
    public string NormalizedEmail { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public int Role { get; set; }
    public int Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class RefreshSessionEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string RefreshTokenHash { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public Guid? ReplacedBySessionId { get; set; }
}

public sealed class AccessTokenEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid SessionId { get; set; }
    public string TokenHash { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
}
