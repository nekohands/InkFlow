using System.Text;
using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Sources.Application;

public sealed record ImportOutcome(
    bool IsSuccess,
    SourceBook? Book,
    IReadOnlyList<string> Errors)
{
    public static ImportOutcome Ok(SourceBook book) => new(true, book, []);
    public static ImportOutcome Fail(IReadOnlyList<string> errors) => new(false, null, errors);
}

/// <summary>
/// 来源目录服务：把 RuleAdapter 的抓取产物转换为 SourceBook / SourceChapter 并落库。
///
/// 字段约定（DSL v1 无列表选择器期间的过渡协议，列表引擎接入后由结构化抽取取代）：
/// - BookInfo：字段 <c>title</c> / <c>author</c> 直接映射书目元数据；
/// - Toc：字段 <c>chapters</c> 为多行文本，每行 <c>externalChapterId TAB title</c>。
/// </summary>
public sealed class SourceCatalogService(
    ISourceRepository sourceRepository,
    ISourceBookRepository sourceBookRepository,
    RuleAdapter ruleAdapter)
{
    public const string ChaptersFieldName = "chapters";
    private static readonly char[] TabSeparator = ['\t'];

    /// <summary>
    /// 抓取并导入一本书的元数据（BookInfo 能力）。已存在的书更新元数据，否则创建。
    /// </summary>
    public async Task<ImportOutcome> ImportBookInfoAsync(
        string sourceId, string externalBookId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var result = await ExecuteCapabilityAsync(
            sourceId, SourceCapability.BookInfo,
            new Dictionary<string, string> { ["bookId"] = externalBookId },
            cancellationToken).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return ImportOutcome.Fail(result.Errors);
        }

        if (!result.Values.TryGetValue("title", out var title))
        {
            return ImportOutcome.Fail(["rules[BookInfo]: required field 'title' missing from extraction."]);
        }

        result.Values.TryGetValue("author", out var author);
        author = string.IsNullOrWhiteSpace(author) ? "未知" : author;

        var existing = await sourceBookRepository
            .GetAsync(sourceId, externalBookId, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            var created = SourceBook.Create(sourceId, externalBookId, title, author, now);
            await sourceBookRepository.AddAsync(created, cancellationToken).ConfigureAwait(false);
            return ImportOutcome.Ok(created);
        }

        existing.UpdateMetadata(title, author, now);
        await sourceBookRepository.SaveAsync(existing, cancellationToken).ConfigureAwait(false);
        return ImportOutcome.Ok(existing);
    }

    /// <summary>
    /// 同步一本书的目录（Toc 能力）。章节按 ExternalChapterId 幂等追加。
    /// </summary>
    public async Task<ImportOutcome> SyncChaptersAsync(
        string sourceId, string externalBookId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var result = await ExecuteCapabilityAsync(
            sourceId, SourceCapability.Toc,
            new Dictionary<string, string> { ["bookId"] = externalBookId },
            cancellationToken).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return ImportOutcome.Fail(result.Errors);
        }

        if (!result.Values.TryGetValue(ChaptersFieldName, out var chaptersBlock))
        {
            return ImportOutcome.Fail([$"rules[Toc]: required field '{ChaptersFieldName}' missing from extraction."]);
        }

        var existing = await sourceBookRepository
            .GetAsync(sourceId, externalBookId, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            return ImportOutcome.Fail(
                [$"catalog: book '{sourceId}/{externalBookId}' must be imported (BookInfo) before syncing chapters."]);
        }

        existing.SyncChapters(ParseChapterLines(chaptersBlock), now);
        await sourceBookRepository.SaveAsync(existing, cancellationToken).ConfigureAwait(false);
        return ImportOutcome.Ok(existing);
    }

    private async Task<RuleExecutionResult> ExecuteCapabilityAsync(
        string sourceId, SourceCapability capability,
        IReadOnlyDictionary<string, string> variables, CancellationToken cancellationToken)
    {
        var source = await sourceRepository.GetAsync(sourceId, cancellationToken).ConfigureAwait(false);
        if (source is null)
        {
            return RuleExecutionResult.Fail([$"source '{sourceId}' does not exist."]);
        }

        var rule = source.FindRule(capability);
        if (rule is null)
        {
            return RuleExecutionResult.Fail(
                [$"source '{sourceId}' declares no rule for capability {capability}."]);
        }

        return await ruleAdapter.ExecuteAsync(rule, source.BaseUrl, variables, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>解析目录块：每行 <c>externalChapterId TAB title</c>，空行与格式错误行跳过。</summary>
    internal static IEnumerable<(string ExternalChapterId, string Title)> ParseChapterLines(string block)
    {
        foreach (var rawLine in block.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var parts = line.Split(TabSeparator, 2);
            if (parts.Length != 2 || parts[0].Length == 0 || parts[1].Length == 0)
            {
                continue;
            }

            yield return (parts[0].Trim(), parts[1].Trim());
        }
    }
}
