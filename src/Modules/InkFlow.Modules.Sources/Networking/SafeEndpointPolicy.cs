using System.Net;
using System.Net.Sockets;

namespace InkFlow.Modules.Sources.Networking;

public interface IHostAddressResolver
{
    Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken);
}

public sealed class DnsHostAddressResolver : IHostAddressResolver
{
    public Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken) =>
        Dns.GetHostAddressesAsync(host, cancellationToken);
}

public sealed record ValidatedEndpoint(Uri Uri, IReadOnlyList<IPAddress> Addresses);

public sealed class UnsafeEndpointException(string message) : InvalidOperationException(message);

public sealed class SafeEndpointValidator(IHostAddressResolver? resolver = null)
{
    private readonly IHostAddressResolver _resolver = resolver ?? new DnsHostAddressResolver();

    public async Task<ValidatedEndpoint> ValidateAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uri);

        if (!uri.IsAbsoluteUri
            || (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            throw new UnsafeEndpointException("Only absolute HTTP and HTTPS targets are allowed.");
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new UnsafeEndpointException("User-info in source URLs is not allowed.");
        }

        var host = uri.IdnHost.TrimEnd('.');
        if (string.IsNullOrWhiteSpace(host)
            || string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase))
        {
            throw new UnsafeEndpointException($"Host '{host}' is not allowed.");
        }

        IPAddress[] addresses;
        if (IPAddress.TryParse(host, out var literalAddress))
        {
            addresses = [literalAddress];
        }
        else
        {
            addresses = await _resolver.ResolveAsync(host, cancellationToken).ConfigureAwait(false);
        }

        if (addresses.Length == 0)
        {
            throw new UnsafeEndpointException($"Host '{host}' did not resolve to an address.");
        }

        foreach (var address in addresses)
        {
            if (!GlobalAddressPolicy.IsAllowed(address))
            {
                throw new UnsafeEndpointException($"Host '{host}' resolves to blocked address '{address}'.");
            }
        }

        return new ValidatedEndpoint(uri, addresses);
    }
}

public static class GlobalAddressPolicy
{
    private sealed record NetworkRange(IPAddress Network, int PrefixLength);

    private static readonly NetworkRange[] BlockedIpv4 =
    [
        Range("0.0.0.0", 8),
        Range("10.0.0.0", 8),
        Range("100.64.0.0", 10),
        Range("127.0.0.0", 8),
        Range("169.254.0.0", 16),
        Range("172.16.0.0", 12),
        Range("192.0.0.0", 24),
        Range("192.0.2.0", 24),
        Range("192.168.0.0", 16),
        Range("198.18.0.0", 15),
        Range("198.51.100.0", 24),
        Range("203.0.113.0", 24),
        Range("224.0.0.0", 4),
        Range("240.0.0.0", 4)
    ];

    private static readonly NetworkRange[] BlockedIpv6 =
    [
        Range("::", 128),
        Range("::1", 128),
        Range("100::", 64),
        Range("2001:db8::", 32),
        Range("fc00::", 7),
        Range("fe80::", 10),
        Range("ff00::", 8)
    ];

    public static bool IsAllowed(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        var ranges = address.AddressFamily switch
        {
            AddressFamily.InterNetwork => BlockedIpv4,
            AddressFamily.InterNetworkV6 => BlockedIpv6,
            _ => null
        };

        return ranges is not null && ranges.All(range => !Contains(range, address));
    }

    private static NetworkRange Range(string address, int prefixLength) =>
        new(IPAddress.Parse(address), prefixLength);

    private static bool Contains(NetworkRange range, IPAddress address)
    {
        var networkBytes = range.Network.GetAddressBytes();
        var addressBytes = address.GetAddressBytes();
        if (networkBytes.Length != addressBytes.Length)
        {
            return false;
        }

        var fullBytes = range.PrefixLength / 8;
        var remainingBits = range.PrefixLength % 8;

        for (var index = 0; index < fullBytes; index++)
        {
            if (networkBytes[index] != addressBytes[index])
            {
                return false;
            }
        }

        if (remainingBits == 0)
        {
            return true;
        }

        var mask = (byte)(0xFF << (8 - remainingBits));
        return (networkBytes[fullBytes] & mask) == (addressBytes[fullBytes] & mask);
    }
}
