using InkFlow.Modules.Identity.Application;
using InkFlow.Modules.Identity.Domain;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class LegadoAccessTokenServiceTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 28, 14, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task Issue_Returns_Raw_Token_Once_And_Stores_Only_Hash()
    {
        var context = CreateContext();

        var result = await context.Service.IssueAsync(context.User.Id, " Reading 3.0 ");

        Assert.IsTrue(result.IsSuccess);
        Assert.IsNotNull(result.Issue);
        Assert.AreEqual("Reading 3.0", result.Issue!.Info.Name);
        Assert.AreEqual("lf_lgd_opaque-token-1", result.Issue.RawToken);
        Assert.AreEqual(
            OpaqueTokenHashing.Hash(result.Issue.RawToken),
            context.Tokens.Store.Single().TokenHash);
        Assert.AreNotEqual(result.Issue.RawToken, context.Tokens.Store.Single().TokenHash);
        Assert.AreEqual(T0.AddDays(90), result.Issue.Info.ExpiresAt);
    }

    [TestMethod]
    public async Task List_Is_Explicitly_Scoped_To_The_Owner()
    {
        var context = CreateContext();
        var otherUser = User.Create("other@example.com", "$hash$only", T0);
        context.Users.Store.Add(otherUser);

        await context.Service.IssueAsync(context.User.Id, "mine");
        await context.Service.IssueAsync(otherUser.Id, "other");

        var values = await context.Service.ListAsync(context.User.Id);

        Assert.AreEqual(1, values.Count);
        Assert.AreEqual("mine", values[0].Name);
        Assert.AreEqual(context.User.Id, values[0].UserId);
    }

    [TestMethod]
    public async Task Validate_Rejects_Expired_Revoked_And_Inactive_User_Tokens()
    {
        var context = CreateContext();
        var issued = await context.Service.IssueAsync(context.User.Id, null);
        var rawToken = issued.Issue!.RawToken;

        var valid = await context.Service.ValidateAsync(rawToken, LegadoTokenScope.Read);
        Assert.IsNotNull(valid);
        Assert.AreEqual(context.User.Id, valid!.UserId);

        context.Clock.Now = issued.Issue.Info.ExpiresAt;
        Assert.IsNull(await context.Service.ValidateAsync(rawToken, LegadoTokenScope.Read));

        context.Clock.Now = T0;
        Assert.AreEqual(
            LegadoTokenResultStatus.Success,
            await context.Service.RevokeAsync(context.User.Id, issued.Issue.Info.Id));
        Assert.IsNull(await context.Service.ValidateAsync(rawToken, LegadoTokenScope.Read));

        var second = await context.Service.IssueAsync(context.User.Id, "second");
        context.User.Suspend(T0.AddMinutes(1));
        Assert.IsNull(await context.Service.ValidateAsync(second.Issue!.RawToken, LegadoTokenScope.Read));
    }

    [TestMethod]
    public async Task Revoke_Requires_Owner_And_Is_Idempotent_For_Owner()
    {
        var context = CreateContext();
        var otherUser = User.Create("other@example.com", "$hash$only", T0);
        context.Users.Store.Add(otherUser);
        var issued = await context.Service.IssueAsync(context.User.Id, "mine");

        Assert.AreEqual(
            LegadoTokenResultStatus.NotFound,
            await context.Service.RevokeAsync(otherUser.Id, issued.Issue!.Info.Id));
        Assert.IsNull(context.Tokens.Store.Single().RevokedAt);

        Assert.AreEqual(
            LegadoTokenResultStatus.Success,
            await context.Service.RevokeAsync(context.User.Id, issued.Issue.Info.Id));
        Assert.AreEqual(
            LegadoTokenResultStatus.Success,
            await context.Service.RevokeAsync(context.User.Id, issued.Issue.Info.Id));
    }

    [TestMethod]
    public async Task Issue_Uses_Default_Name_And_Rejects_Unknown_User()
    {
        var context = CreateContext();

        var defaultName = await context.Service.IssueAsync(context.User.Id, " ");
        Assert.AreEqual("Reading 3.0", defaultName.Issue!.Info.Name);

        var missing = await context.Service.IssueAsync(Guid.NewGuid(), "missing");
        Assert.AreEqual(LegadoTokenResultStatus.NotFound, missing.Status);
    }

    private static TestContext CreateContext()
    {
        var users = new InMemoryUserRepository();
        var user = User.Create("reader@example.com", "$hash$only", T0);
        users.Store.Add(user);
        var tokens = new InMemoryTokenRepository();
        var clock = new MutableClock(T0);
        var service = new LegadoAccessTokenService(
            users,
            tokens,
            new FixedTokenGenerator(),
            clock,
            new IdentityOptions());
        return new TestContext(service, users, tokens, user, clock);
    }

    private sealed record TestContext(
        LegadoAccessTokenService Service,
        InMemoryUserRepository Users,
        InMemoryTokenRepository Tokens,
        User User,
        MutableClock Clock);

    private sealed class FixedTokenGenerator : IOpaqueTokenGenerator
    {
        private int _counter;

        public string CreateToken() => $"opaque-token-{Interlocked.Increment(ref _counter)}";
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
            if (Store.Any(user => user.NormalizedEmail == email))
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

    private sealed class InMemoryTokenRepository : ILegadoAccessTokenRepository
    {
        public List<LegadoAccessToken> Store { get; } = [];

        public Task AddAsync(
            LegadoAccessToken token,
            CancellationToken cancellationToken = default)
        {
            Store.Add(token);
            return Task.CompletedTask;
        }

        public Task<LegadoAccessToken?> FindByHashAsync(
            string tokenHash,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Store.SingleOrDefault(token => token.TokenHash == tokenHash));

        public Task<IReadOnlyList<LegadoAccessToken>> ListForUserAsync(
            Guid userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<LegadoAccessToken>>(
                Store.Where(token => token.UserId == userId).OrderByDescending(token => token.CreatedAt).ToList());

        public Task<bool> RevokeAsync(
            Guid userId,
            Guid tokenId,
            DateTimeOffset now,
            CancellationToken cancellationToken = default)
        {
            var token = Store.SingleOrDefault(candidate =>
                candidate.UserId == userId && candidate.Id == tokenId);
            if (token is null)
            {
                return Task.FromResult(false);
            }

            token.Revoke(now);
            return Task.FromResult(true);
        }
    }

    private sealed class MutableClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
