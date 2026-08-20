namespace InkFlow.Modules.Legado;

public sealed record LegadoSearchBook(
    string Name,
    string Author,
    string BookUrl,
    string? CoverUrl,
    string? Intro,
    string? LastChapter);

public sealed record LegadoBookInfo(
    string Name,
    string Author,
    string BookUrl,
    string TocUrl,
    string? CoverUrl,
    string? Intro,
    string? LastChapter);

public sealed record LegadoChapter(string Name, string Url, long Sequence);
public sealed record LegadoContent(string Content);
