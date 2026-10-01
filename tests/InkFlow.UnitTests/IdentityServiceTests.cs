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

        // 轮换后的令牌在本轮重放检测之前是有效的：验证轮换本身产生了可用会话。
        var validated = await context.Service.ValidateAccessTokenAsync(rotated.Session.AccessToken);
        Assert.IsNotNull(validated);
        Assert.AreEqual(rotated.Session.UserId, validated!.UserId);
        Assert.AreEqual(rotated.Session.SessionId, validated.SessionId);
        Assert.AreEqual(UserRole.Administrator, validated.Role);

        // 随后复用旧令牌：必须判定为重放并吊销整族，因此上一步验证的会话也随之失效。
        var repeated = await context.Service.RefreshAsync(oldRefreshToken);
        Assert.AreEqual(IdentityResultStatus.RefreshTokenReplayDetected, repeated.Status);
        Assert.IsNull(await context.Service.ValidateAccessTokenAsync(rotated.Session.AccessToken));
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
    public async Task Rotated_Refresh_Token_Reuse_Revokes_Whole_Token_Family()
    {
        var context = CreateContext();
        var initial = await context.Service.RegisterAsync("user@example.com", "correct horse battery staple");
        var originalRefreshToken = initial.Session!.RefreshToken;

        context.Clock.Now = T0.AddMinutes(1);
        var rotated = await context.Service.RefreshAsync(originalRefreshToken);
        Assert.IsTrue(rotated.IsSuccess);
        var liveRefreshToken = rotated.Session!.RefreshToken;
        var liveAccessToken = rotated.Session.AccessToken;

        // 重放已轮换的旧 refresh token：必须被识别为重放，而不是普通失效。
        context.Clock.Now = T0.AddMinutes(2);
        var replayed = await context.Service.RefreshAsync(originalRefreshToken);
        Assert.AreEqual(IdentityResultStatus.RefreshTokenReplayDetected, replayed.Status);

        // 令牌族整体吊销：攻击者持有的后继会话及其访问令牌一并失效。
        Assert.IsNull(await context.Service.ValidateAccessTokenAsync(liveAccessToken));
        // 后继会话是被级联吊销（IsRotated 为 false），因此按普通失效拒绝而非再次判定重放。
        var liveAfterReplay = await context.Service.RefreshAsync(liveRefreshToken);
        Assert.AreEqual(IdentityResultStatus.InvalidRefreshToken, liveAfterReplay.Status);
        Assert.AreEqual(2, context.Sessions.RefreshSessions.Count);
        Assert.IsTrue(context.Sessions.RefreshSessions.All(session => session.RevokedAt is not null));
        Assert.IsTrue(context.Sessions.AccessTokens.All(token => token.RevokedAt is not null));
    }

    [TestMethod]
    public async Task Rotated_Refresh_Token_Reuse_Reports_Security_Event()
    {
        var sink = new RecordingSecurityEventSink();
        var context = CreateContext(sink);
        var initial = await context.Service.RegisterAsync("user@example.com", "correct horse battery staple");
        var originalRefreshToken = initial.Session!.RefreshToken;

        context.Clock.Now = T0.AddMinutes(1);
        await context.Service.RefreshAsync(originalRefreshToken);

        context.Clock.Now = T0.AddMinutes(2);
        await context.Service.RefreshAsync(originalRefreshToken);

        var recorded = sink.Replays.Single();
        Assert.AreEqual(initial.Session.UserId, recorded.UserId);
        Assert.AreEqual(2, recorded.RevokedSessionCount);
    }

    [TestMethod]
    public async Task Access_Token_Is_Rejected_Once_Its_Session_Is_Revoked()
    {
        var context = CreateContext();
        var initial = await context.Service.RegisterAsync("user@example.com", "correct horse battery staple");
        var accessToken = initial.Session!.AccessToken;

        // 直接吊销会话而不触碰访问令牌：验证访问令牌的有效性以所属会话为前提。
        await context.Sessions.RevokeSessionAsync(initial.Session.SessionId, T0.AddMinutes(1));

        Assert.IsNull(await context.Service.ValidateAccessTokenAsync(accessToken));
    }

    [TestMethod]
    public async Task Rotation_Invalidates_The_Previous_Access_Token()
    {
        var context = CreateContext();
        var initial = await context.Service.RegisterAsync("user@example.com", "correct horse battery staple");
        var originalAccessToken = initial.Session!.AccessToken;

        context.Clock.Now = T0.AddMinutes(1);
        var rotated = await context.Service.RefreshAsync(initial.Session.RefreshToken);
        Assert.IsTrue(rotated.IsSuccess);

        // 轮换后旧访问令牌立即失效，消除 15 分钟窗口内的幽灵凭证；
        // 存储事实同步：旧令牌行本身被标记吊销，而非仅依赖会话联查拒绝。
        Assert.IsNull(await context.Service.ValidateAccessTokenAsync(originalAccessToken));
        Assert.IsNotNull(await context.Service.ValidateAccessTokenAsync(rotated.Session!.AccessToken));
        Assert.IsTrue(context.Sessions.AccessTokens.Single(token => token.TokenHash ==
            OpaqueTokenHashing.Hash(originalAccessToken)).RevokedAt is not null);
    }

    [TestMethod]
    public async Task Profile_Can_Update_Display_Name_Without_Changing_Login_Email()
    {
        var context = CreateContext();
        var registered = await context.Service.RegisterAsync(
            "reader@example.com",
            "correct horse battery staple");

        var updated = await context.Service.UpdateProfileAsync(
            registered.Session!.UserId,
            "墨客");

        Assert.AreEqual(ProfileResultStatus.Success, updated.Status);
        Assert.AreEqual("墨客", updated.Profile!.DisplayName);
        Assert.AreEqual("reader@example.com", updated.Profile.Email);
        Assert.AreEqual(
            "墨客",
            (await context.Service.GetProfileAsync(registered.Session.UserId))!.DisplayName);
    }

    [TestMethod]
    public async Task ChangePassword_Requires_Current_Password_And_Revokes_All_Sessions()
    {
        var context = CreateContext();
        var initial = await context.Service.RegisterAsync(
            "reader@example.com",
            "correct horse battery staple");
        var second = await context.Service.LoginAsync(
            "reader@example.com",
            "correct horse battery staple");

        var invalid = await context.Service.ChangePasswordAsync(
            initial.Session!.UserId,
            "wrong current password",
            "new secure password");

        Assert.AreEqual(PasswordChangeResultStatus.InvalidCredentials, invalid.Status);

        var changed = await context.Service.ChangePasswordAsync(
            initial.Session.UserId,
            "correct horse battery staple",
            "new secure password");

        Assert.AreEqual(PasswordChangeResultStatus.Success, changed.Status);
        Assert.IsNull(await context.Service.ValidateAccessTokenAsync(initial.Session.AccessToken));
        Assert.IsNull(await context.Service.ValidateAccessTokenAsync(second.Session!.AccessToken));
        Assert.AreEqual(
            IdentityResultStatus.InvalidRefreshToken,
            (await context.Service.RefreshAsync(initial.Session.RefreshToken)).Status);
        Assert.AreEqual(
            IdentityResultStatus.InvalidRefreshToken,
            (await context.Service.RefreshAsync(second.Session.RefreshToken)).Status);
        Assert.IsTrue((await context.Service.LoginAsync(
            "reader@example.com",
            "new secure password")).IsSuccess);
    }

    [TestMethod]
    public async Task Avatar_Can_Be_Uploaded_And_Replaced_Only_For_The_Authenticated_User()
    {
        var context = CreateContext();
        var registered = await context.Service.RegisterAsync(
            "reader@example.com",
            "correct horse battery staple");
        var png = new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x01,
        };

        var uploaded = await context.Service.UploadAvatarAsync(
            registered.Session!.UserId,
            new MemoryStream(png));
        var stored = await context.Service.GetAvatarAsync(registered.Session.UserId);

        Assert.AreEqual(AvatarResultStatus.Success, uploaded.Status);
        Assert.IsNotNull(stored);
        Assert.AreEqual("image/png", stored!.ContentType);
        CollectionAssert.AreEqual(png, stored.Content);

        var invalid = await context.Service.UploadAvatarAsync(
            Guid.NewGuid(),
            new MemoryStream(png));

        Assert.AreEqual(AvatarResultStatus.NotFound, invalid.Status);
    }

    [TestMethod]
    public async Task Expired_Access_Token_Is_Not_Authenticated()
    {
        var context = CreateContext();
        var initial = await context.Service.RegisterAsync("user@example.com", "correct horse battery staple");
        context.Clock.Now = T0.AddMinutes(16);

        Assert.IsNull(await context.Service.ValidateAccessTokenAsync(initial.Session!.AccessToken));
    }

    private static TestContext CreateContext(IIdentitySecurityEventSink? securityEvents = null)
    {
        var sessions = new InMemorySessionRepository();
        var users = new InMemoryUserRepository(sessions);
        var avatars = new InMemoryUserAvatarRepository();
        var clock = new MutableClock(T0);
        var service = new IdentityService(
            users,
            avatars,
            sessions,
            new FakePasswordHasher(),
            new SequentialTokenGenerator(),
            clock,
            new IdentityOptions(),
            securityEvents);
        return new TestContext(service, users, sessions, avatars, clock);
    }

    private sealed record ReplayEvent(Guid UserId, int RevokedSessionCount);

    private sealed class RecordingSecurityEventSink : IIdentitySecurityEventSink
    {
        public List<ReplayEvent> Replays { get; } = [];

        public void ReportRefreshReplay(Guid userId, int revokedSessionCount, DateTimeOffset occurredAt) =>
            Replays.Add(new ReplayEvent(userId, revokedSessionCount));
    }

    private sealed record TestContext(
        IdentityService Service,
        InMemoryUserRepository Users,
        InMemorySessionRepository Sessions,
        InMemoryUserAvatarRepository Avatars,
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

    private sealed class InMemoryUserRepository(InMemorySessionRepository? sessions = null) : IUserRepository
    {
        private readonly InMemorySessionRepository? _sessions = sessions;
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

        public Task ChangePasswordAndRevokeSessionsAsync(
            User user,
            DateTimeOffset now,
            CancellationToken cancellationToken = default)
        {
            if (_sessions is not null)
            {
                foreach (var session in _sessions.RefreshSessions.Where(session => session.UserId == user.Id))
                {
                    session.Revoke(now);
                }

                foreach (var token in _sessions.AccessTokens.Where(token => token.UserId == user.Id))
                {
                    token.Revoke(now);
                }
            }

            return Task.CompletedTask;
        }
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

        public Task<(AccessToken Token, RefreshSession Session)?> FindAccessTokenWithSessionAsync(
            string tokenHash,
            CancellationToken cancellationToken = default)
        {
            var token = AccessTokens.SingleOrDefault(candidate => candidate.TokenHash == tokenHash);
            if (token is null)
            {
                return Task.FromResult<(AccessToken Token, RefreshSession Session)?>(null);
            }

            var session = RefreshSessions.SingleOrDefault(candidate => candidate.Id == token.SessionId);
            if (session is null)
            {
                return Task.FromResult<(AccessToken Token, RefreshSession Session)?>(null);
            }

            return Task.FromResult<(AccessToken Token, RefreshSession Session)?>((token, session));
        }

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
            foreach (var previousToken in AccessTokens.Where(token =>
                token.SessionId == current.Id && token.RevokedAt is null))
            {
                // 与 EF 实现保持一致：轮换即吊销旧会话的全部访问令牌。
                previousToken.Revoke(now);
            }

            RefreshSessions.Add(replacement);
            AccessTokens.Add(replacementAccessToken);
            return Task.FromResult(true);
        }

        public Task<int> RevokeSessionFamilyAsync(
            Guid sessionId,
            DateTimeOffset now,
            CancellationToken cancellationToken = default)
        {
            if (sessionId == Guid.Empty)
            {
                return Task.FromResult(0);
            }

            var revoked = new List<Guid>();
            var visited = new HashSet<Guid>();
            var frontier = new List<Guid> { sessionId };

            while (frontier.Count > 0)
            {
                var currentIds = frontier.Where(visited.Add).ToArray();
                if (currentIds.Length == 0)
                {
                    break;
                }

                var currentSessions = RefreshSessions
                    .Where(session => currentIds.Contains(session.Id))
                    .ToArray();
                revoked.AddRange(currentSessions.Select(session => session.Id));
                foreach (var session in currentSessions)
                {
                    session.Revoke(now);
                }

                frontier = currentSessions
                    .Select(session => session.ReplacedBySessionId)
                    .OfType<Guid>()
                    .Distinct()
                    .ToList();
            }

            foreach (var token in AccessTokens.Where(token => revoked.Contains(token.SessionId)))
            {
                token.Revoke(now);
            }

            return Task.FromResult(revoked.Count);
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

    private sealed class InMemoryUserAvatarRepository : IUserAvatarRepository
    {
        private readonly Dictionary<Guid, IdentityAvatar> _store = [];

        public Task<IdentityAvatar?> GetAsync(
            Guid userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_store.GetValueOrDefault(userId));

        public Task SaveAsync(
            IdentityAvatar avatar,
            CancellationToken cancellationToken = default)
        {
            _store[avatar.UserId] = avatar;
            return Task.CompletedTask;
        }
    }

    private sealed class MutableClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
