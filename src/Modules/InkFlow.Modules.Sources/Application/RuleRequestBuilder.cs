using System.Text;
using System.Text.RegularExpressions;
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

    public static List<string> TryBuildAndPrepareInitialRequest(
        RuleRequest requestDefinition,
        SourceCapability capability,
        string baseUrl,
        IReadOnlyDictionary<string, string> variables,
        string? overrideParameterName,
        string? overrideParameterValue,
        Uri sourceOrigin,
        SourceCredential? credential,
        out SourceHttpRequest? request)
    {
        var errors = TryBuildRequest(
            requestDefinition,
            capability,
            baseUrl,
            variables,
            overrideParameterName,
            overrideParameterValue,
            out var builtRequest);
        request = null;
        if (errors.Count > 0)
        {
            return errors;
        }

        if (!RuleRequestUrlPolicy.TryValidateInitialRequest(
                builtRequest!,
                sourceOrigin,
                out var requestError))
        {
            return [requestError];
        }

        if (!RuleCredentialRequestApplier.TryApply(
                builtRequest!,
                credential,
                out request,
                out var credentialError))
        {
            return [credentialError];
        }

        return [];
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
