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
    private readonly RuleResponseValueExtractor _responseValueExtractor;
    private readonly RuleResponseEvaluator _responseEvaluator;
    private readonly RuleExecutionPreflight _preflight;
    private readonly RulePreRequestExecutor _preRequestExecutor;
    private readonly RulePaginationExecutor _paginationExecutor;

    public RuleAdapter(
        ISourceHttpClient httpClient,
        ISelectorEvaluator selectorEvaluator,
        SourceRuleExecutionLimits? limits = null,
        ISourceCredentialProvider? credentialProvider = null)
    {
        _httpClient = httpClient;
        _limits = SourceRuleExecutionLimits.Resolve(limits);
        _credentialResolver = new RuleCredentialResolver(credentialProvider);
        _responseValueExtractor = new RuleResponseValueExtractor(selectorEvaluator, _limits);
        _responseEvaluator = new RuleResponseEvaluator(_responseValueExtractor, _limits);
        _preflight = new RuleExecutionPreflight(_limits);
        _preRequestExecutor = new RulePreRequestExecutor(_responseEvaluator);
        _paginationExecutor = new RulePaginationExecutor(
            _limits,
            _responseEvaluator,
            _responseValueExtractor);
    }

    public SourceRuleExecutionLimits Limits => _limits;

    public async Task<RuleExecutionResult> ExecuteAsync(
        CapabilityRule rule,
        string baseUrl,
        IReadOnlyDictionary<string, string>? variables = null,
        CancellationToken cancellationToken = default,
        SourceExecutionContext? executionContext = null)
    {
        if (!_preflight.TryPrepare(
                rule,
                baseUrl,
                variables,
                out var plan,
                out var preflightErrors))
        {
            return RuleExecutionResult.Fail(preflightErrors);
        }

        var pagination = plan!.Pagination;
        var preRequests = plan.PreRequests;
        var continuationVariables = new Dictionary<string, string>(
            plan.InitialVariables,
            StringComparer.Ordinal);

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
                plan.SourceOrigin,
                cookieJar,
                cancellationToken,
                executionCancellation.Token);
            var requestBudget = new RuleRequestBudget(_limits.MaxRequests);

            if (preRequests.Count > 0)
            {
                var preRequestResult = await _preRequestExecutor
                    .ExecuteAsync(
                        preRequests,
                        rule,
                        plan.BaseUrl,
                        plan.SourceOrigin,
                        credentialResolution.Credential,
                        continuationVariables,
                        plan.FirstRequest,
                        requestExecutor,
                        requestBudget,
                        executionCancellation.Token)
                    .ConfigureAwait(false);
                if (!preRequestResult.IsSuccess)
                {
                    return RuleExecutionResult.Fail(preRequestResult.Errors);
                }

                continuationVariables = new Dictionary<string, string>(
                    preRequestResult.Variables,
                    StringComparer.Ordinal);
            }

            SourceHttpRequest? preparedMainRequest;
            if (preRequests.Count == 0)
            {
                if (!RuleCredentialRequestApplier.TryApply(
                        plan.FirstRequest,
                        credentialResolution.Credential,
                        out preparedMainRequest,
                        out var mainCredentialError))
                {
                    return RuleExecutionResult.Fail([mainCredentialError]);
                }
            }
            else
            {
                var mainBuildErrors = RuleRequestPreparer.TryBuildInitialRequest(
                    rule.Request,
                    rule.Capability,
                    plan.BaseUrl,
                    continuationVariables,
                    plan.InitialParameterName,
                    plan.InitialParameterValue,
                    plan.SourceOrigin,
                    credentialResolution.Credential,
                    out preparedMainRequest);
                if (mainBuildErrors.Count > 0)
                {
                    return RuleExecutionResult.Fail(mainBuildErrors);
                }
            }

            if (pagination is null)
            {
                if (!requestBudget.TryConsume(out var requestBudgetError))
                {
                    return RuleExecutionResult.Fail([requestBudgetError]);
                }

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
                    plan.BaseUrl,
                    plan.SourceOrigin,
                    pagination,
                    preparedMainRequest!,
                    credentialResolution.Credential,
                    continuationVariables,
                    requestBudget,
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
