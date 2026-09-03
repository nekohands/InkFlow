using InkFlow.Modules.Crawling.Domain;

namespace InkFlow.Modules.Crawling.Application;

public sealed record CollectionRunCursor(DateTimeOffset UpdatedAt, Guid Id);

public sealed record CollectionRunPage(
    IReadOnlyList<CollectionRun> Entries,
    CollectionRunCursor? NextCursor);

public sealed record CollectionRunViewPage(
    IReadOnlyList<CollectionRunView> Entries,
    CollectionRunCursor? NextCursor);
