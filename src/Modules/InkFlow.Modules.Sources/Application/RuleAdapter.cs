using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using InkFlow.BuildingBlocks.Security;
using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Sources.Application;

/// <summary>
/// 规则执行器：把声明式 DSL 变成一次或一组有界的真实抓取。
/// 执行顺序固定为：URL 构建 → SSRF 字面量校验 → 发请求（经 ISourceHttpClient）→
/// 状态码检查 → 字段抽取 → 变换管道。任一环节失败即整体失败，不产生部分结果；
/// 请求、响应、时间、正则、分页和结果预算由 <see cref="SourceRuleExecutionLimits"/> 强制约束。
/// </summary>
public sealed class RuleAdapter
{
    private static readonly Regex PlaceholderPattern =
        new(@"\{([A-Za-z_][A-Za-z0-9_]*)\}", RegexOptions.Compiled);
    private const int MaxPaginationLinkLength = 2_048;

    private readonly ISourceHttpClient _httpClient;
    private readonly ISelectorEvaluator _selectorEvaluator;
    private readonly SourceRuleExecutionLimits _limits;

    public RuleAdapter(
        ISourceHttpClient httpClient,
        ISelectorEvaluator selectorEvaluator,
        SourceRuleExecutionLimits? limits = null)
    {
        _httpClient = httpClient;
        _selectorEvaluator = selectorEvaluator;
        _limits = limits ?? SourceRuleExecutionLimits.Default;
        _limits.Validate();
    }

    public SourceRuleExecutionLimits Limits => _limits;

    public async Task<RuleExecutionResult> ExecuteAsync(
        CapabilityRule rule,
        string baseUrl,
        IReadOnlyDictionary<string, string>? variables = null,
        CancellationToken cancellationToken = default)
    {
        variables ??= new Dictionary<string, string>();

        var pagination = rule.Pagination;
        if (pagination is not null)
        {
            var paginationErrors = SourceRuleDslValidator.ValidatePagination(rule);
            if (paginationErrors.Count > 0)
            {
                return RuleExecutionResult.Fail(paginationErrors);
            }
        }

        var initialParameterName = pagination?.Mode == RulePaginationMode.PageNumber
            ? pagination.ParameterName
            : null;
        var initialParameterValue = pagination?.Mode == RulePaginationMode.PageNumber
            ? pagination.StartPage.ToString(CultureInfo.InvariantCulture)
            : null;
        var buildErrors = TryBuildRequest(
            rule,
            baseUrl,
            variables,
            initialParameterName,
            initialParameterValue,
            out var request);
        if (buildErrors.Count > 0)
        {
            return RuleExecutionResult.Fail(buildErrors);
        }

        // 出网前的最后一道闸：字面量 SSRF 校验失败绝不发起请求。
        if (Uri.TryCreate(request!.Url, UriKind.Absolute, out var target))
        {
            var ssrfErrors = SsrfGuard.InspectLiteral(target);
            if (ssrfErrors.Count > 0)
            {
                return RuleExecutionResult.Fail(
                    ssrfErrors.Select(e => $"ssrf: {e}").ToList());
            }
        }
        else
        {
            return RuleExecutionResult.Fail(["request: built URL is not absolute."]);
        }

        // A zero request budget is an explicit fail-closed switch and must be checked
        // before entering the HTTP seam, including when pagination is declared.
        if (_limits.MaxRequests < 1)
        {
            return RuleExecutionResult.Fail(["execution: request budget exceeded."]);
        }

        var sourceOrigin = target!;
        var currentRequest = request!;
        var pageBodies = new List<string>();
        var visitedUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visitedCursors = new HashSet<string>(StringComparer.Ordinal);
        long responseBytes = 0;

        using var executionCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        executionCancellation.CancelAfter(_limits.MaxExecutionTime);

        for (var page = 1; ; page++)
        {
            if (page > _limits.MaxRequests)
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

            SourceHttpResponse response;
            try
            {
                response = await _httpClient
                    .SendAsync(currentRequest, executionCancellation.Token)
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

            responseBytes += Encoding.UTF8.GetByteCount(response.Body);
            if (responseBytes > _limits.MaxBytes)
            {
                return RuleExecutionResult.Fail(["execution: response exceeded byte budget."]);
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
                            pagination,
                            out var limitError))
                    {
                        return RuleExecutionResult.Fail([limitError]);
                    }

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
                        variables,
                        pagination.ParameterName,
                        nextPageValue.ToString(CultureInfo.InvariantCulture),
                        out var nextRequest);
                    if (pageBuildErrors.Count > 0)
                    {
                        return RuleExecutionResult.Fail(pageBuildErrors);
                    }

                    if (!TryValidateContinuationRequest(
                            nextRequest!,
                            sourceOrigin,
                            out var continuationError))
                    {
                        return RuleExecutionResult.Fail([continuationError]);
                    }

                    currentRequest = nextRequest!;
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
                            pagination,
                            out var limitError))
                    {
                        return RuleExecutionResult.Fail([limitError]);
                    }

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
                        variables,
                        pagination.ParameterName,
                        cursor,
                        out var nextRequest);
                    if (cursorBuildErrors.Count > 0)
                    {
                        return RuleExecutionResult.Fail(cursorBuildErrors);
                    }

                    if (!TryValidateContinuationRequest(
                            nextRequest!,
                            sourceOrigin,
                            out var continuationError))
                    {
                        return RuleExecutionResult.Fail([continuationError]);
                    }

                    currentRequest = nextRequest!;
                    break;
                }

                default:
                    return RuleExecutionResult.Fail(["pagination: mode is not supported."]);
            }
        }
    }

    private bool TryContinueAfterPage(
        int page,
        RulePagination pagination,
        out string error)
    {
        if (page >= pagination.MaxPages)
        {
            error = "pagination: page limit exceeded.";
            return false;
        }

        if (page >= _limits.MaxRequests)
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

    private static List<string> TryBuildRequest(
        CapabilityRule rule,
        string baseUrl,
        IReadOnlyDictionary<string, string> variables,
        string? overrideParameterName,
        string? overrideParameterValue,
        out SourceHttpRequest? request)
    {
        request = null;
        var errors = new List<string>();
        var prefix = $"rules[{rule.Capability}]";

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            errors.Add("request: source base URL must not be empty.");
            return errors;
        }

        var path = FillTemplate($"{prefix}.pathTemplate", rule.Request.PathTemplate, variables, errors);
        var queryValues = rule.Request.Query.ToDictionary(
            pair => pair.Key,
            pair => string.Equals(pair.Key, overrideParameterName, StringComparison.Ordinal)
                ? overrideParameterValue ?? string.Empty
                : FillTemplate($"{prefix}.query['{pair.Key}']", pair.Value, variables, errors));

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

        var formBody = rule.Request.Method == RuleHttpMethod.Post && rule.Request.Form.Count > 0
            ? string.Join('&', rule.Request.Form.Select(p =>
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
            rule.Request.Method,
            builder.Uri.AbsoluteUri,
            rule.Request.Headers,
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
                    field.Name,
                    rule.Capability,
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
        string fieldName,
        SourceCapability capability,
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
            errors.Add($"rules[{capability}]['{fieldName}']: invalid regex pattern — {ex.Message}");
            return null;
        }
        catch (RegexMatchTimeoutException)
        {
            errors.Add($"rules[{capability}]['{fieldName}']: regex evaluation timed out.");
            return null;
        }
    }
}
