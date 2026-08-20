namespace InkFlow.Modules.Content;

public sealed record QualityEvidence(string Code, double Delta, string Detail);
public sealed record ContentQualityResult(double Score, IReadOnlyList<QualityEvidence> Evidence);
public sealed record ContentCandidate(Guid VersionId, double QualityScore, DateTimeOffset CreatedAtUtc);

public static class ContentQualityEngine
{
    public const string AlgorithmVersion = "content-quality-v1";
    private static readonly string[] SuspiciousTokens = ["最新网址", "请收藏", "手机用户请浏览", "本章未完", "广告内容"];

    public static ContentQualityResult Evaluate(ContentDocument document, double sourceHealthScore)
    {
        var evidence = new List<QualityEvidence>();
        var text = document.ToCanonicalText();
        double score = 0;

        var lengthScore = Math.Clamp(text.Length / 40.0, 0, 35);
        score += lengthScore;
        evidence.Add(new("Length", lengthScore, $"Canonical content length is {text.Length} characters."));

        var paragraphs = document.Blocks.OfType<ParagraphBlock>().Count();
        var structureScore = Math.Clamp(paragraphs * 2.0, 0, 20);
        score += structureScore;
        evidence.Add(new("Structure", structureScore, $"Content contains {paragraphs} paragraph blocks."));

        var suspicious = SuspiciousTokens.Count(token => text.Contains(token, StringComparison.OrdinalIgnoreCase));
        var cleanlinessScore = Math.Max(0, 25 - suspicious * 8);
        score += cleanlinessScore;
        evidence.Add(new("Cleanliness", cleanlinessScore, suspicious == 0 ? "No known junk markers detected." : $"Detected {suspicious} junk markers."));

        var reliability = Math.Clamp(sourceHealthScore, 0, 100) * 0.20;
        score += reliability;
        evidence.Add(new("SourceReliability", reliability, $"Source health score is {sourceHealthScore:0.##}."));

        if (text.Length < 120)
        {
            score -= 25;
            evidence.Add(new("SuspiciousTruncation", -25, "Content is unusually short and may be truncated."));
        }

        return new(Math.Clamp(score, 0, 100), evidence);
    }
}

public static class ContentSelectionEngine
{
    public static ContentCandidate? Select(IReadOnlyCollection<ContentCandidate> candidates, Guid? lockedVersionId = null)
    {
        if (lockedVersionId.HasValue)
        {
            return candidates.SingleOrDefault(candidate => candidate.VersionId == lockedVersionId.Value);
        }

        return candidates
            .OrderByDescending(candidate => candidate.QualityScore)
            .ThenByDescending(candidate => candidate.CreatedAtUtc)
            .FirstOrDefault();
    }
}
