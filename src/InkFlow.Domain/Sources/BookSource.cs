namespace InkFlow.Domain.Sources;

public sealed class BookSource
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required Guid BookId { get; init; }
    public required string SourceKey { get; init; }
    public required string ExternalId { get; init; }
    public required Uri SourceUri { get; init; }
}
