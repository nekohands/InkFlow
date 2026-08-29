using InkFlow.Modules.Sources.Application;
using InkFlow.Modules.Sources.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class SourceCredentialBindingServiceTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 30, 10, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task Set_Updates_Source_And_Persists_Only_The_Reference()
    {
        var source = Source.Create(
            "official-a",
            "Official",
            "https://books.example.com",
            T0);
        source.SetDefaultCredentialReference("legacy-reader", T0);
        var repository = new InMemorySourceRepository(source);
        var service = new SourceCredentialBindingService(repository, new FixedClock(T0.AddMinutes(1)));

        var result = await service.SetDefaultAsync("official-a", "platform-reader");

        Assert.AreEqual(SourceCredentialBindingResultStatus.Updated, result.Status);
        Assert.AreEqual("official-a", result.SourceId);
        Assert.AreEqual("platform-reader", result.CredentialReferenceId);
        Assert.AreEqual("platform-reader", source.DefaultCredentialReferenceId);
        Assert.AreEqual(1, repository.SaveCalls);
        Assert.AreEqual(T0.AddMinutes(1), source.UpdatedAt);
    }

    [TestMethod]
    public async Task Clear_Removes_The_Default_Reference()
    {
        var source = Source.Create(
            "official-a",
            "Official",
            "https://books.example.com",
            T0);
        source.SetDefaultCredentialReference("platform-reader", T0);
        var repository = new InMemorySourceRepository(source);
        var service = new SourceCredentialBindingService(repository, new FixedClock(T0.AddMinutes(1)));

        var result = await service.SetDefaultAsync("official-a", null);

        Assert.AreEqual(SourceCredentialBindingResultStatus.Cleared, result.Status);
        Assert.IsNull(result.CredentialReferenceId);
        Assert.IsNull(source.DefaultCredentialReferenceId);
        Assert.AreEqual(1, repository.SaveCalls);
    }

    [TestMethod]
    public async Task Invalid_Reference_Is_Rejected_Without_Read_Or_Save()
    {
        var repository = new InMemorySourceRepository(null);
        var service = new SourceCredentialBindingService(repository, new FixedClock(T0));

        var result = await service.SetDefaultAsync("official-a", "../secret");

        Assert.AreEqual(SourceCredentialBindingResultStatus.InvalidRequest, result.Status);
        Assert.AreEqual(0, repository.GetCalls);
        Assert.AreEqual(0, repository.SaveCalls);
    }

    [TestMethod]
    public async Task Missing_Source_Returns_NotFound_Without_Save()
    {
        var repository = new InMemorySourceRepository(null);
        var service = new SourceCredentialBindingService(repository, new FixedClock(T0));

        var result = await service.SetDefaultAsync("missing-source", "platform-reader");

        Assert.AreEqual(SourceCredentialBindingResultStatus.SourceNotFound, result.Status);
        Assert.AreEqual("missing-source", result.SourceId);
        Assert.AreEqual(1, repository.GetCalls);
        Assert.AreEqual(0, repository.SaveCalls);
    }

    private sealed class InMemorySourceRepository(Source? current) : ISourceRepository
    {
        public Source? Current { get; private set; } = current;
        public int GetCalls { get; private set; }
        public int SaveCalls { get; private set; }

        public Task AddAsync(Source source, CancellationToken cancellationToken = default)
        {
            Current = source;
            return Task.CompletedTask;
        }

        public Task<Source?> GetAsync(
            string sourceId,
            CancellationToken cancellationToken = default)
        {
            GetCalls++;
            return Task.FromResult(
                Current is not null && Current.Id == sourceId ? Current : null);
        }

        public Task<IReadOnlyList<Source>> ListAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Source>>(
                Current is null ? [] : [Current]);

        public Task SaveAsync(Source source, CancellationToken cancellationToken = default)
        {
            SaveCalls++;
            Current = source;
            return Task.CompletedTask;
        }
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
