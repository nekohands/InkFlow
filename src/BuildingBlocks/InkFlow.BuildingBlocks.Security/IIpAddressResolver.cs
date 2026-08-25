using System.Net;
using System.Net.Sockets;

namespace InkFlow.BuildingBlocks.Security;

/// <summary>主机名 → IP 地址列表的解析抽象，便于测试注入固定结果。</summary>
public interface IIpAddressResolver
{
    Task<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken = default);
}

public sealed class DnsIpAddressResolver : IIpAddressResolver
{
    public async Task<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken = default)
    {
        var addresses = await Dns.GetHostAddressesAsync(host, cancellationToken).ConfigureAwait(false);
        return addresses;
    }
}
