using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Sources.Application;

/// <summary>
/// Resolves the non-sensitive execution context into one short-lived request credential.
/// It owns scope validation and provider error handling, but never exposes provider details.
/// </summary>
internal sealed class RuleCredentialResolver
{
    private readonly ISourceCredentialProvider? _provider;

    public RuleCredentialResolver(ISourceCredentialProvider? provider)
    {
        _provider = provider;
    }

    public async Task<(SourceCredential? Credential, string? Error)> ResolveAsync(
        SourceExecutionContext? executionContext,
        CancellationToken callerCancellationToken,
        CancellationToken executionCancellationToken)
    {
        if (executionContext is null)
        {
            return (null, null);
        }

        if (!executionContext.HasCredentialReference)
        {
            return executionContext.CredentialOwnerScope is null ||
                   executionContext.EffectiveCredentialOwnerScope.IsValid
                ? (null, null)
                : (null, "credential: credential owner scope is invalid.");
        }

        var referenceId = executionContext.CredentialReferenceId!;
        if (string.IsNullOrWhiteSpace(executionContext.SourceId) ||
            executionContext.SourceId.Any(char.IsControl))
        {
            return (null, "credential: source execution context is invalid.");
        }

        if (!SourceCredentialReference.IsValid(referenceId))
        {
            return (null, "credential: credential reference is invalid.");
        }

        var ownerScope = executionContext.EffectiveCredentialOwnerScope;
        if (!ownerScope.IsValid)
        {
            return (null, "credential: credential owner scope is invalid.");
        }

        var resolutionContext = new SourceCredentialResolutionContext(
            executionContext.SourceId,
            referenceId,
            ownerScope);
        if (!resolutionContext.IsValid)
        {
            return (null, "credential: credential resolution context is invalid.");
        }

        if (_provider is null)
        {
            return (null, "credential: credential provider is unavailable.");
        }

        try
        {
            var credential = await _provider
                .ResolveAsync(
                    resolutionContext,
                    executionCancellationToken)
                .WaitAsync(executionCancellationToken)
                .ConfigureAwait(false);
            return credential is null
                ? (null, "credential: credential reference is unavailable.")
                : (credential, null);
        }
        catch (OperationCanceledException) when (callerCancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (executionCancellationToken.IsCancellationRequested)
        {
            return (null, "credential: credential resolution timed out.");
        }
        catch
        {
            // Provider exceptions may contain secret-store details; never expose them to the caller.
            return (null, "credential: credential resolution failed.");
        }
    }
}
