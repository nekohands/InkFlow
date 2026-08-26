using InkFlow.Modules.Sources.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class FetchArtifactTests
{
    [TestMethod]
    public void RawHash_Is_Deterministic_For_Same_Body()
    {
        var a = FetchArtifact.Capture("src", "book-1", "ch-1", "<p>正文</p>", T0);
        var b = FetchArtifact.Capture("src", "book-1", "ch-1", "<p>正文</p>", T0.AddHours(1));

        Assert.AreEqual(a.RawHash, b.RawHash);
        Assert.AreNotEqual(Guid.Empty, a.Id);
    }

    [TestMethod]
    public void RawHash_Differs_For_Different_Bodies()
    {
        var a = FetchArtifact.Capture("src", "book-1", "ch-1", "<p>正文A</p>", T0);
        var b = FetchArtifact.Capture("src", "book-1", "ch-1", "<p>正文B</p>", T0);

        Assert.AreNotEqual(a.RawHash, b.RawHash);
    }

    [TestMethod]
    public void Capture_Records_Body_Length()
    {
        var body = "<p>正文内容</p>";
        var artifact = FetchArtifact.Capture("src", "book-1", "ch-1", body, T0);

        Assert.AreEqual(body.Length, artifact.BodyLength);
        Assert.AreEqual(64, artifact.RawHash.Length, "SHA-256 十六进制应为 64 字符");
    }

    private static readonly DateTimeOffset T0 = new(2026, 8, 26, 14, 0, 0, TimeSpan.Zero);
}
