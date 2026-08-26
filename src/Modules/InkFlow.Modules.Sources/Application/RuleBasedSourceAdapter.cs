using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Sources.Application;

/// <summary>
/// 规则型来源适配器:解释 Source 聚合的 RuleDsl,把统一契约的四个能力操作
/// 翻译为规则执行。新增规则型站点零代码——登记 Source 记录即可。
///
/// 过渡协议(DSL v1 无列表选择器引擎期间):
/// - Search:字段 <c>results</c> 多行文本,每行 <c>externalBookId TAB title TAB author</c>;
/// - Toc:字段 <c>chapters</c> 多行文本,每行 <c>externalChapterId TAB title</c>。
/// 列表选择器引擎接入后,以上文本协议由结构化抽取取代。
/// </summary>
public sealed class RuleBasedSourceAdapter(Source source, RuleAdapter ruleAdapter) : ISourceAdapter
{
    public string SourceId => source.Id;

    private const string ResultsField = "results";
    private const string ChaptersField = "chapters";
    private static readonly char[] Tab = ['\t'];

    public async Task<IReadOnlyList<SourceSearchResult>> SearchAsync(
        string keyword, CancellationToken cancellationToken = default)
    {
        var rule = source.FindRule(SourceCapability.Search);
        if (rule is null)
        {
            return [];
        }

        var result = await ruleAdapter
            .ExecuteAsync(rule, source.BaseUrl, new Dictionary<string, string> { ["key"] = keyword }, cancellationToken)
            .ConfigureAwait(false);

        return result.IsSuccess && result.Values.TryGetValue(ResultsField, out var block)
            ? ParseLines(block, 3,
                parts => new SourceSearchResult(parts[0], parts[1], parts.Length > 2 ? parts[2] : ""))
            : [];
    }

    public async Task<SourceBookInfo?> GetBookInfoAsync(
        string externalBookId, CancellationToken cancellationToken = default)
    {
        var rule = source.FindRule(SourceCapability.BookInfo);
        if (rule is null)
        {
            return null;
        }

        var result = await ExecuteWithBookIdAsync(rule, externalBookId, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess ||
            !result.Values.TryGetValue("title", out var title) ||
            string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        result.Values.TryGetValue("author", out var author);
        return new SourceBookInfo(title.Trim(), string.IsNullOrWhiteSpace(author) ? "未知" : author.Trim());
    }

    public async Task<IReadOnlyList<SourceTocEntry>> GetTableOfContentsAsync(
        string externalBookId, CancellationToken cancellationToken = default)
    {
        var rule = source.FindRule(SourceCapability.Toc);
        if (rule is null)
        {
            return [];
        }

        var result = await ExecuteWithBookIdAsync(rule, externalBookId, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess || !result.Values.TryGetValue(ChaptersField, out var block))
        {
            return [];
        }

        var index = 0;
        var entries = new List<SourceTocEntry>();
        foreach (var line in block.Split('\n'))
        {
            var trimmed = line.Trim('\r', ' ');
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                continue;
            }

            var parts = trimmed.Split(Tab, 2);
            if (parts.Length != 2 || parts[0].Length == 0 || parts[1].Length == 0)
            {
                continue;
            }

            entries.Add(new SourceTocEntry(parts[0].Trim(), index++, parts[1].Trim()));
        }

        return entries;
    }

    public async Task<string?> GetChapterContentAsync(
        string externalChapterId, CancellationToken cancellationToken = default)
    {
        var rule = source.FindRule(SourceCapability.Content);
        if (rule is null)
        {
            return null;
        }

        var result = await ruleAdapter
            .ExecuteAsync(
                rule, source.BaseUrl,
                new Dictionary<string, string>
                {
                    // v1:Content 规则模板仅依赖 chapterId;bookId 上下文随章节映射增强后提供。
                    ["chapterId"] = externalChapterId,
                },
                cancellationToken)
            .ConfigureAwait(false);

        return result.IsSuccess && result.Values.TryGetValue("content", out var content)
            ? content
            : null;
    }

    /// <summary>v1 目录协议不含书 ID;正文抓取的 bookId 由调用方上下文提供时才可用。</summary>

    private Task<RuleExecutionResult> ExecuteWithBookIdAsync(
        CapabilityRule rule, string externalBookId, CancellationToken cancellationToken) =>
        ruleAdapter.ExecuteAsync(
            rule, source.BaseUrl, new Dictionary<string, string> { ["bookId"] = externalBookId }, cancellationToken);

    private static IReadOnlyList<T> ParseLines<T>(string block, int minParts, Func<string[], T> map)
    {
        var items = new List<T>();
        foreach (var raw in block.Split('\n'))
        {
            var line = raw.Trim('\r', ' ');
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var parts = line.Split(Tab);
            if (parts.Length < minParts || parts.Any(p => p.Length == 0))
            {
                continue;
            }

            items.Add(map(parts));
        }

        return items;
    }
}
