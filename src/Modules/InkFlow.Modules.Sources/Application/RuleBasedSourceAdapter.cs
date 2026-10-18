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
public sealed class RuleBasedSourceAdapter : ISourceAdapter
{
    private readonly Source _source;
    private readonly RuleAdapter _ruleAdapter;
    private readonly SourceRuleExecutionLimits _limits;
    private readonly RuleListProjector _listProjector;

    public RuleBasedSourceAdapter(
        Source source,
        RuleAdapter ruleAdapter,
        ISelectorEvaluator selectorEvaluator,
        SourceRuleExecutionLimits? limits = null)
    {
        _source = source;
        _ruleAdapter = ruleAdapter;
        _limits = ResolveLimits(ruleAdapter, limits);
        _listProjector = new RuleListProjector(selectorEvaluator, _limits);
    }

    public string SourceId => _source.Id;

    public bool TryResolveBookUrl(Uri url, out string externalBookId)
    {
        externalBookId = string.Empty;
        var rule = _source.FindRule(SourceCapability.BookInfo);
        if (rule is null)
        {
            return false;
        }

        return RuleBasedSourceProjection.TryExtractBookId(
            rule.Request.PathTemplate,
            url.AbsolutePath,
            out externalBookId);
    }

    public Task<IReadOnlyList<SourceSearchResult>> SearchAsync(
        string keyword, CancellationToken cancellationToken = default) =>
        SearchAsync(keyword, cancellationToken, executionContext: null);

    public async Task<IReadOnlyList<SourceSearchResult>> SearchAsync(
        string keyword,
        CancellationToken cancellationToken,
        SourceExecutionContext? executionContext)
    {
        var rule = _source.FindRule(SourceCapability.Search);
        if (rule?.List is null)
        {
            return [];
        }

        return await ExecuteListAsync(
                rule,
                new Dictionary<string, string> { ["key"] = keyword },
                cancellationToken,
                executionContext,
                (result, token) => _listProjector.Project(
                    result.ResponseBodies,
                    rule.List,
                    token,
                    item => RuleBasedSourceProjection.ProjectSearchItem(item, rule.List)))
            .ConfigureAwait(false);
    }

    public Task<SourceBookInfo?> GetBookInfoAsync(
        string externalBookId, CancellationToken cancellationToken = default) =>
        GetBookInfoAsync(externalBookId, cancellationToken, executionContext: null);

    public async Task<SourceBookInfo?> GetBookInfoAsync(
        string externalBookId,
        CancellationToken cancellationToken,
        SourceExecutionContext? executionContext)
    {
        var rule = _source.FindRule(SourceCapability.BookInfo);
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
        var rule = _source.FindRule(SourceCapability.Toc);
        if (rule?.List is null)
        {
            return [];
        }

        var index = 0;
        return await ExecuteListAsync(
                rule,
                new Dictionary<string, string> { ["bookId"] = externalBookId },
                cancellationToken,
                executionContext,
                (result, token) => _listProjector.Project(
                    result.ResponseBodies,
                    rule.List,
                    token,
                    item => RuleBasedSourceProjection.ProjectTocItem(item, rule.List, ref index)))
            .ConfigureAwait(false);
    }

    public Task<string?> GetChapterContentAsync(
        string externalChapterId, CancellationToken cancellationToken = default) =>
        GetChapterContentAsync(externalChapterId, cancellationToken, executionContext: null);

    public async Task<string?> GetChapterContentAsync(
        string externalChapterId,
        CancellationToken cancellationToken,
        SourceExecutionContext? executionContext)
    {
        var rule = _source.FindRule(SourceCapability.Content);
        if (rule is null)
        {
            return null;
        }

        // v1:Content 规则模板仅依赖 chapterId;bookId 上下文随章节映射增强后提供。
        var result = await ExecuteWithVariablesAsync(
                rule,
                new Dictionary<string, string> { ["chapterId"] = externalChapterId },
                cancellationToken,
                executionContext)
            .ConfigureAwait(false);

        return result.IsSuccess && result.Values.TryGetValue("content", out var content)
            ? content
            : null;
    }

    private async Task<IReadOnlyList<T>> ExecuteListAsync<T>(
        CapabilityRule rule,
        IReadOnlyDictionary<string, string> variables,
        CancellationToken cancellationToken,
        SourceExecutionContext? executionContext,
        Func<RuleExecutionResult, CancellationToken, IReadOnlyList<T>> project)
    {
        using var executionCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        executionCancellation.CancelAfter(_limits.MaxExecutionTime);

        try
        {
            var result = await ExecuteWithVariablesAsync(
                    rule,
                    variables,
                    executionCancellation.Token,
                    executionContext)
                .ConfigureAwait(false);
            return result.IsSuccess ? project(result, executionCancellation.Token) : [];
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (executionCancellation.IsCancellationRequested)
        {
            return [];
        }
    }

    private Task<RuleExecutionResult> ExecuteWithVariablesAsync(
        CapabilityRule rule,
        IReadOnlyDictionary<string, string> variables,
        CancellationToken cancellationToken,
        SourceExecutionContext? executionContext) =>
        _ruleAdapter.ExecuteAsync(
            rule,
            _source.BaseUrl,
            variables,
            cancellationToken,
            NormalizeExecutionContext(executionContext));

    private SourceExecutionContext NormalizeExecutionContext(
        SourceExecutionContext? executionContext)
    {
        if (executionContext is not null &&
            !string.Equals(executionContext.SourceId, _source.Id, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "source execution context does not match adapter source.");
        }

        var credentialReferenceId = _source.ResolveCredentialReference(
            executionContext?.CredentialReferenceId);
        // 仅显式传入的引用携带调用方 Owner Scope；来源默认绑定属于 Platform Scope，
        // 不能把用户/组织上下文静默套到平台默认凭据上。
        var ownerScope = !string.IsNullOrEmpty(executionContext?.CredentialReferenceId)
            ? executionContext?.CredentialOwnerScope
            : SourceCredentialOwnerScope.Platform;
        return new SourceExecutionContext(
            _source.Id,
            credentialReferenceId,
            ownerScope);
    }

    private static SourceRuleExecutionLimits ResolveLimits(
        RuleAdapter ruleAdapter,
        SourceRuleExecutionLimits? limits)
    {
        var value = limits ?? ruleAdapter.Limits;
        value.Validate();
        return value;
    }

}
