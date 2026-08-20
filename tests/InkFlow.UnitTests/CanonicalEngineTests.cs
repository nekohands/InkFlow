using InkFlow.Modules.Content;
using InkFlow.Modules.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class CanonicalEngineTests
{
    [TestMethod]
    public void Book_match_auto_links_same_normalized_title_and_author()
    {
        var result = BookMatchEngine.Evaluate("斗破 苍穹", "天蚕土豆", "斗破苍穹", "天蚕土豆 著");
        Assert.IsTrue(result.AutoMatch);
        Assert.IsTrue(result.Evidence.Any(item => item.Code == "TitleExact"));
    }

    [TestMethod]
    public void Book_match_rejects_conflicting_author()
    {
        var result = BookMatchEngine.Evaluate("同名小说", "作者甲", "同名小说", "作者乙");
        Assert.IsFalse(result.AutoMatch);
        Assert.IsTrue(result.Evidence.Any(item => item.Code == "AuthorConflict"));
    }

    [TestMethod]
    public void Chapter_number_parser_handles_arabic_and_chinese_numbers()
    {
        Assert.AreEqual(102, ChapterNumberParser.Parse("第102章 风起"));
        Assert.AreEqual(102, ChapterNumberParser.Parse("第一百零二章 风起"));
    }

    [TestMethod]
    public void Chapter_alignment_auto_maps_matching_number_title_and_sequence()
    {
        var result = ChapterAlignmentEngine.Evaluate(new("第十章 风起", 10), new("第十章 风起", 10));
        Assert.IsTrue(result.AutoMap);
    }

    [TestMethod]
    public void Content_normalization_produces_stable_hash_for_whitespace_only_changes()
    {
        var left = ContentNormalizer.FromPlainText("第一段   内容\r\n\r\n第二段");
        var right = ContentNormalizer.FromPlainText("第一段 内容\n第二段");
        Assert.AreEqual(left.CanonicalHash, right.CanonicalHash);
    }

    [TestMethod]
    public void Quality_engine_penalizes_truncated_content()
    {
        var shortContent = ContentNormalizer.FromPlainText("太短了");
        var longContent = ContentNormalizer.FromPlainText(string.Join('\n', Enumerable.Repeat("这是一段完整的小说正文内容，用于质量评分。", 50)));
        var shortScore = ContentQualityEngine.Evaluate(shortContent.Document, 90).Score;
        var longScore = ContentQualityEngine.Evaluate(longContent.Document, 90).Score;
        Assert.IsGreaterThan(shortScore, longScore);
    }

    [TestMethod]
    public void Selection_uses_highest_quality_unless_locked()
    {
        var low = new ContentCandidate(Guid.CreateVersion7(), 50, DateTimeOffset.UtcNow);
        var high = new ContentCandidate(Guid.CreateVersion7(), 90, DateTimeOffset.UtcNow.AddSeconds(1));
        Assert.AreEqual(high.VersionId, ContentSelectionEngine.Select([low, high])?.VersionId);
        Assert.AreEqual(low.VersionId, ContentSelectionEngine.Select([low, high], low.VersionId)?.VersionId);
    }
}
