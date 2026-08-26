using System.Security.Cryptography;
using System.Text;

namespace InkFlow.Modules.Sources.Domain;

/// <summary>
/// 一次成功 Content 抓取的原始产物记录。
/// RawHash 为响应体的 SHA-256：相同哈希说明上游内容未变，服务层据此跳过重复落库；
/// 正文清洗/规范化（Content AST）属于 Content 模块，本类型只保存"原样抓到的东西"的元数据。
/// </summary>
public sealed record FetchArtifact(
    Guid Id,
    string SourceId,
    string ExternalBookId,
    string ExternalChapterId,
    string RawHash,
    int BodyLength,
    DateTimeOffset FetchedAt)
{
    public static FetchArtifact Capture(
        string sourceId, string externalBookId, string externalChapterId,
        string rawBody, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
        {
            throw new ArgumentException("source id must not be empty.", nameof(sourceId));
        }

        if (string.IsNullOrWhiteSpace(externalChapterId))
        {
            throw new ArgumentException("external chapter id must not be empty.", nameof(externalChapterId));
        }

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawBody));
        var hash = Convert.ToHexString(hashBytes);

        return new FetchArtifact(
            Guid.NewGuid(),
            sourceId,
            externalBookId,
            externalChapterId,
            hash,
            rawBody.Length,
            now);
    }
}
