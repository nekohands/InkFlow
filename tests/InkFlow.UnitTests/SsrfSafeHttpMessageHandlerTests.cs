using System.Net;
using System.Net.Sockets;
using InkFlow.BuildingBlocks.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class SsrfSafeHttpMessageHandlerTests
{
    private sealed class FixedResolver(params IPAddress[] addresses) : IIpAddressResolver
    {
        public int CallCount { get; private set; }

        public Task<IReadOnlyList<IPAddress>> ResolveAsync(
            string host,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult<IReadOnlyList<IPAddress>>(addresses);
        }
    }

    private sealed class ThrowingResolver : IIpAddressResolver
    {
        public Task<IReadOnlyList<IPAddress>> ResolveAsync(
            string host,
            CancellationToken cancellationToken = default) =>
            throw new SocketException();
    }

    [TestMethod]
    public async Task Connection_Callback_Rejects_Private_Address_Before_Opening_Socket()
    {
        var resolver = new FixedResolver(IPAddress.Loopback);
        using var client = CreateClient(resolver);

        var exception = await ExpectRequestExceptionAsync(
            () => client.GetAsync("http://public.example/"));

        StringAssert.Contains(exception.ToString(), "blocked address");
        Assert.AreEqual(1, resolver.CallCount);
    }

    [TestMethod]
    public async Task Connection_Callback_Rejects_Mixed_Public_And_Private_Answers()
    {
        var resolver = new FixedResolver(
            IPAddress.Parse("93.184.216.34"),
            IPAddress.Parse("192.168.0.7"));
        using var client = CreateClient(resolver);

        var exception = await ExpectRequestExceptionAsync(
            () => client.GetAsync("https://public.example/"));

        StringAssert.Contains(exception.ToString(), "blocked address");
        Assert.AreEqual(1, resolver.CallCount);
    }

    [TestMethod]
    public async Task Connection_Callback_Rejects_Private_Literal_Even_When_Resolver_Is_Public()
    {
        var resolver = new FixedResolver(IPAddress.Parse("93.184.216.34"));
        using var client = CreateClient(resolver);

        var exception = await ExpectRequestExceptionAsync(
            () => client.GetAsync("http://127.0.0.1/"));

        StringAssert.Contains(exception.ToString(), "blocked");
        Assert.AreEqual(0, resolver.CallCount);
    }

    [TestMethod]
    public async Task Connection_Callback_Rejects_Non_Standard_Port_Before_Dns()
    {
        var resolver = new FixedResolver(IPAddress.Parse("93.184.216.34"));
        using var client = CreateClient(resolver);

        var exception = await ExpectRequestExceptionAsync(
            () => client.GetAsync("http://public.example:8080/"));

        StringAssert.Contains(exception.ToString(), "port 8080");
        Assert.AreEqual(0, resolver.CallCount);
    }

    [TestMethod]
    public async Task Connection_Callback_Converts_Dns_Failure_To_Safe_Request_Error()
    {
        using var client = CreateClient(new ThrowingResolver());

        var exception = await ExpectRequestExceptionAsync(
            () => client.GetAsync("http://public.example/"));

        StringAssert.Contains(exception.ToString(), "could not be resolved");
    }

    private static HttpClient CreateClient(IIpAddressResolver resolver) =>
        new(new SsrfSafeHttpMessageHandler(resolver));

    private static async Task<HttpRequestException> ExpectRequestExceptionAsync(
        Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (HttpRequestException exception)
        {
            return exception;
        }

        Assert.Fail("expected HttpRequestException");
        return null!;
    }
}
