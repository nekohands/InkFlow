using System.Net;
using System.Text;
using InkFlow.BuildingBlocks.Security;
using InkFlow.Modules.Sources.Application;
using InkFlow.Modules.Sources.Domain;
using InkFlow.Modules.Sources.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class ProductionSafeSourceHttpClientTests
{
    [TestMethod]
    public async Task Response_Content_Length_Over_Budget_Is_Rejected_Before_Decoding()
    {
        using var http = new HttpClient(new FixedResponseHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("123456"),
            }));
        var resolver = new FixedResolver(IPAddress.Parse("93.184.216.34"));
        var client = new ProductionSafeSourceHttpClient(
            http,
            resolver,
            new SourceRuleExecutionLimits { MaxBytes = 5 });

        var exception = await Assert.ThrowsAsync<SourceResponseTooLargeException>(() =>
            client.SendAsync(new SourceHttpRequest(
                RuleHttpMethod.Get,
                "https://books.example.com/",
                new Dictionary<string, string>())));

        StringAssert.Contains(exception.Message, "response exceeded byte budget");
    }

    [TestMethod]
    public async Task Chunked_Response_Over_Budget_Is_Rejected_While_Reading()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(new NonSeekableStream(Encoding.UTF8.GetBytes("123456"))),
        };
        response.Content.Headers.ContentLength = null;
        using var http = new HttpClient(new FixedResponseHandler(response));
        var resolver = new FixedResolver(IPAddress.Parse("93.184.216.34"));
        var client = new ProductionSafeSourceHttpClient(
            http,
            resolver,
            new SourceRuleExecutionLimits { MaxBytes = 5 });

        await Assert.ThrowsAsync<SourceResponseTooLargeException>(() =>
            client.SendAsync(new SourceHttpRequest(
                RuleHttpMethod.Get,
                "https://books.example.com/",
                new Dictionary<string, string>())));
    }

    [TestMethod]
    public async Task Transient_Cookie_Header_Is_Sent_And_Response_Cookies_Are_Exposed()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("ok"),
        };
        response.Headers.Add("Set-Cookie", "sid=server; Path=/");
        var handler = new RecordingResponseHandler(response);
        using var http = new HttpClient(handler);
        var resolver = new FixedResolver(IPAddress.Parse("93.184.216.34"));
        var client = new ProductionSafeSourceHttpClient(http, resolver);

        var result = await client.SendAsync(new SourceHttpRequest(
            RuleHttpMethod.Get,
            "https://books.example.com/",
            new Dictionary<string, string>())
        {
            CookieHeader = "sid=client",
        });

        Assert.AreEqual("sid=client", handler.CookieHeader);
        CollectionAssert.AreEqual(new[] { "sid=server; Path=/" }, result.SetCookieHeaders.ToArray());
        Assert.AreEqual("https://books.example.com/", result.ResponseUri!.AbsoluteUri);
    }

    private sealed class FixedResolver(params IPAddress[] addresses) : IIpAddressResolver
    {
        public Task<IReadOnlyList<IPAddress>> ResolveAsync(
            string host,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<IPAddress>>(addresses);
    }

    private sealed class FixedResponseHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(response);
    }

    private sealed class RecordingResponseHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public string? CookieHeader { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CookieHeader = request.Headers.GetValues("Cookie").SingleOrDefault();
            return Task.FromResult(response);
        }
    }

    private sealed class NonSeekableStream(byte[] bytes) : MemoryStream(bytes, writable: false)
    {
        public override bool CanSeek => false;
    }
}
