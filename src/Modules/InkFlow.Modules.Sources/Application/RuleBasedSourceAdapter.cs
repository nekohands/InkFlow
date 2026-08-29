using System.Text;
using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Sources.Application;

/// <summary>
/// 规则型来源适配器:解释 Source 聚合的 RuleDsl,把统一契约的四个能力操作
/// 翻译为规则执行。新增规则型站点零代码——登记 Source 记录即可。
///
/// 抽取模型:
/// - 单结果能力(BookInfo/Content):Fields 逐字段抽取(selector 可带 attribute 取值);
/// - 多结果能力(Toc/Search):List 绑定条目集(ItemsSelector),
///   外部 ID = 条目的 ExternalIdAttribute 值剥离 IdPrefixToStrip/IdSuffixToStrip,
///   标题取条目文本。
/// </summary>
public sealed class RuleBasedSourceAdapter(
    Source source,
    RuleAdapter ruleAdapter,
    ISelectorEvaluator selectorEvaluator,
    SourceRuleExecutionLimits? limits = null) : ISourceAdapter
{
    public string SourceId => source.Id;

    private readonly SourceRuleExecutionLimits _limits = ResolveLimits(ruleAdapter, limits);

    private static readonly char[] Tab = ['\t'];

    public Task<IReadOnlyList<SourceSearchResult>> SearchAsync(
        string keyword, CancellationToken cancellationToken = default) =>
        SearchAsync(keyword, cancellationToken, executionContext: null);

    public async Task<IReadOnlyList<SourceSearchResult>> SearchAsync(
        string keyword,
        CancellationToken cancellationToken,
        SourceExecutionContext? executionContext)
    {
        var rule = source.FindRule(SourceCapability.Search);
        if (rule?.List is null)
        {
            return [];
        }

        var result = await ruleAdapter
            .ExecuteAsync(
                rule,
                source.BaseUrl,
                new Dictionary<string, string> { ["key"] = keyword },
                cancellationToken,
                NormalizeExecutionContext(executionContext))
            .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return [];
        }

        var results = new List<SourceSearchResult>();
        long resultBytes = 0;

        foreach (var body in result.ResponseBodies)
        {
            var items = selectorEvaluator.SelectAll(body, ToSelector(rule.List));
            foreach (var item in items)
            {
                var externalId = ExtractExternalId(item, rule.List);
                if (string.IsNullOrEmpty(externalId))
                {
                    continue;
                }

                const string unknownAuthor = "未知";
                var title = GetItemText(item, rule.List);
                var itemBytes = (long)Encoding.UTF8.GetByteCount(externalId) +
                    Encoding.UTF8.GetByteCount(title) +
                    Encoding.UTF8.GetByteCount(unknownAuthor);
                if (resultBytes + itemBytes > _limits.MaxResultSize)
                {
                    return [];
                }

                resultBytes += itemBytes;
                results.Add(new SourceSearchResult(externalId, title, unknownAuthor));
            }
        }

        return results;
    }

    public Task<SourceBookInfo?> GetBookInfoAsync(
        string externalBookId, CancellationToken cancellationToken = default) =>
        GetBookInfoAsync(externalBookId, cancellationToken, executionContext: null);

    public async Task<SourceBookInfo?> GetBookInfoAsync(
        string externalBookId,
        CancellationToken cancellationToken,
        SourceExecutionContext? executionContext)
    {
        var rule = source.FindRule(SourceCapability.BookInfo);
        if (rule is null)
        {
            return null;
        }

        var result = await ExecuteWithVariablesAsync(
            rule,
            new Dictionary<string, string> { ["bookId"] = externalBookId },
            cancellationToken,
            executionContext).ConfigureAwait(false);

        if (!result.IsSuccess ||
            !result.Values.TryGetValue("title", out var title) ||
            string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        result.Values.TryGetValue("author", out var author);
        return new SourceBookInfo(title.Trim(), string.IsNullOrWhiteSpace(author) ? "未知" : author.Trim());
    }

    public Task<IReadOnlyList<SourceTocEntry>> GetTableOfContentsAsync(
        string externalBookId, CancellationToken cancellationToken = default) =>
        GetTableOfContentsAsync(externalBookId, cancellationToken, executionContext: null);

    public async Task<IReadOnlyList<SourceTocEntry>> GetTableOfContentsAsync(
        string externalBookId,
        CancellationToken cancellationToken,
        SourceExecutionContext? executionContext)
    {
        var rule = source.FindRule(SourceCapability.Toc);
        if (rule?.List is null)
        {
            return [];
        }

        var result = await ExecuteWithVariablesAsync(
            rule,
            new Dictionary<string, string> { ["bookId"] = externalBookId },
            cancellationToken,
            executionContext).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return [];
        }

        var bodies = result.ResponseBodies;

        var index = 0;
        var entries = new List<SourceTocEntry>();
        long resultBytes = 0;

        foreach (var body in bodies)
        {
            var items = selectorEvaluator.SelectAll(body, ToSelector(rule.List));
            foreach (var item in items)
            {
                var externalId = ExtractExternalId(item, rule.List);
                var title = GetItemText(item, rule.List);

                if (string.IsNullOrEmpty(externalId) || string.IsNullOrEmpty(title))
                {
                    continue;
                }

                var itemBytes = (long)Encoding.UTF8.GetByteCount(externalId) +
                    Encoding.UTF8.GetByteCount(title);
                if (resultBytes + itemBytes > _limits.MaxResultSize)
                {
                    return [];
                }

                resultBytes += itemBytes;
                entries.Add(new SourceTocEntry(externalId, index++, title));
            }
        }

        return entries;
    }

    public Task<string?> GetChapterContentAsync(
        string externalChapterId, CancellationToken cancellationToken = default) =>
        GetChapterContentAsync(externalChapterId, cancellationToken, executionContext: null);

    public async Task<string?> GetChapterContentAsync(
        string externalChapterId,
        CancellationToken cancellationToken,
        SourceExecutionContext? executionContext)
    {
        var rule = source.FindRule(SourceCapability.Content);
        if (rule is null)
        {
            return null;
        }

        // v1:Content 规则模板仅依赖 chapterId;bookId 上下文随章节映射增强后提供。
        var result = await ruleAdapter
            .ExecuteAsync(
                rule, source.BaseUrl,
                new Dictionary<string, string> { ["chapterId"] = externalChapterId },
                cancellationToken,
                NormalizeExecutionContext(executionContext))
            .ConfigureAwait(false);

        return result.IsSuccess && result.Values.TryGetValue("content", out var content)
            ? content
            : null;
    }

    private Task<RuleExecutionResult> ExecuteWithVariablesAsync(
        CapabilityRule rule,
        IReadOnlyDictionary<string, string> variables,
        CancellationToken cancellationToken,
        SourceExecutionContext? executionContext) =>
        ruleAdapter.ExecuteAsync(
            rule,
            source.BaseUrl,
            variables,
            cancellationToken,
            NormalizeExecutionContext(executionContext));

    private SourceExecutionContext NormalizeExecutionContext(
        SourceExecutionContext? executionContext)
    {
        if (executionContext is not null &&
            !string.Equals(executionContext.SourceId, source.Id, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "source execution context does not match adapter source.");
        }

        var credentialReferenceId = source.ResolveCredentialReference(
            executionContext?.CredentialReferenceId);
        return new SourceExecutionContext(source.Id, credentialReferenceId);
    }

    private static SourceRuleExecutionLimits ResolveLimits(
        RuleAdapter ruleAdapter,
        SourceRuleExecutionLimits? limits)
    {
        var value = limits ?? ruleAdapter.Limits;
        value.Validate();
        return value;
    }

    private static RuleSelector ToSelector(RuleListBinding binding) =>
        new(binding.ItemsSelectorKind, binding.ItemsSelector);

    private static string GetItemText(
        SelectorElementSnapshot element,
        RuleListBinding binding)
    {
        if (!string.IsNullOrWhiteSpace(binding.TextAttribute) &&
            element.Attributes.TryGetValue(binding.TextAttribute, out var attributeValue))
        {
            return attributeValue.Trim();
        }

        return element.TextContent.Trim();
    }

    /// <summary>外部 ID = 条目属性值剥离声明的前缀/后缀。</summary>
    private static string? ExtractExternalId(
        SelectorElementSnapshot element, RuleListBinding binding)
    {
        if (!element.Attributes.TryGetValue(binding.ExternalIdAttribute, out var raw))
        {
            return null;
        }

        var id = raw;

        if (binding.IdPrefixToStrip.Length > 0 &&
            id.StartsWith(binding.IdPrefixToStrip, StringComparison.Ordinal))
        {
            id = id[binding.IdPrefixToStrip.Length..];
        }

        if (binding.IdSuffixToStrip.Length > 0 &&
            id.EndsWith(binding.IdSuffixToStrip, StringComparison.Ordinal))
        {
            id = id[..^binding.IdSuffixToStrip.Length];
        }

        return id.Length == 0 ? null : id;
    }
}
