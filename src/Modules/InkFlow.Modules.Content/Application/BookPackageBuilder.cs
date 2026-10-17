using InkFlow.Modules.Content.Domain;

namespace InkFlow.Modules.Content.Application;

/// <summary>
/// v1 书籍打包入口：按格式路由到具体构建器，保留统一的进度和取消契约。
/// </summary>
public sealed class BookPackageBuilder : IBookPackageBuilder
{
    public Task BuildAsync(
        BookPackageDocument document,
        BookPackageFormat format,
        Stream output,
        Func<int, Task> progress,
        CancellationToken cancellationToken = default) =>
        format switch
        {
            BookPackageFormat.Zip => BookPackageZipBuilder.BuildAsync(
                document, output, progress, cancellationToken),
            BookPackageFormat.Epub => BookPackageEpubBuilder.BuildAsync(
                document, output, progress, cancellationToken),
            BookPackageFormat.Txt => BookPackageTextBuilder.BuildAsync(
                document, output, progress, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(format)),
        };
}
