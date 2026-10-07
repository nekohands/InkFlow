using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using InkFlow.BuildingBlocks.Security;
using InkFlow.Modules.Sources.Application;
using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Sources.Adapters.Kanunu8;

/// <summary>
/// 努努书坊(kanunu8.com)定制适配器 —— CodeAdapter 形态的第一个示例:
/// 处理 GB18030 编码与非标准页面结构,实现与规则型来源相同的
/// <see cref="ISourceAdapter"/> 统一契约,上层操作零改动。
///
/// 外部标识约定(适配器内部自包含定位):
/// - externalBookId = "book/{id}"(相对目录);
/// - externalChapterId = "book/{id}/{chapterFile}"(相对正文路径)。
/// </summary>
public sealed class KanunuSourceAdapter(
    HttpClient http,
    IIpAddressResolver resolver,
    SourceRuleExecutionLimits? limits = null) : ISourceAdapter
{
    public const string SourceIdValue = "kanunu8";
    public const string DisplayNameValue = "努努书坊(kanunu8)";
    public const string BaseUrlValue = "https://www.kanunu8.com";

    private static readonly HtmlParserHolder ParserHolder = new();
    private readonly int _maxResponseBytes = ValidateLimits(limits).MaxBytes;
    private readonly Regex _chapterLinkPattern = NewRegex(
        @"href=""(\d+[0-9]*\.html)""[^>]*>([^<]+)</a>",
        (limits ?? SourceRuleExecutionLimits.Default).MaxRegexTime);
    private readonly Regex _titleByAuthorPattern = NewRegex(
        @"\s+by\s+(.+?)\s+-",
        (limits ?? SourceRuleExecutionLimits.Default).MaxRegexTime);

    public string SourceId => SourceIdValue;

    public bool TryResolveBookUrl(Uri url, out string externalBookId)
    {
        externalBookId = string.Empty;
        var path = url.AbsolutePath.Trim('/');
        if (!path.StartsWith("book/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var id = path["book/".Length..];
        if (id.Length == 0 || id.Any(character => character is < '0' or > '9'))
        {
            return false;
        }

        externalBookId = $"book/{id}";
        return true;
    }

    public async Task<IReadOnlyList<SourceSearchResult>> SearchAsync(
        string keyword, CancellationToken cancellationToken = default)
    {
        // kanunu8 无站内搜索接口:Search 能力暂以空结果表示,后续接入搜索镜像后启用。
        await Task.CompletedTask.ConfigureAwait(false);
        return [];
    }

    public async Task<SourceBookInfo?> GetBookInfoAsync(
        string externalBookId, CancellationToken cancellationToken = default)
    {
        var html = await FetchHtmlAsync($"{externalBookId}/", cancellationToken).ConfigureAwait(false);
        var document = ParserHolder.Parser.ParseDocument(html);

        var title = document.QuerySelector("h1")?.TextContent.Trim();
        if (string.IsNullOrEmpty(title))
        {
            return null;
        }

        // 页面标题形如 "{书名} by {作者} - 小说在线阅读 - 努努书坊"。
        var pageTitle = document.Title ?? string.Empty;
        Match? authorMatch;
        try
        {
            authorMatch = _titleByAuthorPattern.Match(pageTitle);
        }
        catch (RegexMatchTimeoutException)
        {
            return null;
        }

        var author = authorMatch is { Success: true } m
            ? m.Groups[1].Value.Trim()
            : "未知";

        return new SourceBookInfo(title, author);
    }

    public async Task<IReadOnlyList<SourceTocEntry>> GetTableOfContentsAsync(
        string externalBookId, CancellationToken cancellationToken = default)
    {
        var html = await FetchHtmlAsync($"{externalBookId}/", cancellationToken).ConfigureAwait(false);

        var index = 0;
        var entries = new List<SourceTocEntry>();
        try
        {
            foreach (Match match in _chapterLinkPattern.Matches(html))
            {
                entries.Add(new SourceTocEntry(
                    $"{externalBookId}/{match.Groups[1].Value}",
                    index++,
                    match.Groups[2].Value.Trim()));
            }
        }
        catch (RegexMatchTimeoutException)
        {
            return [];
        }

        return entries;
    }

    public async Task<string?> GetChapterContentAsync(
        string externalChapterId, CancellationToken cancellationToken = default)
    {
        var html = await FetchHtmlAsync($"{externalChapterId}", cancellationToken).ConfigureAwait(false);
        var document = ParserHolder.Parser.ParseDocument(html);

        var paragraphs = document.QuerySelectorAll("p")
            .Select(p => p.TextContent.Trim())
            .Where(text => text.Length > 0 && text != "&nbsp;")
            .ToList();

        return paragraphs.Count == 0 ? null : string.Join("\n\n", paragraphs);
    }

    private async Task<string> FetchHtmlAsync(string relativeUrl, CancellationToken cancellationToken)
    {
        var fullUrl = $"{BaseUrl}/{relativeUrl.TrimStart('/')}";
        if (!Uri.TryCreate(fullUrl, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException($"invalid target url '{fullUrl}'.");
        }

        // 与其他出网请求一致,先过 SSRF 安全校验。
        var errors = await SsrfGuard.InspectResolvedAsync(uri, resolver, cancellationToken).ConfigureAwait(false);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException($"ssrf: {string.Join("; ", errors)}");
        }

        using var response = await http
            .GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var bytes = await SourceResponseReader.ReadBoundedBytesAsync(
            response.Content,
            _maxResponseBytes,
            cancellationToken).ConfigureAwait(false);
        return Gb18030.GetString(bytes);
    }

    private const string BaseUrl = BaseUrlValue;
    private static readonly Encoding Gb18030 = SourceEncodings.Gb18030;

    private static SourceRuleExecutionLimits ValidateLimits(SourceRuleExecutionLimits? limits)
    {
        var value = limits ?? SourceRuleExecutionLimits.Default;
        value.Validate();
        return value;
    }

    private static Regex NewRegex(string pattern, TimeSpan timeout) =>
        new(pattern, RegexOptions.Compiled, timeout);

    /// <summary>延迟持有解析器,避免静态初始化顺序问题。</summary>
    private sealed class HtmlParserHolder
    {
        public AngleSharp.Html.Parser.HtmlParser Parser { get; } = new();
    }
}
