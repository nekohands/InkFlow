using InkFlow.Modules.Operations.Application;
using System.Security.Cryptography;
using System.Text.Json;
using InkFlow.Api;
using InkFlow.ConsistencyFixtures;
using InkFlow.Modules.Crawling.Domain;
using InkFlow.Modules.Library.Domain;
using InkFlow.Modules.Sources.Domain;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class ConsistencyCharacterizationTests
{
    [TestMethod]
    public void Host_metadata_tracks_all_domain_enum_values()
    {
        var values = ConsistencyDomainValues.Current;
        Assert.IsTrue(values.SourceCapabilities.SetEquals(Enum.GetValues<SourceCapability>().Select(v => (int)v)));
        Assert.IsTrue(values.SourceHealthStatuses.SetEquals(Enum.GetValues<SourceHealthStatus>().Select(v => (int)v)));
        Assert.IsTrue(values.MatchCandidateStatuses.SetEquals(Enum.GetValues<MatchCandidateStatus>().Select(v => (int)v)));
        Assert.IsTrue(values.CrawlerTaskStatuses.SetEquals(Enum.GetValues<CrawlerTaskStatus>().Select(v => (int)v)));
        Assert.AreEqual((int)CrawlerTaskStatus.DeadLettered, values.DeadLetteredTaskStatus);
    }

    [TestMethod]
    [DataRow(999)]
    [DataRow(1000)]
    [DataRow(1001)]
    public async Task Report_boundary_and_ordinal_sample_are_preserved(int count)
    {
        var snapshot = ConsistencySnapshot.Empty with
        {
            CrawlerTasks = Enumerable.Range(1, count).Reverse().Select(i =>
                new CrawlerTaskSnapshot(ConsistencyFixture.Id(i), "missing", (int)CrawlerTaskStatus.Pending)).ToArray(),
        };
        var report = await new ConsistencyCheckService(new ConsistencyFixture.Reader(snapshot), new ConsistencyFixture.Clock(), ConsistencyDomainValues.Current).CheckAsync();
        Assert.AreEqual(count, report.TotalIssueCount);
        Assert.AreEqual(count > 1000, report.Truncated);
        CollectionAssert.AreEqual(snapshot.CrawlerTasks.Select(t => t.Id.ToString("D")).Order(StringComparer.Ordinal).Take(1000).ToArray(), report.Issues.Select(i => i.ResourceId).ToArray());
    }

    [TestMethod]
    [DataRow(0, false, 0, "4F53CDA18C2BAA0C0354BB5F9A3ECBE5ED12AB4D8E11BA873C2F11161202B945")]
    [DataRow(1000, false, 0, "4F53CDA18C2BAA0C0354BB5F9A3ECBE5ED12AB4D8E11BA873C2F11161202B945")]
    [DataRow(1000, true, 2030, "4EE3B84A11590B77B4116B42477C09F8E486A320B076D0204F4BE22535248815")]
    public async Task Complete_ordered_issues_match_pre_refactor_baseline(int size, bool corrupt, int count, string hash)
    {
        var snapshot = ConsistencyFixture.Create(size, corrupt);
        var issues = ConsistencyCheckValidator.Validate(snapshot, ConsistencyDomainValues.Current);
        Assert.AreEqual(hash, Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(issues))));
        var report = await new ConsistencyCheckService(new ConsistencyFixture.Reader(snapshot), new ConsistencyFixture.Clock(), ConsistencyDomainValues.Current).CheckAsync();
        Assert.AreEqual(ConsistencyFixture.Now, report.CheckedAt);
        Assert.AreEqual(count, report.TotalIssueCount);
        Assert.AreEqual(Math.Min(count, 1000), report.ReturnedIssueCount);
        Assert.AreEqual(count > 1000, report.Truncated);
        Assert.AreEqual(count == 0, report.IsHealthy);
        Assert.AreEqual(count == 0 ? "healthy" : "issues_found", report.Status);
        CollectionAssert.AreEqual(issues.Take(1000).ToArray(), report.Issues.ToArray());
    }

    [TestMethod]
    public async Task Late_issues_and_ties_preserve_complete_count_and_full_sort_sample()
    {
        var snapshot = ConsistencyFixture.Create(10000, true);
        // Reverse arrival order and repeat an issue at the sample boundary.
        snapshot = snapshot with
        {
            CrawlerTasks = snapshot.CrawlerTasks.Reverse().Concat(snapshot.CrawlerTasks.Take(2)).ToArray(),
            ContentVersions = snapshot.ContentVersions.Reverse().ToArray(),
        };
        var all = ConsistencyCheckValidator.Validate(snapshot, ConsistencyDomainValues.Current);
        var report = await new ConsistencyCheckService(new ConsistencyFixture.Reader(snapshot), new ConsistencyFixture.Clock(), ConsistencyDomainValues.Current).CheckAsync();
        Assert.IsTrue(all.Count > 1000);
        Assert.AreEqual(all.Count, report.TotalIssueCount);
        CollectionAssert.AreEqual(all.Take(1000).ToArray(), report.Issues.ToArray());
    }

    [TestMethod]
    public async Task Duplicate_lookup_keys_keep_first_row_and_resolve_late_references()
    {
        var snapshot = ConsistencyFixture.Create(1000, false);
        var first = snapshot.CanonicalChapters[0];
        // Conflicting duplicate is last: references must still resolve to the first parent.
        snapshot = snapshot with
        {
            CanonicalChapters = snapshot.CanonicalChapters.Append(first with { BookId = ConsistencyFixture.Id(-1) }).ToArray(),
            Sources = snapshot.Sources.Concat(snapshot.Sources).ToArray(),
            SourceChapters = snapshot.SourceChapters.Reverse().ToArray(),
        };
        var report = await new ConsistencyCheckService(new ConsistencyFixture.Reader(snapshot), new ConsistencyFixture.Clock(), ConsistencyDomainValues.Current).CheckAsync();
        Assert.AreEqual(1, report.TotalIssueCount);
        Assert.AreEqual("canonical_chapter_book_missing", report.Issues.Single().Code);
    }

    [TestMethod]
    public async Task Cancellation_is_forwarded_to_reader_and_no_report_is_returned()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var service = new ConsistencyCheckService(new ConsistencyFixture.Reader(ConsistencySnapshot.Empty), new ConsistencyFixture.Clock(), ConsistencyDomainValues.Current);
        var error = await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => service.CheckAsync(cancellation.Token));
        Assert.AreEqual(cancellation.Token, error.CancellationToken);
    }
}
