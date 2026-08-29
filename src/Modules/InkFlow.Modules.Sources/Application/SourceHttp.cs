using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Sources.Application;

/// <summary>发往来源的已构建请求：绝对 URL + 方法 + 头，可选表单体。</summary>
public sealed record SourceHttpRequest(
    RuleHttpMethod Method,
    string Url,
    IReadOnlyDictionary<string, string> Headers,
    string? FormBody = null);

/// <summary>来源响应。仅暴露执行规则所需的最小面；生产读取在解码前受字节预算约束。</summary>
public sealed record SourceHttpResponse(int StatusCode, string Body)
{
    public bool IsSuccess => StatusCode is >= 200 and < 300;
}

/// <summary>响应体超过来源执行预算时的稳定、无数据泄漏错误。</summary>
public sealed class SourceResponseTooLargeException() : InvalidOperationException(
    "execution: response exceeded byte budget.");

/// <summary>
/// 来源 HTTP 执行抽象。生产实现负责 SafeHttpClient（SSRF 校验 + 连接级约束）；
/// 测试使用内存 Fixture，普通 CI 不触网。
/// </summary>
public interface ISourceHttpClient
{
    Task<SourceHttpResponse> SendAsync(SourceHttpRequest request, CancellationToken cancellationToken = default);
}
