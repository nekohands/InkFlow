using InkFlow.Modules.Content.Application;
using InkFlow.Modules.Library.Application;

namespace InkFlow.Modules.Reading.Application;

/// <summary>统一 Reading 对 CanonicalBook 的摘要读取与下架可见性判断。</summary>
internal sealed class ReadingBookVisibility(
    ICanonicalBookRepository books,
    IContentPolicyReader contentPolicy)
{
    public async Task<CanonicalBookSummary?> GetAsync(
        Guid canonicalBookId,
        CancellationToken cancellationToken)
    {
        var summary = await books
            .GetSummaryAsync(canonicalBookId, cancellationToken)
            .ConfigureAwait(false);
        if (summary is null)
        {
            return null;
        }

        return await contentPolicy
            .IsTakedownAsync(canonicalBookId, cancellationToken)
            .ConfigureAwait(false)
            ? null
            : summary;
    }
}
