using System.Globalization;
using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Sources.Application;

/// <summary>
/// 规则执行编排入口：负责验证声明、安排预请求/主请求/分页，并把请求构造、HTTP Session
/// 与响应抽取交给各自的内部责任单元。执行顺序和所有 fail-closed 边界保持稳定。
/// </summary>
public sealed class RuleAdapter
{
    private readonly ISourceHttpClient _httpClient;
    private readonly SourceRuleExecutionLimits _limits;
    private readonly RuleCredentialResolver _credentialResolver;
    private readonly RuleResponseEvaluator _responseEvaluator;

    public RuleAdapter(
        ISourceHttpClient httpClient,
        ISelectorEvaluator selectorEvaluator,
        SourceRuleExecutionLimits? limits = null,
        ISourceCredentialProvider? credentialProvider = null)
    {
        _httpClient = httpClient;
        _limits = limits ?? SourceRuleExecutionLimits.Default;
        _credentialResolver = new RuleCredentialResolver(credentialProvider);
        _limits.Validate();
        _responseEvaluator = new RuleResponseEvaluator(selectorEvaluator, _limits);
    }

    public SourceRuleExecutionLimits Limits => _limits;

    public async Task<RuleExecutionResult> ExecuteAsync(
        CapabilityRule rule,
        string baseUrl,
        IReadOnlyDictionary<string, string>? variables = null,
        CancellationToken cancellationToken = default,
        SourceExecutionContext? executionContext = null)
    {
        variables ??= new Dictionary<string, string>();

        var variableErrors = RuleRequestBuilder.ValidateVariables(_limits, variables);
        if (variableErrors.Count > 0)
        {
            return RuleExecutionResult.Fail(variableErrors);
        }

        var pagination = rule.Pagination;
        if (pagination is not null)
        {
            var paginationErrors = SourceRuleDslValidator.ValidatePagination(rule);
            if (paginationErrors.Count > 0)
            {
                return RuleExecutionResult.Fail(paginationErrors);
            }
        }

        if (rule.Session is not null)
        {
            var sessionErrors = SourceRuleDslValidator.ValidateSession(rule);
            if (sessionErrors.Count > 0)
            {
                return RuleExecutionResult.Fail(sessionErrors);
            }
        }

        var responseVariableDeclarationErrors =
            SourceRuleDslValidator.ValidateResponseVariables(rule);
        if (responseVariableDeclarationErrors.Count > 0)
        {
            return RuleExecutionResult.Fail(responseVariableDeclarationErrors);
        }

        var preRequestDeclarationErrors = SourceRuleDslValidator.ValidatePreRequests(rule);
        if (preRequestDeclarationErrors.Count > 0)
        {
            return RuleExecutionResult.Fail(preRequestDeclarationErrors);
        }

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var sourceOrigin))
        {
            return RuleExecutionResult.Fail(["request: source base URL must be absolute."]);
        }

        var preRequests = rule.PreRequests ?? [];
        var continuationVariables = new Dictionary<string, string>(
            variables,
            StringComparer.Ordinal);
        var initialParameterName = pagination?.Mode == RulePaginationMode.PageNumber
            ? pagination.ParameterName
            : null;
        var initialParameterValue = pagination?.Mode == RulePaginationMode.PageNumber
            ? pagination.StartPage.ToString(CultureInfo.InvariantCulture)
            : null;

        // Inspect the first request before resolving credentials. With a pre-request chain this
        // is the first step; otherwise it is the capability's main request.
        var firstRequestDefinition = preRequests.Count > 0
            ? preRequests[0].Request
            : rule.Request;
        var firstBuildErrors = RuleRequestBuilder.TryBuildRequest(
            firstRequestDefinition,
            rule.Capability,
            baseUrl,
            continuationVariables,
            preRequests.Count == 0 ? initialParameterName : null,
            preRequests.Count == 0 ? initialParameterValue : null,
            out var firstRequest);
        if (firstBuildErrors.Count > 0)
        {
            return RuleExecutionResult.Fail(firstBuildErrors);
        }

        // 出网前的最后一道闸：字面量 SSRF 校验失败绝不发起请求。
        if (!RuleRequestBuilder.TryValidateInitialRequest(
                firstRequest!,
                sourceOrigin,
                out var firstRequestError))
        {
            return RuleExecutionResult.Fail([firstRequestError]);
        }

        // A zero request budget is an explicit fail-closed switch and must be checked before
        // entering the HTTP seam, including when pagination is declared.
        if (_limits.MaxRequests < 1)
        {
            return RuleExecutionResult.Fail(["execution: request budget exceeded."]);
        }

        using var executionCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        executionCancellation.CancelAfter(_limits.MaxExecutionTime);

        try
        {
            // 只有请求结构、SSRF 和执行预算都通过后才解析 secret，减少不必要的敏感材料驻留。
            var credentialResolution = await _credentialResolver.ResolveAsync(
                    executionContext,
                    cancellationToken,
                    executionCancellation.Token)
                .ConfigureAwait(false);
            if (credentialResolution.Error is not null)
            {
                return RuleExecutionResult.Fail([credentialResolution.Error]);
            }

            var cookieJar = rule.Session is null ? null : new RuleCookieJar(rule.Session);
            var requestExecutor = new RuleHttpExecutor(
                _httpClient,
                _limits,
                sourceOrigin,
                cookieJar,
                cancellationToken,
                executionCancellation.Token);
            var requestCount = 0;

            for (var preIndex = 0; preIndex < preRequests.Count; preIndex++)
            {
                var step = preRequests[preIndex];
                SourceHttpRequest? preparedStepRequest;
                if (preIndex == 0)
                {
                    if (!RuleRequestBuilder.TryApplyCredential(
                            firstRequest!,
                            credentialResolution.Credential,
                            out preparedStepRequest,
                            out var firstCredentialError))
                    {
                        return RuleExecutionResult.Fail([firstCredentialError]);
                    }
                }
                else
                {
                    var stepBuildErrors = RuleRequestBuilder.TryBuildRequest(
                        step.Request,
                        rule.Capability,
                        baseUrl,
                        continuationVariables,
                        null,
                        null,
                        out var stepRequest);
                    if (stepBuildErrors.Count > 0)
                    {
                        return RuleExecutionResult.Fail(stepBuildErrors);
                    }

                    if (!RuleRequestBuilder.TryValidateInitialRequest(
                            stepRequest!,
                            sourceOrigin,
                            out var stepRequestError))
                    {
                        return RuleExecutionResult.Fail([stepRequestError]);
                    }

                    if (!RuleRequestBuilder.TryApplyCredential(
                            stepRequest!,
                            credentialResolution.Credential,
                            out preparedStepRequest,
                            out var stepCredentialError))
                    {
                        return RuleExecutionResult.Fail([stepCredentialError]);
                    }
                }

                if (requestCount >= _limits.MaxRequests)
                {
                    return RuleExecutionResult.Fail(["execution: request budget exceeded."]);
                }

                requestCount++;
                var stepResult = await requestExecutor
                    .SendAsync(preparedStepRequest!, "pre-request")
                    .ConfigureAwait(false);
                if (stepResult.Error is not null)
                {
                    return RuleExecutionResult.Fail([stepResult.Error]);
                }

                var stepVariableErrors = _responseEvaluator.TryBuildResponseVariables(
                    step.ResponseVariables,
                    $"rules[{rule.Capability}] preRequests[{preIndex}].responseVariables",
                    stepResult.Response!.Body,
                    continuationVariables,
                    executionCancellation.Token,
                    out var updatedVariables);
                if (stepVariableErrors.Count > 0)
                {
                    return RuleExecutionResult.Fail(stepVariableErrors);
                }

                continuationVariables = updatedVariables!;
            }

            SourceHttpRequest? preparedMainRequest;
            if (preRequests.Count == 0)
            {
                if (!RuleRequestBuilder.TryApplyCredential(
                        firstRequest!,
                        credentialResolution.Credential,
                        out preparedMainRequest,
                        out var mainCredentialError))
                {
                    return RuleExecutionResult.Fail([mainCredentialError]);
                }
            }
            else
            {
                var mainBuildErrors = RuleRequestBuilder.TryBuildRequest(
                    rule.Request,
                    rule.Capability,
                    baseUrl,
                    continuationVariables,
                    initialParameterName,
                    initialParameterValue,
                    out var mainRequest);
                if (mainBuildErrors.Count > 0)
                {
                    return RuleExecutionResult.Fail(mainBuildErrors);
                }

                if (!RuleRequestBuilder.TryValidateInitialRequest(
                        mainRequest!,
                        sourceOrigin,
                        out var mainRequestError))
                {
                    return RuleExecutionResult.Fail([mainRequestError]);
                }

                if (!RuleRequestBuilder.TryApplyCredential(
                        mainRequest!,
                        credentialResolution.Credential,
                        out preparedMainRequest,
                        out var mainCredentialError))
                {
                    return RuleExecutionResult.Fail([mainCredentialError]);
                }
            }

            var currentRequest = preparedMainRequest!;
            var pageBodies = new List<string>();
            var visitedUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var visitedCursors = new HashSet<string>(StringComparer.Ordinal);

            for (var page = 1; ; page++)
            {
                if (requestCount >= _limits.MaxRequests)
                {
                    return RuleExecutionResult.Fail(["execution: request budget exceeded."]);
                }

                if (pagination is not null && page > pagination.MaxPages)
                {
                    return RuleExecutionResult.Fail(["pagination: page limit exceeded."]);
                }

                if (pagination?.Mode == RulePaginationMode.NextLink &&
                    !visitedUrls.Add(currentRequest.Url))
                {
                    return RuleExecutionResult.Fail(["pagination: next-link cycle detected."]);
                }

                requestCount++;
                var responseResult = await requestExecutor
                    .SendAsync(currentRequest, "request")
                    .ConfigureAwait(false);
                if (responseResult.Error is not null)
                {
                    return RuleExecutionResult.Fail([responseResult.Error]);
                }

                var response = responseResult.Response!;
                pageBodies.Add(response.Body);
                if (pagination is null)
                {
                    return _responseEvaluator.ExtractFields(
                        rule,
                        response.Body,
                        executionCancellation.Token);
                }

                switch (pagination.Mode)
                {
                    case RulePaginationMode.NextLink:
                        {
                            var nextLink = _responseEvaluator.EvaluateFirst(
                                response.Body,
                                pagination.NextPageSelector!,
                                pagination.NextPageAttribute,
                                executionCancellation.Token);
                            if (string.IsNullOrWhiteSpace(nextLink))
                            {
                                return CompletePagination(rule, pageBodies, executionCancellation.Token);
                            }

                            if (!RuleRequestBuilder.TryContinueAfterPage(
                                    page,
                                    requestCount,
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
                                executionCancellation.Token);
                            if (string.IsNullOrWhiteSpace(hasNextPage))
                            {
                                return CompletePagination(rule, pageBodies, executionCancellation.Token);
                            }

                            if (!RuleRequestBuilder.TryContinueAfterPage(
                                    page,
                                    requestCount,
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
                                executionCancellation.Token,
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
                                    credentialResolution.Credential,
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
                                executionCancellation.Token);
                            if (string.IsNullOrWhiteSpace(rawCursor))
                            {
                                return CompletePagination(rule, pageBodies, executionCancellation.Token);
                            }

                            if (!RuleRequestBuilder.TryContinueAfterPage(
                                    page,
                                    requestCount,
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
                                executionCancellation.Token,
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
                                    credentialResolution.Credential,
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
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (executionCancellation.IsCancellationRequested)
        {
            return RuleExecutionResult.Fail(["execution: time budget exceeded."]);
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
