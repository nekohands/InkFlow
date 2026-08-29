namespace InkFlow.Modules.Developers.Infrastructure.Persistence;

public static class DevelopersSchema
{
    public const string Name = "developers";
}

public sealed class DeveloperApplicationEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = null!;
    public int Environment { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
}

public sealed class DeveloperApiKeyEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid ApplicationId { get; set; }
    public string Name { get; set; } = null!;
    public string Prefix { get; set; } = null!;
    public string SecretHash { get; set; } = null!;
    public string Scope { get; set; } = null!;
    public int Environment { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
}
