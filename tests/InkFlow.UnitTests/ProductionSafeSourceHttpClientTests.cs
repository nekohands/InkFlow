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

    private sealed class NonSeekableStream(byte[] bytes) : MemoryStream(bytes, writable: false)
    {
        public override bool CanSeek => false;
    }
}
