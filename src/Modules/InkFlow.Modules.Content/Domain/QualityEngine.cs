using System.Security.Cryptography;
using System.Text;

namespace InkFlow.Modules.Content.Domain;

public sealed record QualityEvidence(int ParagraphCount, int TotalCharacters, double AverageParagraphLength)
{
    public string Describe() =>
        $"paragraphs={ParagraphCount}, chars={TotalCharacters}, avgLength={AverageParagraphLength:F0}";
}

/// <summary>
/// 质量引擎 v1：基于可解释证据的启发式评分（0-100）。
/// 证据：段落数量、总字符数、平均段落长度。
/// 规则刻意简单且可解释——Phase 2 将引入跨源一致性等更强的信号。
/// </summary>
public static class QualityEngine
{
    private const int MinGoodParagraphs = 3;
    private const int MinGoodCharacters = 300;

    public static (int Score, QualityEvidence Evidence) Evaluate(ContentDocument document)
    {
        var paragraphCount = document.Paragraphs.Count;
        var totalChars = document.Paragraphs.Sum(p => p.Text.Length);
        var average = paragraphCount == 0 ? 0 : (double)totalChars / paragraphCount;

        var score = 0;

        // 段落数量:至少 1 段才有意义,3 段以上给满这部分分。
        score += paragraphCount switch
        {
            >= MinGoodParagraphs => 40,
            2 => 25,
            1 => 10,
            _ => 0,
        };

        // 总字符量:短正文可能是抓取失败,长正文更可信。
        score += totalChars switch
        {
            >= MinGoodCharacters => 40,
            >= 100 => 25,
            > 0 => 10,
            _ => 0,
        };

        // 平均段落长度:过短的平均值暗示碎片化/导航残留。
        score += average switch
        {
            >= 50 => 20,
            >= 15 => 10,
            _ => 0,
        };

        return (Math.Min(score, 100), new QualityEvidence(paragraphCount, totalChars, average));
    }

    /// <summary>CanonicalHash：规范化文本的 SHA-256,内容幂等的唯一依据。</summary>
    public static string ComputeCanonicalHash(ContentDocument document)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(document.CanonicalText));
        return Convert.ToHexString(bytes);
    }
}
