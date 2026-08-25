namespace InkFlow.Modules.Sources.Domain;

/// <summary>
/// 来源可声明的能力。来源健康按能力细分：某能力故障不代表整个来源不可用。
/// </summary>
public enum SourceCapability
{
    Search,
    BookInfo,
    Toc,
    Content,
    Update,
}
