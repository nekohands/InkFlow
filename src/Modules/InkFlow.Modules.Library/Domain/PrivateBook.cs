namespace InkFlow.Modules.Library.Domain;

/// <summary>
/// 用户私有书目聚合。它与 CanonicalBook 使用不同的身份和生命周期，不参与公共书库匹配。
/// </summary>
public sealed class PrivateBook
{
    public const int MaxTitleLength = 512;
    public const int MaxAuthorLength = 256;

    public Guid UserId { get; private set; }
    public Guid Id { get; private set; }
    public string Title { get; private set; } = null!;
    public string? Author { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private PrivateBook() { }

    public static PrivateBook Create(
        Guid userId,
        string title,
        string? author,
        DateTimeOffset now)
    {
        ValidateUserId(userId);
        var normalizedTitle = NormalizeRequired(title, MaxTitleLength, nameof(title));
        var normalizedAuthor = NormalizeOptional(author, MaxAuthorLength, nameof(author));

        return new PrivateBook
        {
            UserId = userId,
            Id = Guid.CreateVersion7(),
            Title = normalizedTitle,
            Author = normalizedAuthor,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    public static PrivateBook Rehydrate(
        Guid userId,
        Guid id,
        string title,
        string? author,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        ValidateUserId(userId);
        if (id == Guid.Empty)
        {
            throw new ArgumentException("private book id must not be empty.", nameof(id));
        }

        return new PrivateBook
        {
            UserId = userId,
            Id = id,
            Title = NormalizeRequired(title, MaxTitleLength, nameof(title)),
            Author = NormalizeOptional(author, MaxAuthorLength, nameof(author)),
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
        };
    }

    public void UpdateMetadata(string title, string? author, DateTimeOffset now)
    {
        var normalizedTitle = NormalizeRequired(title, MaxTitleLength, nameof(title));
        var normalizedAuthor = NormalizeOptional(author, MaxAuthorLength, nameof(author));
        Title = normalizedTitle;
        Author = normalizedAuthor;
        UpdatedAt = now;
    }

    private static void ValidateUserId(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("private book owner must not be empty.", nameof(userId));
        }
    }

    private static string NormalizeRequired(string value, int maxLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("value must not be empty.", parameterName);
        }

        var normalized = value.Trim();
        if (normalized.Length > maxLength || normalized.Any(char.IsControl))
        {
            throw new ArgumentException(
                $"value must be at most {maxLength} characters and contain no control characters.",
                parameterName);
        }

        return normalized;
    }

    private static string? NormalizeOptional(string? value, int maxLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return NormalizeRequired(value, maxLength, parameterName);
    }
}
