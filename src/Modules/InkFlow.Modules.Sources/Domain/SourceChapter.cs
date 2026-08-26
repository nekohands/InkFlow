namespace InkFlow.Modules.Sources.Domain;

/// <summary>
/// 来源章节：来源侧的目录条目，以 (SourceId, ExternalChapterId) 在来源内唯一定位。
/// Canonical Chapter 由 Library 模块另行创建并映射——本类型不含任何正典身份。
/// </summary>
public sealed record SourceChapter(
    Guid Id,
    Guid SourceBookId,
    string ExternalChapterId,
    int Index,
    string Title);
