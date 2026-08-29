using System.Net.Http;
using System.Text;
using InkFlow.BuildingBlocks.Security;
using InkFlow.Modules.Sources.Application;

namespace InkFlow.Modules.Sources.Infrastructure;

/// <summary>
/// 生产 ISourceHttpClient:出网前做 DNS 解析级 SSRF 校验(防 rebinding),
/// 随后经 HttpClient 抓取，并在解码前执行有限的响应体流读取。校验或预算失败抛异常,
/// 由 RuleAdapter 归类为失败原因。
/// </summary>
public sealed class ProductionSafeSourceHttpClient(
    HttpClient http,
    IIpAddressResolver resolver,
    SourceRuleExecutionLimits? limits = null) : ISourceHttpClient
{
    private readonly SourceRuleExecutionLimits _limits = ValidateLimits(limits);

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

        if (!string.IsNullOrWhiteSpace(request.CookieHeader))
        {
            if (request.CookieHeader.Any(char.IsControl))
            {
                throw new InvalidOperationException("request cookie header contains control characters.");
            }

            message.Headers.TryAddWithoutValidation("Cookie", request.CookieHeader);
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
        var bytes = await ReadBoundedBytesAsync(
            response.Content,
            _limits.MaxBytes,
            cancellationToken).ConfigureAwait(false);
        var body = Decode(bytes, response.Content.Headers.ContentType?.CharSet);
        var setCookieHeaders = response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.ToArray()
            : [];
        return new SourceHttpResponse(
            (int)response.StatusCode,
            body,
            setCookieHeaders,
            response.RequestMessage?.RequestUri ?? uri);
    }

    private static SourceRuleExecutionLimits ValidateLimits(SourceRuleExecutionLimits? limits)
    {
        var value = limits ?? SourceRuleExecutionLimits.Default;
        value.Validate();
        return value;
    }

    private static async Task<byte[]> ReadBoundedBytesAsync(
        HttpContent content,
        int maxBytes,
        CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength is { } contentLength && contentLength > maxBytes)
        {
            throw new SourceResponseTooLargeException();
        }

        using var stream = await content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        using var buffer = new MemoryStream(Math.Min(maxBytes, 81_920));
        var chunk = new byte[Math.Min(maxBytes, 81_920)];
        var totalBytes = 0;

        while (true)
        {
            var read = await stream
                .ReadAsync(chunk.AsMemory(), cancellationToken)
                .ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            if (read > maxBytes - totalBytes)
            {
                throw new SourceResponseTooLargeException();
            }

            buffer.Write(chunk, 0, read);
            totalBytes += read;
        }

        return buffer.ToArray();
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
