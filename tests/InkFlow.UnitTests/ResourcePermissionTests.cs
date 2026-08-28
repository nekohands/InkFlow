using InkFlow.Modules.Identity.Application;
using InkFlow.Modules.Identity.Domain;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class ResourcePermissionTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 28, 21, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void PermissionGrant_Rejects_Unsafe_Values_And_Preserves_Revocation_History()
    {
        var operatorId = Guid.CreateVersion7();
        var grant = PermissionGrant.Create(
            operatorId,
            "source.read",
            "source",
            "official-a",
            Guid.CreateVersion7(),
            T0);

        Assert.IsTrue(grant.IsActive);
        grant.Revoke(T0.AddMinutes(1));
        grant.Revoke(T0.AddMinutes(2));

        Assert.IsFalse(grant.IsActive);
        Assert.AreEqual(T0.AddMinutes(1), grant.RevokedAt);
        Assert.ThrowsExactly<ArgumentException>(() => PermissionGrant.Create(
            operatorId,
            "source.read",
            "source",
            "official a",
            Guid.CreateVersion7(),
            T0));
    }

    [TestMethod]
    public async Task Grant_Is_Idempotent_And_Only_Administrator_Can_Manage_It()
    {
        var context = CreateContext();

        var first = await context.Service.GrantAsync(
            context.Administrator.Id,
            context.Administrator.Role,
            context.Operator.Id,
            "SOURCE",
            "official-a",
            "SOURCE.READ");
        var repeated = await context.Service.GrantAsync(
            context.Administrator.Id,
            context.Administrator.Role,
            context.Operator.Id,
            IdentityResourceTypes.Source,
            "official-a",
            IdentityPermissions.SourceRead);
        var operatorAttempt = await context.Service.GrantAsync(
            context.Operator.Id,
            context.Operator.Role,
            context.Operator.Id,
            IdentityResourceTypes.Source,
            "official-b",
            IdentityPermissions.SourceRead);

        Assert.AreEqual(ResourcePermissionResultStatus.Success, first.Status);
        Assert.AreEqual(ResourcePermissionResultStatus.AlreadyGranted, repeated.Status);
        Assert.AreEqual(first.Grant!.Id, repeated.Grant!.Id);
        Assert.AreEqual(ResourcePermissionResultStatus.ActorNotAllowed, operatorAttempt.Status);
        Assert.AreEqual(1, context.Permissions.Store.Count);
    }

    [TestMethod]
    public async Task Authorization_Uses_Exact_Resource_And_Manage_Implied_Read()
    {
        var context = CreateContext();
        await context.Service.GrantAsync(
            context.Administrator.Id,
            context.Administrator.Role,
            context.Operator.Id,
            IdentityResourceTypes.Source,
            "official-a",
            IdentityPermissions.SourceManage);

        Assert.IsTrue(await context.Authorization.CanAccessAsync(
            context.Administrator.Id,
            UserRole.Administrator,
            IdentityPermissions.SourceRead,
            IdentityResourceTypes.Source,
            "official-b"));
        Assert.IsTrue(await context.Authorization.CanAccessAsync(
            context.Operator.Id,
            UserRole.Operator,
            IdentityPermissions.SourceManage,
            IdentityResourceTypes.Source,
            "official-a"));
        Assert.IsTrue(await context.Authorization.CanAccessAsync(
            context.Operator.Id,
            UserRole.Operator,
            IdentityPermissions.SourceRead,
            IdentityResourceTypes.Source,
            "official-a"));
        Assert.IsFalse(await context.Authorization.CanAccessAsync(
            context.Operator.Id,
            UserRole.Operator,
            IdentityPermissions.SourceRead,
            IdentityResourceTypes.Source,
            "official-b"));
        Assert.IsFalse(await context.Authorization.CanAccessAsync(
            context.Reader.Id,
            UserRole.Reader,
            IdentityPermissions.SourceRead,
            IdentityResourceTypes.Source,
            "official-a"));
    }

    [TestMethod]
    public async Task Allowed_Resource_List_Contains_Read_And_Manage_Grants_Only()
    {
        var context = CreateContext();
        await context.Service.GrantAsync(
            context.Administrator.Id,
            UserRole.Administrator,
            context.Operator.Id,
            IdentityResourceTypes.Source,
            "official-a",
            IdentityPermissions.SourceRead);
        await context.Service.GrantAsync(
            context.Administrator.Id,
            UserRole.Administrator,
            context.Operator.Id,
            IdentityResourceTypes.Source,
            "official-b",
            IdentityPermissions.SourceManage);

        var allowed = await context.Authorization.ListAllowedResourceIdsAsync(
            context.Operator.Id,
            UserRole.Operator,
            IdentityPermissions.SourceRead,
            IdentityResourceTypes.Source);

        CollectionAssert.AreEquivalent(new[] { "official-a", "official-b" }, allowed.ToList());
    }

    [TestMethod]
    public async Task Grant_Rejects_Reader_Suspended_And_Unknown_Targets()
    {
        var context = CreateContext();
        var reader = context.Reader;
        var suspendedOperator = CreateUser(
            "suspended@example.com",
            UserRole.Operator,
            UserStatus.Suspended);
        context.Users.Store.Add(suspendedOperator);

        var readerResult = await context.Service.GrantAsync(
            context.Administrator.Id,
            UserRole.Administrator,
            reader.Id,
            IdentityResourceTypes.Source,
            "official-a",
            IdentityPermissions.SourceRead);
        var suspendedResult = await context.Service.GrantAsync(
            context.Administrator.Id,
            UserRole.Administrator,
            suspendedOperator.Id,
            IdentityResourceTypes.Source,
            "official-a",
            IdentityPermissions.SourceRead);
        var missingResult = await context.Service.GrantAsync(
            context.Administrator.Id,
            UserRole.Administrator,
            Guid.CreateVersion7(),
            IdentityResourceTypes.Source,
            "official-a",
            IdentityPermissions.SourceRead);

        Assert.AreEqual(ResourcePermissionResultStatus.TargetNotEligible, readerResult.Status);
        Assert.AreEqual(ResourcePermissionResultStatus.TargetNotEligible, suspendedResult.Status);
        Assert.AreEqual(ResourcePermissionResultStatus.TargetNotFound, missingResult.Status);
    }

    [TestMethod]
    public async Task Revoke_Is_Resource_Scoped_And_Retains_The_Grant_Record()
    {
        var context = CreateContext();
        var granted = await context.Service.GrantAsync(
            context.Administrator.Id,
            UserRole.Administrator,
            context.Operator.Id,
            IdentityResourceTypes.Source,
            "official-a",
            IdentityPermissions.SourceRead);

        var wrongResource = await context.Service.RevokeAsync(
            context.Administrator.Id,
            UserRole.Administrator,
            granted.Grant!.Id,
            IdentityResourceTypes.Source,
            "official-b");
        var revoked = await context.Service.RevokeAsync(
            context.Administrator.Id,
            UserRole.Administrator,
            granted.Grant.Id,
            IdentityResourceTypes.Source,
            "official-a");
        var repeated = await context.Service.RevokeAsync(
            context.Administrator.Id,
            UserRole.Administrator,
            granted.Grant.Id,
            IdentityResourceTypes.Source,
            "official-a");

        Assert.AreEqual(ResourcePermissionResultStatus.NotFound, wrongResource.Status);
        Assert.AreEqual(ResourcePermissionResultStatus.Success, revoked.Status);
        Assert.IsFalse(revoked.Grant!.IsActive);
        Assert.AreEqual(ResourcePermissionResultStatus.AlreadyRevoked, repeated.Status);
        Assert.IsFalse(context.Permissions.Store.Single().IsActive);
    }

    private static TestContext CreateContext()
    {
        var users = new InMemoryUserRepository();
        var administrator = CreateUser("admin@example.com", UserRole.Administrator, UserStatus.Active);
        var operatorUser = CreateUser("operator@example.com", UserRole.Operator, UserStatus.Active);
        var reader = CreateUser("reader@example.com", UserRole.Reader, UserStatus.Active);
        users.Store.AddRange([administrator, operatorUser, reader]);
        var permissions = new InMemoryResourcePermissionRepository();
        var clock = new FixedClock(T0);
        return new TestContext(
            new ResourcePermissionService(users, permissions, clock),
            new ResourceAuthorizationService(permissions),
            users,
            permissions,
            administrator,
            operatorUser,
            reader);
    }

    private static User CreateUser(string email, UserRole role, UserStatus status) =>
        User.Rehydrate(
            Guid.CreateVersion7(),
            email,
            email,
            "$hash$only",
            role,
            status,
            T0,
            T0);

    private sealed record TestContext(
        ResourcePermissionService Service,
        ResourceAuthorizationService Authorization,
        InMemoryUserRepository Users,
        InMemoryResourcePermissionRepository Permissions,
        User Administrator,
        User Operator,
        User Reader);

    private sealed class InMemoryUserRepository : IUserRepository
    {
        public List<User> Store { get; } = [];

        public Task<User?> FindByNormalizedEmailAsync(
            string normalizedEmail,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Store.SingleOrDefault(user => user.NormalizedEmail == normalizedEmail));

        public Task<User?> GetAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Store.SingleOrDefault(user => user.Id == id));

        public Task AddAsync(User user, CancellationToken cancellationToken = default)
        {
            Store.Add(user);
            return Task.CompletedTask;
        }

        public Task SaveAsync(User user, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class InMemoryResourcePermissionRepository : IResourcePermissionRepository
    {
        public List<PermissionGrant> Store { get; } = [];

        public Task<PermissionGrant?> GetAsync(
            Guid grantId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Store.SingleOrDefault(grant => grant.Id == grantId));

        public Task<PermissionGrant?> FindActiveAsync(
            Guid userId,
            string resourceType,
            string resourceId,
            string permission,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Store.SingleOrDefault(grant =>
                grant.IsActive &&
                grant.UserId == userId &&
                grant.ResourceType == resourceType &&
                grant.ResourceId == resourceId &&
                grant.Permission == permission));

        public Task<IReadOnlyList<PermissionGrant>> ListActiveForResourceAsync(
            string resourceType,
            string resourceId,
            int limit,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PermissionGrant>>(Store
                .Where(grant => grant.IsActive &&
                                grant.ResourceType == resourceType &&
                                grant.ResourceId == resourceId)
                .OrderByDescending(grant => grant.GrantedAt)
                .Take(limit)
                .ToList());

        public Task<IReadOnlyList<PermissionGrant>> ListActiveForUserResourceAsync(
            Guid userId,
            string resourceType,
            string resourceId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PermissionGrant>>(Store
                .Where(grant => grant.IsActive &&
                                grant.UserId == userId &&
                                grant.ResourceType == resourceType &&
                                grant.ResourceId == resourceId)
                .OrderByDescending(grant => grant.GrantedAt)
                .ToList());

        public Task<IReadOnlyList<PermissionGrant>> ListActiveForUserAsync(
            Guid userId,
            string resourceType,
            int limit,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PermissionGrant>>(Store
                .Where(grant => grant.IsActive &&
                                grant.UserId == userId &&
                                grant.ResourceType == resourceType)
                .OrderByDescending(grant => grant.GrantedAt)
                .Take(limit)
                .ToList());

        public Task AddAsync(
            PermissionGrant grant,
            CancellationToken cancellationToken = default)
        {
            Store.Add(grant);
            return Task.CompletedTask;
        }

        public Task SaveAsync(
            PermissionGrant grant,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
