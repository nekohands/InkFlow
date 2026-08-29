using DotNet.Testcontainers.Images;
using InkFlow.Modules.Billing.Application;
using InkFlow.Modules.Billing.Domain;
using InkFlow.Modules.Billing.Infrastructure.Persistence;
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

    private static DeveloperDbContext CreateDeveloperDb() =>
        new(new DbContextOptionsBuilder<DeveloperDbContext>()
            .UseNpgsql(_container!.GetConnectionString())
            .Options);

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
}
