using System.Globalization;
using System.Text.RegularExpressions;

namespace InkFlow.Modules.Library;

public sealed record ChapterIdentity(string Title, long Sequence, int? Number = null);
public sealed record ChapterAlignmentResult(double Score, IReadOnlyList<MatchEvidence> Evidence)
{
    public bool AutoMap => Score >= 80;
}

public static partial class ChapterNumberParser
{
    public static int? Parse(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var arabic = ArabicChapterRegex().Match(title);
        if (arabic.Success && int.TryParse(arabic.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
        {
            return number;
        }

        var chinese = ChineseChapterRegex().Match(title);
        return chinese.Success ? ParseChineseNumber(chinese.Groups[1].Value) : null;
    }

    private static int? ParseChineseNumber(string value)
    {
        var digits = new Dictionary<char, int> { ['零']=0,['〇']=0,['一']=1,['二']=2,['两']=2,['三']=3,['四']=4,['五']=5,['六']=6,['七']=7,['八']=8,['九']=9 };
        var units = new Dictionary<char, int> { ['十']=10,['百']=100,['千']=1000,['万']=10000 };
        var total = 0;
        var section = 0;
        var digit = 0;
        foreach (var ch in value)
        {
            if (digits.TryGetValue(ch, out var d))
            {
                digit = d;
                continue;
            }

            if (!units.TryGetValue(ch, out var unit))
            {
                return null;
            }

            if (unit == 10000)
            {
                section = (section + digit) * unit;
                total += section;
                section = 0;
                digit = 0;
            }
            else
            {
                section += (digit == 0 ? 1 : digit) * unit;
                digit = 0;
            }
        }

        return total + section + digit;
    }

    [GeneratedRegex(@"(?:第\s*)?(\d{1,9})\s*(?:章|节|回|话)?", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex ArabicChapterRegex();

    [GeneratedRegex(@"第\s*([零〇一二两三四五六七八九十百千万]+)\s*(?:章|节|回|话)", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex ChineseChapterRegex();
}

public static class ChapterAlignmentEngine
{
    public const string AlgorithmVersion = "chapter-align-v1";

    public static ChapterAlignmentResult Evaluate(ChapterIdentity source, ChapterIdentity candidate)
    {
        var evidence = new List<MatchEvidence>();
        var sourceNumber = source.Number ?? ChapterNumberParser.Parse(source.Title);
        var candidateNumber = candidate.Number ?? ChapterNumberParser.Parse(candidate.Title);
        double score = 0;

        if (sourceNumber.HasValue && candidateNumber.HasValue)
        {
            if (sourceNumber == candidateNumber)
            {
                score += 55;
                evidence.Add(new("ChapterNumberExact", 55, $"Both chapters resolve to number {sourceNumber}."));
            }
            else
            {
                score -= 50;
                evidence.Add(new("ChapterNumberConflict", -50, $"Chapter numbers differ: {sourceNumber} vs {candidateNumber}."));
            }
        }

        if (TextIdentityNormalizer.Normalize(source.Title) == TextIdentityNormalizer.Normalize(candidate.Title))
        {
            score += 35;
            evidence.Add(new("ChapterTitleExact", 35, "Normalized chapter titles are identical."));
        }

        var delta = Math.Abs(source.Sequence - candidate.Sequence);
        if (delta == 0)
        {
            score += 10;
            evidence.Add(new("SequenceExact", 10, "Source sequences are identical."));
        }
        else if (delta <= 1)
        {
            score += 5;
            evidence.Add(new("SequenceAdjacent", 5, "Source sequences differ by at most one."));
        }

        return new(Math.Clamp(score, 0, 100), evidence);
    }
}
