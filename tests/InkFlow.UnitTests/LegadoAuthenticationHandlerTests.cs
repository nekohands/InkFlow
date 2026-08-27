using System.Security.Claims;
using System.Text.Encodings.Web;
using InkFlow.Modules.Identity.Application;
using InkFlow.Modules.Identity.Domain;
using InkFlow.Modules.Identity.Infrastructure.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class LegadoAuthenticationHandlerTests
{
    private static readonly Guid UserId = Guid.Parse("018f1b3a-9c0a-7b11-8a2e-0123456789ab");
    private static readonly Guid TokenId = Guid.Parse("018f1b3a-9c0a-7b12-8a2e-0123456789ab");

    [TestMethod]
    public async Task Missing_Header_Returns_No_Result()
    {
        var context = new DefaultHttpContext();
        var handler = CreateHandler(context, new StubService(null));

        await handler.InitializeAsync(Scheme(), context);
        var result = await handler.AuthenticateAsync();

        Assert.IsFalse(result.Succeeded);
        Assert.IsNull(result.Failure);
    }

    [TestMethod]
    public async Task Valid_Header_Produces_Personal_Token_Claims_Without_Raw_Token()
    {
        const string rawToken = "lf_lgd_opaque-token";
        var context = new DefaultHttpContext();
        context.Request.Headers[IdentityAuthenticationDefaults.LegadoTokenHeader] = rawToken;
        var handler = CreateHandler(
            context,
            new StubService(new AuthenticatedLegadoToken(UserId, TokenId, LegadoTokenScope.Read)));

        await handler.InitializeAsync(Scheme(), context);
        var result = await handler.AuthenticateAsync();

        Assert.IsTrue(result.Succeeded);
        var principal = result.Principal!;
        Assert.AreEqual(UserId.ToString(), principal.FindFirstValue("sub"));
        Assert.AreEqual(TokenId.ToString(), principal.FindFirstValue(IdentityAuthenticationDefaults.LegadoTokenIdClaim));
        Assert.AreEqual("read", principal.FindFirstValue(IdentityAuthenticationDefaults.LegadoScopeClaim));
        Assert.IsFalse(principal.Claims.Any(claim => claim.Value == rawToken));
        Assert.IsTrue(principal.Identity!.IsAuthenticated);
    }

    [TestMethod]
    public async Task Duplicate_Or_Overlong_Header_Fails_Closed()
    {
        var duplicate = new DefaultHttpContext();
        duplicate.Request.Headers.Append(IdentityAuthenticationDefaults.LegadoTokenHeader, "one");
        duplicate.Request.Headers.Append(IdentityAuthenticationDefaults.LegadoTokenHeader, "two");
        var duplicateHandler = CreateHandler(duplicate, new StubService(null));

        await duplicateHandler.InitializeAsync(Scheme(), duplicate);
        var duplicateResult = await duplicateHandler.AuthenticateAsync();
        Assert.IsFalse(duplicateResult.Succeeded);
        Assert.IsNotNull(duplicateResult.Failure);

        var overlong = new DefaultHttpContext();
        overlong.Request.Headers[IdentityAuthenticationDefaults.LegadoTokenHeader] = new string('x', 513);
        var overlongHandler = CreateHandler(overlong, new StubService(null));

        await overlongHandler.InitializeAsync(Scheme(), overlong);
        var overlongResult = await overlongHandler.AuthenticateAsync();
        Assert.IsFalse(overlongResult.Succeeded);
        Assert.IsNotNull(overlongResult.Failure);
    }

    private static LegadoTokenAuthenticationHandler CreateHandler(
        HttpContext context,
        ILegadoAccessTokenService tokens) =>
        new(
            new TestOptionsMonitor<AuthenticationSchemeOptions>(),
            NullLoggerFactory.Instance,
            UrlEncoder.Default,
            tokens);

    private static AuthenticationScheme Scheme() =>
        new(
            IdentityAuthenticationDefaults.LegadoScheme,
            null,
            typeof(LegadoTokenAuthenticationHandler));

    private sealed class StubService(AuthenticatedLegadoToken? authenticated) : ILegadoAccessTokenService
    {
        public Task<LegadoTokenOperationResult> IssueAsync(
            Guid userId,
            string? name,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<LegadoTokenInfo>> ListAsync(
            Guid userId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<LegadoTokenResultStatus> RevokeAsync(
            Guid userId,
            Guid tokenId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<AuthenticatedLegadoToken?> ValidateAsync(
            string rawToken,
            LegadoTokenScope requiredScope,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(authenticated);
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
