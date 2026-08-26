namespace InkFlow.Modules.Content.Domain;

/// <summary>正文段落：AST v1 的最小节点。</summary>
public sealed record ContentParagraph(int Position, string Text);

/// <summary>
/// 规范化后的章节内容文档（AST v1）：段落的有序序列。
/// 上游来源的排版差异在 Normalizer 中抹平；本类型之后的任何消费
/// （渲染、CanonicalHash、质量评估）都只面对规范化形态。
/// </summary>
public sealed record ContentDocument(IReadOnlyList<ContentParagraph> Paragraphs)
{
    /// <summary>以换行连接的规范化纯文本。</summary>
    public string CanonicalText => string.Join("\n\n", Paragraphs.Select(p => p.Text));

    public static ContentDocument Empty { get; } = new([]);
}
