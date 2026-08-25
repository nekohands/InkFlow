using System.Net;

namespace InkFlow.BuildingBlocks.Security;

/// <summary>
/// IP 地址分类：判断一个地址是否落入 SSRF 必须阻断的网段。
/// 规则依据 architecture/security-model 与 source-runtime 第 6 节：
/// loopback、RFC1918、link-local（含云 metadata endpoint）、ULA、组播与未指定地址一律拒绝。
/// </summary>
public static class IpAddressClassification
{
    public static bool IsBlocked(IPAddress address)
    {
        if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast)
        {
            return true;
        }

        if (IPAddress.Loopback.Equals(address) ||
            IPAddress.IPv6Loopback.Equals(address) ||
            IPAddress.Any.Equals(address) ||
            IPAddress.IPv6Any.Equals(address))
        {
            return true;
        }

        return address.AddressFamily switch
        {
            System.Net.Sockets.AddressFamily.InterNetwork => IsBlockedIpv4(address),
            System.Net.Sockets.AddressFamily.InterNetworkV6 => IsBlockedIpv6(address),
            _ => true, // 未知地址族按不安全处理
        };
    }

    private static bool IsBlockedIpv4(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return bytes[0] switch
        {
            // loopback 127/8；0/8 保留
            127 or 0 => true,
            // RFC1918：10/8
            10 => true,
            // link-local 169.254/16 —— 包含云厂商 metadata endpoint 169.254.169.254
            169 when bytes[1] == 254 => true,
            // RFC1918：172.16/12
            172 when bytes[1] >= 16 && bytes[1] <= 31 => true,
            // RFC1918：192.168/16
            192 when bytes[1] == 168 => true,
            // CGNAT 100.64/10（运营商级 NAT，内部寻址）
            100 when bytes[1] >= 64 && bytes[1] <= 127 => true,
            _ => false,
        };
    }

    private static bool IsBlockedIpv6(IPAddress address)
    {
        var bytes = address.GetAddressBytes();

        // unique local address fc00::/7
        if ((bytes[0] & 0xFE) == 0xFC)
        {
            return true;
        }

        // IPv4-mapped ::ffff:0:0/96 —— 按内嵌 IPv4 判定
        if (address.IsIPv4MappedToIPv6)
        {
            var v4 = new IPAddress(bytes[^4..]);
            return IsBlockedIpv4(v4);
        }

        return false;
    }
}
