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
            new[] { "users", "user_avatars", "sessions", "access_tokens", "legado_tokens", "permission_grants" },
            tables.ToList());
    }

    [TestMethod]
    public async Task Concurrent_Registrations_Assign_Administrator_To_Only_First_User()
    {
        await using var isolatedContainer = new PostgreSqlBuilder(
            new DockerImage("postgres:18-alpine")).Build();
        await isolatedContainer.StartAsync().ConfigureAwait(false);

        await using var firstDb = CreateDb(isolatedContainer);
        await using var secondDb = CreateDb(isolatedContainer);
        var firstUsers = new EfUserRepository(firstDb);
        var secondUsers = new EfUserRepository(secondDb);

        var users = await Task.WhenAll(
            firstUsers.AddRegistrationAsync("first-registration@example.com", "$hash$first", T0),
            secondUsers.AddRegistrationAsync("second-registration@example.com", "$hash$second", T0))
            .ConfigureAwait(false);

        Assert.AreEqual(2, users.Count(user => user is not null));
        Assert.AreEqual(
            1,
            users.Count(user => user?.Role == UserRole.Administrator));
        Assert.AreEqual(
            1,
            users.Count(user => user?.Role == UserRole.Reader));

        await using var verifyDb = CreateDb(isolatedContainer);
        var persistedRoles = await verifyDb.Users
            .AsNoTracking()
            .Select(user => user.Role)
            .ToListAsync()
            .ConfigureAwait(false);
        CollectionAssert.AreEquivalent(
            new[] { (int)UserRole.Administrator, (int)UserRole.Reader },
            persistedRoles);
    }

    [TestMethod]
    public async Task Permission_Grants_Roundtrip_And_Regrant_After_Revocation()
    {
        await using var db = CreateDb();
        var users = new EfUserRepository(db);
        var permissions = new EfResourcePermissionRepository(db);
        var operatorUser = User.Rehydrate(
            Guid.CreateVersion7(),
            "operator@example.com",
            "operator@example.com",
            "$hash$only",
            UserRole.Operator,
            UserStatus.Active,
            T0,
            T0);
        await users.AddAsync(operatorUser).ConfigureAwait(false);

        var first = PermissionGrant.Create(
            operatorUser.Id,
            IdentityPermissions.SourceRead,
            IdentityResourceTypes.Source,
            "official-a",
            operatorUser.Id,
            T0);
        await permissions.AddAsync(first).ConfigureAwait(false);

        var loaded = await permissions.GetAsync(first.Id).ConfigureAwait(false);
        Assert.IsNotNull(loaded);
        Assert.AreEqual(first.Id, loaded!.Id);
        Assert.IsTrue((await permissions.ListActiveForResourceAsync(
            IdentityResourceTypes.Source,
            "official-a",
            10)).Single().IsActive);

        loaded.Revoke(T0.AddMinutes(1));
        await permissions.SaveAsync(loaded).ConfigureAwait(false);
        Assert.AreEqual(
            0,
            (await permissions.ListActiveForUserResourceAsync(
                operatorUser.Id,
                IdentityResourceTypes.Source,
                "official-a")).Count);

        var second = PermissionGrant.Create(
            operatorUser.Id,
            IdentityPermissions.SourceRead,
            IdentityResourceTypes.Source,
            "official-a",
            operatorUser.Id,
            T0.AddMinutes(2));
        await permissions.AddAsync(second).ConfigureAwait(false);

        Assert.AreEqual(
            second.Id,
            (await permissions.ListActiveForUserResourceAsync(
                operatorUser.Id,
                IdentityResourceTypes.Source,
                "official-a")).Single().Id);
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
            .RevokeAsync(user.Id, legado.Id)
            .ConfigureAwait(false));
        Assert.IsNull(await legadoTokens
            .FindByHashAsync(OpaqueTokenHashing.Hash(rawLegado))
            .ConfigureAwait(false));
        Assert.AreEqual(0, (await legadoTokens
            .ListForUserAsync(user.Id)
            .ConfigureAwait(false)).Count);
    }

    [TestMethod]
    public async Task User_Avatar_Roundtrips_And_Replaces()
    {
        await using var db = CreateDb();
        var users = new EfUserRepository(db);
        var avatars = new EfUserAvatarRepository(db);
        var user = User.Create("avatar@example.com", "$hash$only", T0);
        await users.AddAsync(user).ConfigureAwait(false);

        var original = new IdentityAvatar(user.Id, "image/png", [0x89, 0x50, 0x4E, 0x47], T0);
        await avatars.SaveAsync(original).ConfigureAwait(false);

        var loaded = await avatars.GetAsync(user.Id).ConfigureAwait(false);
        Assert.IsNotNull(loaded);
        Assert.AreEqual("image/png", loaded!.ContentType);
        CollectionAssert.AreEqual(original.Content, loaded.Content);

        var replacement = new IdentityAvatar(user.Id, "image/jpeg", [0xFF, 0xD8, 0xFF], T0.AddMinutes(1));
        await avatars.SaveAsync(replacement).ConfigureAwait(false);

        loaded = await avatars.GetAsync(user.Id).ConfigureAwait(false);
        Assert.IsNotNull(loaded);
        Assert.AreEqual("image/jpeg", loaded!.ContentType);
        CollectionAssert.AreEqual(replacement.Content, loaded.Content);
        Assert.AreEqual(replacement.UpdatedAt, loaded.UpdatedAt);
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

    [TestMethod]
    public async Task Rotation_Revokes_Previous_Access_Token_Rows()
    {
        await using var db = CreateDb();
        var users = new EfUserRepository(db);
        var sessions = new EfIdentitySessionRepository(db);
        var user = User.Create("rotation-revoke@example.com", "$hash$only", T0);
        await users.AddAsync(user).ConfigureAwait(false);

        const string currentRaw = "rotation-revoke-current-refresh";
        var current = RefreshSession.Create(
            user.Id,
            OpaqueTokenHashing.Hash(currentRaw),
            T0,
            T0.AddDays(30));
        const string previousAccessRaw = "rotation-revoke-previous-access";
        var previousAccess = AccessToken.Create(
            user.Id,
            current.Id,
            OpaqueTokenHashing.Hash(previousAccessRaw),
            T0,
            T0.AddMinutes(15));
        await sessions.AddSessionAsync(current, previousAccess).ConfigureAwait(false);

        var replacement = CreateReplacement(user.Id, "rotation-revoke-next-refresh", "rotation-revoke-next-access");
        var rotated = await sessions
            .RotateRefreshSessionAsync(
                OpaqueTokenHashing.Hash(currentRaw),
                replacement.Session,
                replacement.Access,
                T0.AddMinutes(1))
            .ConfigureAwait(false);
        Assert.IsTrue(rotated);
        db.ChangeTracker.Clear();

        // 轮换后旧访问令牌行本身必须被吊销：验证语义（按会话拒绝）与存储事实一致，
        // 保留清理也才能把该令牌判定为终态。
        var storedPrevious = await db.AccessTokens
            .AsNoTracking()
            .SingleAsync(token => token.TokenHash == OpaqueTokenHashing.Hash(previousAccessRaw))
            .ConfigureAwait(false);
        Assert.IsNotNull(storedPrevious.RevokedAt);
    }

    [TestMethod]
    public async Task RevokeSessionFamily_Revokes_Whole_Rotation_Chain()
    {
        await using var db = CreateDb();
        var users = new EfUserRepository(db);
        var sessions = new EfIdentitySessionRepository(db);
        var user = User.Create("family-revoke@example.com", "$hash$only", T0);
        await users.AddAsync(user).ConfigureAwait(false);

        const string firstRaw = "family-revoke-first-refresh";
        var first = RefreshSession.Create(
            user.Id,
            OpaqueTokenHashing.Hash(firstRaw),
            T0,
            T0.AddDays(30));
        await sessions.AddSessionAsync(
            first,
            AccessToken.Create(user.Id, first.Id, OpaqueTokenHashing.Hash("family-access-1"), T0, T0.AddMinutes(15)))
            .ConfigureAwait(false);

        var second = CreateReplacement(user.Id, "family-revoke-second-refresh", "family-access-2");
        Assert.IsTrue(await sessions
            .RotateRefreshSessionAsync(
                OpaqueTokenHashing.Hash(firstRaw),
                second.Session,
                second.Access,
                T0.AddMinutes(1))
            .ConfigureAwait(false));
        var third = CreateReplacement(user.Id, "family-revoke-third-refresh", "family-access-3");
        Assert.IsTrue(await sessions
            .RotateRefreshSessionAsync(
                OpaqueTokenHashing.Hash("family-revoke-second-refresh"),
                third.Session,
                third.Access,
                T0.AddMinutes(2))
            .ConfigureAwait(false));
        db.ChangeTracker.Clear();

        var revoked = await sessions
            .RevokeSessionFamilyAsync(first.Id, T0.AddMinutes(3))
            .ConfigureAwait(false);
        Assert.AreEqual(3, revoked);
        db.ChangeTracker.Clear();

        var sessionRows = await db.Sessions
            .AsNoTracking()
            .Where(session => session.UserId == user.Id)
            .ToListAsync()
            .ConfigureAwait(false);
        Assert.IsTrue(sessionRows.All(session => session.RevokedAt is not null));
        var tokenRows = await db.AccessTokens
            .AsNoTracking()
            .Where(token => token.UserId == user.Id)
            .ToListAsync()
            .ConfigureAwait(false);
        Assert.AreEqual(3, tokenRows.Count);
        Assert.IsTrue(tokenRows.All(token => token.RevokedAt is not null));
    }

    [TestMethod]
    public async Task Retention_Deletes_Only_Terminal_Facts_And_Respects_Cascade_Guard()
    {
        await using var db = CreateDb();
        var users = new EfUserRepository(db);
        var user = User.Create("retention@example.com", "$hash$only", T0);
        await users.AddAsync(user).ConfigureAwait(false);

        var fullyExpiredSession = SeedSession(db, user.Id, "retention-session-a", revokedAt: T0.AddDays(-31));
        SeedToken(db, user.Id, fullyExpiredSession, "retention-token-a", revokedAt: T0.AddDays(-31));
        var guardedSession = SeedSession(db, user.Id, "retention-session-b", revokedAt: T0.AddDays(-31));
        SeedToken(db, user.Id, guardedSession, "retention-token-b", revokedAt: null);
        var liveSession = SeedSession(db, user.Id, "retention-session-c", revokedAt: null);
        SeedToken(db, user.Id, liveSession, "retention-token-c", revokedAt: T0.AddDays(-31));
        var recentSession = SeedSession(db, user.Id, "retention-session-d", revokedAt: T0.AddDays(-1), expiresAt: T0.AddDays(29));
        SeedToken(db, user.Id, recentSession, "retention-token-d", revokedAt: T0.AddDays(-1));
        await db.SaveChangesAsync().ConfigureAwait(false);
        db.ChangeTracker.Clear();

        // A（会话与令牌均已吊销且过期）可删；B 的令牌未吊销，NOT EXISTS 护栏禁止
        // 会话随 CASCADE 带走未终态令牌；C/D 的会话保留，但其已吊销且过期的令牌属终态可删。
        var store = new EfIdentityRetentionStore(db);
        var result = await store
            .DeleteExpiredBatchAsync(T0, T0, batchSize: 100)
            .ConfigureAwait(false);

        Assert.AreEqual(1, result.DeletedSessions);
        Assert.AreEqual(3, result.DeletedAccessTokens);

        var sessionHashes = await db.Sessions
            .AsNoTracking()
            .Where(session => session.UserId == user.Id)
            .Select(session => session.RefreshTokenHash)
            .ToListAsync()
            .ConfigureAwait(false);
        CollectionAssert.AreEquivalent(
            new[] { "retention-session-b", "retention-session-c", "retention-session-d" },
            sessionHashes);
        var tokenHashes = await db.AccessTokens
            .AsNoTracking()
            .Where(token => token.UserId == user.Id)
            .Select(token => token.TokenHash)
            .ToListAsync()
            .ConfigureAwait(false);
        CollectionAssert.AreEquivalent(
            new[] { "retention-token-b" },
            tokenHashes);
    }

    private static Guid SeedSession(
        IdentityDbContext db,
        Guid userId,
        string refreshTokenHash,
        DateTimeOffset? revokedAt,
        DateTimeOffset? expiresAt = null)
    {
        var session = new RefreshSessionEntity
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            RefreshTokenHash = refreshTokenHash,
            CreatedAt = T0.AddDays(-30),
            ExpiresAt = expiresAt ?? T0.AddDays(-1),
            RevokedAt = revokedAt,
        };
        db.Sessions.Add(session);
        return session.Id;
    }

    private static void SeedToken(
        IdentityDbContext db,
        Guid userId,
        Guid sessionId,
        string tokenHash,
        DateTimeOffset? revokedAt) =>
        db.AccessTokens.Add(new AccessTokenEntity
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            SessionId = sessionId,
            TokenHash = tokenHash,
            CreatedAt = T0.AddDays(-30),
            ExpiresAt = T0.AddDays(-1),
            RevokedAt = revokedAt,
        });

    private static IdentityDbContext CreateDb()
        => CreateDb(_container!);

    private static IdentityDbContext CreateDb(PostgreSqlContainer container)
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(container.GetConnectionString())
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
