using System.Globalization;
using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Sources.Application;

/// <summary>
/// Executes the bounded continuation loop for a rule's pagination strategy.
/// It owns page state, cycle detection and continuation request construction.
/// </summary>
internal sealed class RulePaginationExecutor
{
    private readonly SourceRuleExecutionLimits _limits;
    private readonly RuleResponseEvaluator _responseEvaluator;
    private readonly RuleResponseValueExtractor _responseValueExtractor;

    public RulePaginationExecutor(
        SourceRuleExecutionLimits limits,
        RuleResponseEvaluator responseEvaluator,
        RuleResponseValueExtractor responseValueExtractor)
    {
        _limits = limits;
        _responseEvaluator = responseEvaluator;
        _responseValueExtractor = responseValueExtractor;
    }

    public async Task<RuleExecutionResult> ExecuteAsync(
        CapabilityRule rule,
        string baseUrl,
        Uri sourceOrigin,
        RulePagination pagination,
        SourceHttpRequest initialRequest,
        SourceCredential? credential,
        IReadOnlyDictionary<string, string> initialVariables,
        int requestsUsed,
        RuleHttpExecutor requestExecutor,
        CancellationToken executionCancellationToken)
    {
        var currentRequest = initialRequest;
        var continuationVariables = new Dictionary<string, string>(
            initialVariables,
            StringComparer.Ordinal);
        var pageBodies = new List<string>();
        var visitedUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visitedCursors = new HashSet<string>(StringComparer.Ordinal);

        for (var page = 1; ; page++)
        {
            if (requestsUsed >= _limits.MaxRequests)
            {
                return RuleExecutionResult.Fail(["execution: request budget exceeded."]);
            }

            if (page > pagination.MaxPages)
            {
                return RuleExecutionResult.Fail(["pagination: page limit exceeded."]);
            }

            if (pagination.Mode == RulePaginationMode.NextLink &&
                !visitedUrls.Add(currentRequest.Url))
            {
                return RuleExecutionResult.Fail(["pagination: next-link cycle detected."]);
            }

            requestsUsed++;
            var responseResult = await requestExecutor
                .SendAsync(currentRequest, "request")
                .ConfigureAwait(false);
            if (responseResult.Error is not null)
            {
                return RuleExecutionResult.Fail([responseResult.Error]);
            }

            var response = responseResult.Response!;
            pageBodies.Add(response.Body);

            if (pagination.Mode is not (RulePaginationMode.NextLink or
                RulePaginationMode.PageNumber or
                RulePaginationMode.Cursor))
            {
                return RuleExecutionResult.Fail(["pagination: mode is not supported."]);
            }

            var continuationValue = pagination.Mode == RulePaginationMode.Cursor
                ? _responseValueExtractor.EvaluateFirst(
                    response.Body,
                    pagination.CursorSelector!,
                    pagination.CursorAttribute,
                    executionCancellationToken)
                : _responseValueExtractor.EvaluateFirst(
                    response.Body,
                    pagination.NextPageSelector!,
                    pagination.NextPageAttribute,
                    executionCancellationToken);
            if (string.IsNullOrWhiteSpace(continuationValue))
            {
                return CompletePagination(rule, pageBodies, executionCancellationToken);
            }

            if (page >= pagination.MaxPages)
            {
                return RuleExecutionResult.Fail(["pagination: page limit exceeded."]);
            }

            if (requestsUsed >= _limits.MaxRequests)
            {
                return RuleExecutionResult.Fail(["execution: request budget exceeded."]);
            }

            switch (pagination.Mode)
            {
                case RulePaginationMode.NextLink:
                    {
                        if (!RuleRequestUrlPolicy.TryBuildNextRequest(
                                currentRequest,
                                continuationValue!,
                                sourceOrigin,
                                out var nextRequest,
                                out var nextRequestError))
                        {
                            return RuleExecutionResult.Fail([nextRequestError]);
                        }

                        currentRequest = nextRequest!;
                        break;
                    }

                case RulePaginationMode.PageNumber:
                    {
                        var responseVariableErrors = TryUpdateContinuationVariables(
                            rule,
                            response.Body,
                            continuationVariables,
                            executionCancellationToken,
                            out var updatedVariables);
                        if (responseVariableErrors.Count > 0)
                        {
                            return RuleExecutionResult.Fail(responseVariableErrors);
                        }

                        continuationVariables = updatedVariables!;

                        var nextPageValue = (long)pagination.StartPage +
                            (long)page * pagination.PageStep;
                        if (nextPageValue > SourceRuleDslValidator.MaxPaginationPageValue)
                        {
                            return RuleExecutionResult.Fail(
                                ["pagination: generated page value exceeds the allowed range."]);
                        }

                        var continuationErrors = RuleRequestPreparer.TryBuildContinuationRequest(
                            rule,
                            baseUrl,
                            sourceOrigin,
                            pagination.ParameterName,
                            nextPageValue.ToString(CultureInfo.InvariantCulture),
                            continuationVariables,
                            credential,
                            out var preparedNextRequest);
                        if (continuationErrors.Count > 0)
                        {
                            return RuleExecutionResult.Fail(continuationErrors);
                        }

                        currentRequest = preparedNextRequest!;
                        break;
                    }

                case RulePaginationMode.Cursor:
                    {
                        var responseVariableErrors = TryUpdateContinuationVariables(
                            rule,
                            response.Body,
                            continuationVariables,
                            executionCancellationToken,
                            out var updatedVariables);
                        if (responseVariableErrors.Count > 0)
                        {
                            return RuleExecutionResult.Fail(responseVariableErrors);
                        }

                        continuationVariables = updatedVariables!;

                        var cursor = continuationValue!.Trim();
                        if (cursor.Length > SourceRuleDslValidator.MaxPaginationCursorLength ||
                            cursor.Any(char.IsControl))
                        {
                            return RuleExecutionResult.Fail(
                                ["pagination: cursor value is invalid or too long."]);
                        }

                        if (!visitedCursors.Add(cursor))
                        {
                            return RuleExecutionResult.Fail(["pagination: cursor cycle detected."]);
                        }

                        var continuationErrors = RuleRequestPreparer.TryBuildContinuationRequest(
                            rule,
                            baseUrl,
                            sourceOrigin,
                            pagination.ParameterName,
                            cursor,
                            continuationVariables,
                            credential,
                            out var preparedNextRequest);
                        if (continuationErrors.Count > 0)
                        {
                            return RuleExecutionResult.Fail(continuationErrors);
                        }

                        currentRequest = preparedNextRequest!;
                        break;
                    }

                default:
                    return RuleExecutionResult.Fail(["pagination: mode is not supported."]);
            }
        }
    }

    private List<string> TryUpdateContinuationVariables(
        CapabilityRule rule,
        string body,
        IReadOnlyDictionary<string, string> currentVariables,
        CancellationToken executionCancellationToken,
        out Dictionary<string, string>? updatedVariables) =>
        _responseEvaluator.TryBuildResponseVariables(
            rule.ResponseVariables,
            $"rules[{rule.Capability}] responseVariables",
            body,
            currentVariables,
            executionCancellationToken,
            out updatedVariables);

    private RuleExecutionResult CompletePagination(
        CapabilityRule rule,
        IReadOnlyList<string> pageBodies,
        CancellationToken executionCancellationToken)
    {
        var extracted = _responseEvaluator.ExtractFields(
            rule,
            pageBodies[0],
            executionCancellationToken);
        return extracted.IsSuccess
            ? extracted with { PageBodies = pageBodies.ToArray() }
            : extracted;
    }
}
