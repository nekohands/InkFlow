using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using InkFlow.ReaderFixtures;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class ReaderOutputBaselineTests
{
    [TestMethod]
    public void Every_reader_page_and_resource_matches_the_pre_migration_output()
    {
        using var baseline = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "reader-output-baseline.json")));
        var pages = ReaderFixture.Create();
        Assert.AreEqual(baseline.RootElement.GetArrayLength(), pages.Count);
        foreach (var expected in baseline.RootElement.EnumerateArray())
        {
            var name = expected.GetProperty("name").GetString()!;
            var actual = pages[name];
            var bytes = Encoding.UTF8.GetBytes(actual.Body.Replace("\r\n", "\n", StringComparison.Ordinal));
            Assert.AreEqual(expected.GetProperty("Path").GetString(), actual.Path, name);
            Assert.AreEqual(expected.GetProperty("ContentType").GetString(), actual.ContentType, name);
            Assert.AreEqual(expected.GetProperty("Status").GetInt32(), actual.Status, name);
            Assert.AreEqual(expected.GetProperty("bytes").GetInt32(), bytes.Length, name);
            Assert.AreEqual(expected.GetProperty("sha256").GetString(), Convert.ToHexString(SHA256.HashData(bytes)), name);
        }
    }
}
