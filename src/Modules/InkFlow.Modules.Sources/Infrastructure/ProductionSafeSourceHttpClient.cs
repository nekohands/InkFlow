using System.Net.Http;
using System.Text;
using InkFlow.BuildingBlocks.Security;
using InkFlow.Modules.Sources.Application;

namespace InkFlow.Modules.Sources.Infrastructure;

/// <summary>
/// 生产 ISourceHttpClient:出网前做 DNS 解析级 SSRF 校验(防 rebinding),
/// 随后经 HttpClient 抓取。校验失败抛异常,由 RuleAdapter 归类为失败原因。
/// </summary>
public sealed class ProductionSafeSourceHttpClient(
    HttpClient http,
    IIpAddressResolver resolver) : ISourceHttpClient
{
    public async Task<SourceHttpResponse> SendAsync(
        SourceHttpRequest request, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException($"invalid request url '{request.Url}'.");
        }

        // DNS 解析级复检:全部解析地址都必须落在允许网段。
        var errors = await SsrfGuard.InspectResolvedAsync(uri, resolver, cancellationToken)
            .ConfigureAwait(false);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException($"ssrf: {string.Join("; ", errors)}");
        }

        using var message = new HttpRequestMessage(new HttpMethod(request.Method.ToString().ToUpperInvariant()), uri);
        foreach (var header in request.Headers)
        {
            message.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (request.FormBody is not null)
        {
            message.Content = new StringContent(
                request.FormBody, Encoding.UTF8, "application/x-www-form-urlencoded");
        }

        using var response = await http
            .SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        // 兼容老站点的非 UTF-8 编码(GB2312/GBK 等):按响应声明的 charset 解码,未知则回退 UTF-8。
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        var body = Decode(bytes, response.Content.Headers.ContentType?.CharSet);
        return new SourceHttpResponse((int)response.StatusCode, body);
    }

    private static string Decode(byte[] bytes, string? charset)
    {
        if (string.IsNullOrWhiteSpace(charset))
        {
            return Encoding.UTF8.GetString(bytes);
        }

        try
        {
            return Encoding.GetEncoding(charset).GetString(bytes);
        }
        catch (ArgumentException)
        {
            return Encoding.UTF8.GetString(bytes);
        }
    }
}
