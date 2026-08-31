using DotNet.Testcontainers.Images;
using InkFlow.Modules.Billing.Application;
using InkFlow.Modules.Billing.Domain;
using InkFlow.Modules.Billing.Infrastructure.Persistence;
using InkFlow.Modules.Developers.Application;
using InkFlow.Modules.Developers.Domain;
using InkFlow.Modules.Developers.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace InkFlow.IntegrationTests;

/// <summary>
/// 真实 PostgreSQL 验证商业基础迁移、密钥撤销联动和跨 API Key 的用户级月度配额串行化。
/// 本机没有 Docker 时由类初始化明确阻塞，不能把环境缺失伪装成通过。
/// </summary>
[TestClass]
public sealed class DeveloperBillingPersistenceTests
{
    private static readonly DateTimeOffset T0 =
        new(2026, 8, 29, 13, 0, 0, TimeSpan.Zero);
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
    public async Task Migrations_Create_Commercial_Schemas_And_Built_In_Plans()
    {
        await using var developers = CreateDeveloperDb();
        await developers.Database.MigrateAsync().ConfigureAwait(false);
        await using var billing = CreateBillingDb();
        await billing.Database.MigrateAsync().ConfigureAwait(false);

        var developerTables = await developers.Database.SqlQuery<string>(
                $"""SELECT table_name AS "Value" FROM information_schema.tables WHERE table_schema = 'developers'""")
            .ToListAsync()
            .ConfigureAwait(false);
        CollectionAssert.AreEquivalent(
            new[] { "applications", "api_keys" },
            developerTables.ToList());

        var billingTables = await billing.Database.SqlQuery<string>(
                $"""SELECT table_name AS "Value" FROM information_schema.tables WHERE table_schema = 'billing'""")
            .ToListAsync()
            .ConfigureAwait(false);
        CollectionAssert.AreEquivalent(
            new[] { "plans", "entitlement_assignments", "usage_periods", "usage_ledger" },
            billingTables.ToList());
        Assert.AreEqual(3, await billing.Plans.CountAsync().ConfigureAwait(false));
        Assert.AreEqual(0, (await billing.Database.GetPendingMigrationsAsync().ConfigureAwait(false)).Count());
    }

    [TestMethod]
    public async Task Revoked_Application_Is_Excluded_From_Key_Authentication_Lookup()
    {
        await using var db = CreateDeveloperDb();
        await db.Database.MigrateAsync().ConfigureAwait(false);
        var applications = new EfDeveloperApplicationRepository(db);
        var keys = new EfDeveloperApiKeyRepository(db);
        var userId = Guid.CreateVersion7();
        var application = DeveloperApplication.Create(userId, "Integration App", T0);
        const string raw = "lf_dev_integration-secret";
        var key = DeveloperApiKey.Create(
            userId,
            application.Id,
            "Primary",
            "lf_dev_integr",
            DeveloperApiKey.HashSecret(raw),
            DeveloperApiScopes.CatalogRead,
            DeveloperEnvironment.Production,
            T0,
            T0.AddDays(30));

        await applications.AddAsync(application).ConfigureAwait(false);
        await keys.AddAsync(key).ConfigureAwait(false);
        Assert.IsNotNull(await keys.FindByHashAsync(key.SecretHash).ConfigureAwait(false));
        Assert.AreEqual(1, (await keys.ListForApplicationAsync(userId, application.Id)).Count);

        Assert.IsTrue(await applications.RevokeAsync(userId, application.Id, T0.AddMinutes(1)));
        Assert.IsNull(await keys.FindByHashAsync(key.SecretHash).ConfigureAwait(false));
    }

    [TestMethod]
    public async Task Concurrent_Application_And_Key_Creation_Respect_Active_Limits()
    {
        await using var setup = CreateDeveloperDb();
        await setup.Database.MigrateAsync().ConfigureAwait(false);

        var applicationOwner = Guid.CreateVersion7();
        var applicationRepository = new EfDeveloperApplicationRepository(setup);
        for (var index = 0; index < DeveloperLimits.MaxApplicationsPerUser - 1; index++)
        {
            var seeded = DeveloperApplication.Create(
                applicationOwner,
                $"Seed application {index}",
                T0);
            Assert.IsTrue(await applicationRepository.AddAsync(seeded).ConfigureAwait(false));
        }

        await using var firstApplicationDb = CreateDeveloperDb();
        await using var secondApplicationDb = CreateDeveloperDb();
        var firstApplication = DeveloperApplication.Create(
            applicationOwner,
            "Concurrent application A",
            T0);
        var secondApplication = DeveloperApplication.Create(
            applicationOwner,
            "Concurrent application B",
            T0);
        var applicationResults = await Task.WhenAll(
            new EfDeveloperApplicationRepository(firstApplicationDb).AddAsync(firstApplication),
            new EfDeveloperApplicationRepository(secondApplicationDb).AddAsync(secondApplication))
            .ConfigureAwait(false);

        Assert.AreEqual(1, applicationResults.Count(result => result));
        Assert.AreEqual(1, applicationResults.Count(result => !result));

        var keyOwner = Guid.CreateVersion7();
        var keyApplication = DeveloperApplication.Create(keyOwner, "Key limit application", T0);
        Assert.IsTrue(await applicationRepository.AddAsync(keyApplication).ConfigureAwait(false));
        var keyRepository = new EfDeveloperApiKeyRepository(setup);
        for (var index = 0; index < DeveloperLimits.MaxActiveKeysPerApplication - 1; index++)
        {
            var raw = $"lf_dev_seed_key-{index}";
            var seeded = DeveloperApiKey.Create(
                keyOwner,
                keyApplication.Id,
                $"Seed key {index}",
                raw[..16],
                DeveloperApiKey.HashSecret(raw),
                DeveloperApiScopes.CatalogRead,
                DeveloperEnvironment.Production,
                T0,
                T0.AddDays(30));
            Assert.IsTrue(await keyRepository.AddAsync(seeded).ConfigureAwait(false));
        }

        await using var firstKeyDb = CreateDeveloperDb();
        await using var secondKeyDb = CreateDeveloperDb();
        var firstKey = CreateKey(keyOwner, keyApplication.Id, "Concurrent key A", "a");
        var secondKey = CreateKey(keyOwner, keyApplication.Id, "Concurrent key B", "b");
        var keyResults = await Task.WhenAll(
            new EfDeveloperApiKeyRepository(firstKeyDb).AddAsync(firstKey),
            new EfDeveloperApiKeyRepository(secondKeyDb).AddAsync(secondKey))
            .ConfigureAwait(false);

        Assert.AreEqual(1, keyResults.Count(result => result));
        Assert.AreEqual(1, keyResults.Count(result => !result));
    }

    [TestMethod]
    public async Task Rotating_An_Expired_Key_Cannot_Exceed_The_Active_Key_Limit()
    {
        await using var db = CreateDeveloperDb();
        await db.Database.MigrateAsync().ConfigureAwait(false);

        var userId = Guid.CreateVersion7();
        var application = DeveloperApplication.Create(userId, "Expired rotation application", T0);
        var applications = new EfDeveloperApplicationRepository(db);
        var keys = new EfDeveloperApiKeyRepository(db);
        Assert.IsTrue(await applications.AddAsync(application).ConfigureAwait(false));

        var expiredRaw = "lf_dev_expired-rotation";
        var expired = DeveloperApiKey.Create(
            userId,
            application.Id,
            "Expired key",
            expiredRaw[..16],
            DeveloperApiKey.HashSecret(expiredRaw),
            DeveloperApiScopes.CatalogRead,
            DeveloperEnvironment.Production,
            T0.AddDays(-2),
            T0.AddDays(-1));
        Assert.IsTrue(await keys.AddAsync(expired).ConfigureAwait(false));

        for (var index = 0; index < DeveloperLimits.MaxActiveKeysPerApplication; index++)
        {
            Assert.IsTrue(
                await keys.AddAsync(
                    CreateKey(userId, application.Id, $"Active key {index}", $"active-{index}"))
                    .ConfigureAwait(false));
        }

        var replacement = CreateKey(userId, application.Id, "Replacement key", "replacement");
        Assert.IsFalse(
            await keys.RotateAsync(
                    userId,
                    application.Id,
                    expired.Id,
                    replacement,
                    T0)
                .ConfigureAwait(false));

        var current = await keys.GetAsync(userId, application.Id, expired.Id).ConfigureAwait(false);
        Assert.IsNotNull(current);
        Assert.IsNull(current!.RevokedAt);
        Assert.AreEqual(
            DeveloperLimits.MaxActiveKeysPerApplication,
            (await keys.ListForApplicationAsync(userId, application.Id).ConfigureAwait(false))
                .Count(key => key.IsActive(T0)));
    }

    [TestMethod]
    public async Task Monthly_Quota_Is_Enforced_At_User_Level_Across_Keys()
    {
        await using (var setup = CreateBillingDb())
        {
            await setup.Database.MigrateAsync().ConfigureAwait(false);
        }

        var userId = Guid.CreateVersion7();
        var first = CreateQuotaService();
        var second = CreateQuotaService();

        var results = await Task.WhenAll(
            first.Service.ReserveAsync(new QuotaReservationRequest(
                userId,
                Guid.CreateVersion7(),
                Guid.CreateVersion7(),
                "catalog.get_content",
                600,
                "trace-a")),
            second.Service.ReserveAsync(new QuotaReservationRequest(
                userId,
                Guid.CreateVersion7(),
                Guid.CreateVersion7(),
                "catalog.get_content",
                600,
                "trace-b"))).ConfigureAwait(false);

        Assert.AreEqual(1, results.Count(result => result.Status == QuotaReservationStatus.Reserved));
        Assert.AreEqual(1, results.Count(result => result.Status == QuotaReservationStatus.Exceeded));

        await using var verify = CreateBillingDb();
        var used = await verify.UsagePeriods
            .Where(period => period.UserId == userId)
            .Select(period => period.UsedUnits)
            .SingleAsync()
            .ConfigureAwait(false);
        Assert.AreEqual(600, used);
        Assert.AreEqual(1, await verify.UsageLedger.CountAsync(entry => entry.UserId == userId));
        await first.DisposeAsync();
        await second.DisposeAsync();
    }

    [TestMethod]
    public async Task Quota_Snapshot_Cache_Cannot_Return_A_Different_User()
    {
        await using (var setup = CreateBillingDb())
        {
            await setup.Database.MigrateAsync().ConfigureAwait(false);
        }

        var userId = Guid.CreateVersion7();
        var otherUserId = Guid.CreateVersion7();
        await using var db = CreateBillingDb();
        var entitlements = new EntitlementService(
            new EfPlanRepository(db),
            new EfEntitlementAssignmentRepository(db),
            new ActiveBillingUserReader(),
            new FixedClock(T0));
        var entitlement = await entitlements.GetForUserAsync(userId).ConfigureAwait(false);
        Assert.IsNotNull(entitlement);

        var periodStart = new DateTimeOffset(
            T0.Year,
            T0.Month,
            1,
            0,
            0,
            0,
            TimeSpan.Zero);
        var cache = new FixedQuotaCache(new QuotaSnapshot(
            otherUserId,
            entitlement!.Plan.Code,
            entitlement.Plan.Version,
            periodStart,
            periodStart.AddMonths(1),
            entitlement.Plan.MonthlyQuotaUnits,
            UsedUnits: 999,
            RemainingUnits: 1,
            AlgorithmVersion: entitlement.Plan.QuotaAlgorithmVersion));
        var service = new QuotaService(
            db,
            entitlements,
            cache,
            new FixedClock(T0));

        var snapshot = await service.GetSnapshotAsync(userId).ConfigureAwait(false);

        Assert.IsNotNull(snapshot);
        Assert.AreEqual(userId, snapshot!.UserId);
        Assert.AreEqual(0, snapshot.UsedUnits);
        Assert.AreEqual(1, cache.SetCount, "A rejected cache payload must be replaced by the authoritative snapshot.");
    }

    private static DeveloperDbContext CreateDeveloperDb() =>
        new(new DbContextOptionsBuilder<DeveloperDbContext>()
            .UseNpgsql(_container!.GetConnectionString())
            .Options);

    private static DeveloperApiKey CreateKey(
        Guid userId,
        Guid applicationId,
        string name,
        string suffix)
    {
        var raw = $"lf_dev_concurrent-{suffix}";
        return DeveloperApiKey.Create(
            userId,
            applicationId,
            name,
            raw[..16],
            DeveloperApiKey.HashSecret(raw),
            DeveloperApiScopes.CatalogRead,
            DeveloperEnvironment.Production,
            T0,
            T0.AddDays(30));
    }

    private static BillingDbContext CreateBillingDb() =>
        new(new DbContextOptionsBuilder<BillingDbContext>()
            .UseNpgsql(_container!.GetConnectionString())
            .Options);

    private static QuotaFixture CreateQuotaService()
    {
        var db = CreateBillingDb();
        var entitlements = new EntitlementService(
            new EfPlanRepository(db),
            new EfEntitlementAssignmentRepository(db),
            new ActiveBillingUserReader(),
            new FixedClock(T0));
        return new QuotaFixture(
            db,
            new QuotaService(db, entitlements, new InMemoryQuotaCache(), new FixedClock(T0)));
    }

    private sealed record QuotaFixture(BillingDbContext Db, QuotaService Service) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    private sealed class ActiveBillingUserReader : IBillingUserStatusReader
    {
        public Task<bool> IsActiveAsync(
            Guid userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(userId != Guid.Empty);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class InMemoryQuotaCache : IQuotaSnapshotCache
    {
        private readonly Dictionary<(Guid UserId, DateTimeOffset PeriodStart), QuotaSnapshot> _items = [];
        private readonly object _gate = new();

        public Task<QuotaSnapshot?> GetAsync(
            Guid userId,
            DateTimeOffset periodStart,
            CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                return Task.FromResult(_items.TryGetValue((userId, periodStart), out var value) ? value : null);
            }
        }

        public Task SetAsync(
            QuotaSnapshot snapshot,
            CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                _items[(snapshot.UserId, snapshot.PeriodStart)] = snapshot;
            }

            return Task.CompletedTask;
        }

        public Task RemoveAsync(
            Guid userId,
            DateTimeOffset periodStart,
            CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                _items.Remove((userId, periodStart));
            }

            return Task.CompletedTask;
        }
    }

    private sealed class FixedQuotaCache(QuotaSnapshot? value) : IQuotaSnapshotCache
    {
        private QuotaSnapshot? _value = value;

        public int SetCount { get; private set; }

        public Task<QuotaSnapshot?> GetAsync(
            Guid userId,
            DateTimeOffset periodStart,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_value);

        public Task SetAsync(
            QuotaSnapshot snapshot,
            CancellationToken cancellationToken = default)
        {
            _value = snapshot;
            SetCount++;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(
            Guid userId,
            DateTimeOffset periodStart,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
