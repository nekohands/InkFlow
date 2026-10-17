using InkFlow.Modules.Library.Domain;

namespace InkFlow.Modules.Library.Application;

/// <summary>私有书导入用例：解析、构造领域对象并以单次仓储边界写入。</summary>
internal sealed class PrivateBookImportUseCase(
    IPrivateBookRepository repository,
    IPrivateBookImportParser parser,
    TimeProvider clock)
{
    public async Task<PrivateLibraryOperationResult<PrivateBookImportView>> ImportAsync(
        Guid userId,
        string fileName,
        string? contentType,
        Stream content,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
        {
            return Invalid<PrivateBookImportView>();
        }

        var parsed = await parser
            .ParseAsync(fileName, contentType, content, cancellationToken)
            .ConfigureAwait(false);
        if (parsed.Status != PrivateLibraryResultStatus.Success || parsed.Draft is null)
        {
            return new(parsed.Status, null);
        }

        try
        {
            var now = clock.GetUtcNow();
            var book = PrivateBook.Create(
                userId,
                parsed.Draft.Title,
                parsed.Draft.Author,
                now);
            var chapters = parsed.Draft.Chapters
                .Select((chapter, index) => PrivateChapter.Create(
                    userId,
                    book.Id,
                    index,
                    chapter.Title,
                    PrivateContentDocument.FromParagraphs(chapter.Paragraphs),
                    now))
                .ToArray();

            await repository.AddWithChaptersAsync(book, chapters, cancellationToken)
                .ConfigureAwait(false);
            return Success<PrivateBookImportView>(new(
                ToView(book),
                chapters.Length));
        }
        catch (ArgumentException)
        {
            return Invalid<PrivateBookImportView>();
        }
    }

    private static PrivateBookView ToView(PrivateBook book) =>
        new(book.Id, book.Title, book.Author, book.CreatedAt, book.UpdatedAt, book.Version);

    private static PrivateLibraryOperationResult<T> Success<T>(T value) =>
        new(PrivateLibraryResultStatus.Success, value);

    private static PrivateLibraryOperationResult<T> Invalid<T>() =>
        new(PrivateLibraryResultStatus.InvalidRequest, default);
}
