using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using InkFlow.BuildingBlocks.Security;
using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Sources.Application;

/// <summary>
/// 规则执行器：把声明式 DSL 变成一次或一组有界的真实抓取。
/// 执行顺序固定为：URL 构建 → SSRF 字面量校验 → 凭据解析/投影 → 发请求（经 ISourceHttpClient）→
/// 状态码检查 → 字段抽取 → 变换管道。任一环节失败即整体失败，不产生部分结果；
/// 请求、响应、时间、正则、分页、模板变量和结果预算由
/// <see cref="SourceRuleExecutionLimits"/> 强制约束。
/// </summary>
public sealed class RuleAdapter
{
    private static readonly Regex PlaceholderPattern =
        new(@"\{([A-Za-z_][A-Za-z0-9_]*)\}", RegexOptions.Compiled);
    private static readonly Regex VariableNamePattern =
        new(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);
    private const int MaxPaginationLinkLength = 2_048;

    private readonly ISourceHttpClient _httpClient;
    private readonly ISelectorEvaluator _selectorEvaluator;
    private readonly SourceRuleExecutionLimits _limits;
    private readonly ISourceCredentialProvider? _credentialProvider;

    public RuleAdapter(
        ISourceHttpClient httpClient,
        ISelectorEvaluator selectorEvaluator,
        SourceRuleExecutionLimits? limits = null,
        ISourceCredentialProvider? credentialProvider = null)
    {
        _httpClient = httpClient;
        _selectorEvaluator = selectorEvaluator;
        _limits = limits ?? SourceRuleExecutionLimits.Default;
        _credentialProvider = credentialProvider;
        _limits.Validate();
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

        var variableErrors = ValidateVariables(variables);
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

        // Build and inspect the first request before resolving credentials. With a pre-request
        // chain this is the first step; otherwise it is the capability's main request.
        var firstRequestDefinition = preRequests.Count > 0
            ? preRequests[0].Request
            : rule.Request;
        var firstBuildErrors = TryBuildRequest(
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
        if (!TryValidateInitialRequest(
                firstRequest!,
                sourceOrigin,
                out var firstRequestError))
        {
            return RuleExecutionResult.Fail([firstRequestError]);
        }

        // A zero request budget is an explicit fail-closed switch and must be checked
        // before entering the HTTP seam, including when pagination is declared.
        if (_limits.MaxRequests < 1)
        {
            return RuleExecutionResult.Fail(["execution: request budget exceeded."]);
        }

        using var executionCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        executionCancellation.CancelAfter(_limits.MaxExecutionTime);

        // 只有请求结构、SSRF 和执行预算都通过后才解析 secret，减少不必要的敏感材料驻留。
        var credentialResolution = await ResolveCredentialAsync(
                executionContext,
                cancellationToken,
                executionCancellation.Token)
            .ConfigureAwait(false);
        if (credentialResolution.Error is not null)
        {
            return RuleExecutionResult.Fail([credentialResolution.Error]);
        }

        var cookieJar = rule.Session is null ? null : new RuleCookieJar(rule.Session);
        var requestCount = 0;
        long responseBytes = 0;

        for (var preIndex = 0; preIndex < preRequests.Count; preIndex++)
        {
            var step = preRequests[preIndex];
            SourceHttpRequest? preparedStepRequest;
            if (preIndex == 0)
            {
                if (!TryApplyCredential(
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
                var stepBuildErrors = TryBuildRequest(
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

                if (!TryValidateInitialRequest(
                        stepRequest!,
                        sourceOrigin,
                        out var stepRequestError))
                {
                    return RuleExecutionResult.Fail([stepRequestError]);
                }

                if (!TryApplyCredential(
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

            if (preparedStepRequest!.FormBody is not null &&
                Encoding.UTF8.GetByteCount(preparedStepRequest.FormBody) > _limits.MaxBytes)
            {
                return RuleExecutionResult.Fail(["execution: request exceeded byte budget."]);
            }

            requestCount++;
            SourceHttpResponse stepResponse;
            try
            {
                var requestToSend = preparedStepRequest;
                if (cookieJar is not null &&
                    Uri.TryCreate(preparedStepRequest.Url, UriKind.Absolute, out var requestUri))
                {
                    requestToSend = preparedStepRequest with
                    {
                        CookieHeader = cookieJar.BuildCookieHeader(requestUri),
                    };
                }

                stepResponse = await _httpClient
                    .SendAsync(requestToSend, executionCancellation.Token)
                    .WaitAsync(executionCancellation.Token)
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
            catch (SourceResponseTooLargeException)
            {
                return RuleExecutionResult.Fail(["execution: response exceeded byte budget."]);
            }
            catch
            {
                return RuleExecutionResult.Fail(["http: transport failure."]);
            }

            if (!stepResponse.IsSuccess)
            {
                return RuleExecutionResult.Fail(
                    [$"http: upstream returned status {stepResponse.StatusCode}."]);
            }

            responseBytes += Encoding.UTF8.GetByteCount(stepResponse.Body);
            if (responseBytes > _limits.MaxBytes)
            {
                return RuleExecutionResult.Fail(["execution: response exceeded byte budget."]);
            }

            if (!TryValidateResponseOrigin(
                    preparedStepRequest,
                    stepResponse,
                    sourceOrigin,
                    "pre-request",
                    out var stepResponseOriginError))
            {
                return RuleExecutionResult.Fail([stepResponseOriginError]);
            }

            if (!TryAcceptResponseCookies(
                    cookieJar,
                    preparedStepRequest,
                    stepResponse,
                    sourceOrigin,
                    out var stepSessionError))
            {
                return RuleExecutionResult.Fail([stepSessionError]);
            }

            var stepVariableErrors = TryBuildResponseVariables(
                step.ResponseVariables,
                $"rules[{rule.Capability}] preRequests[{preIndex}].responseVariables",
                stepResponse.Body,
                continuationVariables,
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
            if (!TryApplyCredential(
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
            var mainBuildErrors = TryBuildRequest(
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

            if (!TryValidateInitialRequest(
                    mainRequest!,
                    sourceOrigin,
                    out var mainRequestError))
            {
                return RuleExecutionResult.Fail([mainRequestError]);
            }

            if (!TryApplyCredential(
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

            if (currentRequest.FormBody is not null &&
                Encoding.UTF8.GetByteCount(currentRequest.FormBody) > _limits.MaxBytes)
            {
                return RuleExecutionResult.Fail(["execution: request exceeded byte budget."]);
            }

            requestCount++;
            SourceHttpResponse response;
            try
            {
                var requestToSend = currentRequest;
                if (cookieJar is not null &&
                    Uri.TryCreate(currentRequest.Url, UriKind.Absolute, out var requestUri))
                {
                    requestToSend = currentRequest with
                    {
                        CookieHeader = cookieJar.BuildCookieHeader(requestUri),
                    };
                }

                response = await _httpClient
                    .SendAsync(requestToSend, executionCancellation.Token)
                    .WaitAsync(executionCancellation.Token)
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
            catch (SourceResponseTooLargeException)
            {
                return RuleExecutionResult.Fail(["execution: response exceeded byte budget."]);
            }
            catch (Exception ex)
            {
                return RuleExecutionResult.Fail([$"http: transport failure — {ex.Message}"]);
            }

            if (!response.IsSuccess)
            {
                return RuleExecutionResult.Fail([$"http: upstream returned status {(int)response.StatusCode}."]);
            }

            if (!TryValidateResponseOrigin(
                    currentRequest,
                    response,
                    sourceOrigin,
                    "request",
                    out var responseOriginError))
            {
                return RuleExecutionResult.Fail([responseOriginError]);
            }

            responseBytes += Encoding.UTF8.GetByteCount(response.Body);
            if (responseBytes > _limits.MaxBytes)
            {
                return RuleExecutionResult.Fail(["execution: response exceeded byte budget."]);
            }

            if (cookieJar is not null)
            {
                if (!Uri.TryCreate(currentRequest.Url, UriKind.Absolute, out var requestUri))
                {
                    return RuleExecutionResult.Fail(["session: request URI is invalid."]);
                }

                var responseUri = response.ResponseUri ?? requestUri;
                if (!responseUri.IsAbsoluteUri || !IsSameOrigin(sourceOrigin, responseUri))
                {
                    return RuleExecutionResult.Fail(
                        ["session: response origin changed during redirect."]);
                }

                var sessionError = cookieJar.Accept(response.SetCookieHeaders, responseUri);
                if (sessionError is not null)
                {
                    return RuleExecutionResult.Fail([sessionError]);
                }
            }

            pageBodies.Add(response.Body);
            if (pagination is null)
            {
                return ExtractFields(rule, response.Body);
            }

            switch (pagination.Mode)
            {
                case RulePaginationMode.NextLink:
                {
                    var nextLink = _selectorEvaluator.EvaluateFirst(
                        response.Body,
                        pagination.NextPageSelector!,
                        pagination.NextPageAttribute);
                    if (string.IsNullOrWhiteSpace(nextLink))
                    {
                        return CompletePagination(rule, pageBodies);
                    }

                    if (!TryContinueAfterPage(
                            page,
                            requestCount,
                            pagination,
                            out var limitError))
                    {
                        return RuleExecutionResult.Fail([limitError]);
                    }

                    if (!TryBuildNextRequest(
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
                    var hasNextPage = _selectorEvaluator.EvaluateFirst(
                        response.Body,
                        pagination.NextPageSelector!,
                        pagination.NextPageAttribute);
                    if (string.IsNullOrWhiteSpace(hasNextPage))
                    {
                        return CompletePagination(rule, pageBodies);
                    }

                    if (!TryContinueAfterPage(
                            page,
                            requestCount,
                            pagination,
                            out var limitError))
                    {
                        return RuleExecutionResult.Fail([limitError]);
                    }

                    var responseVariableErrors = TryBuildResponseVariables(
                        rule.ResponseVariables,
                        $"rules[{rule.Capability}] responseVariables",
                        response.Body,
                        continuationVariables,
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

                    var pageBuildErrors = TryBuildRequest(
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

                    if (!TryApplyCredential(
                            nextRequest!,
                            credentialResolution.Credential,
                            out var preparedNextRequest,
                            out var pageCredentialError))
                    {
                        return RuleExecutionResult.Fail([pageCredentialError]);
                    }

                    if (!TryValidateContinuationRequest(
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
                    var rawCursor = _selectorEvaluator.EvaluateFirst(
                        response.Body,
                        pagination.CursorSelector!,
                        pagination.CursorAttribute);
                    if (string.IsNullOrWhiteSpace(rawCursor))
                    {
                        return CompletePagination(rule, pageBodies);
                    }

                    if (!TryContinueAfterPage(
                            page,
                            requestCount,
                            pagination,
                            out var limitError))
                    {
                        return RuleExecutionResult.Fail([limitError]);
                    }

                    var responseVariableErrors = TryBuildResponseVariables(
                        rule.ResponseVariables,
                        $"rules[{rule.Capability}] responseVariables",
                        response.Body,
                        continuationVariables,
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

                    var cursorBuildErrors = TryBuildRequest(
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

                    if (!TryApplyCredential(
                            nextRequest!,
                            credentialResolution.Credential,
                            out var preparedNextRequest,
                            out var cursorCredentialError))
                    {
                        return RuleExecutionResult.Fail([cursorCredentialError]);
                    }

                    if (!TryValidateContinuationRequest(
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

    private async Task<(SourceCredential? Credential, string? Error)> ResolveCredentialAsync(
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

        if (_credentialProvider is null)
        {
            return (null, "credential: credential provider is unavailable.");
        }

        try
        {
            var credential = await _credentialProvider
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

    private static bool TryApplyCredential(
        SourceHttpRequest request,
        SourceCredential? credential,
        out SourceHttpRequest? preparedRequest,
        out string error)
    {
        preparedRequest = request;
        error = string.Empty;
        if (credential is null)
        {
            return true;
        }

        if (!credential.TryBuildHeader(out var headerName, out var headerValue))
        {
            error = "credential: credential material is invalid.";
            return false;
        }

        if (request.Headers.Keys.Any(existingHeaderName =>
                string.Equals(existingHeaderName, headerName, StringComparison.OrdinalIgnoreCase)))
        {
            error = "credential: credential header conflicts with rule header.";
            return false;
        }

        var headers = new Dictionary<string, string>(request.Headers, StringComparer.OrdinalIgnoreCase)
        {
            [headerName] = headerValue,
        };
        preparedRequest = request with { Headers = headers };
        return true;
    }

    private List<string> TryBuildResponseVariables(
        IReadOnlyList<RuleResponseVariable>? responseVariables,
        string prefix,
        string body,
        IReadOnlyDictionary<string, string> currentVariables,
        out Dictionary<string, string>? updatedVariables)
    {
        updatedVariables = null;
        if (responseVariables is null || responseVariables.Count == 0)
        {
            updatedVariables = new Dictionary<string, string>(
                currentVariables,
                StringComparer.Ordinal);
            return [];
        }

        var candidate = new Dictionary<string, string>(
            currentVariables,
            StringComparer.Ordinal);
        var errors = new List<string>();

        foreach (var variable in responseVariables)
        {
            string? extracted;
            var errorPrefix = $"{prefix}['{variable.Name}']";

            if (variable.Selector is { } selector)
            {
                extracted = _selectorEvaluator.EvaluateFirst(
                    body,
                    selector,
                    variable.Attribute);
            }
            else if (variable.Regex is { } regexSpec)
            {
                extracted = EvaluateRegex(
                    body,
                    regexSpec,
                    errorPrefix,
                    _limits.MaxRegexTime,
                    errors);
            }
            else
            {
                errors.Add($"{errorPrefix}: no extraction source defined.");
                continue;
            }

            if (extracted is null)
            {
                errors.Add($"{errorPrefix}: no match found in response.");
                continue;
            }

            foreach (var transform in variable.Transforms)
            {
                extracted = transform switch
                {
                    TrimTransform => extracted.Trim(),
                    ReplaceTransform replace => extracted.Replace(replace.From, replace.To),
                    _ => extracted,
                };
            }

            // Derived values are transient request context. Reuse the same bounded
            // validation as caller variables before they can reach a continuation URL,
            // header, query, or form value.
            candidate[variable.Name] = extracted;
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        var variableErrors = ValidateVariables(candidate);
        if (variableErrors.Count > 0)
        {
            return variableErrors;
        }

        updatedVariables = candidate;
        return [];
    }

    private bool TryContinueAfterPage(
        int page,
        int requestsUsed,
        RulePagination pagination,
        out string error)
    {
        if (page >= pagination.MaxPages)
        {
            error = "pagination: page limit exceeded.";
            return false;
        }

        if (requestsUsed >= _limits.MaxRequests)
        {
            error = "execution: request budget exceeded.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private RuleExecutionResult CompletePagination(
        CapabilityRule rule,
        IReadOnlyList<string> pageBodies)
    {
        var extracted = ExtractFields(rule, pageBodies[0]);
        return extracted.IsSuccess
            ? extracted with { PageBodies = pageBodies.ToArray() }
            : extracted;
    }

    private static bool TryValidateContinuationRequest(
        SourceHttpRequest request,
        Uri sourceOrigin,
        out string error)
    {
        if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var target) ||
            target.Fragment.Length > 0 ||
            target.UserInfo.Length > 0)
        {
            error = "pagination: generated continuation request is not a permitted URL.";
            return false;
        }

        var ssrfErrors = SsrfGuard.InspectLiteral(target);
        if (ssrfErrors.Count > 0)
        {
            error = $"ssrf: {string.Join("; ", ssrfErrors)}";
            return false;
        }

        if (!IsSameOrigin(sourceOrigin, target))
        {
            error = "pagination: generated continuation request must stay on the source origin.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool TryBuildNextRequest(
        SourceHttpRequest currentRequest,
        string rawNextLink,
        Uri sourceOrigin,
        out SourceHttpRequest? nextRequest,
        out string error)
    {
        nextRequest = null;
        error = string.Empty;
        var value = rawNextLink.Trim();

        if (value.Length == 0)
        {
            error = "pagination: next link must not be empty.";
            return false;
        }

        if (value.Length > MaxPaginationLinkLength || value.Any(char.IsControl))
        {
            error = "pagination: next link is invalid or too long.";
            return false;
        }

        if (!Uri.TryCreate(value, UriKind.RelativeOrAbsolute, out var parsedLink))
        {
            error = "pagination: next link is not a valid URL.";
            return false;
        }

        Uri? target;
        if (parsedLink.IsAbsoluteUri)
        {
            target = parsedLink;
        }
        else if (!Uri.TryCreate(new Uri(currentRequest.Url), parsedLink, out target))
        {
            error = "pagination: next link is not a valid URL.";
            return false;
        }

        if (target is null || !target.IsAbsoluteUri ||
            target.Fragment.Length > 0 || target.UserInfo.Length > 0)
        {
            error = "pagination: next link is not a permitted URL.";
            return false;
        }

        var ssrfErrors = SsrfGuard.InspectLiteral(target);
        if (ssrfErrors.Count > 0)
        {
            error = $"ssrf: {string.Join("; ", ssrfErrors)}";
            return false;
        }

        if (!IsSameOrigin(sourceOrigin, target))
        {
            error = "pagination: next link must stay on the source origin.";
            return false;
        }

        nextRequest = currentRequest with
        {
            Method = RuleHttpMethod.Get,
            Url = target.AbsoluteUri,
            FormBody = null,
        };
        return true;
    }

    private static bool IsSameOrigin(Uri expected, Uri actual) =>
        string.Equals(expected.Scheme, actual.Scheme, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(expected.Host, actual.Host, StringComparison.OrdinalIgnoreCase) &&
        expected.Port == actual.Port;

    private static bool TryValidateInitialRequest(
        SourceHttpRequest request,
        Uri sourceOrigin,
        out string error)
    {
        if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var target) ||
            target.Fragment.Length > 0 ||
            target.UserInfo.Length > 0)
        {
            error = "request: built URL is not a permitted URL.";
            return false;
        }

        var ssrfErrors = SsrfGuard.InspectLiteral(target);
        if (ssrfErrors.Count > 0)
        {
            error = $"ssrf: {string.Join("; ", ssrfErrors)}";
            return false;
        }

        if (!IsSameOrigin(sourceOrigin, target))
        {
            error = "request: built URL must stay on the source origin.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool TryValidateResponseOrigin(
        SourceHttpRequest request,
        SourceHttpResponse response,
        Uri sourceOrigin,
        string requestContext,
        out string error)
    {
        if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var requestUri))
        {
            error = $"{requestContext}: request URI is invalid.";
            return false;
        }

        var responseUri = response.ResponseUri ?? requestUri;
        if (!responseUri.IsAbsoluteUri ||
            responseUri.Fragment.Length > 0 ||
            responseUri.UserInfo.Length > 0 ||
            !IsSameOrigin(sourceOrigin, responseUri))
        {
            error = $"{requestContext}: response origin changed during redirect.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool TryAcceptResponseCookies(
        RuleCookieJar? cookieJar,
        SourceHttpRequest request,
        SourceHttpResponse response,
        Uri sourceOrigin,
        out string error)
    {
        if (cookieJar is null)
        {
            error = string.Empty;
            return true;
        }

        if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var requestUri))
        {
            error = "session: request URI is invalid.";
            return false;
        }

        var responseUri = response.ResponseUri ?? requestUri;
        if (!responseUri.IsAbsoluteUri || !IsSameOrigin(sourceOrigin, responseUri))
        {
            error = "session: response origin changed during redirect.";
            return false;
        }

        var sessionError = cookieJar.Accept(response.SetCookieHeaders, responseUri);
        if (sessionError is not null)
        {
            error = sessionError;
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static List<string> TryBuildRequest(
        CapabilityRule rule,
        string baseUrl,
        IReadOnlyDictionary<string, string> variables,
        string? overrideParameterName,
        string? overrideParameterValue,
        out SourceHttpRequest? request)
        => TryBuildRequest(
            rule.Request,
            rule.Capability,
            baseUrl,
            variables,
            overrideParameterName,
            overrideParameterValue,
            out request);

    private static List<string> TryBuildRequest(
        RuleRequest requestDefinition,
        SourceCapability capability,
        string baseUrl,
        IReadOnlyDictionary<string, string> variables,
        string? overrideParameterName,
        string? overrideParameterValue,
        out SourceHttpRequest? request)
    {
        request = null;
        var errors = new List<string>();
        var prefix = $"rules[{capability}]";

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            errors.Add("request: source base URL must not be empty.");
            return errors;
        }

        if (requestDefinition.Headers.Keys.Any(IsCookieHeaderName))
        {
            errors.Add(
                $"{prefix}: Cookie/Set-Cookie headers are not allowed; " +
                "declare a bounded session instead of persisting cookie values.");
            return errors;
        }

        var path = FillTemplate(
            $"{prefix}.pathTemplate",
            requestDefinition.PathTemplate,
            variables,
            errors);
        var queryValues = requestDefinition.Query.ToDictionary(
            pair => pair.Key,
            pair => string.Equals(pair.Key, overrideParameterName, StringComparison.Ordinal)
                ? overrideParameterValue ?? string.Empty
                : FillTemplate($"{prefix}.query['{pair.Key}']", pair.Value, variables, errors));

        var headers = requestDefinition.Headers.ToDictionary(
            pair => pair.Key,
            pair => FillTemplate($"{prefix}.headers['{pair.Key}']", pair.Value, variables,
                errors, encodeValues: false),
            StringComparer.OrdinalIgnoreCase);

        foreach (var header in headers)
        {
            if (header.Key.Any(char.IsControl))
            {
                errors.Add($"{prefix}.headers: header name contains control characters.");
            }

            if (header.Value.Any(char.IsControl))
            {
                errors.Add(
                    $"{prefix}.headers['{header.Key}']: rendered value contains control characters.");
            }
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        var builder = new UriBuilder(baseUrl.TrimEnd('/') + path);
        if (queryValues.Count > 0)
        {
            var existing = builder.Query.TrimStart('?');
            var appended = string.Join('&', queryValues.Select(p =>
                $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));
            builder.Query = string.IsNullOrEmpty(existing) ? appended : $"{existing}&{appended}";
        }

        var formBody = requestDefinition.Method == RuleHttpMethod.Post && requestDefinition.Form.Count > 0
            ? string.Join('&', requestDefinition.Form.Select(p =>
                $"{Uri.EscapeDataString(FillTemplate(prefix, p.Key, variables, errors, encodeValues: false))}=" +
                $"{Uri.EscapeDataString(string.Equals(p.Key, overrideParameterName, StringComparison.Ordinal)
                    ? overrideParameterValue ?? string.Empty
                    : FillTemplate($"{prefix}.form['{p.Key}']", p.Value, variables, errors, encodeValues: false))}"))
            : null;

        if (errors.Count > 0)
        {
            return errors;
        }

        // AbsoluteUri 保留百分号编码；ToString() 会为显示而把转义还原成原始字符。
        request = new SourceHttpRequest(
            requestDefinition.Method,
            builder.Uri.AbsoluteUri,
            headers,
            formBody);

        return errors;
    }

    private static string FillTemplate(
        string errorPrefix,
        string template,
        IReadOnlyDictionary<string, string> variables,
        List<string> errors,
        bool encodeValues = true)
    {
        var withoutPlaceholders = PlaceholderPattern.Replace(template, string.Empty);
        if (withoutPlaceholders.Contains('{') || withoutPlaceholders.Contains('}'))
        {
            errors.Add($"{errorPrefix}: contains a malformed placeholder.");
        }

        return PlaceholderPattern.Replace(template, match =>
        {
            var name = match.Groups[1].Value;
            if (!variables.TryGetValue(name, out var value))
            {
                errors.Add($"{errorPrefix}: missing variable '{name}'.");
                return match.Value;
            }

            // 路径和查询模板先编码；表单在最终拼接时统一编码一次。
            return encodeValues ? Uri.EscapeDataString(value) : value;
        });
    }

    private List<string> ValidateVariables(IReadOnlyDictionary<string, string> variables)
    {
        var errors = new List<string>();
        if (variables.Count > _limits.MaxVariableCount)
        {
            errors.Add("execution: variable context exceeds count budget.");
        }

        long totalBytes = 0;
        foreach (var variable in variables)
        {
            var key = variable.Key;
            var value = variable.Value;

            if (string.IsNullOrWhiteSpace(key) ||
                key.Length > _limits.MaxVariableNameLength ||
                !VariableNamePattern.IsMatch(key))
            {
                errors.Add("execution: variable name is invalid.");
            }

            if (value is null || value.Length > _limits.MaxVariableValueLength)
            {
                errors.Add("execution: variable value exceeds the length budget.");
                continue;
            }

            if (value.Any(char.IsControl))
            {
                errors.Add("execution: variable value contains control characters.");
            }

            totalBytes += (long)Encoding.UTF8.GetByteCount(key ?? string.Empty) +
                Encoding.UTF8.GetByteCount(value);
        }

        if (totalBytes > _limits.MaxVariableBytes)
        {
            errors.Add("execution: variable context exceeds byte budget.");
        }

        return errors;
    }

    private static bool IsCookieHeaderName(string name) =>
        name.Equals("cookie", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("set-cookie", StringComparison.OrdinalIgnoreCase);

    private RuleExecutionResult ExtractFields(CapabilityRule rule, string body)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var errors = new List<string>();
        long resultBytes = 0;

        foreach (var field in rule.Fields)
        {
            string? extracted;

            if (field.Selector is { } selector)
            {
                extracted = _selectorEvaluator.EvaluateFirst(
                    body, selector, field.Attribute);
            }
            else if (field.Regex is { } regexSpec)
            {
                extracted = EvaluateRegex(
                    body,
                    regexSpec,
                    $"rules[{rule.Capability}]['{field.Name}']",
                    _limits.MaxRegexTime,
                    errors);
            }
            else
            {
                errors.Add($"rules[{rule.Capability}]['{field.Name}']: no extraction source defined.");
                continue;
            }

            if (extracted is null)
            {
                errors.Add($"rules[{rule.Capability}]['{field.Name}']: no match found in response.");
                continue;
            }

            foreach (var transform in field.Transforms)
            {
                extracted = transform switch
                {
                    TrimTransform => extracted.Trim(),
                    ReplaceTransform replace => extracted.Replace(replace.From, replace.To),
                    _ => extracted,
                };
            }

            var extractedBytes = Encoding.UTF8.GetByteCount(extracted);
            resultBytes += extractedBytes;
            if (resultBytes > _limits.MaxResultSize)
            {
                errors.Add("execution: result exceeded size budget.");
                continue;
            }

            values[field.Name] = extracted;
        }

        return errors.Count > 0 ? RuleExecutionResult.Fail(errors) : RuleExecutionResult.Ok(values, body);
    }

    private static string? EvaluateRegex(
        string body,
        RuleRegex regexSpec,
        string errorPrefix,
        TimeSpan maxRegexTime,
        List<string> errors)
    {
        try
        {
            var declaredTimeoutMilliseconds = regexSpec.TimeoutMilliseconds;
            var executionTimeoutMilliseconds = (int)Math.Min(
                declaredTimeoutMilliseconds,
                maxRegexTime.TotalMilliseconds);
            var regex = new Regex(
                regexSpec.Pattern,
                RegexOptions.None,
                TimeSpan.FromMilliseconds(executionTimeoutMilliseconds));

            var match = regex.Match(body);
            if (!match.Success)
            {
                return null;
            }

            return match.Groups.Count > 1 ? match.Groups[1].Value : match.Value;
        }
        catch (ArgumentException ex)
        {
            errors.Add($"{errorPrefix}: invalid regex pattern — {ex.Message}");
            return null;
        }
        catch (RegexMatchTimeoutException)
        {
            errors.Add($"{errorPrefix}: regex evaluation timed out.");
            return null;
        }
    }
}
