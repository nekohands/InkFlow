using InkFlow.Modules.Sources.Application;
using InkFlow.Modules.Sources.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.UnitTests;

/// <summary>
/// 主动巡检式健康探测验证:仅探冷却期满的 Unhealthy 能力;Search 连通即成功、
/// 异常即失败;Toc 以首本导入书为样本(无样本静默跳过、空目录计失败);
/// 探针成败经真实 SourceHealthService 上报,由健康聚合裁定恢复与退避。
/// 全部内存执行。
/// </summary>
[TestClass]
public sealed class HealthProbeServiceTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 29, 14, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task Due_Unhealthy_Search_Probe_Recovers_On_Connectivity()
    {
        var harness = CreateHarness(
            new[]
            {
                (("probe-src", SourceCapability.Search), T0),
                (("probe-src", SourceCapability.Toc), T0),
            });
        harness.Clock.Now = T0.AddMinutes(30);

        var results = await harness.Service.ProbeDueAsync();

        // Search 恒可探且连通即成功;Toc 因无导入样本静默跳过,不产生结果行。
        var result = results.Single(r => r.Capability == SourceCapability.Search);
        Assert.AreEqual("probe-src", result.SourceId);
        Assert.IsTrue(result.Recovered);
        Assert.IsNull(result.FailureReason);

        var search = harness.HealthRepo.Store.Single(h => h.Capability == SourceCapability.Search);
        Assert.AreEqual(SourceHealthStatus.Healthy, search.Status, "探针成功应恢复 Healthy");
        Assert.AreEqual(0, search.ConsecutiveFailures);

        var toc = harness.HealthRepo.Store.Single(h => h.Capability == SourceCapability.Toc);
        Assert.AreEqual(SourceHealthStatus.Unhealthy, toc.Status,
            "无样本的 Toc 不得被误报成功或失败");
        Assert.AreEqual(1, results.Count, "静默跳过的能力不出现在结果里");
        Assert.AreEqual(1, harness.Adapter.CallCount, "只有 Search 被真实触达");
    }

    [TestMethod]
    public async Task Search_Probe_Failure_Renews_Cooldown_With_Depth()
    {
        var harness = CreateHarness(
            (("probe-src", SourceCapability.Search), T0),
            searchThrows: new HttpRequestException("connection refused"));
        harness.Clock.Now = T0.AddMinutes(30);

        await harness.Service.ProbeDueAsync();

        var health = harness.HealthRepo.Store.Single();
        Assert.AreEqual(SourceHealthStatus.Unhealthy, health.Status);
        Assert.AreEqual(4, health.ConsecutiveFailures, "探针失败计入失败深度");
        StringAssert.Contains(health.LastFailureReason!, "probe:");
        StringAssert.Contains(health.LastFailureReason!, "connection refused");

        // 失败深度 4 → 冷却翻倍至 60 分钟:锚点后 59 分钟的下一次巡检不得触达。
        harness.Clock.Now = health.UpdatedAt.AddMinutes(59);
        await harness.Service.ProbeDueAsync();
        Assert.AreEqual(1, harness.Adapter.CallCount);

        // 到期后的下一次巡检才会重新探测(CallCount 增加)。
        harness.Clock.Now = health.UpdatedAt.AddMinutes(60);
        await harness.Service.ProbeDueAsync();
        Assert.AreEqual(2, harness.Adapter.CallCount,
            "翻倍后的冷却同样会在期满时放行下一次探针");
    }

    [TestMethod]
    public void Probe_Cooldown_Ladder_Caps_At_One_Day()
    {
        Assert.AreEqual(TimeSpan.FromMinutes(30), SourceHealthPolicy.ProbeCooldown(3));
        Assert.AreEqual(TimeSpan.FromMinutes(60), SourceHealthPolicy.ProbeCooldown(4));
        Assert.AreEqual(TimeSpan.FromDays(1), SourceHealthPolicy.ProbeCooldown(12));
    }

    [TestMethod]
    public async Task Toc_Probe_Uses_First_Sample_Book_And_Empty_Toc_Counts_As_Failure()
    {
        var sourceBooks = new InMemorySourceBooks();
        sourceBooks.Add("probe-src", "book-7");
        sourceBooks.Add("probe-src", "book-8");
        var harness = CreateHarness(
            new[] { (("probe-src", SourceCapability.Toc), T0) },
            sourceBooks: sourceBooks,
            tocEntries: []);
        harness.Clock.Now = T0.AddMinutes(30);

        var results = await harness.Service.ProbeDueAsync();

        var result = results.Single();
        Assert.IsFalse(result.Recovered);
        StringAssert.Contains(result.FailureReason!, "empty-toc");

        var health = harness.HealthRepo.Store.Single();
        Assert.AreEqual(SourceHealthStatus.Unhealthy, health.Status);
        Assert.AreEqual(4, health.ConsecutiveFailures);
    }

    [TestMethod]
    public async Task Toc_Probe_NonEmpty_Toc_Recovers()
    {
        var sourceBooks = new InMemorySourceBooks();
        sourceBooks.Add("probe-src", "book-7");
        var harness = CreateHarness(
            new[] { (("probe-src", SourceCapability.Toc), T0) },
            sourceBooks: sourceBooks,
            tocEntries: [new SourceTocEntry("c-1", 0, "第一章")]);
        harness.Clock.Now = T0.AddMinutes(30);

        var results = await harness.Service.ProbeDueAsync();

        Assert.IsTrue(results.Single().Recovered);
        Assert.AreEqual(SourceHealthStatus.Healthy,
            harness.HealthRepo.Store.Single().Status);
    }

    [TestMethod]
    public async Task Not_Due_Sources_Are_Not_Touched()
    {
        // Unhealthy 但冷却未满(anchor=T0+5m,now=T0+10m):不应触达适配器。
        var harness = CreateHarness(
            (("probe-src", SourceCapability.Search), T0.AddMinutes(5)));
        harness.Clock.Now = T0.AddMinutes(10);

        var results = await harness.Service.ProbeDueAsync();

        Assert.AreEqual(0, results.Count);
        Assert.AreEqual(0, harness.Adapter.CallCount, "冷却期内不得触发探针");
    }

    private static Harness CreateHarness(
        ((string SourceId, SourceCapability Capability), DateTimeOffset anchor) unhealthyRow,
        Exception? searchThrows = null,
        InMemorySourceBooks? sourceBooks = null,
        IReadOnlyList<SourceTocEntry>? tocEntries = null)
    {
        return CreateHarness([unhealthyRow], searchThrows, sourceBooks, tocEntries);
    }

    private static Harness CreateHarness(
        ((string SourceId, SourceCapability Capability), DateTimeOffset anchor)[] unhealthyRows,
        Exception? searchThrows = null,
        InMemorySourceBooks? sourceBooks = null,
        IReadOnlyList<SourceTocEntry>? tocEntries = null)
    {
        sourceBooks ??= new InMemorySourceBooks();

        var healthRepo = new InMemoryHealthRepository();
        foreach (var ((sourceId, capability), anchor) in unhealthyRows)
        {
            var health = SourceCapabilityHealth.Create(sourceId, capability, anchor);
            for (var i = 0; i < SourceHealthPolicy.UnhealthyAfterConsecutiveFailures; i++)
            {
                health.RecordFailure("seeded-failure", anchor);
            }

            healthRepo.Store.Add(health);
        }

        string[] registeredIds = [.. unhealthyRows.Select(u => u.Item1.SourceId).Distinct()];
        var adapters = registeredIds.ToDictionary<string, string, ISourceAdapter>(
            id => id,
            _ => new ProbeAdapter(searchThrows, tocEntries ?? []));

        var factory = new KeyedAdapterFactory(adapters);
        var clock = new MutableClock(T0);

        var recorder = new SourceHealthService(healthRepo, clock);
        var service = new HealthProbeService(healthRepo, recorder, factory, sourceBooks, clock);

        return new Harness(service, healthRepo, clock,
            (ProbeAdapter)adapters.Values.Single());
    }

    private sealed record Harness(
        HealthProbeService Service,
        InMemoryHealthRepository HealthRepo,
        MutableClock Clock,
        ProbeAdapter Adapter);

    /// <summary>时间可推进的时钟:巡检门控与上报共用同一时间线。</summary>
    private sealed class MutableClock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public DateTimeOffset Now
        {
            get => _now;
            set => _now = value;
        }

        public override DateTimeOffset GetUtcNow() => _now;
    }

    /// <summary>单源探针适配器替身:Search 与 Toc 行为均可编程。</summary>
    private sealed class ProbeAdapter(
        Exception? searchThrows,
        IReadOnlyList<SourceTocEntry> tocEntries) : ISourceAdapter
    {
        public string SourceId => "probe-src";

        public int CallCount { get; private set; }

        public Task<IReadOnlyList<SourceSearchResult>> SearchAsync(
            string keyword,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            if (searchThrows is not null)
            {
                throw searchThrows;
            }

            return Task.FromResult<IReadOnlyList<SourceSearchResult>>([]);
        }

        public Task<SourceBookInfo?> GetBookInfoAsync(
            string externalBookId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<SourceBookInfo?>(null);

        public Task<IReadOnlyList<SourceTocEntry>> GetTableOfContentsAsync(
            string externalBookId,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            if (searchThrows is not null)
            {
                throw searchThrows;
            }

            return Task.FromResult(tocEntries);
        }

        public Task<string?> GetChapterContentAsync(
            string externalChapterId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);
    }

    private sealed class KeyedAdapterFactory(
        IReadOnlyDictionary<string, ISourceAdapter> adapters) : ISourceAdapterFactory
    {
        public Task<ISourceAdapter?> GetAdapterAsync(
            string sourceId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(adapters.TryGetValue(sourceId, out var adapter) ? adapter : null);
    }

    private sealed class InMemorySourceBooks : ISourceBookRepository
    {
        private readonly Dictionary<(string SourceId, string ExternalId), SourceBook> _store = [];

        public void Add(string sourceId, string externalBookId)
        {
            var book = SourceBook.Create(sourceId, externalBookId, $"书 {externalBookId}",
                "作者", T0);
            _store[(sourceId, externalBookId)] = book;
        }

        public Task AddAsync(SourceBook book, CancellationToken cancellationToken = default)
        {
            _store[(book.SourceId, book.ExternalBookId)] = book;
            return Task.CompletedTask;
        }

        public Task<SourceBook?> GetAsync(
            string sourceId,
            string externalBookId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_store.TryGetValue((sourceId, externalBookId), out var book) ? book : null);

        public Task<IReadOnlyList<SourceBook>> ListAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SourceBook>>(_store.Values.ToList());

        public Task SaveAsync(SourceBook book, CancellationToken cancellationToken = default)
        {
            _store[(book.SourceId, book.ExternalBookId)] = book;
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryHealthRepository : ISourceHealthRepository
    {
        public List<SourceCapabilityHealth> Store { get; } = [];

        public Task<SourceCapabilityHealth?> GetAsync(
            string sourceId,
            SourceCapability capability,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<SourceCapabilityHealth?>(Store.SingleOrDefault(h =>
                h.SourceId == sourceId && h.Capability == capability));

        public Task AddAsync(
            SourceCapabilityHealth health,
            CancellationToken cancellationToken = default)
        {
            Store.Add(health);
            return Task.CompletedTask;
        }

        public Task SaveAsync(
            SourceCapabilityHealth health,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<SourceCapabilityHealth>> ListForSourceAsync(
            string sourceId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SourceCapabilityHealth>>(
                Store.Where(h => h.SourceId == sourceId).ToList());

        public Task<IReadOnlyList<SourceCapabilityHealth>> ListUnhealthyAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SourceCapabilityHealth>>(
                Store.Where(h => h.Status == SourceHealthStatus.Unhealthy).ToList());
    }
}
