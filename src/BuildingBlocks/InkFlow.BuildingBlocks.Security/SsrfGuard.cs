using System.Net;
using System.Net.Sockets;

namespace InkFlow.BuildingBlocks.Security;

/// <summary>
/// SSRF 防线：对外发请求前校验目标 URL。
/// 两阶段校验——
/// 1) <see cref="InspectLiteral"/>：不触网，检查 scheme、主机语法与字面量 IP；
/// 2) <see cref="InspectResolvedAsync"/>：解析 DNS 后对全部结果复检，防 DNS rebinding
///    必须配合"连接时使用同一批已验证地址"的执行器实现。
/// </summary>
public static class SsrfGuard
{
    private static readonly HashSet<string> AllowedSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "http",
        "https",
    };

    /// <summary>允许的端口白名单之外一律拒绝，避免借非标准端口访问内部服务。</summary>
    public static bool IsPortAllowed(Uri url) => url.Port is 80 or 443;

    public static IReadOnlyList<string> InspectLiteral(Uri? url)
    {
        var errors = new List<string>();

        if (url is null || !url.IsAbsoluteUri)
        {
            errors.Add("url: must be an absolute URL.");
            return errors;
        }

        if (!AllowedSchemes.Contains(url.Scheme))
        {
            errors.Add($"url: scheme '{url.Scheme}' is not allowed; only http/https.");
        }

        if (string.IsNullOrWhiteSpace(url.Host))
        {
            errors.Add("url: host must not be empty.");
            return errors;
        }

        if (!IsPortAllowed(url))
        {
            errors.Add($"url: port {url.Port} is not allowed; only 80/443.");
        }

        // 纯数字主机是十进制/八进制 IP 混淆的常见形态（如 http://2130706433/）：
        // 能按字面 IP 解析的走网段判定；解析不了的纯数字主机名一律拒绝。
        if (IPAddress.TryParse(url.Host, out var literalIp))
        {
            if (IpAddressClassification.IsBlocked(literalIp))
            {
                errors.Add($"url: literal address '{url.Host}' resolves to a blocked range.");
            }
        }
        else if (url.Host.All(char.IsDigit))
        {
            errors.Add($"url: numeric-only host '{url.Host}' looks like an obfuscated address.");
        }

        return errors;
    }

    public static async Task<IReadOnlyList<string>> InspectResolvedAsync(
        Uri url,
        IIpAddressResolver resolver,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>(InspectLiteral(url));
        if (errors.Count > 0)
        {
            return errors;
        }

        IReadOnlyList<IPAddress> addresses;
        try
        {
            addresses = await resolver.ResolveAsync(url.Host, cancellationToken).ConfigureAwait(false);
        }
        catch (SocketException)
        {
            errors.Add($"dns: host '{url.Host}' could not be resolved.");
            return errors;
        }

        if (addresses.Count == 0)
        {
            errors.Add($"dns: host '{url.Host}' resolved to no addresses.");
            return errors;
        }

        foreach (var address in addresses)
        {
            if (IpAddressClassification.IsBlocked(address))
            {
                errors.Add($"dns: '{url.Host}' resolves to blocked address {address}.");
            }
        }

        return errors;
    }
}
