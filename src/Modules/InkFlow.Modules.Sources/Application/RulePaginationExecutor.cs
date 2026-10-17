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

    public RulePaginationExecutor(
        SourceRuleExecutionLimits limits,
        RuleResponseEvaluator responseEvaluator)
    {
        _limits = limits;
        _responseEvaluator = responseEvaluator;
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

            switch (pagination.Mode)
            {
                case RulePaginationMode.NextLink:
                    {
                        var nextLink = _responseEvaluator.EvaluateFirst(
                            response.Body,
                            pagination.NextPageSelector!,
                            pagination.NextPageAttribute,
                            executionCancellationToken);
                        if (string.IsNullOrWhiteSpace(nextLink))
                        {
                            return CompletePagination(rule, pageBodies, executionCancellationToken);
                        }

                        if (!RuleRequestBuilder.TryContinueAfterPage(
                                page,
                                requestsUsed,
                                pagination,
                                _limits,
                                out var limitError))
                        {
                            return RuleExecutionResult.Fail([limitError]);
                        }

                        if (!RuleRequestBuilder.TryBuildNextRequest(
                                currentRequest,
                                nextLink,
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
                        var hasNextPage = _responseEvaluator.EvaluateFirst(
                            response.Body,
                            pagination.NextPageSelector!,
                            pagination.NextPageAttribute,
                            executionCancellationToken);
                        if (string.IsNullOrWhiteSpace(hasNextPage))
                        {
                            return CompletePagination(rule, pageBodies, executionCancellationToken);
                        }

                        if (!RuleRequestBuilder.TryContinueAfterPage(
                                page,
                                requestsUsed,
                                pagination,
                                _limits,
                                out var limitError))
                        {
                            return RuleExecutionResult.Fail([limitError]);
                        }

                        var responseVariableErrors = _responseEvaluator.TryBuildResponseVariables(
                            rule.ResponseVariables,
                            $"rules[{rule.Capability}] responseVariables",
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

                        var pageBuildErrors = RuleRequestBuilder.TryBuildRequest(
                            rule,
                            baseUrl,
                            continuationVariables,
                            pagination.ParameterName,
                            nextPageValue.ToString(CultureInfo.InvariantCulture),
                            out var nextRequest);
                        if (pageBuildErrors.Count > 0)
                        {
                            return RuleExecutionResult.Fail(pageBuildErrors);
                        }

                        if (!RuleRequestBuilder.TryApplyCredential(
                                nextRequest!,
                                credential,
                                out var preparedNextRequest,
                                out var pageCredentialError))
                        {
                            return RuleExecutionResult.Fail([pageCredentialError]);
                        }

                        if (!RuleRequestBuilder.TryValidateContinuationRequest(
                                preparedNextRequest!,
                                sourceOrigin,
                                out var continuationError))
                        {
                            return RuleExecutionResult.Fail([continuationError]);
                        }

                        currentRequest = preparedNextRequest!;
                        break;
                    }

                case RulePaginationMode.Cursor:
                    {
                        var rawCursor = _responseEvaluator.EvaluateFirst(
                            response.Body,
                            pagination.CursorSelector!,
                            pagination.CursorAttribute,
                            executionCancellationToken);
                        if (string.IsNullOrWhiteSpace(rawCursor))
                        {
                            return CompletePagination(rule, pageBodies, executionCancellationToken);
                        }

                        if (!RuleRequestBuilder.TryContinueAfterPage(
                                page,
                                requestsUsed,
                                pagination,
                                _limits,
                                out var limitError))
                        {
                            return RuleExecutionResult.Fail([limitError]);
                        }

                        var responseVariableErrors = _responseEvaluator.TryBuildResponseVariables(
                            rule.ResponseVariables,
                            $"rules[{rule.Capability}] responseVariables",
                            response.Body,
                            continuationVariables,
                            executionCancellationToken,
                            out var updatedVariables);
                        if (responseVariableErrors.Count > 0)
                        {
                            return RuleExecutionResult.Fail(responseVariableErrors);
                        }

                        continuationVariables = updatedVariables!;

                        var cursor = rawCursor.Trim();
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

                        var cursorBuildErrors = RuleRequestBuilder.TryBuildRequest(
                            rule,
                            baseUrl,
                            continuationVariables,
                            pagination.ParameterName,
                            cursor,
                            out var nextRequest);
                        if (cursorBuildErrors.Count > 0)
                        {
                            return RuleExecutionResult.Fail(cursorBuildErrors);
                        }

                        if (!RuleRequestBuilder.TryApplyCredential(
                                nextRequest!,
                                credential,
                                out var preparedNextRequest,
                                out var cursorCredentialError))
                        {
                            return RuleExecutionResult.Fail([cursorCredentialError]);
                        }

                        if (!RuleRequestBuilder.TryValidateContinuationRequest(
                                preparedNextRequest!,
                                sourceOrigin,
                                out var continuationError))
                        {
                            return RuleExecutionResult.Fail([continuationError]);
                        }

                        currentRequest = preparedNextRequest!;
                        break;
                    }

                default:
                    return RuleExecutionResult.Fail(["pagination: mode is not supported."]);
            }
        }
    }

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
