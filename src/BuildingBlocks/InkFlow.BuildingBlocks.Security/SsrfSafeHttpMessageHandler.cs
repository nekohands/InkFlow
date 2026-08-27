using System.Net;
using System.Net.Http;
using System.Net.Sockets;

namespace InkFlow.BuildingBlocks.Security;

/// <summary>
/// 让 HTTP 请求的实际 TCP 连接使用同一批经过 SSRF 校验的解析结果。
///
/// <see cref="HttpClient"/> 默认会自行解析主机名；仅在调用前单独解析一次
/// 无法阻止 DNS rebinding。此 Handler 在每次建立新连接时解析并校验所有地址，
/// 然后直接连接其中一个已校验地址，因此连接不会再次触发系统 DNS 解析。
/// 自动重定向仍由 HttpClient 处理，但每个重定向目标建立连接时都会重复这套校验。
/// </summary>
public sealed class SsrfSafeHttpMessageHandler : HttpMessageHandler
{
    private const int MaxAutomaticRedirections = 5;
    private readonly HttpMessageInvoker _inner;

    public SsrfSafeHttpMessageHandler(IIpAddressResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver);

        var transport = new SocketsHttpHandler
        {
            // 来源请求不应通过环境代理绕过目标地址校验；代理本身也可能成为内网跳板。
            UseProxy = false,
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = MaxAutomaticRedirections,
            ConnectCallback = (context, cancellationToken) =>
                ConnectToVerifiedAddressAsync(context, resolver, cancellationToken),
        };
        _inner = new HttpMessageInvoker(transport, disposeHandler: true);
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken) =>
        _inner.SendAsync(request, cancellationToken);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _inner.Dispose();
        }

        base.Dispose(disposing);
    }

    private static async ValueTask<Stream> ConnectToVerifiedAddressAsync(
        SocketsHttpConnectionContext context,
        IIpAddressResolver resolver,
        CancellationToken cancellationToken)
    {
        var endpoint = context.DnsEndPoint;

        // 对当前请求再做一次字面量检查。对于自动重定向，
        // InitialRequestMessage 可能就是重定向后的请求；即使运行时只提供端点，
        // 下面的 endpoint.Host 检查也会阻止直接使用被禁止的字面量地址。
        var requestUri = context.InitialRequestMessage?.RequestUri;
        if (requestUri is not null)
        {
            var literalErrors = SsrfGuard.InspectLiteral(requestUri);
            if (literalErrors.Count > 0)
            {
                throw new HttpRequestException($"ssrf: {string.Join("; ", literalErrors)}");
            }
        }

        if (endpoint.Port is not (80 or 443))
        {
            throw new HttpRequestException(
                $"ssrf: port {endpoint.Port} is not allowed; only 80/443.");
        }

        if (IPAddress.TryParse(endpoint.Host, out var literalAddress) &&
            IpAddressClassification.IsBlocked(literalAddress))
        {
            throw new HttpRequestException(
                $"ssrf: literal address '{endpoint.Host}' resolves to a blocked range.");
        }

        if (!IPAddress.TryParse(endpoint.Host, out _) && endpoint.Host.All(char.IsDigit))
        {
            throw new HttpRequestException(
                $"ssrf: numeric-only host '{endpoint.Host}' looks like an obfuscated address.");
        }

        IReadOnlyList<IPAddress> addresses;
        try
        {
            addresses = await resolver
                .ResolveAsync(endpoint.Host, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (SocketException exception)
        {
            throw new HttpRequestException(
                $"ssrf: host '{endpoint.Host}' could not be resolved.", exception);
        }

        if (addresses.Count == 0)
        {
            throw new HttpRequestException(
                $"ssrf: host '{endpoint.Host}' resolved to no addresses.");
        }

        // 不接受“部分安全”的解析结果：若同一主机同时返回内网地址，
        // 让连接器自行选择会留下不确定的 SSRF 路径，因此整个目标拒绝。
        var blocked = addresses.FirstOrDefault(IpAddressClassification.IsBlocked);
        if (blocked is not null)
        {
            throw new HttpRequestException(
                $"ssrf: '{endpoint.Host}' resolves to blocked address {blocked}.");
        }

        Exception? lastException = null;
        foreach (var address in addresses)
        {
            Socket? socket = null;
            try
            {
                socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
                {
                    NoDelay = true,
                };
                await socket
                    .ConnectAsync(new IPEndPoint(address, endpoint.Port), cancellationToken)
                    .ConfigureAwait(false);

                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                socket?.Dispose();
                throw;
            }
            catch (Exception exception) when (exception is SocketException or IOException)
            {
                socket?.Dispose();
                lastException = exception;
            }
        }

        throw new HttpRequestException(
            $"ssrf-safe connection to '{endpoint.Host}' failed for all resolved addresses.",
            lastException);
    }
}
