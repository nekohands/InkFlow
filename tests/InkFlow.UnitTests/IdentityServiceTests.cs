using InkFlow.Modules.Identity.Application;
using InkFlow.Modules.Identity.Domain;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class IdentityServiceTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 28, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task Register_Stores_Only_Hash_And_Issues_Separate_Opaque_Tokens()
    {
        var context = CreateContext();

        var result = await context.Service.RegisterAsync(" User@Example.com ", "correct horse battery staple");

        Assert.IsTrue(result.IsSuccess);
        Assert.IsNotNull(result.Session);
        Assert.AreEqual("user@example.com", result.Session!.Email);
        Assert.AreEqual(UserRole.Administrator, result.Session.Role);
        Assert.AreNotEqual(result.Session.AccessToken, result.Session.RefreshToken);
        Assert.IsFalse(context.Users.Store.Single().PasswordHash.Contains("correct horse", StringComparison.Ordinal));
        Assert.AreEqual(1, context.Sessions.RefreshSessions.Count);
        Assert.AreEqual(1, context.Sessions.AccessTokens.Count);
        Assert.AreEqual(
            OpaqueTokenHashing.Hash(result.Session.RefreshToken),
            context.Sessions.RefreshSessions.Single().RefreshTokenHash);
    }

    [TestMethod]
    public async Task Register_Assigns_Reader_To_Accounts_After_The_First()
    {
        var context = CreateContext();

        var first = await context.Service.RegisterAsync(
            "first@example.com",
            "correct horse battery staple");
        var second = await context.Service.RegisterAsync(
            "second@example.com",
            "another correct password");

        Assert.AreEqual(UserRole.Administrator, first.Session!.Role);
        Assert.AreEqual(UserRole.Reader, second.Session!.Role);
        Assert.AreEqual(
            UserRole.Administrator,
            context.Users.Store.Single(user => user.Email == "first@example.com").Role);
        Assert.AreEqual(
            UserRole.Reader,
            context.Users.Store.Single(user => user.Email == "second@example.com").Role);
    }

    [TestMethod]
    public async Task Register_Rejects_Invalid_Password_And_Duplicate_Email()
    {
        var context = CreateContext();

        var invalid = await context.Service.RegisterAsync("user@example.com", "too-short");
        Assert.AreEqual(IdentityResultStatus.InvalidRequest, invalid.Status);

        var first = await context.Service.RegisterAsync("user@example.com", "correct horse battery staple");
        Assert.IsTrue(first.IsSuccess);

        var duplicate = await context.Service.RegisterAsync(" USER@example.com ", "another correct password");
        Assert.AreEqual(IdentityResultStatus.EmailAlreadyRegistered, duplicate.Status);
    }

    [TestMethod]
    public async Task Login_Uses_Generic_Failure_For_Wrong_Password_Or_Inactive_User()
    {
        var context = CreateContext();
        var registered = await context.Service.RegisterAsync("user@example.com", "correct horse battery staple");
        var user = context.Users.Store.Single();

        var wrongPassword = await context.Service.LoginAsync("user@example.com", "wrong password");
        Assert.AreEqual(IdentityResultStatus.InvalidCredentials, wrongPassword.Status);

        user.Suspend(T0.AddMinutes(1));
        var suspended = await context.Service.LoginAsync("user@example.com", "correct horse battery staple");
        Assert.AreEqual(IdentityResultStatus.InvalidCredentials, suspended.Status);
        Assert.IsNotNull(registered.Session);
    }

    [TestMethod]
    public async Task Refresh_Rotates_Once_And_Invalidates_Previous_Token()
    {
        var context = CreateContext();
        var initial = await context.Service.RegisterAsync("user@example.com", "correct horse battery staple");
        var oldRefreshToken = initial.Session!.RefreshToken;

        context.Clock.Now = T0.AddMinutes(1);
        var rotated = await context.Service.RefreshAsync(oldRefreshToken);

        Assert.IsTrue(rotated.IsSuccess);
        Assert.AreNotEqual(oldRefreshToken, rotated.Session!.RefreshToken);
        Assert.AreEqual(2, context.Sessions.RefreshSessions.Count);
        Assert.AreEqual(2, context.Sessions.AccessTokens.Count);

        var repeated = await context.Service.RefreshAsync(oldRefreshToken);
        Assert.AreEqual(IdentityResultStatus.InvalidRefreshToken, repeated.Status);

        var validated = await context.Service.ValidateAccessTokenAsync(rotated.Session.AccessToken);
        Assert.IsNotNull(validated);
        Assert.AreEqual(rotated.Session.UserId, validated!.UserId);
        Assert.AreEqual(rotated.Session.SessionId, validated.SessionId);
        Assert.AreEqual(UserRole.Administrator, validated.Role);
    }

    [TestMethod]
    public async Task Logout_Revokes_Access_And_Refresh_Tokens_For_The_Session()
    {
        var context = CreateContext();
        var initial = await context.Service.RegisterAsync("user@example.com", "correct horse battery staple");

        await context.Service.LogoutAsync(initial.Session!.SessionId);

        Assert.IsNull(await context.Service.ValidateAccessTokenAsync(initial.Session.AccessToken));
        var refreshed = await context.Service.RefreshAsync(initial.Session.RefreshToken);
        Assert.AreEqual(IdentityResultStatus.InvalidRefreshToken, refreshed.Status);
        Assert.IsNotNull(context.Sessions.RefreshSessions.Single().RevokedAt);
        Assert.IsNotNull(context.Sessions.AccessTokens.Single().RevokedAt);
    }

    [TestMethod]
    public async Task Expired_Access_Token_Is_Not_Authenticated()
    {
        var context = CreateContext();
        var initial = await context.Service.RegisterAsync("user@example.com", "correct horse battery staple");
        context.Clock.Now = T0.AddMinutes(16);

        Assert.IsNull(await context.Service.ValidateAccessTokenAsync(initial.Session!.AccessToken));
    }

    private static TestContext CreateContext()
    {
        var users = new InMemoryUserRepository();
        var sessions = new InMemorySessionRepository();
        var clock = new MutableClock(T0);
        var service = new IdentityService(
            users,
            sessions,
            new FakePasswordHasher(),
            new SequentialTokenGenerator(),
            clock,
            new IdentityOptions());
        return new TestContext(service, users, sessions, clock);
    }

    private sealed record TestContext(
        IdentityService Service,
        InMemoryUserRepository Users,
        InMemorySessionRepository Sessions,
        MutableClock Clock);

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => $"fake:{OpaqueTokenHashing.Hash(password)}";

        public bool Verify(string password, string passwordHash) =>
            passwordHash == Hash(password);
    }

    private sealed class SequentialTokenGenerator : IOpaqueTokenGenerator
    {
        private int _counter;

        public string CreateToken() => $"token-{Interlocked.Increment(ref _counter)}";
    }

    private sealed class InMemoryUserRepository : IUserRepository
    {
        public List<User> Store { get; } = [];

        public Task<User?> FindByNormalizedEmailAsync(
            string normalizedEmail,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Store.SingleOrDefault(user => user.NormalizedEmail == normalizedEmail));

        public Task<User?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Store.SingleOrDefault(user => user.Id == id));

        public Task<User?> AddRegistrationAsync(
            string email,
            string passwordHash,
            DateTimeOffset now,
            CancellationToken cancellationToken = default)
        {
            if (Store.Any(candidate => candidate.NormalizedEmail == email))
            {
                return Task.FromResult<User?>(null);
            }

            var user = User.Create(
                email,
                passwordHash,
                now,
                Store.Count == 0 ? UserRole.Administrator : UserRole.Reader);
            Store.Add(user);
            return Task.FromResult<User?>(user);
        }

        public Task AddAsync(User user, CancellationToken cancellationToken = default)
        {
            Store.Add(user);
            return Task.CompletedTask;
        }

        public Task SaveAsync(User user, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class InMemorySessionRepository : IIdentitySessionRepository
    {
        public List<RefreshSession> RefreshSessions { get; } = [];
        public List<AccessToken> AccessTokens { get; } = [];

        public Task<RefreshSession?> FindRefreshSessionAsync(
            string refreshTokenHash,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(RefreshSessions.SingleOrDefault(session =>
                session.RefreshTokenHash == refreshTokenHash));

        public Task<AccessToken?> FindAccessTokenAsync(
            string tokenHash,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(AccessTokens.SingleOrDefault(token => token.TokenHash == tokenHash));

        public Task AddSessionAsync(
            RefreshSession session,
            AccessToken accessToken,
            CancellationToken cancellationToken = default)
        {
            RefreshSessions.Add(session);
            AccessTokens.Add(accessToken);
            return Task.CompletedTask;
        }

        public Task<bool> RotateRefreshSessionAsync(
            string currentRefreshTokenHash,
            RefreshSession replacement,
            AccessToken replacementAccessToken,
            DateTimeOffset now,
            CancellationToken cancellationToken = default)
        {
            var current = RefreshSessions.SingleOrDefault(session =>
                session.RefreshTokenHash == currentRefreshTokenHash);
            if (current is null || !current.IsActive(now))
            {
                return Task.FromResult(false);
            }

            current.ReplaceWith(replacement.Id, now);
            RefreshSessions.Add(replacement);
            AccessTokens.Add(replacementAccessToken);
            return Task.FromResult(true);
        }

        public Task RevokeSessionAsync(
            Guid sessionId,
            DateTimeOffset now,
            CancellationToken cancellationToken = default)
        {
            RefreshSessions.SingleOrDefault(session => session.Id == sessionId)?.Revoke(now);
            foreach (var token in AccessTokens.Where(token => token.SessionId == sessionId))
            {
                token.Revoke(now);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class MutableClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
