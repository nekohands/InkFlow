using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Sources.Application;

internal sealed record SourceAdapterReadResult<T>(T? Value, string? Error)
{
    public bool IsSuccess => Error is null;

    public static SourceAdapterReadResult<T> Success(T value) => new(value, null);

    public static SourceAdapterReadResult<T> Failure(string error) => new(default, error);
}

internal sealed record SourceAdapterPreparation(ISourceAdapter? Adapter, string? Error)
{
    public bool IsSuccess => Error is null;

    public static SourceAdapterPreparation Success(ISourceAdapter adapter) => new(adapter, null);

    public static SourceAdapterPreparation Failure(string error) => new(null, error);
}

/// <summary>来源适配器读取 facade：保留目录与正文读取的稳定调用边界。</summary>
internal sealed class SourceAdapterReader
{
    private readonly SourceAdapterAccess access;
    private readonly SourceCatalogAdapterReader catalogReader;
    private readonly SourceContentAdapterReader contentReader;

    public SourceAdapterReader(
        ISourceAdapterFactory adapterFactory,
        ISourceHealthReader? healthReader,
        ISourceHealthRecorder? healthRecorder)
    {
        access = new(adapterFactory, healthReader, healthRecorder);
        catalogReader = new(access);
        contentReader = new(access);
    }

    public const int MaxTocEntries = SourceCatalogAdapterReader.MaxTocEntries;

    public Task<SourceAdapterReadResult<SourceBookInfo>> ReadBookInfoAsync(
        string sourceId,
        string externalBookId,
        CancellationToken cancellationToken,
        SourceExecutionContext? executionContext) =>
        catalogReader.ReadBookInfoAsync(sourceId, externalBookId, cancellationToken, executionContext);

    public Task<SourceAdapterReadResult<IReadOnlyList<SourceTocEntry>>> ReadTocAsync(
        string sourceId,
        string externalBookId,
        CancellationToken cancellationToken,
        SourceExecutionContext? executionContext) =>
        catalogReader.ReadTocAsync(sourceId, externalBookId, cancellationToken, executionContext);

    public Task<SourceAdapterReadResult<string>> ReadChapterContentAsync(
        SourceAdapterPreparation preparation,
        string sourceId,
        string externalChapterId,
        CancellationToken cancellationToken,
        SourceExecutionContext? executionContext) =>
        contentReader.ReadChapterContentAsync(
            preparation,
            sourceId,
            externalChapterId,
            cancellationToken,
            executionContext);

    public Task<SourceAdapterPreparation> PrepareAsync(
        string sourceId,
        SourceCapability capability,
        SourceExecutionContext? executionContext,
        CancellationToken cancellationToken) =>
        access.PrepareAsync(sourceId, capability, executionContext, cancellationToken);
}
