namespace InkFlow.Modules.Operations.Application;

internal sealed partial class ConsistencyRuleEvaluation
{
    private void CheckSources()
    {
        foreach (var group in snapshot.SourceBooks.GroupBy(
                     book => new SourceBookIdentity(book.SourceId, book.ExternalBookId)))
        {
            if (group.Skip(1).Any())
            {
                Add(
                    "source_book_duplicate_identity",
                    "source_book",
                    group.Key.ToString(),
                    "multiple SourceBook rows share the same (SourceId, ExternalBookId) identity.");
            }
        }

        foreach (var book in snapshot.SourceBooks)
        {
            if (!sources.ContainsKey(book.SourceId))
            {
                Add(
                    "source_book_source_missing",
                    "source_book",
                    SourceBookId(book.SourceId, book.ExternalBookId),
                    $"source book refers to missing source '{book.SourceId}'.");
            }
        }

        foreach (var group in snapshot.SourceChapters.GroupBy(
                     chapter => new SourceChapterIdentity(chapter.SourceBookId, chapter.ExternalChapterId)))
        {
            if (group.Skip(1).Any())
            {
                Add(
                    "source_chapter_duplicate_external_id",
                    "source_chapter",
                    group.Key.ToString(),
                    "multiple SourceChapter rows share the same external chapter identity within a source book.");
            }
        }

        foreach (var group in snapshot.SourceChapters.GroupBy(
                     chapter => new SourceChapterIndexIdentity(chapter.SourceBookId, chapter.Index)))
        {
            if (group.Skip(1).Any())
            {
                Add(
                    "source_chapter_duplicate_index",
                    "source_chapter",
                    group.Key.ToString(),
                    "multiple SourceChapter rows share the same chapter index within a source book.");
            }
        }

        foreach (var chapter in snapshot.SourceChapters)
        {
            if (!sourceBooks.ContainsKey(chapter.SourceBookId))
            {
                Add(
                    "source_chapter_book_missing",
                    "source_chapter",
                    chapter.Id.ToString("D"),
                    $"source chapter refers to missing SourceBook '{chapter.SourceBookId:D}'.");
            }
        }

        foreach (var artifact in snapshot.FetchArtifacts)
        {
            if (!sources.ContainsKey(artifact.SourceId))
            {
                Add(
                    "fetch_artifact_source_missing",
                    "fetch_artifact",
                    artifact.Id.ToString("D"),
                    $"fetch artifact refers to missing source '{artifact.SourceId}'.");
            }

            var sourceBookIdentity = new SourceBookIdentity(artifact.SourceId, artifact.ExternalBookId);
            if (!sourceBooksByIdentity.TryGetValue(sourceBookIdentity, out var sourceBook))
            {
                Add(
                    "fetch_artifact_book_missing",
                    "fetch_artifact",
                    artifact.Id.ToString("D"),
                    $"fetch artifact refers to missing source book '{SourceBookId(artifact.SourceId, artifact.ExternalBookId)}'.");
                continue;
            }

            if (!sourceChaptersByIdentity.ContainsKey(
                    new SourceChapterIdentity(sourceBook.Id, artifact.ExternalChapterId)))
            {
                Add(
                    "fetch_artifact_chapter_missing",
                    "fetch_artifact",
                    artifact.Id.ToString("D"),
                    $"fetch artifact refers to missing source chapter '{artifact.ExternalChapterId}' in source book '{sourceBook.Id:D}'.");
            }
        }

        foreach (var health in snapshot.SourceCapabilityHealth)
        {
            if (!sources.ContainsKey(health.SourceId))
            {
                Add(
                    "source_health_source_missing",
                    "source_capability_health",
                    HealthId(health),
                    $"capability health refers to missing source '{health.SourceId}'.");
            }

            if (!values.SourceCapabilities.Contains(health.Capability))
            {
                Add(
                    "source_health_capability_invalid",
                    "source_capability_health",
                    HealthId(health),
                    $"capability value '{health.Capability}' is not defined by SourceCapability.");
            }

            if (!values.SourceHealthStatuses.Contains(health.Status))
            {
                Add(
                    "source_health_status_invalid",
                    "source_capability_health",
                    HealthId(health),
                    $"status value '{health.Status}' is not defined by SourceHealthStatus.");
            }
        }

    }
}
