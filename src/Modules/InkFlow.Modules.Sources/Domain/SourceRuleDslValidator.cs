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

        if (dsl.Rules.Count == 0)
        {
            errors.Add("rules: at least one capability rule is required.");
        }

        var seenCapabilities = new HashSet<SourceCapability>();
        foreach (var rule in dsl.Rules)
        {
            if (!seenCapabilities.Add(rule.Capability))
            {
                errors.Add($"rules[{rule.Capability}]: duplicate rule for the same capability.");
            }

            ValidateRequest(rule, errors);
            ValidateFields(rule, errors);
            ValidateList(rule, errors);
        }

        return errors;
    }

    private static void ValidateRequest(CapabilityRule rule, List<string> errors)
    {
        var prefix = $"rules[{rule.Capability}]";
        var request = rule.Request;

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

        foreach (var header in request.Headers)
        {
            if (string.IsNullOrWhiteSpace(header.Key) || string.IsNullOrWhiteSpace(header.Value))
            {
                errors.Add($"{prefix}: headers must have non-empty key and value.");
                break;
            }
        }

        foreach (var key in request.Query.Keys.Concat(request.Form.Keys))
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                errors.Add($"{prefix}: query/form parameter names must not be empty.");
                break;
            }
        }

        if (request.Method == RuleHttpMethod.Post && request.Form.Count == 0)
        {
            errors.Add($"{prefix}: a POST request requires a non-empty form body.");
        }
    }

    private static void ValidateFields(CapabilityRule rule, List<string> errors)
    {
        var prefix = $"rules[{rule.Capability}]";

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
            if (string.IsNullOrWhiteSpace(field.Name))
            {
                errors.Add($"{prefix}: field name must not be empty.");
                continue;
            }

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

                if (field.Attribute is not null && string.IsNullOrWhiteSpace(field.Attribute))
                {
                    errors.Add($"{prefix}['{field.Name}']: attribute name must not be blank when specified.");
                }
            }

            if (field.Regex is { } regex)
            {
                if (string.IsNullOrWhiteSpace(regex.Pattern))
                {
                    errors.Add($"{prefix}['{field.Name}']: regex pattern must not be empty.");
                }

                if (regex.TimeoutMilliseconds <= 0 || regex.TimeoutMilliseconds > MaxRegexTimeoutMilliseconds)
                {
                    errors.Add(
                        $"{prefix}['{field.Name}']: regex timeout must be within (0, {MaxRegexTimeoutMilliseconds}] ms.");
                }
            }

            foreach (var transform in field.Transforms)
            {
                if (transform is ReplaceTransform { From: null or "" })
                {
                    errors.Add($"{prefix}['{field.Name}']: replace transform requires a non-empty 'from'.");
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

        if (string.IsNullOrWhiteSpace(list.ExternalIdAttribute))
        {
            errors.Add($"{prefix}: list externalIdAttribute must not be empty.");
        }

        if (list.IdPrefixToStrip is null)
        {
            errors.Add($"{prefix}: idPrefixToStrip must be specified (may be empty string).");
        }

        if (list.IdSuffixToStrip is null)
        {
            errors.Add($"{prefix}: idSuffixToStrip must be specified (may be empty string).");
        }
    }
}
