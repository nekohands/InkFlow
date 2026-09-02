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
public sealed class IdentityAuthenticationHandlerTests
{
    private static readonly Guid UserId = Guid.Parse("018f1b3a-9c0a-7b11-8a2e-0123456789ab");
    private static readonly Guid SessionId = Guid.Parse("018f1b3a-9c0a-7b12-8a2e-0123456789ab");

    [TestMethod]
    public async Task Missing_Authorization_Header_Returns_No_Result()
    {
        var context = new DefaultHttpContext();
        var handler = CreateHandler(context, new StubIdentityService(null));

        await handler.InitializeAsync(Scheme(), context);
        var result = await handler.AuthenticateAsync();

        Assert.IsFalse(result.Succeeded);
        Assert.IsNull(result.Failure);
    }

    [TestMethod]
    public async Task Valid_Bearer_Token_Produces_Subject_Role_And_Session_Claims()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Bearer opaque-access-token";
        var handler = CreateHandler(
            context,
            new StubIdentityService(new AuthenticatedIdentity(
                UserId,
                "reader@example.com",
                UserRole.Operator,
                SessionId)));

        await handler.InitializeAsync(Scheme(), context);
        var result = await handler.AuthenticateAsync();

        Assert.IsTrue(result.Succeeded);
        var principal = result.Principal!;
        Assert.AreEqual(UserId.ToString(), principal.FindFirstValue("sub"));
        Assert.AreEqual("reader@example.com", principal.FindFirstValue("email"));
        Assert.AreEqual(UserRole.Operator.ToString(), principal.FindFirstValue(ClaimTypes.Role));
        Assert.AreEqual(SessionId.ToString(), principal.FindFirstValue(IdentityAuthenticationDefaults.SessionIdClaim));
        Assert.IsTrue(principal.Identity!.IsAuthenticated);
    }

    [TestMethod]
    public async Task Invalid_Or_Non_Bearer_Header_Fails_Closed()
    {
        foreach (var header in new[] { "Basic abc", "Bearer", "not-a-header" })
        {
            var context = new DefaultHttpContext();
            context.Request.Headers.Authorization = header;
            var handler = CreateHandler(context, new StubIdentityService(null));

            await handler.InitializeAsync(Scheme(), context);
            var result = await handler.AuthenticateAsync();

            Assert.IsFalse(result.Succeeded, header);
            Assert.IsNotNull(result.Failure, header);
        }
    }

    private static OpaqueBearerAuthenticationHandler CreateHandler(
        HttpContext context,
        IIdentityService identity) =>
        new(
            new TestOptionsMonitor<AuthenticationSchemeOptions>(),
            NullLoggerFactory.Instance,
            UrlEncoder.Default,
            identity);

    private static AuthenticationScheme Scheme() =>
        new(IdentityAuthenticationDefaults.Scheme, null, typeof(OpaqueBearerAuthenticationHandler));

    private sealed class StubIdentityService(AuthenticatedIdentity? identity) : IIdentityService
    {
        public Task<IdentityOperationResult> RegisterAsync(
            string email,
            string password,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IdentityOperationResult> LoginAsync(
            string email,
            string password,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IdentityOperationResult> RefreshAsync(
            string refreshToken,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<AuthenticatedIdentity?> ValidateAccessTokenAsync(
            string accessToken,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(identity);

        public Task LogoutAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IdentityProfile?> GetProfileAsync(
            Guid userId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ProfileOperationResult> UpdateProfileAsync(
            Guid userId,
            string? displayName,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PasswordChangeOperationResult> ChangePasswordAsync(
            Guid userId,
            string currentPassword,
            string newPassword,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
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
