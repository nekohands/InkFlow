using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Sources.Application;

/// <summary>
/// linovelib 的规则型来源定义。
/// 搜索使用站点的 POST 表单，书目与章节链接统一剥离 <c>/novel/</c> 前缀，
/// 使外部 ID 可以直接填充 BookInfo/Content 的路径模板。
/// </summary>
public static class LinovelibSourceDefinition
{
    public const string SourceId = "linovelib";
    public const string BaseUrl = "https://www.linovelib.com";

    public static SourceRuleDsl BuildRuleDsl() => new("1", SourceId,
    [
        new CapabilityRule(
            SourceCapability.Search,
            new RuleRequest(
                RuleHttpMethod.Post,
                "/S6/",
                new Dictionary<string, string>(),
                new Dictionary<string, string>(),
                new Dictionary<string, string> { ["searchkey"] = "{key}" }),
            [],
            List: new RuleListBinding(
                ItemsSelector: "ul li a[href*='/novel/']",
                ExternalIdAttribute: "href",
                IdPrefixToStrip: "/novel/",
                IdSuffixToStrip: ".html")),
        new CapabilityRule(
            SourceCapability.BookInfo,
            RuleRequest.Get("/novel/{bookId}.html"),
            [
                new RuleField("title",
                    new RuleSelector(SelectorKind.Css, "meta[property='og:novel:book_name']"),
                    null, [], Attribute: "content"),
                new RuleField("author",
                    new RuleSelector(SelectorKind.Css, "meta[property='og:novel:author']"),
                    null, [], Attribute: "content"),
            ]),
        new CapabilityRule(
            SourceCapability.Toc,
            RuleRequest.Get("/novel/{bookId}/catalog"),
            [],
            List: new RuleListBinding(
                ItemsSelector: "ul li a[href*='/novel/']",
                ExternalIdAttribute: "href",
                IdPrefixToStrip: "/novel/",
                IdSuffixToStrip: ".html")),
        new CapabilityRule(
            SourceCapability.Content,
            RuleRequest.Get("/novel/{chapterId}.html"),
            [new RuleField("content", new RuleSelector(SelectorKind.Css, "p"), null, [])]),
    ]);
}
