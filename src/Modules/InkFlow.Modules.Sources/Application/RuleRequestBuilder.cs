using System.Text;
using System.Text.RegularExpressions;
using InkFlow.BuildingBlocks.Security;
using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Sources.Application;

/// <summary>
/// Builds and validates transient requests used by a rule execution.
/// It owns template rendering and continuation URL policy; it does not perform I/O.
/// </summary>
internal static class RuleRequestBuilder
{
    private static readonly Regex PlaceholderPattern =
        new(@"\{([A-Za-z_][A-Za-z0-9_]*)\}", RegexOptions.Compiled);
    private static readonly Regex VariableNamePattern =
        new(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);
    private const int MaxPaginationLinkLength = 2_048;

    public static List<string> ValidateVariables(
        SourceRuleExecutionLimits limits,
        IReadOnlyDictionary<string, string> variables)
    {
        var errors = new List<string>();
        if (variables.Count > limits.MaxVariableCount)
        {
            errors.Add("execution: variable context exceeds count budget.");
        }

        long totalBytes = 0;
        foreach (var variable in variables)
        {
            var key = variable.Key;
            var value = variable.Value;

            if (string.IsNullOrWhiteSpace(key) ||
                key.Length > limits.MaxVariableNameLength ||
                !VariableNamePattern.IsMatch(key))
            {
                errors.Add("execution: variable name is invalid.");
            }

            if (value is null || value.Length > limits.MaxVariableValueLength)
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

        if (totalBytes > limits.MaxVariableBytes)
        {
            errors.Add("execution: variable context exceeds byte budget.");
        }

        return errors;
    }

    public static List<string> TryBuildRequest(
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

    public static List<string> TryBuildRequest(
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

    public static bool TryApplyCredential(
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

    public static bool TryContinueAfterPage(
        int page,
        int requestsUsed,
        RulePagination pagination,
        SourceRuleExecutionLimits limits,
        out string error)
    {
        if (page >= pagination.MaxPages)
        {
            error = "pagination: page limit exceeded.";
            return false;
        }

        if (requestsUsed >= limits.MaxRequests)
        {
            error = "execution: request budget exceeded.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public static bool TryValidateContinuationRequest(
        SourceHttpRequest request,
        Uri sourceOrigin,
        out string error)
    {
        if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var target))
        {
            error = "pagination: generated continuation request is not a permitted URL.";
            return false;
        }

        return TryValidateSameOriginUrl(
            target,
            sourceOrigin,
            "pagination: generated continuation request is not a permitted URL.",
            "pagination: generated continuation request must stay on the source origin.",
            out error);
    }

    public static bool TryBuildNextRequest(
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

        if (!TryValidateSameOriginUrl(
                target,
                sourceOrigin,
                "pagination: next link is not a permitted URL.",
                "pagination: next link must stay on the source origin.",
                out error))
        {
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

    internal static bool IsSameOrigin(Uri expected, Uri actual) =>
        string.Equals(expected.Scheme, actual.Scheme, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(expected.Host, actual.Host, StringComparison.OrdinalIgnoreCase) &&
        expected.Port == actual.Port;

    public static bool TryValidateInitialRequest(
        SourceHttpRequest request,
        Uri sourceOrigin,
        out string error)
    {
        if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var target))
        {
            error = "request: built URL is not a permitted URL.";
            return false;
        }

        return TryValidateSameOriginUrl(
            target,
            sourceOrigin,
            "request: built URL is not a permitted URL.",
            "request: built URL must stay on the source origin.",
            out error);
    }

    private static bool TryValidateSameOriginUrl(
        Uri? target,
        Uri sourceOrigin,
        string invalidUrlError,
        string crossOriginError,
        out string error)
    {
        if (target is null ||
            !target.IsAbsoluteUri ||
            target.Fragment.Length > 0 ||
            target.UserInfo.Length > 0)
        {
            error = invalidUrlError;
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
            error = crossOriginError;
            return false;
        }

        error = string.Empty;
        return true;
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

    private static bool IsCookieHeaderName(string name) =>
        name.Equals("cookie", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("set-cookie", StringComparison.OrdinalIgnoreCase);
}
