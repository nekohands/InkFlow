namespace InkFlow.Modules.Library;

public sealed record MatchEvidence(string Code, double Delta, string Detail);
public sealed record BookMatchResult(double Score, IReadOnlyList<MatchEvidence> Evidence)
{
    public bool AutoMatch => Score >= 85;
    public bool RequiresReview => Score >= 60 && Score < 85;
}

public static class BookMatchEngine
{
    public const string AlgorithmVersion = "book-match-v1";

    public static BookMatchResult Evaluate(string sourceTitle, string? sourceAuthor, string candidateTitle, string? candidateAuthor)
    {
        var evidence = new List<MatchEvidence>();
        var title = TextIdentityNormalizer.Normalize(sourceTitle);
        var otherTitle = TextIdentityNormalizer.Normalize(candidateTitle);
        var author = NormalizeAuthor(sourceAuthor);
        var otherAuthor = NormalizeAuthor(candidateAuthor);
        double score = 0;

        if (title.Length > 0 && title == otherTitle)
        {
            score += 60;
            evidence.Add(new("TitleExact", 60, "Normalized titles are identical."));
        }
        else if (title.Length >= 4 && otherTitle.Length >= 4 && (title.Contains(otherTitle, StringComparison.Ordinal) || otherTitle.Contains(title, StringComparison.Ordinal)))
        {
            score += 30;
            evidence.Add(new("TitleContains", 30, "One normalized title contains the other."));
        }
        else
        {
            evidence.Add(new("TitleMismatch", 0, "Normalized titles do not agree."));
        }

        if (author.Length == 0 || otherAuthor.Length == 0)
        {
            score += 5;
            evidence.Add(new("AuthorMissing", 5, "Author evidence is incomplete."));
        }
        else if (author == otherAuthor)
        {
            score += 35;
            evidence.Add(new("AuthorExact", 35, "Normalized authors are identical."));
        }
        else
        {
            score -= 50;
            evidence.Add(new("AuthorConflict", -50, "Both sources have authors and they conflict."));
        }

        return new(Math.Clamp(score, 0, 100), evidence);
    }

    private static string NormalizeAuthor(string? value)
    {
        var normalized = TextIdentityNormalizer.Normalize(value);
        if (normalized.StartsWith("作者", StringComparison.Ordinal))
        {
            normalized = normalized[2..];
        }
        if (normalized.EndsWith("著", StringComparison.Ordinal))
        {
            normalized = normalized[..^1];
        }
        return normalized;
    }
}
