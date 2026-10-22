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

internal sealed partial class ConsistencyRuleEvaluation(
    ConsistencySnapshot snapshot, ConsistencyValueDefinitions values, int? maxReturnedIssues = null)
{
    private readonly List<ConsistencyIssue>? issues = maxReturnedIssues is null ? [] : null;
    // Largest retained issue is at the root, so later smaller issues replace it.
    private readonly PriorityQueue<ConsistencyIssue, ConsistencyIssue> sample = new(
        Comparer<ConsistencyIssue>.Create((left, right) => CompareIssues(right, left)));
    public int TotalIssueCount { get; private set; }
    private readonly Dictionary<Guid, CanonicalBookSnapshot> canonicalBooks = FirstByKey(snapshot.CanonicalBooks, book => book.Id);
    private readonly Dictionary<Guid, CanonicalChapterSnapshot> canonicalChapters = FirstByKey(snapshot.CanonicalChapters, chapter => chapter.Id);
    private readonly Dictionary<string, SourceSnapshot> sources = FirstByKey(snapshot.Sources, source => source.Id, StringComparer.Ordinal);
    private readonly Dictionary<Guid, SourceBookSnapshot> sourceBooks = FirstByKey(snapshot.SourceBooks, book => book.Id);
    private readonly Dictionary<SourceBookIdentity, SourceBookSnapshot> sourceBooksByIdentity = FirstByKey(snapshot.SourceBooks, book => new SourceBookIdentity(book.SourceId, book.ExternalBookId));
    private readonly Dictionary<Guid, SourceChapterSnapshot> sourceChapters = FirstByKey(snapshot.SourceChapters, chapter => chapter.Id);
    private readonly Dictionary<SourceChapterIdentity, SourceChapterSnapshot> sourceChaptersByIdentity = FirstByKey(snapshot.SourceChapters, chapter => new SourceChapterIdentity(chapter.SourceBookId, chapter.ExternalChapterId));
    private readonly Dictionary<Guid, ContentVersionSnapshot> contentVersions = FirstByKey(snapshot.ContentVersions, version => version.Id);
    private readonly Dictionary<Guid, CrawlerTaskSnapshot> crawlerTasks = FirstByKey(snapshot.CrawlerTasks, task => task.Id);

    public IReadOnlyList<ConsistencyIssue> Run()
    {
        CheckSources();
        CheckLibrary();
        CheckContent();
        CheckCrawling();
        return (issues ?? sample.UnorderedItems.Select(item => item.Element))
            .OrderBy(issue => issue.Code, StringComparer.Ordinal)
            .ThenBy(issue => issue.ResourceType, StringComparer.Ordinal)
            .ThenBy(issue => issue.ResourceId, StringComparer.Ordinal)
            .ThenBy(issue => issue.Message, StringComparer.Ordinal)
            .ToList();
    }

    private void Add(string code, string resourceType, string resourceId, string message)
    {
        TotalIssueCount++;
        var issue = ConsistencyIssue.Error(code, resourceType, resourceId, message);
        if (issues is not null)
            issues.Add(issue);
        else if (sample.Count < maxReturnedIssues)
            sample.Enqueue(issue, issue);
        else
            sample.EnqueueDequeue(issue, issue);
    }

    private static int CompareIssues(ConsistencyIssue left, ConsistencyIssue right)
    {
        var comparison = StringComparer.Ordinal.Compare(left.Code, right.Code);
        if (comparison != 0) return comparison;
        comparison = StringComparer.Ordinal.Compare(left.ResourceType, right.ResourceType);
        if (comparison != 0) return comparison;
        comparison = StringComparer.Ordinal.Compare(left.ResourceId, right.ResourceId);
        if (comparison != 0) return comparison;
        // Severity is always "error"; equal ordering keys are identical issue records.
        return StringComparer.Ordinal.Compare(left.Message, right.Message);
    }

    private static Dictionary<TKey, T> FirstByKey<T, TKey>(
        IReadOnlyList<T> values,
        Func<T, TKey> keySelector,
        IEqualityComparer<TKey>? comparer = null) where TKey : notnull
    {
        var result = new Dictionary<TKey, T>(values.Count, comparer);
        foreach (var value in values)
            result.TryAdd(keySelector(value), value); // Duplicate keys retain the first row.
        return result;
    }

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
