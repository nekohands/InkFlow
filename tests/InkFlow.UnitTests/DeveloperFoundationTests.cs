using InkFlow.Modules.Billing.Application;
using InkFlow.Modules.Billing.Domain;
using InkFlow.Modules.Developers.Application;
using InkFlow.Modules.Developers.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class DeveloperDomainTests
{
    private static readonly DateTimeOffset T0 =
        new(2026, 8, 29, 8, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void Application_Name_Is_Normalized_And_Revocation_Is_Idempotent()
    {
        var userId = Guid.CreateVersion7();
        var application = DeveloperApplication.Create(userId, "  Reader App  ", T0);

        Assert.AreEqual("Reader App", application.Name);
        Assert.IsTrue(application.IsActive);

        application.Revoke(T0.AddMinutes(1));
        application.Revoke(T0.AddMinutes(2));

        Assert.IsFalse(application.IsActive);
        Assert.AreEqual(T0.AddMinutes(1), application.RevokedAt);
    }

    [TestMethod]
    public void Api_Key_Uses_One_Way_Hash_And_Expires_Or_Revokes()
    {
        var createdAt = T0;
        var raw = "lf_dev_test-secret";
        var key = DeveloperApiKey.Create(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "Reader",
            "lf_dev_test",
            DeveloperApiKey.HashSecret(raw),
            DeveloperApiScopes.CatalogRead,
            DeveloperEnvironment.Production,
            createdAt,
            createdAt.AddDays(1));

        Assert.AreNotEqual(raw, key.SecretHash);
        Assert.IsTrue(key.IsActive(createdAt.AddHours(1)));
        Assert.IsFalse(key.IsActive(createdAt.AddDays(1)));

        key.Revoke(createdAt.AddHours(2));
        Assert.IsFalse(key.IsActive(createdAt.AddHours(3)));
    }
}

[TestClass]
public sealed class DeveloperApplicationServiceTests
{
    private static readonly DateTimeOffset T0 =
        new(2026, 8, 29, 8, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task Issue_And_Rotate_Return_Secret_Only_At_Issuance()
    {
        var userId = Guid.CreateVersion7();
        var applications = new FakeApplicationRepository();
        var keys = new FakeKeyRepository();
        var service = CreateService(applications, keys);

        var application = await service.CreateApplicationAsync(userId, "Reader App");
        Assert.IsTrue(application.IsSuccess);

        var issued = await service.IssueKeyAsync(userId, application.Value!.ApplicationId, "CLI", 30);
        Assert.IsTrue(issued.IsSuccess);
        Assert.IsFalse(string.IsNullOrWhiteSpace(issued.Value!.RawKey));
        StringAssert.StartsWith(issued.Value.RawKey, issued.Value.Key.Prefix);

        var listed = await service.ListKeysAsync(userId, application.Value.ApplicationId);
        Assert.AreEqual(1, listed.Count);
        Assert.AreEqual(issued.Value.Key.KeyId, listed[0].KeyId);

        var rotated = await service.RotateKeyAsync(
            userId,
            application.Value.ApplicationId,
            issued.Value.Key.KeyId,
            30);
        Assert.IsTrue(rotated.IsSuccess);
        Assert.AreNotEqual(issued.Value.RawKey, rotated.Value!.RawKey);
        Assert.IsNull(await service.ValidateAsync(issued.Value.RawKey));
        Assert.IsNotNull(await service.ValidateAsync(rotated.Value.RawKey));
    }

    [TestMethod]
    public async Task Revoking_Application_Also_Invalidates_Its_Keys()
    {
        var userId = Guid.CreateVersion7();
        var applications = new FakeApplicationRepository();
        var keys = new FakeKeyRepository();
        var service = CreateService(applications, keys);

        var application = await service.CreateApplicationAsync(userId, "Reader App");
        var issued = await service.IssueKeyAsync(
            userId,
            application.Value!.ApplicationId,
            null,
            null);
        Assert.IsTrue(issued.IsSuccess);

        Assert.AreEqual(
            DeveloperOperationStatus.Success,
            await service.RevokeApplicationAsync(userId, application.Value.ApplicationId));
        Assert.IsNull(await service.ValidateAsync(issued.Value!.RawKey));
    }

    private static DeveloperApplicationService CreateService(
        FakeApplicationRepository applications,
        FakeKeyRepository keys) =>
        new(
            applications,
            keys,
            new FixedSecretGenerator(),
            new ActiveUserReader(),
            new FixedClock(T0));

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class ActiveUserReader : IDeveloperUserStatusReader
    {
        public Task<bool> IsActiveAsync(
            Guid userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(userId != Guid.Empty);
    }

    private sealed class FixedSecretGenerator : IDeveloperApiKeySecretGenerator
    {
        private int _sequence;

        public DeveloperApiKeySecret Generate()
        {
            var raw = $"lf_dev_unit-secret-{++_sequence}";
            return new DeveloperApiKeySecret(
                raw,
                raw[..15],
                DeveloperApiKey.HashSecret(raw));
        }
    }

    private sealed class FakeApplicationRepository : IDeveloperApplicationRepository
    {
        private readonly Dictionary<Guid, DeveloperApplication> _items = [];

        public Task AddAsync(
            DeveloperApplication application,
            CancellationToken cancellationToken = default)
        {
            _items[application.Id] = application;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<DeveloperApplication>> ListForUserAsync(
            Guid requestedUserId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DeveloperApplication>>(
                _items.Values.Where(item => item.UserId == requestedUserId).ToList());

        public Task<DeveloperApplication?> GetAsync(
            Guid requestedUserId,
            Guid applicationId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                _items.TryGetValue(applicationId, out var item) && item.UserId == requestedUserId
                    ? item
                    : null);

        public Task<bool> RevokeAsync(
            Guid requestedUserId,
            Guid applicationId,
            DateTimeOffset now,
            CancellationToken cancellationToken = default)
        {
            if (!_items.TryGetValue(applicationId, out var item) || item.UserId != requestedUserId)
            {
                return Task.FromResult(false);
            }

            item.Revoke(now);
            return Task.FromResult(true);
        }
    }

    private sealed class FakeKeyRepository : IDeveloperApiKeyRepository
    {
        private readonly Dictionary<Guid, DeveloperApiKey> _items = [];

        public Task AddAsync(
            DeveloperApiKey key,
            CancellationToken cancellationToken = default)
        {
            _items[key.Id] = key;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<DeveloperApiKey>> ListForApplicationAsync(
            Guid userId,
            Guid applicationId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DeveloperApiKey>>(_items.Values
                .Where(key => key.UserId == userId && key.ApplicationId == applicationId)
                .ToList());

        public Task<DeveloperApiKey?> GetAsync(
            Guid userId,
            Guid applicationId,
            Guid keyId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                _items.TryGetValue(keyId, out var key) &&
                key.UserId == userId &&
                key.ApplicationId == applicationId
                    ? key
                    : null);

        public Task<DeveloperApiKey?> FindByHashAsync(
            string secretHash,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<DeveloperApiKey?>(_items.Values.SingleOrDefault(
                key => key.SecretHash == secretHash));

        public Task<bool> RevokeAsync(
            Guid userId,
            Guid applicationId,
            Guid keyId,
            DateTimeOffset now,
            CancellationToken cancellationToken = default)
        {
            var key = GetLocal(userId, applicationId, keyId);
            if (key is null)
            {
                return Task.FromResult(false);
            }

            key.Revoke(now);
            return Task.FromResult(true);
        }

        public Task<bool> RotateAsync(
            Guid userId,
            Guid applicationId,
            Guid keyId,
            DeveloperApiKey replacement,
            DateTimeOffset now,
            CancellationToken cancellationToken = default)
        {
            var current = GetLocal(userId, applicationId, keyId);
            if (current is null || current.RevokedAt is not null)
            {
                return Task.FromResult(false);
            }

            current.Revoke(now);
            _items[replacement.Id] = replacement;
            return Task.FromResult(true);
        }

        public Task MarkUsedAsync(
            Guid keyId,
            DateTimeOffset now,
            CancellationToken cancellationToken = default)
        {
            _items[keyId].MarkUsed(now);
            return Task.CompletedTask;
        }

        private DeveloperApiKey? GetLocal(Guid userId, Guid applicationId, Guid keyId) =>
            _items.TryGetValue(keyId, out var key) &&
            key.UserId == userId &&
            key.ApplicationId == applicationId
                ? key
                : null;
    }
}

[TestClass]
public sealed class CommercialFoundationTests
{
    private static readonly DateTimeOffset T0 =
        new(2026, 8, 29, 8, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void Built_In_Plans_Are_Versioned_And_Grant_Catalog_Read()
    {
        Assert.AreEqual(3, BuiltInPlans.All.Count);
        CollectionAssert.AreEquivalent(
            new[] { "free", "pro", "developer" },
            BuiltInPlans.All.Select(plan => plan.Code).ToArray());
        Assert.IsTrue(BuiltInPlans.All.All(plan =>
            plan.Version == CommercialPlanCodes.Version &&
            plan.Grants(CommercialEntitlements.DeveloperCatalogRead)));
    }

    [TestMethod]
    public async Task Missing_Assignment_Uses_Free_And_Admin_Assignment_Changes_It()
    {
        var userId = Guid.CreateVersion7();
        var assignments = new FakeAssignments();
        var service = new EntitlementService(
            new FakePlans(),
            assignments,
            new ActiveBillingUserReader(),
            new FixedClock(T0));

        var initial = await service.GetForUserAsync(userId);
        Assert.IsNotNull(initial);
        Assert.AreEqual(CommercialPlanCodes.Free, initial!.Plan.Code);

        var assigned = await service.AssignAsync(
            Guid.CreateVersion7(),
            userId,
            CommercialPlanCodes.Pro,
            "manual verification upgrade");
        Assert.AreEqual(EntitlementOperationStatus.Success, assigned.Status);
        Assert.AreEqual(CommercialPlanCodes.Pro, assigned.Value!.Plan.Code);
        Assert.AreEqual(CommercialPlanCodes.Pro,
            (await service.GetForUserAsync(userId))!.Plan.Code);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class ActiveBillingUserReader : IBillingUserStatusReader
    {
        public Task<bool> IsActiveAsync(
            Guid userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(userId != Guid.Empty);
    }

    private sealed class FakePlans : IPlanRepository
    {
        public Task<IReadOnlyList<PlanDefinition>> ListAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(BuiltInPlans.All);

        public Task<PlanDefinition?> GetAsync(
            string code,
            int version,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<PlanDefinition?>(BuiltInPlans.All.SingleOrDefault(plan =>
                plan.Code == code && plan.Version == version));
    }

    private sealed class FakeAssignments : IEntitlementAssignmentRepository
    {
        private readonly List<EntitlementAssignment> _items = [];

        public Task<EntitlementAssignment?> GetLatestForUserAsync(
            Guid userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<EntitlementAssignment?>(_items
                .Where(item => item.UserId == userId)
                .OrderByDescending(item => item.CreatedAt)
                .FirstOrDefault());

        public Task AddAsync(
            EntitlementAssignment assignment,
            CancellationToken cancellationToken = default)
        {
            _items.Add(assignment);
            return Task.CompletedTask;
        }
    }
}
