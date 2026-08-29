using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Sources.Application;

/// <summary>发往来源的已构建请求：绝对 URL + 方法 + 头，可选表单体。</summary>
public sealed record SourceHttpRequest(
    RuleHttpMethod Method,
    string Url,
    IReadOnlyDictionary<string, string> Headers,
    string? FormBody = null)
{
    /// <summary>
    /// 当前 RuleAdapter 执行临时生成的 Cookie 头；它不属于 Rule JSON 或持久化请求定义。
    /// </summary>
    public string? CookieHeader { get; init; }
}

/// <summary>来源响应。仅暴露执行规则所需的最小面；生产读取在解码前受字节预算约束。</summary>
public sealed record SourceHttpResponse(int StatusCode, string Body)
{
    public SourceHttpResponse(
        int statusCode,
        string body,
        IReadOnlyList<string>? setCookieHeaders,
        Uri? responseUri = null)
        : this(statusCode, body)
    {
        SetCookieHeaders = setCookieHeaders ?? [];
        ResponseUri = responseUri;
    }

    public bool IsSuccess => StatusCode is >= 200 and < 300;

    /// <summary>响应中的原始 Set-Cookie 头，仅在单次执行内消费，不写入日志或结果。</summary>
    public IReadOnlyList<string> SetCookieHeaders { get; init; } = [];

    /// <summary>生产 HTTP 客户端最终响应的 URI，用于限制重定向 Cookie 的来源。</summary>
    public Uri? ResponseUri { get; init; }
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
