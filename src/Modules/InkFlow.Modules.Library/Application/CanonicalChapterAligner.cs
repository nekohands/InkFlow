using InkFlow.Modules.Library.Domain;
using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Library.Application;

internal sealed record CanonicalChapterAlignment(
    CanonicalChapter? Chapter,
    string Evidence);

/// <summary>章节对齐策略：只决定复用哪一章，不负责读取或持久化。</summary>
internal static class CanonicalChapterAligner
{
    public static CanonicalChapterAlignment Align(
        CanonicalBook canonicalBook,
        SourceChapter sourceChapter)
    {
        var normalizedTitle = NormalizeTitle(sourceChapter.Title);

        var samePosition = canonicalBook.Chapters.FirstOrDefault(chapter =>
            chapter.Index == sourceChapter.Index &&
            NormalizeTitle(chapter.Title) == normalizedTitle);
        if (samePosition is not null)
        {
            return new(
                samePosition,
                $"source-index={sourceChapter.Index};canonical-index={samePosition.Index};normalized-title");
        }

        var sameTitle = canonicalBook.Chapters
            .Where(chapter => NormalizeTitle(chapter.Title) == normalizedTitle)
            .ToList();
        if (sameTitle.Count == 1)
        {
            var match = sameTitle[0];
            return new(
                match,
                $"source-index={sourceChapter.Index};canonical-index={match.Index};unique-normalized-title");
        }

        return new(null, $"source-index={sourceChapter.Index};new-canonical-chapter");
    }

    private static string NormalizeTitle(string title) =>
        string.Concat(title.Where(c => !char.IsWhiteSpace(c) && !char.IsPunctuation(c)))
            .ToLowerInvariant();
}
