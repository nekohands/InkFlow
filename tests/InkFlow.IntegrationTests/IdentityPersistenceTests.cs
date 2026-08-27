using DotNet.Testcontainers.Images;
using InkFlow.Modules.Identity.Application;
using InkFlow.Modules.Identity.Domain;
using InkFlow.Modules.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace InkFlow.IntegrationTests;

/// <summary>
/// 真实 PostgreSQL 上验证 Identity Migration、不可逆令牌摘要与 refresh 一次性轮换。
/// 本机没有 Docker 时由类初始化明确阻塞，不能把环境缺失伪装成通过。
/// </summary>
[TestClass]
public sealed class IdentityPersistenceTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 28, 13, 0, 0, TimeSpan.Zero);
    private static PostgreSqlContainer? _container;

    [ClassInitialize]
    public static async Task StartContainerAsync(TestContext _)
    {
        _container = new PostgreSqlBuilder(new DockerImage("postgres:18-alpine")).Build();
        await _container.StartAsync().ConfigureAwait(false);
    }

    [ClassCleanup]
    public static async Task StopContainerAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync().ConfigureAwait(false);
        }
    }

    [TestMethod]
    public async Task Migration_Creates_Identity_Tables_And_No_Pending_Migrations()
    {
        await using var db = CreateDb();
        await db.Database.MigrateAsync().ConfigureAwait(false);

        var pending = await db.Database.GetPendingMigrationsAsync().ConfigureAwait(false);
        Assert.IsFalse(pending.Any());

        var tables = await db.Database.SqlQuery<string>(
                $"""SELECT table_name AS "Value" FROM information_schema.tables WHERE table_schema = 'identity'""")
            .ToListAsync()
            .ConfigureAwait(false);
        CollectionAssert.AreEquivalent(
            new[] { "users", "sessions", "access_tokens", "legado_tokens" },
            tables.ToList());
    }

    [TestMethod]
    public async Task User_And_Tokens_Roundtrip_Without_Storing_Raw_Tokens()
    {
        await using var db = CreateDb();
        var users = new EfUserRepository(db);
        var sessions = new EfIdentitySessionRepository(db);
        var legadoTokens = new EfLegadoAccessTokenRepository(db);
        var user = User.Create("reader@example.com", "$hash$only", T0);
        await users.AddAsync(user).ConfigureAwait(false);

        const string rawRefresh = "refresh-token-only-in-memory";
        const string rawAccess = "access-token-only-in-memory";
        var session = RefreshSession.Create(
            user.Id,
            OpaqueTokenHashing.Hash(rawRefresh),
            T0,
            T0.AddDays(30));
        var access = AccessToken.Create(
            user.Id,
            session.Id,
            OpaqueTokenHashing.Hash(rawAccess),
            T0,
            T0.AddMinutes(15));
        await sessions.AddSessionAsync(session, access).ConfigureAwait(false);

        const string rawLegado = "lf_lgd_personal-token-only-in-memory";
        var legado = LegadoAccessToken.Create(
            user.Id,
            "Reading 3.0",
            "lf_lgd_person",
            OpaqueTokenHashing.Hash(rawLegado),
            LegadoTokenScope.Read,
            T0,
            T0.AddDays(90));
        await legadoTokens.AddAsync(legado).ConfigureAwait(false);

        var loadedUser = await users.FindByNormalizedEmailAsync("reader@example.com").ConfigureAwait(false);
        var loadedSession = await sessions
            .FindRefreshSessionAsync(OpaqueTokenHashing.Hash(rawRefresh))
            .ConfigureAwait(false);
        var loadedAccess = await sessions
            .FindAccessTokenAsync(OpaqueTokenHashing.Hash(rawAccess))
            .ConfigureAwait(false);
        var loadedLegado = await legadoTokens
            .FindByHashAsync(OpaqueTokenHashing.Hash(rawLegado))
            .ConfigureAwait(false);

        Assert.IsNotNull(loadedUser);
        Assert.AreEqual("$hash$only", loadedUser!.PasswordHash);
        Assert.IsNotNull(loadedSession);
        Assert.IsNotNull(loadedAccess);
        Assert.AreNotEqual(rawRefresh, loadedSession!.RefreshTokenHash);
        Assert.AreNotEqual(rawAccess, loadedAccess!.TokenHash);
        Assert.AreEqual(user.Id, loadedAccess.UserId);
        Assert.AreEqual(session.Id, loadedAccess.SessionId);
        Assert.IsNotNull(loadedLegado);
        Assert.AreNotEqual(rawLegado, loadedLegado!.TokenHash);
        Assert.AreEqual(LegadoTokenScope.Read, loadedLegado.Scope);

        var listed = await legadoTokens.ListForUserAsync(user.Id).ConfigureAwait(false);
        Assert.AreEqual(1, listed.Count);
        Assert.AreEqual(legado.Id, listed[0].Id);

        Assert.IsTrue(await legadoTokens
            .RevokeAsync(user.Id, legado.Id, T0.AddMinutes(1))
            .ConfigureAwait(false));
        Assert.IsNotNull((await legadoTokens
            .FindByHashAsync(OpaqueTokenHashing.Hash(rawLegado))
            .ConfigureAwait(false))!.RevokedAt);
    }

    [TestMethod]
    public async Task Refresh_Rotation_Allows_Only_One_Concurrent_Winner()
    {
        await using var seedDb = CreateDb();
        var users = new EfUserRepository(seedDb);
        var seedSessions = new EfIdentitySessionRepository(seedDb);
        var user = User.Create("concurrent@example.com", "$hash$only", T0);
        await users.AddAsync(user).ConfigureAwait(false);

        const string currentRaw = "current-refresh-token";
        var current = RefreshSession.Create(
            user.Id,
            OpaqueTokenHashing.Hash(currentRaw),
            T0,
            T0.AddDays(30));
        var currentAccess = AccessToken.Create(
            user.Id,
            current.Id,
            OpaqueTokenHashing.Hash("current-access-token"),
            T0,
            T0.AddMinutes(15));
        await seedSessions.AddSessionAsync(current, currentAccess).ConfigureAwait(false);

        await using var firstDb = CreateDb();
        await using var secondDb = CreateDb();
        var first = new EfIdentitySessionRepository(firstDb);
        var second = new EfIdentitySessionRepository(secondDb);
        var firstReplacement = CreateReplacement(user.Id, "replacement-refresh-a", "replacement-access-a");
        var secondReplacement = CreateReplacement(user.Id, "replacement-refresh-b", "replacement-access-b");

        var results = await Task.WhenAll(
            first.RotateRefreshSessionAsync(
                OpaqueTokenHashing.Hash(currentRaw),
                firstReplacement.Session,
                firstReplacement.Access,
                T0.AddMinutes(1)),
            second.RotateRefreshSessionAsync(
                OpaqueTokenHashing.Hash(currentRaw),
                secondReplacement.Session,
                secondReplacement.Access,
                T0.AddMinutes(1))).ConfigureAwait(false);

        Assert.AreEqual(1, results.Count(result => result));
        Assert.AreEqual(1, results.Count(result => !result));

        var verify = CreateDb();
        await using (verify)
        {
            var sessionsCount = await verify.Sessions
                .CountAsync(session => session.UserId == user.Id)
                .ConfigureAwait(false);
            var accessCount = await verify.AccessTokens
                .CountAsync(token => token.UserId == user.Id)
                .ConfigureAwait(false);
            Assert.AreEqual(2, sessionsCount);
            Assert.AreEqual(2, accessCount);
        }
    }

    private static IdentityDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(_container!.GetConnectionString())
            .Options;
        var db = new IdentityDbContext(options);
        db.Database.Migrate();
        return db;
    }

    private static (RefreshSession Session, AccessToken Access) CreateReplacement(
        Guid userId,
        string refreshToken,
        string accessToken)
    {
        var session = RefreshSession.Create(
            userId,
            OpaqueTokenHashing.Hash(refreshToken),
            T0.AddMinutes(1),
            T0.AddDays(30));
        return (
            session,
            AccessToken.Create(
                userId,
                session.Id,
                OpaqueTokenHashing.Hash(accessToken),
                T0.AddMinutes(1),
                T0.AddMinutes(16)));
    }
}
