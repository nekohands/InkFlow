using InkFlow.Modules.Content.Application;
using InkFlow.Modules.Content.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class ContentPolicyServiceTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 28, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task Takedown_Is_Recorded_And_Repeated_Command_Is_Idempotent()
    {
        var bookId = Guid.CreateVersion7();
        var repository = new InMemoryPolicyRepository();
        var service = new ContentPolicyService(repository, new FixedClock(T0));

        var applied = await service.TakedownAsync(bookId, "operator-1", "版权方通知");
        var repeated = await service.TakedownAsync(bookId, "operator-2", "重复请求");

        Assert.IsTrue(applied.Changed);
        Assert.IsFalse(repeated.Changed);
        Assert.AreEqual(applied.Decision!.Id, repeated.Decision!.Id);
        Assert.AreEqual(1, repository.Store.Count);
        Assert.IsTrue(await service.IsTakedownAsync(bookId));

        var status = await service.GetStatusAsync(bookId);
        Assert.IsTrue(status.IsTakedown);
        Assert.AreEqual(applied.Decision.Id, status.LatestDecision!.Id);
    }

    [TestMethod]
    public async Task Restore_Appends_A_Decision_And_Reopens_Public_State()
    {
        var bookId = Guid.CreateVersion7();
        var repository = new InMemoryPolicyRepository();
        var service = new ContentPolicyService(repository, new FixedClock(T0));

        await service.TakedownAsync(bookId, "admin-1", "待核实内容");
        var restored = await new ContentPolicyService(
            repository,
            new FixedClock(T0.AddMinutes(1)))
            .RestoreAsync(bookId, "admin-1", "已完成授权核验");

        Assert.IsTrue(restored.Changed);
        Assert.IsFalse(restored.IsTakedown);
        Assert.AreEqual(ContentPolicyAction.Restore, restored.Decision!.Action);
        Assert.AreEqual(2, repository.Store.Count);
        Assert.IsFalse(await service.IsTakedownAsync(bookId));
        Assert.AreEqual(0, (await service.ListAsync(takenDownOnly: true, limit: 10)).Count);
    }

    [TestMethod]
    public async Task Restore_Without_A_Previous_Takedown_Is_A_Noop()
    {
        var repository = new InMemoryPolicyRepository();
        var service = new ContentPolicyService(repository, new FixedClock(T0));

        var result = await service.RestoreAsync(Guid.CreateVersion7(), "admin-1", "确认内容可公开");

        Assert.IsFalse(result.Changed);
        Assert.IsFalse(result.IsTakedown);
        Assert.IsNull(result.Decision);
        Assert.AreEqual(0, repository.Store.Count);
    }

    [TestMethod]
    public void Decision_Rejects_Empty_Or_Overlong_Reason()
    {
        var bookId = Guid.CreateVersion7();

        Assert.ThrowsExactly<ArgumentException>(() => ContentPolicyDecision.Create(
            bookId,
            ContentPolicyAction.Takedown,
            "admin-1",
            "   ",
            T0));

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => ContentPolicyDecision.Create(
            bookId,
            ContentPolicyAction.Takedown,
            "admin-1",
            new string('字', ContentPolicyDecision.MaxReasonLength + 1),
            T0));
    }

    private sealed class InMemoryPolicyRepository : IContentPolicyRepository
    {
        public List<ContentPolicyDecision> Store { get; } = [];

        public Task<ContentPolicyDecision?> GetLatestAsync(
            Guid canonicalBookId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ContentPolicyDecision?>(Store
                .Where(decision => decision.CanonicalBookId == canonicalBookId)
                .OrderByDescending(decision => decision.CreatedAt)
                .ThenByDescending(decision => decision.Id)
                .FirstOrDefault());

        public Task<IReadOnlyList<ContentPolicyDecision>> ListLatestAsync(
            bool takenDownOnly,
            int limit,
            CancellationToken cancellationToken = default)
        {
            var latest = Store
                .GroupBy(decision => decision.CanonicalBookId)
                .Select(group => group
                    .OrderByDescending(decision => decision.CreatedAt)
                    .ThenByDescending(decision => decision.Id)
                    .First())
                .Where(decision => !takenDownOnly || decision.Action == ContentPolicyAction.Takedown)
                .OrderByDescending(decision => decision.CreatedAt)
                .ThenByDescending(decision => decision.Id)
                .Take(limit)
                .ToList();
            return Task.FromResult<IReadOnlyList<ContentPolicyDecision>>(latest);
        }

        public Task AddAsync(
            ContentPolicyDecision decision,
            CancellationToken cancellationToken = default)
        {
            Store.Add(decision);
            return Task.CompletedTask;
        }
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
