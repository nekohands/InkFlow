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

        var buildErrors = TryBuildRequest(rule, baseUrl, variables, out var request);
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

        if (rule.Pagination is not null &&
            (rule.List is null || rule.Capability is not (SourceCapability.Search or SourceCapability.Toc)))
        {
            return RuleExecutionResult.Fail(
                ["pagination: a paginated rule requires a Search/Toc list binding."]);
        }

        if (rule.Pagination is { } configuredPagination)
        {
            if (configuredPagination.MaxPages < 1 ||
                configuredPagination.MaxPages > SourceRuleDslValidator.MaxPaginationPages)
            {
                return RuleExecutionResult.Fail(["pagination: maxPages is outside the allowed range."]);
            }

            if (configuredPagination.NextPageSelector is null)
            {
                return RuleExecutionResult.Fail(["pagination: nextPageSelector must be configured."]);
            }

            var nextPageExpression = configuredPagination.NextPageSelector.Expression?.TrimStart() ?? string.Empty;
            if (!Enum.IsDefined(configuredPagination.NextPageSelector.Kind) ||
                string.IsNullOrWhiteSpace(nextPageExpression) ||
                nextPageExpression.Length > SourceRuleDslValidator.MaxSelectorExpressionLength)
            {
                return RuleExecutionResult.Fail(["pagination: nextPageSelector is invalid."]);
            }

            if (configuredPagination.NextPageSelector.Kind == SelectorKind.JsonPath &&
                !nextPageExpression.StartsWith('$'))
            {
                return RuleExecutionResult.Fail(["pagination: JSONPath next-page selector must start with '$'."]);
            }

            if (configuredPagination.NextPageSelector.Kind == SelectorKind.XPath &&
                !nextPageExpression.StartsWith('/') &&
                !nextPageExpression.StartsWith('.'))
            {
                return RuleExecutionResult.Fail(
                    ["pagination: XPath next-page selector must start with '/' or '.'."]);
            }

            if (configuredPagination.NextPageAttribute is { } nextPageAttribute &&
                (string.IsNullOrWhiteSpace(nextPageAttribute) ||
                 nextPageAttribute.Any(char.IsControl) ||
                 nextPageAttribute.Length > SourceRuleDslValidator.MaxAttributeNameLength))
            {
                return RuleExecutionResult.Fail(["pagination: nextPageAttribute is invalid."]);
            }

            if (configuredPagination.NextPageSelector.Kind == SelectorKind.Css &&
                string.IsNullOrWhiteSpace(configuredPagination.NextPageAttribute))
            {
                return RuleExecutionResult.Fail(
                    ["pagination: CSS next-page selector requires a non-empty nextPageAttribute."]);
            }
        }

        // A zero request budget is an explicit fail-closed switch and must be checked
        // before entering the HTTP seam, including when pagination is declared.
        if (_limits.MaxRequests < 1)
        {
            return RuleExecutionResult.Fail(["execution: request budget exceeded."]);
        }

        var sourceOrigin = target!;
        var currentRequest = request!;
        var pagination = rule.Pagination;
        var pageBodies = new List<string>();
        var visitedUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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

            if (!visitedUrls.Add(currentRequest.Url))
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

            var nextLink = _selectorEvaluator.EvaluateFirst(
                response.Body,
                pagination.NextPageSelector,
                pagination.NextPageAttribute);
            if (string.IsNullOrWhiteSpace(nextLink))
            {
                var extracted = ExtractFields(rule, pageBodies[0]);
                return extracted.IsSuccess
                    ? extracted with { PageBodies = pageBodies.ToArray() }
                    : extracted;
            }

            if (page >= pagination.MaxPages)
            {
                return RuleExecutionResult.Fail(["pagination: page limit exceeded."]);
            }

            if (page >= _limits.MaxRequests)
            {
                return RuleExecutionResult.Fail(["execution: request budget exceeded."]);
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
        }
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
            pair => FillTemplate($"{prefix}.query['{pair.Key}']", pair.Value, variables, errors));

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
                $"{Uri.EscapeDataString(FillTemplate($"{prefix}.form['{p.Key}']", p.Value, variables, errors, encodeValues: false))}"))
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
