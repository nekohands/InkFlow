namespace InkFlow.Modules.Crawling.Application;

/// <summary>
/// Aggregates successful discovery hits by canonical identity and owns the
/// bounded result policy used by the discovery workflow.
/// </summary>
internal sealed class DiscoveredBookAccumulator(int maxBooks)
{
    private readonly Dictionary<Guid, DiscoveredBook> books = [];
    private readonly HashSet<Guid> newlyCreated = [];

    public bool IsTruncated { get; private set; }

    public bool HasCapacity(ICollection<string> warnings)
    {
        if (books.Count < maxBooks)
        {
            return true;
        }

        warnings.Add($"search: results truncated at {maxBooks}.");
        IsTruncated = true;
        return false;
    }

    public void Add(string sourceId, BookDiscoveryHitResult result)
    {
        if (!result.IsSuccess || result.Book is null)
        {
            return;
        }

        if (result.NewlyCreated)
        {
            newlyCreated.Add(result.Book.Id);
        }

        if (books.TryGetValue(result.Book.Id, out var existing))
        {
            if (!existing.SourceIds.Contains(sourceId))
            {
                books[result.Book.Id] = existing with
                {
                    SourceIds = [.. existing.SourceIds, sourceId],
                };
            }

            return;
        }

        books[result.Book.Id] = new DiscoveredBook(
            result.Book.Id,
            result.Book.Title,
            result.Book.Author,
            [sourceId],
            AlreadyInLibrary: !newlyCreated.Contains(result.Book.Id));
    }

    public IReadOnlyList<DiscoveredBook> Build() =>
        books.Values
            .OrderBy(book => book.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
}
