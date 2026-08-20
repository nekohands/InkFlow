using System.Net;
using System.Text;
using InkFlow.Modules.Sources.Networking;
using InkFlow.Modules.Sources.Rules;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class SafeHttpTests
{
    [TestMethod]
    [DataRow("http://127.0.0.1/")]
    [DataRow("http://10.1.2.3/")]
    [DataRow("http://169.254.169.254/latest/meta-data/")]
    [DataRow("http://192.168.1.1/")]
    [DataRow("http://[::1]/")]
    [DataRow("http://localhost/")]
    public async Task Validator_blocks_private_and_local_targets(string url)
    {
        var validator = new SafeEndpointValidator();

        await Assert.ThrowsAsync<UnsafeEndpointException>(
            () => validator.ValidateAsync(new Uri(url)));
    }

    [TestMethod]
    public async Task Validator_accepts_public_literal_address()
    {
        var validator = new SafeEndpointValidator();

        var endpoint = await validator.ValidateAsync(new Uri("https://93.184.216.34/"));

        Assert.HasCount(1, endpoint.Addresses);
        Assert.AreEqual(IPAddress.Parse("93.184.216.34"), endpoint.Addresses[0]);
    }

    [TestMethod]
    public async Task Validator_rejects_dns_answer_if_any_address_is_private()
    {
        var validator = new SafeEndpointValidator(new FixedResolver(
            IPAddress.Parse("93.184.216.34"),
            IPAddress.Parse("10.0.0.10")));

        await Assert.ThrowsAsync<UnsafeEndpointException>(
            () => validator.ValidateAsync(new Uri("https://books.example/")));
    }

    [TestMethod]
    public async Task Executor_revalidates_redirect_targets()
    {
        var handler = new StubHandler(request =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.Redirect);
            response.Headers.Location = new Uri("http://127.0.0.1/internal");
            return response;
        });
        using var client = new HttpClient(handler);
        var validator = new SafeEndpointValidator(new FixedResolver(IPAddress.Parse("93.184.216.34")));
        using var executor = new SafeHttpExecutor(validator, client);
        var request = new CompiledSourceRequest(
            "GET",
            new Uri("https://books.example/start"),
            new Dictionary<string, string>(),
            new Dictionary<string, string>());

        var exception = await Assert.ThrowsAsync<SourceNetworkException>(
            () => executor.ExecuteAsync(request, new RuleExecutionBudget()));

        Assert.AreEqual(SourceNetworkErrorKind.BlockedTarget, exception.Kind);
        Assert.AreEqual(1, handler.RequestCount);
    }

    [TestMethod]
    public async Task Executor_enforces_streaming_response_size_budget()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(Encoding.UTF8.GetBytes(new string('x', 2048)))
        });
        using var client = new HttpClient(handler);
        var validator = new SafeEndpointValidator(new FixedResolver(IPAddress.Parse("93.184.216.34")));
        using var executor = new SafeHttpExecutor(validator, client);
        var request = new CompiledSourceRequest(
            "GET",
            new Uri("https://books.example/content"),
            new Dictionary<string, string>(),
            new Dictionary<string, string>());
        var budget = new RuleExecutionBudget(MaxBytes: 1024);

        var exception = await Assert.ThrowsAsync<SourceNetworkException>(
            () => executor.ExecuteAsync(request, budget));

        Assert.AreEqual(SourceNetworkErrorKind.ResponseTooLarge, exception.Kind);
    }

    private sealed class FixedResolver(params IPAddress[] addresses) : IHostAddressResolver
    {
        public Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken) =>
            Task.FromResult(addresses);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> factory) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(factory(request));
        }
    }
}
