using InkFlow.Api;

namespace InkFlow.ConsistencyFixtures;

public static class ConsistencyFixture
{
    public static readonly DateTimeOffset Now = new(2026, 10, 22, 0, 0, 0, TimeSpan.Zero);

    public static Guid Id(int value) => new(value, 0, 0, new byte[8]);

    public static ConsistencySnapshot Create(int chapters, bool corrupt = false)
    {
        if (chapters == 0) return ConsistencySnapshot.Empty;
        var snapshot = ConsistencySnapshot.Empty with
        {
            CanonicalBooks = [new(Id(1))],
            CanonicalChapters = Enumerable.Range(1, chapters).Select(i => new CanonicalChapterSnapshot(Id(i), Id(1), i)).ToArray(),
            Sources = [new("a"), new("b")],
            SourceBooks = [new(Id(1), "a", "book"), new(Id(2), "b", "book")],
            SourceChapters = Enumerable.Range(1, chapters * 2).Select(i => new SourceChapterSnapshot(Id(i), Id(i % 2 + 1), ((i + 1) / 2).ToString(), (i + 1) / 2)).ToArray(),
            ContentVersions = Enumerable.Range(1, chapters * 2).Select(i => new ContentVersionSnapshot(Id(i), Id(1), Id((i + 1) / 2), i % 2 == 1 ? "a" : "b", "hash-" + i, 64, 1, i % 2 == 1)).ToArray(),
        };
        if (!corrupt) return snapshot;
        return snapshot with
        {
            SourceBooks = [.. snapshot.SourceBooks, new(Id(3), "a", "book"), new(Id(4), "missing", "orphan")],
            CanonicalChapters = [.. snapshot.CanonicalChapters, new(Id(chapters + 1), Id(1), 1)],
            SourceChapters = [.. snapshot.SourceChapters, new(Id(chapters * 2 + 1), Id(1), "1", 1)],
            SourceCapabilityHealth = [new("missing", 999, 999)],
            FetchArtifacts = [new(Id(1), "missing", "book", "chapter")],
            MatchCandidates = [new(Id(1), Id(-1), "missing", "book", 999), new(Id(2), Id(1), "missing", "book", 999)],
            ChapterMappings = [new(Id(1), "wrong", "wrong", Id(1), Id(-1), Id(1), "", "")],
            ContentSelectionDecisions = [new(Id(1), Id(1), Id(2), "", "", Now)],
            CrawlerTasks = Enumerable.Range(1, chapters + 1).Select(i => new CrawlerTaskSnapshot(Id(i), "missing", 999)).ToArray(),
            DeadLetters = [new(Id(1), Id(1), "other", Id(-1))],
        };
    }

    public sealed class Reader(ConsistencySnapshot snapshot) : IConsistencySnapshotReader
    {
        public Task<ConsistencySnapshot> ReadAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(snapshot);
        }
    }

    public sealed class Clock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
