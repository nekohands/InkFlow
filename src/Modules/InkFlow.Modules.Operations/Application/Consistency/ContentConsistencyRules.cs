namespace InkFlow.Modules.Operations.Application;

internal sealed partial class ConsistencyRuleEvaluation
{
    private void CheckContent()
    {
        foreach (var group in snapshot.ContentVersions.GroupBy(
                     version => new ContentIdentity(version.CanonicalChapterId, version.CanonicalHash)))
        {
            if (group.Skip(1).Any())
            {
                Add(
                    "content_version_duplicate_hash",
                    "content_version",
                    group.Key.ToString(),
                    "multiple ContentVersion rows share the same canonical hash within a chapter.");
            }
        }

        foreach (var group in snapshot.ContentVersions.Where(version => version.IsCurrent)
                     .GroupBy(version => version.CanonicalChapterId))
        {
            if (group.Skip(1).Any())
            {
                Add(
                    "content_version_multiple_current",
                    "canonical_chapter",
                    group.Key.ToString("D"),
                    "more than one ContentVersion is marked current for the same canonical chapter.");
            }
        }

        foreach (var version in snapshot.ContentVersions)
        {
            if (!sources.ContainsKey(version.SourceId))
            {
                Add(
                    "content_version_source_missing",
                    "content_version",
                    version.Id.ToString("D"),
                    $"content version refers to missing source '{version.SourceId}'.");
            }

            if (!canonicalBooks.ContainsKey(version.CanonicalBookId))
            {
                Add(
                    "content_version_canonical_book_missing",
                    "content_version",
                    version.Id.ToString("D"),
                    $"content version refers to missing canonical book '{version.CanonicalBookId:D}'.");
            }

            if (!canonicalChapters.TryGetValue(version.CanonicalChapterId, out var canonicalChapter))
            {
                Add(
                    "content_version_canonical_chapter_missing",
                    "content_version",
                    version.Id.ToString("D"),
                    $"content version refers to missing canonical chapter '{version.CanonicalChapterId:D}'.");
            }
            else if (canonicalChapter.BookId != version.CanonicalBookId)
            {
                Add(
                    "content_version_canonical_book_mismatch",
                    "content_version",
                    version.Id.ToString("D"),
                    "content version canonical book does not match the referenced CanonicalChapter parent.");
            }

            if (string.IsNullOrWhiteSpace(version.CanonicalHash))
            {
                Add(
                    "content_version_hash_missing",
                    "content_version",
                    version.Id.ToString("D"),
                    "content version must retain a canonical hash.");
            }

            if (version.CanonicalTextLength < 1 || version.ParagraphCount < 1)
            {
                Add(
                    "content_version_payload_invalid",
                    "content_version",
                    version.Id.ToString("D"),
                    "content version must retain non-empty canonical text and a positive paragraph count.");
            }
        }

        foreach (var decision in snapshot.ContentSelectionDecisions)
        {
            if (!canonicalChapters.ContainsKey(decision.CanonicalChapterId))
            {
                Add(
                    "content_selection_chapter_missing",
                    "content_selection_decision",
                    decision.Id.ToString("D"),
                    $"selection decision refers to missing canonical chapter '{decision.CanonicalChapterId:D}'.");
            }

            if (!contentVersions.TryGetValue(decision.SelectedVersionId, out var selectedVersion))
            {
                Add(
                    "content_selection_version_missing",
                    "content_selection_decision",
                    decision.Id.ToString("D"),
                    $"selection decision refers to missing content version '{decision.SelectedVersionId:D}'.");
            }
            else
            {
                if (selectedVersion.CanonicalChapterId != decision.CanonicalChapterId)
                {
                    Add(
                        "content_selection_chapter_mismatch",
                        "content_selection_decision",
                        decision.Id.ToString("D"),
                        "selection decision chapter does not match the selected content version chapter.");
                }

                if (!selectedVersion.IsCurrent)
                {
                    Add(
                        "content_selection_version_not_current",
                        "content_selection_decision",
                        decision.Id.ToString("D"),
                        "the selected version recorded by the decision is not marked current.");
                }
            }

            if (string.IsNullOrWhiteSpace(decision.AlgorithmVersion) ||
                string.IsNullOrWhiteSpace(decision.Evidence))
            {
                Add(
                    "content_selection_explainability_missing",
                    "content_selection_decision",
                    decision.Id.ToString("D"),
                    "selection decision must retain both algorithm version and evidence.");
            }
        }

        foreach (var group in snapshot.ContentSelectionDecisions.GroupBy(
                     decision => decision.CanonicalChapterId))
        {
            var latest = group
                .OrderByDescending(decision => decision.CreatedAt)
                .ThenByDescending(decision => decision.Id)
                .First();

            if (contentVersions.TryGetValue(latest.SelectedVersionId, out var selectedVersion) &&
                !selectedVersion.IsCurrent)
            {
                Add(
                    "content_selection_latest_not_current",
                    "canonical_chapter",
                    latest.CanonicalChapterId.ToString("D"),
                    $"latest selection decision '{latest.Id:D}' does not point to a current content version.");
            }
        }

    }
}
