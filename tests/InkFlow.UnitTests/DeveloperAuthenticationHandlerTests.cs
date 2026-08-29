using System.Security.Claims;
using System.Text.Encodings.Web;
using InkFlow.Modules.Developers.Application;
using InkFlow.Modules.Developers.Domain;
using InkFlow.Modules.Developers.Infrastructure.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class DeveloperAuthenticationHandlerTests
{
    private static readonly Guid UserId = Guid.Parse("0190f1b3-a10b-7b11-8a2e-0123456789ab");
    private static readonly Guid ApplicationId = Guid.Parse("0190f1b3-a10b-7b12-8a2e-0123456789ab");
    private static readonly Guid KeyId = Guid.Parse("0190f1b3-a10b-7b13-8a2e-0123456789ab");

    [TestMethod]
    public async Task Missing_Header_Returns_No_Result()
    {
        var context = new DefaultHttpContext();
        var handler = CreateHandler(context, new StubValidator(null));

        await handler.InitializeAsync(Scheme(), context);
        var result = await handler.AuthenticateAsync();

        Assert.IsFalse(result.Succeeded);
        Assert.IsNull(result.Failure);
    }

    [TestMethod]
    public async Task Valid_Header_Produces_Stable_Developer_Claims()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[DeveloperAuthenticationDefaults.ApiKeyHeader] = "lf_dev_test-secret";
        var handler = CreateHandler(
            context,
            new StubValidator(new DeveloperKeyAuthentication(
                UserId,
                ApplicationId,
                KeyId,
                DeveloperApiScopes.CatalogRead,
                DeveloperEnvironment.Production)));

        await handler.InitializeAsync(Scheme(), context);
        var result = await handler.AuthenticateAsync();

        Assert.IsTrue(result.Succeeded);
        var principal = result.Principal!;
        Assert.AreEqual(UserId.ToString(), principal.FindFirstValue("sub"));
        Assert.AreEqual(ApplicationId.ToString(), principal.FindFirstValue("client_id"));
        Assert.AreEqual(KeyId.ToString(), principal.FindFirstValue("developer_api_key_id"));
        Assert.AreEqual(DeveloperApiScopes.CatalogRead, principal.FindFirstValue("scope"));
        Assert.AreEqual("production", principal.FindFirstValue("environment"));
        Assert.AreEqual(DeveloperAuthenticationDefaults.Scheme, principal.Identity!.AuthenticationType);
    }

    [TestMethod]
    public async Task Duplicate_Or_Overlong_Header_Fails_Closed()
    {
        foreach (var header in new[]
        {
            new[] { "lf_dev_one", "lf_dev_two" },
            new[] { new string('x', 513) },
        })
        {
            var context = new DefaultHttpContext();
            context.Request.Headers[DeveloperAuthenticationDefaults.ApiKeyHeader] = header;
            var handler = CreateHandler(
                context,
                new StubValidator(new DeveloperKeyAuthentication(
                    UserId,
                    ApplicationId,
                    KeyId,
                    DeveloperApiScopes.CatalogRead,
                    DeveloperEnvironment.Production)));

            await handler.InitializeAsync(Scheme(), context);
            var result = await handler.AuthenticateAsync();

            Assert.IsFalse(result.Succeeded);
            Assert.IsNotNull(result.Failure);
        }
    }

    private static DeveloperApiKeyAuthenticationHandler CreateHandler(
        HttpContext context,
        IDeveloperApiKeyValidator validator) =>
        new(
            new TestOptionsMonitor<AuthenticationSchemeOptions>(),
            NullLoggerFactory.Instance,
            UrlEncoder.Default,
            validator);

    private static AuthenticationScheme Scheme() =>
        new(
            DeveloperAuthenticationDefaults.Scheme,
            null,
            typeof(DeveloperApiKeyAuthenticationHandler));

    private sealed class StubValidator(DeveloperKeyAuthentication? authentication)
        : IDeveloperApiKeyValidator
    {
        public Task<DeveloperKeyAuthentication?> ValidateAsync(
            string rawKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(authentication);
    }

    private sealed class TestOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public TestOptionsMonitor()
            : this(Activator.CreateInstance<T>())
        {
        }

        public T CurrentValue => value;

        public T Get(string? name) => value;

        public IDisposable? OnChange(Action<T, string> listener) => null;
    }
}
