using InkFlow.Modules.Content.Application;
using InkFlow.Modules.Content.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class ContentPublishingServiceTests
{
    private sealed class InMemoryVersionRepository : IContentVersionRepository
    {
        public List<ContentVersion> Store { get; } = [];
        public int SetCurrentCalls { get; private set; }
        public Guid? CurrentVersionId { get; private set; }

        public Task AddAsync(ContentVersion version, CancellationToken cancellationToken = default)
        {
            Store.Add(version);
            return Task.CompletedTask;
        }

        public Task<ContentVersion?> FindByHashAsync(Guid canonicalChapterId, string canonicalHash, CancellationToken cancellationToken = default)
            => Task.FromResult<ContentVersion?>(
                Store.FirstOrDefault(v => v.CanonicalChapterId == canonicalChapterId && v.CanonicalHash == canonicalHash));

        public Task<ContentVersion?> GetCurrentForChapterAsync(Guid canonicalChapterId, CancellationToken cancellationToken = default)
            => Task.FromResult<ContentVersion?>(
                Store.FirstOrDefault(v => v.CanonicalChapterId == canonicalChapterId && v.IsCurrent));

        public Task<Guid?> GetCurrentCanonicalBookIdAsync(Guid canonicalChapterId, CancellationToken cancellationToken = default)
            => Task.FromResult<Guid?>(Store.FirstOrDefault(v =>
                v.CanonicalChapterId == canonicalChapterId && v.IsCurrent)?.CanonicalBookId);

        public Task<IReadOnlyList<ContentVersion>> ListForChapterAsync(Guid canonicalChapterId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ContentVersion>>(
                Store.Where(v => v.CanonicalChapterId == canonicalChapterId).ToList());

        public Task SetCurrentAsync(Guid chapterId, Guid versionId, CancellationToken cancellationToken = default)
        {
            SetCurrentCalls++;
            CurrentVersionId = versionId;
            return Task.CompletedTask;
        }
    }

    [TestMethod]
    public async Task First_Publish_Persists_Version_And_Sets_Current()
    {
        var repo = new InMemoryVersionRepository();
        var service = new ContentPublishingService(repo);

        var outcome = await service.PublishAsync(
            Guid.NewGuid(), Guid.NewGuid(), "example-source", "<p>第一章正文</p><p>更多内容</p>");

        Assert.IsTrue(outcome.IsSuccess);
        Assert.IsFalse(outcome.Unchanged);
        Assert.AreEqual(1, repo.Store.Count);
        Assert.AreEqual(1, repo.SetCurrentCalls);
        Assert.AreEqual(QualityEngine.AlgorithmVersion, outcome.Version!.QualityAlgorithmVersion);
        StringAssert.Contains(outcome.Version.QualityEvidence, "paragraphs=2");
    }

    [TestMethod]
    public async Task Same_Normalized_Content_Is_Unchanged_Even_If_Markup_Differs()
    {
        var repo = new InMemoryVersionRepository();
        var service = new ContentPublishingService(repo);
        var chapterId = Guid.NewGuid();

        await service.PublishAsync(Guid.NewGuid(), chapterId, "src", "<p>第一段</p><p>第二段</p>");
        var second = await service.PublishAsync(
            Guid.NewGuid(), chapterId, "src", "<div>\n第一段\n\n第二段\n</div>");

        Assert.IsTrue(second.IsSuccess);
        Assert.IsTrue(second.Unchanged);
        Assert.AreEqual(1, repo.Store.Count);
    }

    [TestMethod]
    public async Task Higher_Quality_Newer_Version_Becomes_Current()
    {
        var repo = new InMemoryVersionRepository();
        var service = new ContentPublishingService(repo);
        var chapterId = Guid.NewGuid();
        var bookId = Guid.NewGuid();

        // 先发布低质量版本(单段、极短)
        await service.PublishAsync(bookId, chapterId, "src-a", "短文本。");
        // 再发布高质量版本
        await service.PublishAsync(
            bookId, chapterId, "src-b",
            "<p>这是修订后的完整正文,包含多个段落与足够的内容长度。</p><p>第二段提供了更多有效信息。</p><p>第三段进一步丰富了正文内容。</p>");

        Assert.AreEqual(2, repo.Store.Count);
        Assert.AreEqual(repo.Store[1].Id, repo.CurrentVersionId, "质量更高的新版本应成为当前版本");
    }

    [TestMethod]
    public async Task Empty_Normalized_Content_Fails_Clearly()
    {
        var repo = new InMemoryVersionRepository();
        var service = new ContentPublishingService(repo);

        var outcome = await service.PublishAsync(Guid.NewGuid(), Guid.NewGuid(), "src", "<div></div>");

        Assert.IsFalse(outcome.IsSuccess);
        StringAssert.Contains(outcome.Errors[0], "empty document");
        Assert.AreEqual(0, repo.Store.Count);
    }
}
