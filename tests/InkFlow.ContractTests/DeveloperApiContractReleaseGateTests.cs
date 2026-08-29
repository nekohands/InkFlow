using System.Text.Json;
using InkFlow.Api;
using InkFlow.Modules.Developers.Application;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.ContractTests;

/// <summary>
/// Developer API 发布门禁：验证稳定的目录 DTO、密钥一次性签发字段和脱敏边界。
/// 不代表真实数据库、真实客户端或生产密钥验收证据。
/// </summary>
[TestClass]
public sealed class DeveloperApiContractReleaseGateTests
{
    private static readonly JsonSerializerOptions WebJson =
        new(JsonSerializerDefaults.Web);

    [TestMethod]
    public void Issued_Key_Payload_Contains_One_Time_Secret_And_No_Persisted_Hash()
    {
        var response = new DeveloperApiKeyIssueResponse(
            new DeveloperApiKeyResponse(
                Guid.CreateVersion7(),
                Guid.CreateVersion7(),
                "CLI",
                "lf_dev_abc123",
                "catalog.read",
                "production",
                DateTimeOffset.Parse("2026-08-29T00:00:00Z"),
                DateTimeOffset.Parse("2027-08-29T00:00:00Z"),
                null,
                null),
            "lf_dev_opaque-secret");

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(response, WebJson));
        var root = document.RootElement;
        var key = root.GetProperty("key");

        Assert.AreEqual("lf_dev_opaque-secret", root.GetProperty("apiKey").GetString());
        Assert.AreEqual("catalog.read", key.GetProperty("scope").GetString());
        Assert.AreEqual("production", key.GetProperty("environment").GetString());
        Assert.IsFalse(root.TryGetProperty("secretHash", out _));
        Assert.IsFalse(key.TryGetProperty("secretHash", out _));
    }

    [TestMethod]
    public void Developer_Catalog_Uses_Stable_Read_Only_Policy_And_DTO_Fields()
    {
        var book = new DeveloperBookDetail(
            Guid.CreateVersion7(),
            "Contract Book",
            "InkFlow",
            12);
        var payload = JsonSerializer.Serialize(book, WebJson);

        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;

        Assert.AreEqual("Contract Book", root.GetProperty("title").GetString());
        Assert.AreEqual(12, root.GetProperty("chapterCount").GetInt32());
        Assert.IsFalse(root.TryGetProperty("sourceId", out _));
    }
}
