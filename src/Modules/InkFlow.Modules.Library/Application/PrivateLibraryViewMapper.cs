using InkFlow.Modules.Library.Domain;

namespace InkFlow.Modules.Library.Application;

internal static class PrivateLibraryViewMapper
{
    public static PrivateBookView ToView(PrivateBook book) =>
        new(book.Id, book.Title, book.Author, book.CreatedAt, book.UpdatedAt, book.Version);
}
