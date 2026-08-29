namespace InkFlow.Modules.Sources.Domain;

/// <summary>受控分页的续页方式。</summary>
public enum RulePaginationMode
{
    /// <summary>从响应中读取同源的下一页 URL。</summary>
    NextLink,

    /// <summary>在声明的 query/form 参数中递增页码。</summary>
    PageNumber,

    /// <summary>从响应中读取游标，并写入声明的 query/form 参数。</summary>
    Cursor,
}

/// <summary>
/// A bounded pagination declaration. Legacy instances default to next-link mode;
/// page-number and cursor modes use the declared request query/form parameter for
/// continuation and remain subject to the same finite execution budgets.
/// </summary>
public sealed record RulePagination(
    RuleSelector? NextPageSelector = null,
    string? NextPageAttribute = "href",
    int MaxPages = 8)
{
    /// <summary>未显式设置时保持 v1 的 next-link 语义。</summary>
    public RulePaginationMode Mode { get; init; } = RulePaginationMode.NextLink;

    /// <summary>
    /// PageNumber/Cursor 模式写入的 query 或 form 参数名；必须在 RuleRequest 中声明且只出现一次。
    /// </summary>
    public string? ParameterName { get; init; }

    /// <summary>PageNumber 模式的首个页码，允许从 0 开始。</summary>
    public int StartPage { get; init; } = 1;

    /// <summary>PageNumber 模式每次递增的步长。</summary>
    public int PageStep { get; init; } = 1;

    /// <summary>Cursor 模式从响应中抽取下一游标的选择器。</summary>
    public RuleSelector? CursorSelector { get; init; }

    /// <summary>Cursor 模式选择器的属性终端（通常用于 CSS/XPath）。</summary>
    public string? CursorAttribute { get; init; }
}
