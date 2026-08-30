using InkFlow.Modules.Content.Application;
using InkFlow.Modules.Content.Domain;
using InkFlow.Modules.Sources.Application;
using InkFlow.Modules.Sources.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class ContentSelectionServiceTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 27, 19, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task Unavailable_High_Quality_Source_Is_Excluded_And_Healthy_Candidate_Is_Selected()
    {
        var chapterId = Guid.NewGuid();
        var bookId = Guid.NewGuid();
        var good = NewVersion(bookId, chapterId, "official-a", RichContent(), T0);
        var fallback = NewVersion(bookId, chapterId, "official-b", "截断正文。", T0.AddMinutes(1));
        var versions = new InMemoryVersionRepository(good, fallback);
        await versions.SetCurrentAsync(chapterId, good.Id);

        var health = new InMemoryHealthReader("official-a");
        var decisions = new InMemoryDecisionRepository();
        var service = new ContentSelectionService(
            versions, health, decisions, new FixedClock(T0.AddMinutes(2)));

        var outcome = await service.SelectCurrentAsync(chapterId);

        Assert.IsTrue(outcome.IsSuccess);
        Assert.IsTrue(outcome.Changed);
        Assert.IsFalse(outcome.UsedFallback);
        Assert.AreEqual(fallback.Id, outcome.SelectedVersion!.Id);
        Assert.AreEqual(fallback.Id, (await versions.GetCurrentForChapterAsync(chapterId))!.Id);
        StringAssert.Contains(outcome.Evidence, "excludedSources=official-a");
        StringAssert.Contains(outcome.Evidence, "fallback=False");
        Assert.AreEqual(1, decisions.Store.Count);
        Assert.AreEqual(fallback.Id, decisions.Store[0].SelectedVersionId);
    }

    [TestMethod]
    public async Task All_Sources_Unavailable_Preserves_The_Existing_Current_Version()
    {
        var chapterId = Guid.NewGuid();
        var bookId = Guid.NewGuid();
        var first = NewVersion(bookId, chapterId, "official-a", RichContent(), T0);
        var second = NewVersion(bookId, chapterId, "official-b", "另一个正文。", T0.AddMinutes(1));
        var versions = new InMemoryVersionRepository(first, second);
        await versions.SetCurrentAsync(chapterId, first.Id);

        var service = new ContentSelectionService(
            versions,
            new InMemoryHealthReader("official-a", "official-b"),
            new InMemoryDecisionRepository(),
            new FixedClock(T0.AddMinutes(2)));

        var outcome = await service.SelectCurrentAsync(chapterId);

        Assert.IsTrue(outcome.IsSuccess);
        Assert.IsFalse(outcome.Changed);
        Assert.IsTrue(outcome.UsedFallback);
        Assert.AreEqual(first.Id, outcome.SelectedVersion!.Id);
        StringAssert.Contains(outcome.Evidence, "fallback=True");
    }

    private static ContentVersion NewVersion(
        Guid bookId,
        Guid chapterId,
        string sourceId,
        string rawContent,
        DateTimeOffset createdAt)
    {
        var document = ContentNormalizer.Normalize(rawContent);
        return ContentVersion.Create(bookId, chapterId, sourceId, document, createdAt);
    }

    private static string RichContent() =>
        $"<p>{new string('字', 120)}</p>" +
        $"<p>{new string('字', 120)}</p>" +
        $"<p>{new string('字', 120)}</p>";

    private sealed class InMemoryHealthReader(params string[] unavailable) : ISourceHealthReader
    {
        private readonly HashSet<string> _unavailable = unavailable.ToHashSet(StringComparer.Ordinal);

        public Task<bool> IsAvailableAsync(
            string sourceId,
            SourceCapability capability,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(!_unavailable.Contains(sourceId));
    }

    private sealed class InMemoryDecisionRepository : IContentSelectionDecisionRepository
    {
        public List<ContentSelectionDecision> Store { get; } = [];

        public Task AddAsync(
            ContentSelectionDecision decision,
            CancellationToken cancellationToken = default)
        {
            Store.Add(decision);
            return Task.CompletedTask;
        }

        public Task<ContentSelectionDecision?> GetLatestAsync(
            Guid canonicalChapterId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ContentSelectionDecision?>(Store
                .Where(decision => decision.CanonicalChapterId == canonicalChapterId)
                .OrderByDescending(decision => decision.CreatedAt)
                .FirstOrDefault());
    }

    private sealed class InMemoryVersionRepository(params ContentVersion[] initial) : IContentVersionRepository
    {
        public List<ContentVersion> Store { get; } = [.. initial];

        public Task AddAsync(ContentVersion version, CancellationToken cancellationToken = default)
        {
            Store.Add(version);
            return Task.CompletedTask;
        }

        public Task<ContentVersion?> FindByHashAsync(
            Guid canonicalChapterId,
            string canonicalHash,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ContentVersion?>(Store.FirstOrDefault(version =>
                version.CanonicalChapterId == canonicalChapterId && version.CanonicalHash == canonicalHash));

        public Task<IReadOnlyList<ContentVersion>> ListForChapterAsync(
            Guid canonicalChapterId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ContentVersion>>(Store
                .Where(version => version.CanonicalChapterId == canonicalChapterId)
                .ToList());

        public Task<ContentVersion?> GetCurrentForChapterAsync(
            Guid canonicalChapterId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ContentVersion?>(Store.SingleOrDefault(version =>
                version.CanonicalChapterId == canonicalChapterId && version.IsCurrent));

        public Task<IReadOnlyList<ContentVersion>> ListCurrentForBookAsync(
            Guid canonicalBookId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ContentVersion>>(Store
                .Where(version => version.CanonicalBookId == canonicalBookId && version.IsCurrent)
                .ToList());

        public Task<Guid?> GetCurrentCanonicalBookIdAsync(
            Guid canonicalChapterId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Guid?>(Store.SingleOrDefault(version =>
                version.CanonicalChapterId == canonicalChapterId && version.IsCurrent)?.CanonicalBookId);

        public Task SetCurrentAsync(
            Guid chapterId,
            Guid versionId,
            CancellationToken cancellationToken = default)
        {
            for (var i = 0; i < Store.Count; i++)
            {
                var version = Store[i];
                Store[i] = ContentVersion.Rehydrate(
                    version.Id,
                    version.CanonicalBookId,
                    version.CanonicalChapterId,
                    version.SourceId,
                    version.CanonicalHash,
                    version.CanonicalText,
                    version.ParagraphCount,
                    version.QualityScore,
                    version.Id == versionId,
                    version.CreatedAt,
                    version.QualityAlgorithmVersion,
                    version.QualityEvidence);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
