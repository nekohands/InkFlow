namespace InkFlow.Modules.Identity.Domain;

public enum UserRole
{
    Reader = 1,
    Operator = 2,
    Administrator = 3,
}

public enum UserStatus
{
    Active = 1,
    Suspended = 2,
    Disabled = 3,
}

/// <summary>用户聚合。密码只以经过专用哈希器处理的值进入聚合，不接收明文密码。</summary>
public sealed class User
{
    public Guid Id { get; private set; }
    public string Email { get; private set; } = null!;
    public string NormalizedEmail { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public UserRole Role { get; private set; }
    public UserStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private User() { }

    public static User Create(string email, string passwordHash, DateTimeOffset now)
    {
        var normalizedEmail = UserEmailAddress.Normalize(email);
        ValidatePasswordHash(passwordHash);

        return new User
        {
            Id = Guid.CreateVersion7(),
            Email = normalizedEmail,
            NormalizedEmail = normalizedEmail,
            PasswordHash = passwordHash,
            Role = UserRole.Reader,
            Status = UserStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    public static User Rehydrate(
        Guid id,
        string email,
        string normalizedEmail,
        string passwordHash,
        UserRole role,
        UserStatus status,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt) =>
        new()
        {
            Id = id,
            Email = email,
            NormalizedEmail = normalizedEmail,
            PasswordHash = passwordHash,
            Role = role,
            Status = status,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
        };

    public bool CanAuthenticate => Status == UserStatus.Active;

    public void ChangePasswordHash(string passwordHash, DateTimeOffset now)
    {
        ValidatePasswordHash(passwordHash);
        PasswordHash = passwordHash;
        UpdatedAt = now;
    }

    public void Suspend(DateTimeOffset now)
    {
        Status = UserStatus.Suspended;
        UpdatedAt = now;
    }

    public void Disable(DateTimeOffset now)
    {
        Status = UserStatus.Disabled;
        UpdatedAt = now;
    }

    public void Activate(DateTimeOffset now)
    {
        Status = UserStatus.Active;
        UpdatedAt = now;
    }

    private static void ValidatePasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash) || passwordHash.Length > 1024)
        {
            throw new ArgumentException(
                "password hash must be non-empty and at most 1024 characters.",
                nameof(passwordHash));
        }
    }
}

public static class UserEmailAddress
{
    public static string Normalize(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("email must not be empty.", nameof(email));
        }

        var normalized = email.Trim().ToLowerInvariant();
        var at = normalized.IndexOf('@');
        if (normalized.Length > 256 ||
            normalized.Any(char.IsWhiteSpace) ||
            at <= 0 ||
            at == normalized.Length - 1 ||
            at != normalized.LastIndexOf('@'))
        {
            throw new ArgumentException("email must be a valid single-address identifier.", nameof(email));
        }

        return normalized;
    }
}
