using InkFlow.Modules.Library.Application;

namespace InkFlow.Modules.Content.Application;

internal static class BookPackageSnapshotFactory
{
    internal static async Task<BookPackageDocument> CreateAsync(
        ICanonicalBookRepository books,
        IContentVersionRepository versions,
        IContentPolicyReader policyReader,
        Guid canonicalBookId,
        DateTimeOffset snapshotAt,
        CancellationToken cancellationToken)
    {
        if (await policyReader.IsTakedownAsync(canonicalBookId, cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("taken-down books cannot be packaged.");
        }

        var book = await books.GetAsync(canonicalBookId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("canonical book was not found.");
        var currentVersions = await versions
            .ListCurrentForBookAsync(canonicalBookId, cancellationToken)
            .ConfigureAwait(false);
        var currentByChapter = currentVersions.ToDictionary(version => version.CanonicalChapterId);
        var snapshot = new List<BookPackageChapter>(book.Chapters.Count);
        foreach (var chapter in book.Chapters.OrderBy(chapter => chapter.Index))
        {
            if (!currentByChapter.TryGetValue(chapter.Id, out var version) ||
                version.CanonicalBookId != book.Id ||
                version.CanonicalChapterId != chapter.Id ||
                string.IsNullOrWhiteSpace(version.CanonicalText))
            {
                throw new InvalidOperationException(
                    $"chapter {chapter.Id} does not have a current published content version.");
            }

            snapshot.Add(new BookPackageChapter(
                chapter.Id,
                chapter.Index,
                chapter.Title,
                version.CanonicalText,
                version.Id,
                version.CanonicalHash));
        }

        return new(book.Id, book.Title, book.Author, snapshot, snapshotAt);
    }
}
