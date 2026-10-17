using InkFlow.Modules.Content.Domain;
using InkFlow.Modules.Library.Application;

namespace InkFlow.Modules.Content.Application;

/// <summary>校验书籍并创建排队中的书籍包任务。</summary>
internal sealed class BookPackageCreationUseCase(
    IBookPackageJobRepository jobs,
    ICanonicalBookRepository books,
    IContentPolicyReader policyReader,
    BookPackageOptions options,
    TimeProvider clock)
{
    public async Task<BookPackageCreateOutcome> CreateAsync(
        Guid canonicalBookId,
        BookPackageFormat format,
        CancellationToken cancellationToken)
    {
        if (canonicalBookId == Guid.Empty || !Enum.IsDefined(format))
        {
            return BookPackageCreateOutcome.Failure(
                "package.invalid-request", "book ID or package format is invalid.");
        }

        var book = await books.GetAsync(canonicalBookId, cancellationToken).ConfigureAwait(false);
        if (book is null)
        {
            return BookPackageCreateOutcome.Failure(
                "package.book-not-found", "canonical book was not found.");
        }

        if (await policyReader.IsTakedownAsync(canonicalBookId, cancellationToken).ConfigureAwait(false))
        {
            return BookPackageCreateOutcome.Failure(
                "package.book-taken-down", "a taken-down book cannot be packaged.");
        }

        if (book.Chapters.Count == 0)
        {
            return BookPackageCreateOutcome.Failure(
                "package.no-chapters", "the book has no chapters to package.");
        }

        var now = clock.GetUtcNow();
        var job = BookPackageJob.Create(
            canonicalBookId,
            format,
            now,
            now + options.Retention);
        await jobs.AddAsync(job, cancellationToken).ConfigureAwait(false);
        return new(true, BookPackageViewMapper.ToView(job), null, null);
    }
}
