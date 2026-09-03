using InkFlow.Modules.Sources.Application;
using InkFlow.Modules.Sources.Domain;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class SourceLifecycleServiceTests
{
    private static readonly DateTimeOffset T0 =
        new(2026, 9, 3, 10, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task Disable_And_Enable_Persist_Source_State()
    {
        var source = Source.Create("source", "来源", "https://source.example", T0);
        var repository = new InMemorySourceRepository(source);
        var service = new SourceLifecycleService(repository, new FixedClock(T0.AddMinutes(1)));

        var disabled = await service.SetEnabledAsync("source", enabled: false);

        Assert.IsNotNull(disabled);
        Assert.IsFalse(disabled!.IsEnabled);
        Assert.AreEqual(1, repository.SaveCalls);

        var enabled = await service.SetEnabledAsync("source", enabled: true);

        Assert.IsNotNull(enabled);
        Assert.IsTrue(enabled!.IsEnabled);
        Assert.AreEqual(2, repository.SaveCalls);
    }

    private sealed class InMemorySourceRepository(Source source) : ISourceRepository
    {
        private Source Current { get; set; } = source;
        public int SaveCalls { get; private set; }

        public Task AddAsync(Source value, CancellationToken cancellationToken = default)
        {
            Current = value;
            return Task.CompletedTask;
        }

        public Task<Source?> GetAsync(string sourceId, CancellationToken cancellationToken = default) =>
            Task.FromResult<Source?>(Current.Id == sourceId ? Current : null);

        public Task<IReadOnlyList<Source>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Source>>([Current]);

        public Task SaveAsync(Source value, CancellationToken cancellationToken = default)
        {
            SaveCalls++;
            Current = value;
            return Task.CompletedTask;
        }
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
