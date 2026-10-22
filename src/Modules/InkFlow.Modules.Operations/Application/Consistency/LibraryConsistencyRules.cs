namespace InkFlow.Modules.Operations.Application;

internal sealed partial class ConsistencyRuleEvaluation
{
    private void CheckLibrary()
    {
        foreach (var group in snapshot.CanonicalChapters.GroupBy(
                     chapter => new CanonicalChapterIndexIdentity(chapter.BookId, chapter.Index)))
        {
            if (group.Skip(1).Any())
            {
                Add(
                    "canonical_chapter_duplicate_index",
                    "canonical_chapter",
                    group.Key.ToString(),
                    "multiple CanonicalChapter rows share the same index within a canonical book.");
            }
        }

        foreach (var chapter in snapshot.CanonicalChapters)
        {
            if (!canonicalBooks.ContainsKey(chapter.BookId))
            {
                Add(
                    "canonical_chapter_book_missing",
                    "canonical_chapter",
                    chapter.Id.ToString("D"),
                    $"canonical chapter refers to missing CanonicalBook '{chapter.BookId:D}'.");
            }
        }

        foreach (var group in snapshot.MatchCandidates.GroupBy(
                     candidate => new SourceBookIdentity(candidate.SourceId, candidate.ExternalBookId)))
        {
            if (group.Skip(1).Any())
            {
                Add(
                    "match_candidate_duplicate_identity",
                    "match_candidate",
                    group.Key.ToString(),
                    "multiple MatchCandidate rows share the same source book identity.");
            }
        }

        foreach (var candidate in snapshot.MatchCandidates)
        {
            if (!canonicalBooks.ContainsKey(candidate.CanonicalBookId))
            {
                Add(
                    "match_candidate_canonical_book_missing",
                    "match_candidate",
                    candidate.Id.ToString("D"),
                    $"match candidate refers to missing canonical book '{candidate.CanonicalBookId:D}'.");
            }

            if (!sourceBooksByIdentity.ContainsKey(
                    new SourceBookIdentity(candidate.SourceId, candidate.ExternalBookId)))
            {
                Add(
                    "match_candidate_source_book_missing",
                    "match_candidate",
                    candidate.Id.ToString("D"),
                    $"match candidate refers to missing source book '{SourceBookId(candidate.SourceId, candidate.ExternalBookId)}'.");
            }

            if (!values.MatchCandidateStatuses.Contains(candidate.Status))
            {
                Add(
                    "match_candidate_status_invalid",
                    "match_candidate",
                    candidate.Id.ToString("D"),
                    $"status value '{candidate.Status}' is not defined by MatchCandidateStatus.");
            }
        }

        foreach (var group in snapshot.ChapterMappings.GroupBy(
                     mapping => new MappingIdentity(mapping.SourceId, mapping.ExternalChapterId)))
        {
            if (group.Skip(1).Any())
            {
                Add(
                    "chapter_mapping_duplicate_identity",
                    "chapter_mapping",
                    group.Key.ToString(),
                    "multiple ChapterMapping rows share the same (SourceId, ExternalChapterId) identity.");
            }
        }

        foreach (var mapping in snapshot.ChapterMappings)
        {
            if (!sourceChapters.TryGetValue(mapping.SourceChapterId, out var sourceChapter))
            {
                Add(
                    "chapter_mapping_source_chapter_missing",
                    "chapter_mapping",
                    mapping.Id.ToString("D"),
                    $"chapter mapping refers to missing source chapter '{mapping.SourceChapterId:D}'.");
            }
            else if (sourceBooks.TryGetValue(sourceChapter.SourceBookId, out var sourceBook))
            {
                if (!string.Equals(sourceBook.SourceId, mapping.SourceId, StringComparison.Ordinal))
                {
                    Add(
                        "chapter_mapping_source_id_mismatch",
                        "chapter_mapping",
                        mapping.Id.ToString("D"),
                        $"mapping source '{mapping.SourceId}' does not match source chapter's source '{sourceBook.SourceId}'.");
                }

                if (!string.Equals(sourceChapter.ExternalChapterId, mapping.ExternalChapterId, StringComparison.Ordinal))
                {
                    Add(
                        "chapter_mapping_external_id_mismatch",
                        "chapter_mapping",
                        mapping.Id.ToString("D"),
                        "mapping external chapter ID does not match the referenced SourceChapter.");
                }
            }

            if (!canonicalBooks.ContainsKey(mapping.CanonicalBookId))
            {
                Add(
                    "chapter_mapping_canonical_book_missing",
                    "chapter_mapping",
                    mapping.Id.ToString("D"),
                    $"chapter mapping refers to missing canonical book '{mapping.CanonicalBookId:D}'.");
            }

            if (!canonicalChapters.TryGetValue(mapping.CanonicalChapterId, out var canonicalChapter))
            {
                Add(
                    "chapter_mapping_canonical_chapter_missing",
                    "chapter_mapping",
                    mapping.Id.ToString("D"),
                    $"chapter mapping refers to missing canonical chapter '{mapping.CanonicalChapterId:D}'.");
            }
            else if (canonicalChapter.BookId != mapping.CanonicalBookId)
            {
                Add(
                    "chapter_mapping_canonical_book_mismatch",
                    "chapter_mapping",
                    mapping.Id.ToString("D"),
                    "mapping canonical book does not match the referenced CanonicalChapter parent.");
            }

            if (string.IsNullOrWhiteSpace(mapping.AlignmentAlgorithmVersion) ||
                string.IsNullOrWhiteSpace(mapping.AlignmentEvidence))
            {
                Add(
                    "chapter_mapping_explainability_missing",
                    "chapter_mapping",
                    mapping.Id.ToString("D"),
                    "chapter mapping must retain both alignment algorithm version and evidence.");
            }
        }

    }
}
