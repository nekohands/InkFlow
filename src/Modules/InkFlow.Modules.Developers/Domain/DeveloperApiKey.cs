using System.Security.Cryptography;
using System.Text;

namespace InkFlow.Modules.Developers.Domain;

public static class DeveloperApiScopes
{
    public const string CatalogRead = "catalog.read";
}

/// <summary>绑定到 DeveloperApplication 的可撤销 opaque API Key。</summary>
public sealed class DeveloperApiKey
{
    public const int MaxPrefixLength = 32;
    public const int MaxHashLength = 128;
    public const int MaxNameLength = 128;
    public const int MaxScopeLength = 128;

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid ApplicationId { get; private set; }
    public string Name { get; private set; } = null!;
    public string Prefix { get; private set; } = null!;
    public string SecretHash { get; private set; } = null!;
    public string Scope { get; private set; } = null!;
    public DeveloperEnvironment Environment { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? LastUsedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    private DeveloperApiKey() { }

    public static DeveloperApiKey Create(
        Guid userId,
        Guid applicationId,
        string name,
        string prefix,
        string secretHash,
        string scope,
        DeveloperEnvironment environment,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("userId must not be empty.", nameof(userId));
        }

        if (applicationId == Guid.Empty)
        {
            throw new ArgumentException("applicationId must not be empty.", nameof(applicationId));
        }

        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > MaxNameLength ||
            name.Any(char.IsControl))
        {
            throw new ArgumentException("name is invalid.", nameof(name));
        }

        if (!Enum.IsDefined(environment))
        {
            throw new ArgumentOutOfRangeException(nameof(environment));
        }

        if (string.IsNullOrWhiteSpace(prefix) || prefix.Length > MaxPrefixLength ||
            prefix.Any(char.IsControl))
        {
            throw new ArgumentException("prefix is invalid.", nameof(prefix));
        }

        if (string.IsNullOrWhiteSpace(secretHash) || secretHash.Length > MaxHashLength ||
            secretHash.Any(char.IsWhiteSpace) || secretHash.Any(char.IsControl))
        {
            throw new ArgumentException("secretHash is invalid.", nameof(secretHash));
        }

        if (!string.Equals(scope, DeveloperApiScopes.CatalogRead, StringComparison.Ordinal))
        {
            throw new ArgumentException("unsupported API key scope.", nameof(scope));
        }

        if (expiresAt <= createdAt)
        {
            throw new ArgumentException("expiresAt must be after createdAt.", nameof(expiresAt));
        }

        return new DeveloperApiKey
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            ApplicationId = applicationId,
            Name = name.Trim(),
            Prefix = prefix,
            SecretHash = secretHash,
            Scope = scope,
            Environment = environment,
            CreatedAt = createdAt,
            ExpiresAt = expiresAt,
        };
    }

    public static DeveloperApiKey Rehydrate(
        Guid id,
        Guid userId,
        Guid applicationId,
        string name,
        string prefix,
        string secretHash,
        string scope,
        DeveloperEnvironment environment,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt,
        DateTimeOffset? lastUsedAt,
        DateTimeOffset? revokedAt) => new()
        {
            Id = id,
            UserId = userId,
            ApplicationId = applicationId,
            Name = name,
            Prefix = prefix,
            SecretHash = secretHash,
            Scope = scope,
            Environment = environment,
            CreatedAt = createdAt,
            ExpiresAt = expiresAt,
            LastUsedAt = lastUsedAt,
            RevokedAt = revokedAt,
        };

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;

    public void Revoke(DateTimeOffset now)
    {
        RevokedAt ??= now;
    }

    public void MarkUsed(DateTimeOffset now)
    {
        if (LastUsedAt is null || LastUsedAt < now)
        {
            LastUsedAt = now;
        }
    }

    public static string HashSecret(string rawSecret) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawSecret)));
}
