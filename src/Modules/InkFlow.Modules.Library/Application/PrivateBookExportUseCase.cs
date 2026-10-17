using InkFlow.Modules.Library.Domain;

namespace InkFlow.Modules.Library.Application;

/// <summary>私有书导出用例：按用户范围读取并委托既有格式化器生成文件。</summary>
internal sealed class PrivateBookExportUseCase(IPrivateBookRepository repository)
{
    public async Task<PrivateLibraryOperationResult<PrivateLibraryExport>> ExportAsync(
        Guid userId,
        Guid privateBookId,
        string? format,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty || privateBookId == Guid.Empty)
        {
            return NotFound<PrivateLibraryExport>();
        }

        if (!TryParseFormat(format, out var exportFormat))
        {
            return new(PrivateLibraryResultStatus.InvalidRequest, null);
        }

        var book = await repository.GetAsync(userId, privateBookId, cancellationToken)
            .ConfigureAwait(false);
        if (book is null)
        {
            return NotFound<PrivateLibraryExport>();
        }

        var chapters = await repository
            .ListChaptersAsync(userId, privateBookId, cancellationToken)
            .ConfigureAwait(false);
        return Success(PrivateBookExportFormatter.Export(book, chapters, exportFormat));
    }

    private static bool TryParseFormat(string? value, out PrivateLibraryExportFormat format)
    {
        format = PrivateLibraryExportFormat.Txt;
        return value is not null &&
               (string.Equals(value, "txt", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "epub", StringComparison.OrdinalIgnoreCase)) &&
               Enum.TryParse(value, ignoreCase: true, out format);
    }

    private static PrivateLibraryOperationResult<T> Success<T>(T value) =>
        new(PrivateLibraryResultStatus.Success, value);

    private static PrivateLibraryOperationResult<T> NotFound<T>() =>
        new(PrivateLibraryResultStatus.NotFound, default);
}
