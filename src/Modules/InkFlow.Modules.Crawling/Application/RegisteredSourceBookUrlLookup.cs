using InkFlow.Modules.Sources.Application;

namespace InkFlow.Modules.Crawling.Application;

/// <summary>
/// 在已登记来源中按页查找可由适配器解析的书籍地址。
/// </summary>
internal sealed class RegisteredSourceBookUrlLookup(
    ISourceRepository sourceRepository,
    ISourceAdapterFactory adapterFactory)
{
    private const int MaxExternalIdLength = 512;
    private const int MaxSourcesPerPage = 100;

    public async Task<SourceBookUrlResolution?> ResolveAsync(
        Uri input,
        CancellationToken cancellationToken = default)
    {
        SourceScanCursor? after = null;
        while (true)
        {
            var page = await sourceRepository
                .ListPageAsync(after, MaxSourcesPerPage, cancellationToken)
                .ConfigureAwait(false);
            foreach (var source in page.Sources)
            {
                if (!source.IsEnabled || !MatchesRegisteredBase(input, source.BaseUrl))
                {
                    continue;
                }

                var adapter = await adapterFactory
                    .GetAdapterAsync(source.Id, cancellationToken)
                    .ConfigureAwait(false);
                if (adapter is null ||
                    !adapter.TryResolveBookUrl(input, out var externalBookId) ||
                    string.IsNullOrWhiteSpace(externalBookId) ||
                    externalBookId.Length > MaxExternalIdLength ||
                    externalBookId.Any(char.IsControl))
                {
                    continue;
                }

                return SourceBookUrlResolution.Success(
                    source.Id,
                    externalBookId.Trim(),
                    input.GetLeftPart(UriPartial.Path));
            }

            if (!page.HasMore)
            {
                return null;
            }

            after = page.NextCursor ?? throw new InvalidOperationException(
                "source page reported more entries without a continuation cursor.");
        }
    }

    private static bool MatchesRegisteredBase(Uri input, string baseUrl)
    {
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var registered) ||
            !string.Equals(input.Scheme, registered.Scheme, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(input.Host, registered.Host, StringComparison.OrdinalIgnoreCase) ||
            EffectivePort(input) != EffectivePort(registered))
        {
            return false;
        }

        var basePath = registered.AbsolutePath.TrimEnd('/');
        return basePath.Length == 0 ||
            string.Equals(input.AbsolutePath, basePath, StringComparison.Ordinal) ||
            input.AbsolutePath.StartsWith(basePath + "/", StringComparison.Ordinal);
    }

    private static int EffectivePort(Uri uri) =>
        uri.Port >= 0
            ? uri.Port
            : uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) ? 443 : 80;
}
