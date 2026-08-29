using System.Text.RegularExpressions;

namespace InkFlow.Modules.Sources.Domain;

/// <summary>
/// 校验 <see cref="SourceRuleDsl"/> 的结构与安全约束。
/// 返回全部违规项（空列表表示通过）。校验不触网、不执行正则，仅做声明检查。
/// </summary>
public static class SourceRuleDslValidator
{
    public const string SupportedSchemaVersion = "1";

    /// <summary>单条正则超时的上限（毫秒），防止规则作者声明过宽的预算。</summary>
    public const int MaxRegexTimeoutMilliseconds = 2_000;
    public const int MaxRules = 32;
    public const int MaxFieldsPerRule = 64;
    public const int MaxTransformsPerField = 16;
    public const int MaxMapEntries = 64;
    public const int MaxSourceIdLength = 128;
    public const int MaxFieldNameLength = 128;
    public const int MaxMapKeyLength = 256;
    public const int MaxMapValueLength = 2_048;
    public const int MaxPathTemplateLength = 2_048;
    public const int MaxSelectorExpressionLength = 2_048;
    public const int MaxRegexPatternLength = 4_096;
    public const int MaxAttributeNameLength = 128;
    public const int MaxTransformValueLength = 1_024;
    public const int MaxListTrimLength = 512;
    public const int MaxPaginationPages = 32;

    private static readonly System.Text.RegularExpressions.Regex PlaceholderPattern =
        new(@"\{([A-Za-z_][A-Za-z0-9_]*)\}", RegexOptions.Compiled);

    public static IReadOnlyList<string> Validate(SourceRuleDsl? dsl)
    {
        var errors = new List<string>();
        if (dsl is null)
        {
            errors.Add("dsl: document must not be null.");
            return errors;
        }

        if (!string.Equals(dsl.SchemaVersion, SupportedSchemaVersion, StringComparison.Ordinal))
        {
            errors.Add($"schemaVersion: only '{SupportedSchemaVersion}' is supported, got '{dsl.SchemaVersion}'.");
        }

        if (string.IsNullOrWhiteSpace(dsl.SourceId) ||
            dsl.SourceId.Any(char.IsWhiteSpace))
        {
            errors.Add("sourceId: must be non-empty and contain no whitespace.");
        }

        ValidateMaxLength(dsl.SourceId, MaxSourceIdLength, "sourceId", errors);

        if (dsl.Rules is null)
        {
            errors.Add("rules: must be an array.");
            return errors;
        }

        if (dsl.Rules.Count == 0)
        {
            errors.Add("rules: at least one capability rule is required.");
        }
        else if (dsl.Rules.Count > MaxRules)
        {
            errors.Add($"rules: must contain at most {MaxRules} entries.");
        }

        var seenCapabilities = new HashSet<SourceCapability>();
        foreach (var rule in dsl.Rules)
        {
            if (rule is null)
            {
                errors.Add("rules: entries must not be null.");
                continue;
            }

            if (!Enum.IsDefined(rule.Capability))
            {
                errors.Add($"rules[{rule.Capability}]: unknown capability.");
            }

            if (!seenCapabilities.Add(rule.Capability))
            {
                errors.Add($"rules[{rule.Capability}]: duplicate rule for the same capability.");
            }

            ValidateRequest(rule, errors);
            ValidateFields(rule, errors);
            ValidateList(rule, errors);
            ValidatePagination(rule, errors);
        }

        return errors;
    }

    private static void ValidateRequest(CapabilityRule rule, List<string> errors)
    {
        var prefix = $"rules[{rule.Capability}]";
        var request = rule.Request;

        if (request is null)
        {
            errors.Add($"{prefix}: request must not be null.");
            return;
        }

        if (!Enum.IsDefined(request.Method))
        {
            errors.Add($"{prefix}: unknown HTTP method '{request.Method}'.");
        }

        if (string.IsNullOrWhiteSpace(request.PathTemplate) || !request.PathTemplate.StartsWith('/'))
        {
            errors.Add($"{prefix}: pathTemplate must start with '/'.");
        }
        else
        {
            // 占位符必须形如 {name}；任何残留的花括号都是模板书写错误。
            var withoutPlaceholders = PlaceholderPattern.Replace(request.PathTemplate, string.Empty);
            if (withoutPlaceholders.Contains('{') || withoutPlaceholders.Contains('}'))
            {
                errors.Add($"{prefix}: pathTemplate contains a malformed placeholder.");
            }
        }

        ValidateMaxLength(
            request.PathTemplate,
            MaxPathTemplateLength,
            $"{prefix}.pathTemplate",
            errors);

        ValidateMap(request.Headers, $"{prefix}.headers", errors);
        ValidateMap(request.Query, $"{prefix}.query", errors);
        ValidateMap(request.Form, $"{prefix}.form", errors);

        if (request.Headers is null)
        {
            errors.Add($"{prefix}: headers must be an object.");
        }
        else
        {
            foreach (var header in request.Headers)
            {
                if (string.IsNullOrWhiteSpace(header.Key) || string.IsNullOrWhiteSpace(header.Value))
                {
                    errors.Add($"{prefix}: headers must have non-empty key and value.");
                    break;
                }
            }
        }

        if (request.Query is null)
        {
            errors.Add($"{prefix}: query must be an object.");
        }

        if (request.Form is null)
        {
            errors.Add($"{prefix}: form must be an object.");
        }

        var queryKeys = request.Query?.Keys ?? Array.Empty<string>();
        var formKeys = request.Form?.Keys ?? Array.Empty<string>();
        foreach (var key in queryKeys.Concat(formKeys))
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                errors.Add($"{prefix}: query/form parameter names must not be empty.");
                break;
            }
        }

        if (request.Method == RuleHttpMethod.Post && (request.Form is null || request.Form.Count == 0))
        {
            errors.Add($"{prefix}: a POST request requires a non-empty form body.");
        }
    }

    private static void ValidateFields(CapabilityRule rule, List<string> errors)
    {
        var prefix = $"rules[{rule.Capability}]";

        if (rule.Fields is null)
        {
            errors.Add($"{prefix}: fields must be an array.");
            return;
        }

        if (rule.Fields.Count > MaxFieldsPerRule)
        {
            errors.Add($"{prefix}: fields must contain at most {MaxFieldsPerRule} entries.");
        }

        // 列表绑定能力(Toc/Search)由 List 提供结构化输出,单值字段可选;
        // 若同时声明了字段,仍逐个校验其合法性。
        if (rule.Fields.Count == 0)
        {
            if (rule.List is null)
            {
                errors.Add($"{prefix}: at least one output field or a list binding is required.");
            }

            return;
        }

        var seenNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in rule.Fields)
        {
            if (field is null)
            {
                errors.Add($"{prefix}: field entries must not be null.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(field.Name))
            {
                errors.Add($"{prefix}: field name must not be empty.");
                continue;
            }

            ValidateMaxLength(
                field.Name,
                MaxFieldNameLength,
                $"{prefix} field name",
                errors);

            if (!seenNames.Add(field.Name))
            {
                errors.Add($"{prefix}['{field.Name}']: duplicate field name.");
            }

            var hasSelector = field.Selector is not null;
            var hasRegex = field.Regex is not null;
            if (hasSelector == hasRegex)
            {
                errors.Add(
                    $"{prefix}['{field.Name}']: keep exactly one extraction source — " +
                    "either a selector or a regex.");
            }

            if (field.Selector is { } selector)
            {
                if (!Enum.IsDefined(selector.Kind))
                {
                    errors.Add($"{prefix}['{field.Name}']: unknown selector kind '{selector.Kind}'.");
                }

                if (string.IsNullOrWhiteSpace(selector.Expression))
                {
                    errors.Add($"{prefix}['{field.Name}']: selector expression must not be empty.");
                }

                ValidateMaxLength(
                    selector.Expression,
                    MaxSelectorExpressionLength,
                    $"{prefix}['{field.Name}'] selector expression",
                    errors);

                var expression = selector.Expression?.TrimStart() ?? string.Empty;
                if (selector.Kind == SelectorKind.JsonPath && !expression.StartsWith('$'))
                {
                    errors.Add(
                        $"{prefix}['{field.Name}']: JSONPath selector must start with '$'.");
                }

                if (selector.Kind == SelectorKind.XPath &&
                    !expression.StartsWith('/') && !expression.StartsWith('.'))
                {
                    errors.Add(
                        $"{prefix}['{field.Name}']: XPath selector must start with '/' or '.'.");
                }

                if (field.Attribute is not null && string.IsNullOrWhiteSpace(field.Attribute))
                {
                    errors.Add($"{prefix}['{field.Name}']: attribute name must not be blank when specified.");
                }

                ValidateMaxLength(
                    field.Attribute,
                    MaxAttributeNameLength,
                    $"{prefix}['{field.Name}'] attribute",
                    errors);
            }

            if (field.Regex is { } regex)
            {
                if (string.IsNullOrWhiteSpace(regex.Pattern))
                {
                    errors.Add($"{prefix}['{field.Name}']: regex pattern must not be empty.");
                }

                ValidateMaxLength(
                    regex.Pattern,
                    MaxRegexPatternLength,
                    $"{prefix}['{field.Name}'] regex pattern",
                    errors);

                if (regex.TimeoutMilliseconds <= 0 || regex.TimeoutMilliseconds > MaxRegexTimeoutMilliseconds)
                {
                    errors.Add(
                        $"{prefix}['{field.Name}']: regex timeout must be within (0, {MaxRegexTimeoutMilliseconds}] ms.");
                }
            }

            if (field.Transforms is null)
            {
                errors.Add($"{prefix}['{field.Name}']: transforms must be an array.");
                continue;
            }

            if (field.Transforms.Count > MaxTransformsPerField)
            {
                errors.Add(
                    $"{prefix}['{field.Name}']: transforms must contain at most " +
                    $"{MaxTransformsPerField} entries.");
            }

            foreach (var transform in field.Transforms)
            {
                if (transform is null)
                {
                    errors.Add($"{prefix}['{field.Name}']: transform entries must not be null.");
                    continue;
                }

                if (transform is ReplaceTransform { From: null or "" })
                {
                    errors.Add($"{prefix}['{field.Name}']: replace transform requires a non-empty 'from'.");
                }

                if (transform is ReplaceTransform replace)
                {
                    ValidateMaxLength(
                        replace.From,
                        MaxTransformValueLength,
                        $"{prefix}['{field.Name}'] replace 'from'",
                        errors);
                    ValidateMaxLength(
                        replace.To,
                        MaxTransformValueLength,
                        $"{prefix}['{field.Name}'] replace 'to'",
                        errors);
                }
            }
        }
    }

    /// <summary>校验列表抽取绑定(Toc/Search 多结果能力)。</summary>
    private static void ValidateList(CapabilityRule rule, List<string> errors)
    {
        var prefix = $"rules[{rule.Capability}]";
        var list = rule.List;

        if (list is null)
        {
            // 多结果能力(Toc/Search)必须声明列表绑定;单结果能力(BookInfo/Content)不要求。
            if (rule.Capability is SourceCapability.Toc or SourceCapability.Search)
            {
                errors.Add($"{prefix}: capability {rule.Capability} requires a list binding.");
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(list.ItemsSelector))
        {
            errors.Add($"{prefix}: list itemsSelector must not be empty.");
        }

        var listExpression = list.ItemsSelector?.TrimStart() ?? string.Empty;
        if (list.ItemsSelectorKind == SelectorKind.JsonPath &&
            !listExpression.StartsWith('$'))
        {
            errors.Add($"{prefix}: JSONPath list selector must start with '$'.");
        }

        if (list.ItemsSelectorKind == SelectorKind.XPath &&
            !listExpression.StartsWith('/') && !listExpression.StartsWith('.'))
        {
            errors.Add($"{prefix}: XPath list selector must start with '/' or '.'.");
        }

        if (!Enum.IsDefined(list.ItemsSelectorKind))
        {
            errors.Add($"{prefix}: unknown list selector kind '{list.ItemsSelectorKind}'.");
        }

        ValidateMaxLength(
            list.ItemsSelector,
            MaxSelectorExpressionLength,
            $"{prefix} itemsSelector",
            errors);

        if (string.IsNullOrWhiteSpace(list.ExternalIdAttribute))
        {
            errors.Add($"{prefix}: list externalIdAttribute must not be empty.");
        }

        ValidateMaxLength(
            list.ExternalIdAttribute,
            MaxAttributeNameLength,
            $"{prefix} externalIdAttribute",
            errors);

        if (list.IdPrefixToStrip is null)
        {
            errors.Add($"{prefix}: idPrefixToStrip must be specified (may be empty string).");
        }

        if (list.IdSuffixToStrip is null)
        {
            errors.Add($"{prefix}: idSuffixToStrip must be specified (may be empty string).");
        }

        ValidateMaxLength(
            list.IdPrefixToStrip,
            MaxListTrimLength,
            $"{prefix} idPrefixToStrip",
            errors);
        ValidateMaxLength(
            list.IdSuffixToStrip,
            MaxListTrimLength,
            $"{prefix} idSuffixToStrip",
            errors);

        if (list.TextAttribute is not null && string.IsNullOrWhiteSpace(list.TextAttribute))
        {
            errors.Add($"{prefix}: list textAttribute must not be blank when specified.");
        }

        if (list.TextAttribute?.Any(char.IsControl) == true)
        {
            errors.Add($"{prefix}: list textAttribute must not contain control characters.");
        }

        ValidateMaxLength(
            list.TextAttribute,
            MaxAttributeNameLength,
            $"{prefix} textAttribute",
            errors);
    }

    private static void ValidatePagination(CapabilityRule rule, List<string> errors)
    {
        var pagination = rule.Pagination;
        if (pagination is null)
        {
            return;
        }

        var prefix = $"rules[{rule.Capability}] pagination";
        if (rule.Capability is not (SourceCapability.Search or SourceCapability.Toc) ||
            rule.List is null)
        {
            errors.Add($"{prefix}: a paginated rule requires a Search/Toc list binding.");
        }

        if (pagination.MaxPages < 1 || pagination.MaxPages > MaxPaginationPages)
        {
            errors.Add(
                $"{prefix}: maxPages must be between 1 and {MaxPaginationPages}.");
        }

        var selector = pagination.NextPageSelector;
        if (selector is null)
        {
            errors.Add($"{prefix}: nextPageSelector must be an object.");
        }
        else
        {
            if (!Enum.IsDefined(selector.Kind))
            {
                errors.Add($"{prefix}: unknown next-page selector kind '{selector.Kind}'.");
            }

            if (string.IsNullOrWhiteSpace(selector.Expression))
            {
                errors.Add($"{prefix}: nextPageSelector expression must not be empty.");
            }

            ValidateMaxLength(
                selector.Expression,
                MaxSelectorExpressionLength,
                $"{prefix} nextPageSelector expression",
                errors);

            var expression = selector.Expression?.TrimStart() ?? string.Empty;
            if (selector.Kind == SelectorKind.JsonPath && !expression.StartsWith('$'))
            {
                errors.Add($"{prefix}: JSONPath next-page selector must start with '$'.");
            }

            if (selector.Kind == SelectorKind.XPath &&
                !expression.StartsWith('/') && !expression.StartsWith('.'))
            {
                errors.Add($"{prefix}: XPath next-page selector must start with '/' or '.'.");
            }

            if (selector.Kind == SelectorKind.Css &&
                string.IsNullOrWhiteSpace(pagination.NextPageAttribute))
            {
                errors.Add($"{prefix}: CSS next-page selector requires a non-empty nextPageAttribute.");
            }
        }

        if (pagination.NextPageAttribute is not null &&
            string.IsNullOrWhiteSpace(pagination.NextPageAttribute))
        {
            errors.Add($"{prefix}: nextPageAttribute must not be blank when specified.");
        }

        if (pagination.NextPageAttribute?.Any(char.IsControl) == true)
        {
            errors.Add($"{prefix}: nextPageAttribute must not contain control characters.");
        }

        ValidateMaxLength(
            pagination.NextPageAttribute,
            MaxAttributeNameLength,
            $"{prefix} nextPageAttribute",
            errors);
    }

    private static void ValidateMap(
        IReadOnlyDictionary<string, string>? values,
        string prefix,
        List<string> errors)
    {
        if (values is null)
        {
            return;
        }

        if (values.Count > MaxMapEntries)
        {
            errors.Add($"{prefix}: must contain at most {MaxMapEntries} entries.");
        }

        foreach (var pair in values)
        {
            if (pair.Value is null)
            {
                errors.Add($"{prefix}['{pair.Key}']: value must be a string.");
            }

            ValidateMaxLength(pair.Key, MaxMapKeyLength, $"{prefix} key", errors);
            ValidateMaxLength(pair.Value, MaxMapValueLength, $"{prefix}['{pair.Key}']", errors);
        }
    }

    private static void ValidateMaxLength(
        string? value,
        int maximum,
        string path,
        List<string> errors)
    {
        if (value is not null && value.Length > maximum)
        {
            errors.Add($"{path}: length must be at most {maximum} characters.");
        }
    }
}
