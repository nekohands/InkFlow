using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Sources.Application;

/// <summary>
/// Coordinates mode-specific preparation after a rule request has been rendered.
/// </summary>
internal static class RuleRequestPreparer
{
    public static List<string> TryBuildInitialRequest(
        RuleRequest requestDefinition,
        SourceCapability capability,
        string baseUrl,
        IReadOnlyDictionary<string, string> variables,
        string? overrideParameterName,
        string? overrideParameterValue,
        Uri sourceOrigin,
        SourceCredential? credential,
        out SourceHttpRequest? request)
    {
        var errors = RuleRequestBuilder.TryBuildRequest(
            requestDefinition,
            capability,
            baseUrl,
            variables,
            overrideParameterName,
            overrideParameterValue,
            out var builtRequest);
        request = null;
        if (errors.Count > 0)
        {
            return errors;
        }

        if (!RuleRequestUrlPolicy.TryValidateInitialRequest(
                builtRequest!,
                sourceOrigin,
                out var requestError))
        {
            return [requestError];
        }

        if (!RuleCredentialRequestApplier.TryApply(
                builtRequest!,
                credential,
                out request,
                out var credentialError))
        {
            return [credentialError];
        }

        return [];
    }

    public static List<string> TryBuildContinuationRequest(
        CapabilityRule rule,
        string baseUrl,
        Uri sourceOrigin,
        string? parameterName,
        string parameterValue,
        IReadOnlyDictionary<string, string> variables,
        SourceCredential? credential,
        out SourceHttpRequest? request)
    {
        var errors = RuleRequestBuilder.TryBuildRequest(
            rule,
            baseUrl,
            variables,
            parameterName,
            parameterValue,
            out var builtRequest);
        request = null;
        if (errors.Count > 0)
        {
            return errors;
        }

        if (!RuleCredentialRequestApplier.TryApply(
                builtRequest!,
                credential,
                out var preparedRequest,
                out var credentialError))
        {
            return [credentialError];
        }

        if (!RuleRequestUrlPolicy.TryValidateContinuationRequest(
                preparedRequest!,
                sourceOrigin,
                out var continuationError))
        {
            return [continuationError];
        }

        request = preparedRequest;
        return [];
    }
}
