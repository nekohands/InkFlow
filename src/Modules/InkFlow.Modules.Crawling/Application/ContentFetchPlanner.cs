namespace InkFlow.Modules.Crawling.Application;

internal sealed record ContentFetchCandidate(string ExternalChapterId, string Reason);

internal static class ContentFetchPlanner
{
    public static IEnumerable<ContentFetchCandidate> Plan(
        IReadOnlyList<string> chapterIds,
        IReadOnlySet<string> fetchedChapterIds,
        IReadOnlySet<string> freshChapterIds,
        bool forceRefresh)
    {
        foreach (var chapterId in chapterIds)
        {
            if (forceRefresh)
            {
                yield return new ContentFetchCandidate(chapterId, "collection-run");
                continue;
            }

            if (!fetchedChapterIds.Contains(chapterId))
            {
                yield return new ContentFetchCandidate(chapterId, "new");
                continue;
            }

            if (!freshChapterIds.Contains(chapterId))
            {
                yield return new ContentFetchCandidate(chapterId, "refetch");
            }
        }
    }
}
