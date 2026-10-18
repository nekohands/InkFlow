using InkFlow.Modules.Sources.Application;

namespace InkFlow.Modules.Crawling.Application;

public sealed record SourceBookUrlResolution(
    bool IsSuccess,
    string? SourceId,
    string? ExternalBookId,
    string? NormalizedUrl,
    string? ErrorCode,
    string? Error)
{
    public static SourceBookUrlResolution Success(
        string sourceId,
        string externalBookId,
        string normalizedUrl) =>
        new(true, sourceId, externalBookId, normalizedUrl, null, null);

    public static SourceBookUrlResolution Failure(string code, string error) =>
        new(false, null, null, null, code, error);
}

/// <summary>
/// 将人工输入的书籍地址解析为已登记来源和外部书籍 ID。
/// 这里只做注册来源的精确主机校验与适配器自有路径解析，不做代理转发或网络探测。
/// </summary>
public sealed class SourceBookUrlResolver(
    ISourceRepository sourceRepository,
    ISourceAdapterFactory adapterFactory)
{
    private const int MaxUrlLength = 2048;
    private readonly RegisteredSourceBookUrlLookup sourceLookup =
        new(sourceRepository, adapterFactory);

    public async Task<SourceBookUrlResolution> ResolveAsync(
        string? input,
        CancellationToken cancellationToken = default)
    {
        var raw = input?.Trim() ?? string.Empty;
        if (raw.Length == 0)
        {
            return SourceBookUrlResolution.Failure(
                "source-url.empty", "book URL must not be empty.");
        }

        if (raw.Length > MaxUrlLength ||
            !Uri.TryCreate(raw, UriKind.Absolute, out var uri))
        {
            return SourceBookUrlResolution.Failure(
                "source-url.invalid", "book URL must be a valid absolute URL.");
        }

        if (uri.Scheme is not ("http" or "https"))
        {
            return SourceBookUrlResolution.Failure(
                "source-url.scheme", "only HTTP and HTTPS book URLs are supported.");
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            return SourceBookUrlResolution.Failure(
                "source-url.credentials", "book URLs must not contain embedded credentials.");
        }

        if (!string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
        {
            return SourceBookUrlResolution.Failure(
                "source-url.query-not-allowed", "book URLs must not contain a query or fragment.");
        }

        if (!IsDefaultPort(uri))
        {
            return SourceBookUrlResolution.Failure(
                "source-url.port", "non-default URL ports are not supported.");
        }

        var resolution = await sourceLookup
            .ResolveAsync(uri, cancellationToken)
            .ConfigureAwait(false);
        if (resolution is not null)
        {
            return resolution;
        }

        return SourceBookUrlResolution.Failure(
            "source-url.unresolved",
            "the URL is not a supported book page of a registered public source.");
    }

    private static bool IsDefaultPort(Uri uri) =>
        uri.Port < 0 ||
        (uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) && uri.Port == 80) ||
        (uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) && uri.Port == 443);
}
