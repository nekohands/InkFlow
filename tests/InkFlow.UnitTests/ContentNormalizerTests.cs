using InkFlow.Modules.Content.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class ContentNormalizerTests
{
    [TestMethod]
    public void Strips_Tags_And_Splits_Paragraphs()
    {
        var raw = "<div><p>第一段正文</p></div>\n\n<p>第二段正文</p>";

        var doc = ContentNormalizer.Normalize(raw);

        Assert.AreEqual(2, doc.Paragraphs.Count);
        Assert.AreEqual("第一段正文", doc.Paragraphs[0].Text);
        Assert.AreEqual(0, doc.Paragraphs[0].Position);
        Assert.AreEqual(1, doc.Paragraphs[1].Position);
        StringAssert.Contains(doc.CanonicalText, "第二段正文");
    }

    [TestMethod]
    public void Decodes_Common_Html_Entities()
    {
        var doc = ContentNormalizer.Normalize("<p>A &amp; B &nbsp; C</p>");

        Assert.AreEqual(1, doc.Paragraphs.Count);
        StringAssert.Contains(doc.Paragraphs[0].Text, "A & B");
    }

    [TestMethod]
    public void Empty_Input_Yields_Empty_Document()
    {
        Assert.AreEqual(0, ContentNormalizer.Normalize("").Paragraphs.Count);
        Assert.AreEqual(0, ContentNormalizer.Normalize("   \n\n  ").Paragraphs.Count);
    }

    [TestMethod]
    public void CanonicalHash_Is_Stable_For_Equivalent_Markup()
    {
        // 同一正文,不同的标签包裹与空白,规范化后哈希必须一致。
        var docA = ContentNormalizer.Normalize("<p>第一段</p><p>第二段</p>");
        var docB = ContentNormalizer.Normalize("<div>\n  第一段\n\n  第二段\n</div>");

        Assert.AreEqual(
            QualityEngine.ComputeCanonicalHash(docA),
            QualityEngine.ComputeCanonicalHash(docB));
    }
}

[TestClass]
public sealed class QualityEngineTests
{
    private static ContentDocument DocumentWith(int paragraphs, int charsPerParagraph) =>
        new(Enumerable.Range(0, paragraphs)
            .Select(i => new ContentParagraph(i, new string('字', charsPerParagraph)))
            .ToList());

    [TestMethod]
    public void Empty_Document_Scores_Zero()
    {
        var (score, evidence) = QualityEngine.Evaluate(ContentDocument.Empty);
        Assert.AreEqual(0, score);
        Assert.AreEqual(0, evidence.ParagraphCount);
    }

    [TestMethod]
    public void Rich_Content_Scores_Higher_Than_Thin_Content()
    {
        var rich = QualityEngine.Evaluate(DocumentWith(paragraphs: 5, charsPerParagraph: 100));
        var thin = QualityEngine.Evaluate(DocumentWith(paragraphs: 1, charsPerParagraph: 10));

        Assert.IsTrue(rich.Score > thin.Score,
            $"rich={rich.Score} ({rich.Evidence.Describe()}) should beat thin={thin.Score}");
        Assert.IsTrue(rich.Score is > 0 and <= 100);
    }

    [TestMethod]
    public void Score_Never_Exceeds_One_Hundred()
    {
        var huge = QualityEngine.Evaluate(DocumentWith(paragraphs: 50, charsPerParagraph: 500));
        Assert.IsTrue(huge.Score <= 100);
    }
}
