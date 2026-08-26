namespace InkFlow.Modules.Sources.Infrastructure.Persistence;

/// <summary>fetch_artifacts 表实体。</summary>
public sealed class FetchArtifactEntity
{
    public Guid Id { get; set; }
    public string SourceId { get; set; } = null!;
    public string ExternalBookId { get; set; } = null!;
    public string ExternalChapterId { get; set; } = null!;
    public string RawHash { get; set; } = null!;
    public int BodyLength { get; set; }
    public DateTimeOffset FetchedAt { get; set; }
}
