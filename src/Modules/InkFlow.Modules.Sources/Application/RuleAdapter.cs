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
    private readonly RulePreRequestExecutor _preRequestExecutor;
    private readonly RulePaginationExecutor _paginationExecutor;

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
        _preRequestExecutor = new RulePreRequestExecutor(_limits, _responseEvaluator);
        _paginationExecutor = new RulePaginationExecutor(_limits, _responseEvaluator);
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

            if (preRequests.Count > 0)
            {
                var preRequestResult = await _preRequestExecutor
                    .ExecuteAsync(
                        preRequests,
                        rule,
                        baseUrl,
                        sourceOrigin,
                        credentialResolution.Credential,
                        continuationVariables,
                        firstRequest!,
                        requestExecutor,
                        requestCount,
                        executionCancellation.Token)
                    .ConfigureAwait(false);
                if (!preRequestResult.IsSuccess)
                {
                    return RuleExecutionResult.Fail(preRequestResult.Errors);
                }

                continuationVariables = new Dictionary<string, string>(
                    preRequestResult.Variables,
                    StringComparer.Ordinal);
                requestCount = preRequestResult.RequestsUsed;
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

            if (pagination is null)
            {
                if (requestCount >= _limits.MaxRequests)
                {
                    return RuleExecutionResult.Fail(["execution: request budget exceeded."]);
                }

                requestCount++;
                var responseResult = await requestExecutor
                    .SendAsync(preparedMainRequest!, "request")
                    .ConfigureAwait(false);
                if (responseResult.Error is not null)
                {
                    return RuleExecutionResult.Fail([responseResult.Error]);
                }

                return _responseEvaluator.ExtractFields(
                    rule,
                    responseResult.Response!.Body,
                    executionCancellation.Token);
            }

            return await _paginationExecutor.ExecuteAsync(
                    rule,
                    baseUrl,
                    sourceOrigin,
                    pagination,
                    preparedMainRequest!,
                    credentialResolution.Credential,
                    continuationVariables,
                    requestCount,
                    requestExecutor,
                    executionCancellation.Token)
                .ConfigureAwait(false);
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

}
