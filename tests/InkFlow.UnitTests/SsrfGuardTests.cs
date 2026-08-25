using System.Net;
using System.Net.Sockets;
using InkFlow.BuildingBlocks.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class SsrfGuardTests
{
    private sealed class FixedResolver(params IPAddress[] addresses) : IIpAddressResolver
    {
        public Task<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<IPAddress>>(addresses);
    }

    private sealed class ThrowingResolver : IIpAddressResolver
    {
        public Task<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken = default)
            => throw new SocketException();
    }

    [TestMethod]
    public void Public_Http_Url_Passes_Literal_Check()
    {
        var errors = SsrfGuard.InspectLiteral(new Uri("https://example.com/search?q=x"));
        Assert.AreEqual(0, errors.Count);
    }

    [TestMethod]
    public void File_And_Ftp_Schemes_Are_Rejected()
    {
        Assert.IsTrue(SsrfGuard.InspectLiteral(new Uri("file:///etc/passwd")).Count > 0);
        Assert.IsTrue(SsrfGuard.InspectLiteral(new Uri("ftp://example.com/x")).Count > 0);
    }

    [TestMethod]
    public void Non_Standard_Port_Is_Rejected()
    {
        var errors = SsrfGuard.InspectLiteral(new Uri("http://example.com:6379/"));
        Assert.IsTrue(errors.Any(e => e.Contains("port")));
    }

    [TestMethod]
    [DataRow("http://127.0.0.1/")]
    [DataRow("http://10.0.0.9/")]
    [DataRow("http://172.16.0.1/")]
    [DataRow("http://192.168.1.1/")]
    [DataRow("http://169.254.169.254/latest/meta-data/")]
    [DataRow("http://100.64.0.1/")]
    [DataRow("http://[::1]/")]
    [DataRow("http://[fe80::1]/")]
    [DataRow("http://[fc00::1]/")]
    [DataRow("http://2130706433/")]
    public void Literal_Internal_Addresses_Are_Blocked(string url)
    {
        var errors = SsrfGuard.InspectLiteral(new Uri(url));
        Assert.IsTrue(errors.Count > 0, $"{url} should be blocked");
    }

    [TestMethod]
    public void Ipv4_Mapped_Ipv6_Is_Blocked_By_Range_Rules()
    {
        Assert.IsTrue(IpAddressClassification.IsBlocked(IPAddress.Parse("::ffff:127.0.0.1")));
        Assert.IsTrue(IpAddressClassification.IsBlocked(IPAddress.Parse("::ffff:192.168.0.5")));
    }

    [TestMethod]
    public async Task Dns_Resolving_To_Private_Address_Is_Blocked()
    {
        var resolver = new FixedResolver(IPAddress.Parse("93.184.216.34"), IPAddress.Parse("192.168.0.7"));
        var errors = await SsrfGuard.InspectResolvedAsync(new Uri("https://evil.example.com/"), resolver);
        Assert.IsTrue(errors.Any(e => e.Contains("blocked address 192.168.0.7")));
    }

    [TestMethod]
    public async Task Dns_Resolving_To_All_Public_Addresses_Passes()
    {
        var resolver = new FixedResolver(IPAddress.Parse("93.184.216.34"), IPAddress.Parse("2606:2800:220:1:248:1893:25c8:1946"));
        var errors = await SsrfGuard.InspectResolvedAsync(new Uri("https://example.com/"), resolver);
        Assert.AreEqual(0, errors.Count);
    }

    [TestMethod]
    public async Task Unresolvable_Host_Is_Reported()
    {
        var errors = await SsrfGuard.InspectResolvedAsync(new Uri("https://missing.example.net/"), new ThrowingResolver());
        Assert.IsTrue(errors.Any(e => e.Contains("could not be resolved")));
    }

    [TestMethod]
    public async Task Literal_Errors_Short_Circuit_Dns()
    {
        // 字面量已失败时不应再发起 DNS 解析（ThrowingResolver 会抛异常，若被触达则测试失败）。
        var errors = await SsrfGuard.InspectResolvedAsync(
            new Uri("http://127.0.0.1/"),
            new ThrowingResolver());
        Assert.IsTrue(errors.Count > 0);
    }
}
