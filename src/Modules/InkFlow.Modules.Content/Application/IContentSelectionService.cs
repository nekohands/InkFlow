using InkFlow.Modules.Content.Domain;

namespace InkFlow.Modules.Content.Application;

public sealed record ContentSelectionOutcome(
    bool IsSuccess,
    ContentVersion? SelectedVersion,
    bool Changed,
    bool UsedFallback,
    string AlgorithmVersion,
    string Evidence,
    IReadOnlyList<string> Errors)
{
    public static ContentSelectionOutcome Ok(
        ContentVersion selectedVersion,
        bool changed,
        bool usedFallback,
        string evidence) =>
        new(
            true,
            selectedVersion,
            changed,
            usedFallback,
            ContentSelectionAlgorithm.Version,
            evidence,
            []);

    public static ContentSelectionOutcome Fail(IReadOnlyList<string> errors) =>
        new(
            false,
            null,
            false,
            false,
            ContentSelectionAlgorithm.Version,
            string.Empty,
            errors);
}

/// <summary>集中执行健康感知、可解释、可审计的正文当前版本选择。</summary>
public interface IContentSelectionService
{
    Task<ContentSelectionOutcome> SelectCurrentAsync(
        Guid canonicalChapterId,
        CancellationToken cancellationToken = default);
}

/// <summary>正文选择审计记录的权威存储契约。</summary>
public interface IContentSelectionDecisionRepository
{
    Task AddAsync(
        ContentSelectionDecision decision,
        CancellationToken cancellationToken = default);

    Task<ContentSelectionDecision?> GetLatestAsync(
        Guid canonicalChapterId,
        CancellationToken cancellationToken = default);
}
