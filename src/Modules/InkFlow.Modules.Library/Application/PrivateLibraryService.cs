namespace InkFlow.Modules.Library.Application;

public sealed class PrivateLibraryService(
    IPrivateBookRepository repository,
    TimeProvider clock) : IPrivateLibraryService
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 100;

    private readonly PrivateLibraryCommands commands = new(repository, clock);
    private readonly PrivateLibraryQueries queries = new(repository);

    public Task<PrivateLibraryOperationResult<PrivateBookView>> CreateAsync(
        Guid userId,
        string? title,
        string? author,
        CancellationToken cancellationToken = default) =>
        commands.CreateAsync(userId, title, author, cancellationToken);

    public Task<IReadOnlyList<PrivateBookView>> ListAsync(
        Guid userId,
        int limit,
        CancellationToken cancellationToken = default) =>
        queries.ListAsync(userId, limit, cancellationToken);

    public Task<PrivateBookView?> GetAsync(
        Guid userId,
        Guid privateBookId,
        CancellationToken cancellationToken = default) =>
        queries.GetAsync(userId, privateBookId, cancellationToken);

    public Task<PrivateLibraryOperationResult<PrivateBookView>> UpdateAsync(
        Guid userId,
        Guid privateBookId,
        string? title,
        string? author,
        long expectedVersion,
        CancellationToken cancellationToken = default) =>
        commands.UpdateAsync(
            userId,
            privateBookId,
            title,
            author,
            expectedVersion,
            cancellationToken);

    public Task<PrivateLibraryResultStatus> DeleteAsync(
        Guid userId,
        Guid privateBookId,
        CancellationToken cancellationToken = default) =>
        commands.DeleteAsync(userId, privateBookId, cancellationToken);
}
