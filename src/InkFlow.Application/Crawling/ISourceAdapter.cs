namespace InkFlow.Application.Crawling;

public interface ISourceAdapter
{
    string Key { get; }
    Task<IReadOnlyList<SourceBookSearchResult>> SearchAsync(string keyword, CancellationToken cancellationToken = default);
    Task<SourceBookSnapshot> GetBookAsync(string externalId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SourceChapterSnapshot>> GetChapterListAsync(string externalId, CancellationToken cancellationToken = default);
    Task<SourceChapterContent> GetChapterAsync(string externalBookId, string externalChapterId, CancellationToken cancellationToken = default);
}

public sealed record SourceBookSearchResult(string ExternalId, string Title, string? Author, Uri DetailUri);
public sealed record SourceBookSnapshot(string ExternalId, string Title, string? Author, Uri DetailUri);
public sealed record SourceChapterSnapshot(string ExternalId, string Title, int Order, Uri ChapterUri);
public sealed record SourceChapterContent(string ExternalId, string Title, string Content);
