using System.Text.RegularExpressions;

namespace InkFlow.Modules.Sources.Rules;

public sealed record RuleValidationError(string Code, string Path, string Message);

public sealed class SourceRuleValidator
{
    private static readonly HashSet<string> AllowedMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "GET",
        "POST"
    };

    public IReadOnlyList<RuleValidationError> Validate(SourceRuleDocument rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        var errors = new List<RuleValidationError>();

        if (rule.SchemaVersion != 1)
        {
            errors.Add(new("RULE_SCHEMA_UNSUPPORTED", "schemaVersion", "Only schemaVersion 1 is supported."));
        }

        if (string.IsNullOrWhiteSpace(rule.Name))
        {
            errors.Add(new("RULE_NAME_REQUIRED", "name", "Rule name is required."));
        }

        if (!Uri.TryCreate(rule.BaseUrl, UriKind.Absolute, out var baseUri)
            || (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
        {
            errors.Add(new("RULE_BASE_URL_INVALID", "baseUrl", "Base URL must be an absolute HTTP or HTTPS URL."));
        }

        ValidateBudget(rule.Budget, errors);
        ValidateCapability(rule, SourceCapability.Search, SourceOperation.Search, rule.Search, errors);
        ValidateCapability(rule, SourceCapability.BookInfo, SourceOperation.BookInfo, rule.BookInfo, errors);
        ValidateCapability(rule, SourceCapability.Toc, SourceOperation.Toc, rule.Toc, errors);
        ValidateCapability(rule, SourceCapability.Content, SourceOperation.Content, rule.Content, errors);
        ValidateCapability(rule, SourceCapability.Update, SourceOperation.Update, rule.Update, errors);

        return errors;
    }

    private static void ValidateBudget(RuleExecutionBudget budget, ICollection<RuleValidationError> errors)
    {
        if (budget.MaxRequests is < 1 or > 64)
        {
            errors.Add(new("RULE_BUDGET_REQUESTS_INVALID", "budget.maxRequests", "MaxRequests must be between 1 and 64."));
        }

        if (budget.MaxBytes is < 1024 or > 100 * 1024 * 1024)
        {
            errors.Add(new("RULE_BUDGET_BYTES_INVALID", "budget.maxBytes", "MaxBytes must be between 1 KiB and 100 MiB."));
        }

        if (budget.MaxRedirects is < 0 or > 10)
        {
            errors.Add(new("RULE_BUDGET_REDIRECTS_INVALID", "budget.maxRedirects", "MaxRedirects must be between 0 and 10."));
        }

        if (budget.MaxExecutionTimeMs is < 100 or > 60_000)
        {
            errors.Add(new("RULE_BUDGET_TIME_INVALID", "budget.maxExecutionTimeMs", "MaxExecutionTimeMs must be between 100 and 60000."));
        }

        if (budget.MaxRegexTimeMs is < 10 or > 2_000)
        {
            errors.Add(new("RULE_BUDGET_REGEX_INVALID", "budget.maxRegexTimeMs", "MaxRegexTimeMs must be between 10 and 2000."));
        }

        if (budget.MaxResultSize is < 1 or > 100_000)
        {
            errors.Add(new("RULE_BUDGET_RESULTS_INVALID", "budget.maxResultSize", "MaxResultSize must be between 1 and 100000."));
        }
    }

    private static void ValidateCapability(
        SourceRuleDocument document,
        SourceCapability capability,
        SourceOperation operation,
        SourceOperationRule? rule,
        ICollection<RuleValidationError> errors)
    {
        var hasCapability = document.Capabilities.HasFlag(capability);
        var path = operation.ToString().ToLowerInvariant();

        if (hasCapability && rule is null)
        {
            errors.Add(new("RULE_OPERATION_REQUIRED", path, $"Capability {capability} requires a {path} rule."));
            return;
        }

        if (!hasCapability && rule is not null)
        {
            errors.Add(new("RULE_CAPABILITY_REQUIRED", path, $"A {path} rule requires capability {capability}."));
        }

        if (rule is null)
        {
            return;
        }

        if (!AllowedMethods.Contains(rule.Request.Method))
        {
            errors.Add(new("RULE_METHOD_UNSUPPORTED", $"{path}.request.method", "Only GET and POST are supported in DSL v1."));
        }

        if (string.IsNullOrWhiteSpace(rule.Request.Url))
        {
            errors.Add(new("RULE_URL_REQUIRED", $"{path}.request.url", "Request URL is required."));
        }

        if (rule.Fields.Count == 0)
        {
            errors.Add(new("RULE_FIELDS_REQUIRED", $"{path}.fields", "At least one extraction field is required."));
        }

        foreach (var (name, field) in rule.Fields)
        {
            var fieldPath = $"{path}.fields.{name}";
            if (string.IsNullOrWhiteSpace(name))
            {
                errors.Add(new("RULE_FIELD_NAME_REQUIRED", fieldPath, "Field name is required."));
            }

            if (string.IsNullOrWhiteSpace(field.Expression))
            {
                errors.Add(new("RULE_SELECTOR_REQUIRED", $"{fieldPath}.expression", "Selector expression is required."));
                continue;
            }

            if (field.Kind == SelectorKind.Regex)
            {
                ValidateRegex(field.Expression, document.Budget.MaxRegexTimeMs, $"{fieldPath}.expression", errors);
            }

            if (field.Transforms is null)
            {
                continue;
            }

            for (var index = 0; index < field.Transforms.Count; index++)
            {
                var transform = field.Transforms[index];
                if (transform.Kind is TransformKind.RegexReplace or TransformKind.RegexCapture)
                {
                    if (string.IsNullOrWhiteSpace(transform.Argument))
                    {
                        errors.Add(new("RULE_REGEX_REQUIRED", $"{fieldPath}.transforms[{index}]", "Regex transform requires an argument pattern."));
                    }
                    else
                    {
                        ValidateRegex(transform.Argument, document.Budget.MaxRegexTimeMs, $"{fieldPath}.transforms[{index}].argument", errors);
                    }
                }
            }
        }
    }

    private static void ValidateRegex(
        string pattern,
        int timeoutMs,
        string path,
        ICollection<RuleValidationError> errors)
    {
        try
        {
            _ = new Regex(pattern, RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, TimeSpan.FromMilliseconds(timeoutMs));
        }
        catch (ArgumentException exception)
        {
            errors.Add(new("RULE_REGEX_INVALID", path, exception.Message));
        }
    }
}
