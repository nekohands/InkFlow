namespace InkFlow.Modules.Operations.Application;

/// <summary>Pure read-only validation with complete, ordinally sorted issues.</summary>
public static class ConsistencyCheckValidator
{
    public static IReadOnlyList<ConsistencyIssue> Validate(ConsistencySnapshot snapshot, ConsistencyValueDefinitions values)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(values);
        return new ConsistencyRuleEvaluation(snapshot, values).Run();
    }
}

internal sealed partial class ConsistencyRuleEvaluation(ConsistencySnapshot snapshot, ConsistencyValueDefinitions values)
{
    private readonly List<ConsistencyIssue> issues = [];
    private readonly Dictionary<Guid, CanonicalBookSnapshot> canonicalBooks = FirstById(snapshot.CanonicalBooks, book => book.Id);
    private readonly Dictionary<Guid, CanonicalChapterSnapshot> canonicalChapters = FirstById(snapshot.CanonicalChapters, chapter => chapter.Id);
    private readonly Dictionary<string, SourceSnapshot> sources = snapshot.Sources
            .GroupBy(source => source.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
    private readonly Dictionary<Guid, SourceBookSnapshot> sourceBooks = FirstById(snapshot.SourceBooks, book => book.Id);
    private readonly Dictionary<SourceBookIdentity, SourceBookSnapshot> sourceBooksByIdentity = snapshot.SourceBooks
            .GroupBy(book => new SourceBookIdentity(book.SourceId, book.ExternalBookId))
            .ToDictionary(group => group.Key, group => group.First());
    private readonly Dictionary<Guid, SourceChapterSnapshot> sourceChapters = FirstById(snapshot.SourceChapters, chapter => chapter.Id);
    private readonly Dictionary<SourceChapterIdentity, SourceChapterSnapshot> sourceChaptersByIdentity = snapshot.SourceChapters
            .GroupBy(chapter => new SourceChapterIdentity(chapter.SourceBookId, chapter.ExternalChapterId))
            .ToDictionary(group => group.Key, group => group.First());
    private readonly Dictionary<Guid, ContentVersionSnapshot> contentVersions = FirstById(snapshot.ContentVersions, version => version.Id);
    private readonly Dictionary<Guid, CrawlerTaskSnapshot> crawlerTasks = FirstById(snapshot.CrawlerTasks, task => task.Id);

    public IReadOnlyList<ConsistencyIssue> Run()
    {
        CheckSources();
        CheckLibrary();
        CheckContent();
        CheckCrawling();
        return issues
            .OrderBy(issue => issue.Code, StringComparer.Ordinal)
            .ThenBy(issue => issue.ResourceType, StringComparer.Ordinal)
            .ThenBy(issue => issue.ResourceId, StringComparer.Ordinal)
            .ThenBy(issue => issue.Message, StringComparer.Ordinal)
            .ToList();
    }

    private void Add(string code, string resourceType, string resourceId, string message) =>
        issues.Add(ConsistencyIssue.Error(code, resourceType, resourceId, message));

    private static Dictionary<Guid, T> FirstById<T>(
        IEnumerable<T> values,
        Func<T, Guid> idSelector) =>
        values
            .GroupBy(idSelector)
            .ToDictionary(group => group.Key, group => group.First());

    private static string SourceBookId(string sourceId, string externalBookId) =>
        $"{sourceId}/{externalBookId}";

    private static string HealthId(SourceCapabilityHealthSnapshot health) =>
        $"{health.SourceId}/{health.Capability}";

    private readonly record struct SourceBookIdentity(string SourceId, string ExternalBookId)
    {
        public override string ToString() => $"{SourceId}/{ExternalBookId}";
    }

    private readonly record struct SourceChapterIdentity(Guid SourceBookId, string ExternalChapterId)
    {
        public override string ToString() => $"{SourceBookId:D}/{ExternalChapterId}";
    }

    private readonly record struct SourceChapterIndexIdentity(Guid SourceBookId, int Index)
    {
        public override string ToString() => $"{SourceBookId:D}/{Index}";
    }

    private readonly record struct CanonicalChapterIndexIdentity(Guid BookId, int Index)
    {
        public override string ToString() => $"{BookId:D}/{Index}";
    }

    private readonly record struct MappingIdentity(string SourceId, string ExternalChapterId)
    {
        public override string ToString() => $"{SourceId}/{ExternalChapterId}";
    }

    private readonly record struct ContentIdentity(Guid CanonicalChapterId, string CanonicalHash)
    {
        public override string ToString() => $"{CanonicalChapterId:D}/{CanonicalHash}";
    }
}
