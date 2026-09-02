using System.Text.Json;
using InkFlow.Api;
using InkFlow.Modules.Content.Application;
using InkFlow.Modules.Content.Domain;

namespace InkFlow.ContractTests;

[TestClass]
public sealed class BookPackageContractTests
{
    [TestMethod]
    public void Package_View_Uses_Stable_Web_Json_And_Excludes_Lease_Details()
    {
        var value = new BookPackageView(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            BookPackageFormat.Epub,
            BookPackageJobStatus.Completed,
            TotalChapterCount: 12,
            CompletedChapterCount: 12,
            ProgressPercent: 100,
            ArtifactFileName: "package.epub",
            ArtifactSha256: "abc123",
            ArtifactLength: 456,
            FailureReason: null,
            CreatedAt: DateTimeOffset.Parse("2026-09-03T00:00:00Z"),
            UpdatedAt: DateTimeOffset.Parse("2026-09-03T00:01:00Z"),
            ExpiresAt: DateTimeOffset.Parse("2026-09-10T00:01:00Z"));

        var json = JsonSerializer.Serialize(
            BookPackageEndpoints.ToResponse(value),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.AreEqual(value.Id.ToString(), root.GetProperty("id").GetString());
        Assert.AreEqual("epub", root.GetProperty("format").GetString());
        Assert.AreEqual("completed", root.GetProperty("status").GetString());
        Assert.AreEqual(100, root.GetProperty("progressPercent").GetInt32());
        Assert.AreEqual("package.epub", root.GetProperty("artifactFileName").GetString());
        Assert.AreEqual(456, root.GetProperty("artifactLength").GetInt64());
        Assert.IsFalse(json.Contains("lease", StringComparison.OrdinalIgnoreCase));
    }
}
