using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Crawling.Domain;

/// <summary>
/// 任务载荷：对指定来源的一次能力抓取请求。
/// 只携带能力与变量字典，凭据一律通过 CredentialReferenceId 传递（v1 预留字段），
/// 明文凭据禁止进入任务载荷。
/// </summary>
public sealed record CrawlPayload(
    string SourceId,
    SourceCapability Capability,
    IReadOnlyDictionary<string, string> Variables,
    string? CredentialReferenceId = null);
