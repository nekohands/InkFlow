using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Sources.Application;

internal sealed record RulePreRequestResult(
    IReadOnlyDictionary<string, string> Variables,
    int RequestsUsed,
    IReadOnlyList<string> Errors)
{
    public bool IsSuccess => Errors.Count == 0;

    public static RulePreRequestResult Success(
        IReadOnlyDictionary<string, string> variables,
        int requestsUsed) =>
        new(variables, requestsUsed, []);

    public static RulePreRequestResult Failure(IReadOnlyList<string> errors) =>
        new(new Dictionary<string, string>(StringComparer.Ordinal), 0, errors);
}

/// <summary>Executes the bounded serial pre-request chain and carries response variables forward.</summary>
internal sealed class RulePreRequestExecutor(
    SourceRuleExecutionLimits limits,
    RuleResponseEvaluator responseEvaluator)
{
    public async Task<RulePreRequestResult> ExecuteAsync(
        IReadOnlyList<RuleRequestStep> preRequests,
        CapabilityRule rule,
        string baseUrl,
        Uri sourceOrigin,
        SourceCredential? credential,
        IReadOnlyDictionary<string, string> initialVariables,
        SourceHttpRequest firstRequest,
        RuleHttpExecutor requestExecutor,
        int requestsUsed,
        CancellationToken executionCancellationToken)
    {
        var variables = new Dictionary<string, string>(
            initialVariables,
            StringComparer.Ordinal);

        for (var preIndex = 0; preIndex < preRequests.Count; preIndex++)
        {
            var step = preRequests[preIndex];
            SourceHttpRequest? preparedRequest;
            if (preIndex == 0)
            {
                if (!RuleCredentialRequestApplier.TryApply(
                        firstRequest,
                        credential,
                        out preparedRequest,
                        out var firstCredentialError))
                {
                    return RulePreRequestResult.Failure([firstCredentialError]);
                }
            }
            else
            {
                var buildErrors = RuleRequestPreparer.TryBuildInitialRequest(
                    step.Request,
                    rule.Capability,
                    baseUrl,
                    variables,
                    null,
                    null,
                    sourceOrigin,
                    credential,
                    out preparedRequest);
                if (buildErrors.Count > 0)
                {
                    return RulePreRequestResult.Failure(buildErrors);
                }
            }

            if (requestsUsed >= limits.MaxRequests)
            {
                return RulePreRequestResult.Failure(
                    ["execution: request budget exceeded."]);
            }

            requestsUsed++;
            var responseResult = await requestExecutor
                .SendAsync(preparedRequest!, "pre-request")
                .ConfigureAwait(false);
            if (responseResult.Error is not null)
            {
                return RulePreRequestResult.Failure([responseResult.Error]);
            }

            var variableErrors = responseEvaluator.TryBuildResponseVariables(
                step.ResponseVariables,
                $"rules[{rule.Capability}] preRequests[{preIndex}].responseVariables",
                responseResult.Response!.Body,
                variables,
                executionCancellationToken,
                out var updatedVariables);
            if (variableErrors.Count > 0)
            {
                return RulePreRequestResult.Failure(variableErrors);
            }

            variables = updatedVariables!;
        }

        return RulePreRequestResult.Success(variables, requestsUsed);
    }
}
