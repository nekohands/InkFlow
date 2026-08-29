using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Sources.Application;

public enum SourceCredentialBindingResultStatus
{
    Updated,
    Cleared,
    InvalidRequest,
    SourceNotFound,
}

public sealed record SourceCredentialBindingOperationResult(
    SourceCredentialBindingResultStatus Status,
    string SourceId,
    string? CredentialReferenceId);

public interface ISourceCredentialBindingService
{
    Task<SourceCredentialBindingOperationResult> SetDefaultAsync(
        string sourceId,
        string? credentialReferenceId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 管理来源级默认 CredentialReference。服务只持久化非敏感引用，不接触 secret。
/// </summary>
public sealed class SourceCredentialBindingService(
    ISourceRepository repository,
    TimeProvider clock) : ISourceCredentialBindingService
{
    private const int MaxSourceIdLength = 128;

    public async Task<SourceCredentialBindingOperationResult> SetDefaultAsync(
        string sourceId,
        string? credentialReferenceId,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidSourceId(sourceId) ||
            (credentialReferenceId is not null &&
             !SourceCredentialReferenceRules.IsValid(credentialReferenceId)))
        {
            return new(
                SourceCredentialBindingResultStatus.InvalidRequest,
                sourceId,
                null);
        }

        var source = await repository
            .GetAsync(sourceId, cancellationToken)
            .ConfigureAwait(false);
        if (source is null)
        {
            return new(
                SourceCredentialBindingResultStatus.SourceNotFound,
                sourceId,
                null);
        }

        source.SetDefaultCredentialReference(credentialReferenceId, clock.GetUtcNow());
        await repository.SaveAsync(source, cancellationToken).ConfigureAwait(false);

        return new(
            credentialReferenceId is null
                ? SourceCredentialBindingResultStatus.Cleared
                : SourceCredentialBindingResultStatus.Updated,
            source.Id,
            source.DefaultCredentialReferenceId);
    }

    private static bool IsValidSourceId(string? sourceId) =>
        !string.IsNullOrWhiteSpace(sourceId) &&
        sourceId.Length <= MaxSourceIdLength &&
        !sourceId.Any(character =>
            char.IsWhiteSpace(character) || char.IsControl(character));
}
