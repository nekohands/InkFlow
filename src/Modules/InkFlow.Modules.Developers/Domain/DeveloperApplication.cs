namespace InkFlow.Modules.Developers.Domain;

public enum DeveloperEnvironment
{
    Production = 1,
}

/// <summary>用户拥有的外部集成注册，不等同于用户或 API Key。</summary>
public sealed class DeveloperApplication
{
    public const int MaxNameLength = 128;

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Name { get; private set; } = null!;
    public DeveloperEnvironment Environment { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    private DeveloperApplication() { }

    public static DeveloperApplication Create(
        Guid userId,
        string name,
        DateTimeOffset now,
        DeveloperEnvironment environment = DeveloperEnvironment.Production)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("userId must not be empty.", nameof(userId));
        }

        if (!Enum.IsDefined(environment))
        {
            throw new ArgumentOutOfRangeException(nameof(environment));
        }

        var normalizedName = NormalizeName(name);
        return new DeveloperApplication
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Name = normalizedName,
            Environment = environment,
            CreatedAt = now,
        };
    }

    public static DeveloperApplication Rehydrate(
        Guid id,
        Guid userId,
        string name,
        DeveloperEnvironment environment,
        DateTimeOffset createdAt,
        DateTimeOffset? revokedAt) => new()
        {
            Id = id,
            UserId = userId,
            Name = name,
            Environment = environment,
            CreatedAt = createdAt,
            RevokedAt = revokedAt,
        };

    public bool IsActive => RevokedAt is null;

    public void Revoke(DateTimeOffset now)
    {
        RevokedAt ??= now;
    }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("application name must not be empty.", nameof(name));
        }

        var normalized = name.Trim();
        if (normalized.Length > MaxNameLength || normalized.Any(char.IsControl))
        {
            throw new ArgumentOutOfRangeException(
                nameof(name), $"application name must be at most {MaxNameLength} characters.");
        }

        return normalized;
    }
}
