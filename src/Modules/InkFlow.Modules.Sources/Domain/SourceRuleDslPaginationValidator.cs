namespace InkFlow.Modules.Sources.Domain;

internal static class SourceRuleDslPaginationValidator
{
    internal static IReadOnlyList<string> Validate(CapabilityRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        var errors = new List<string>();
        AppendErrors(rule, errors);
        return errors;
    }

    internal static void AppendErrors(CapabilityRule rule, List<string> errors)
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

        if (pagination.MaxPages < 1 || pagination.MaxPages > SourceRuleDslValidator.MaxPaginationPages)
        {
            errors.Add(
                $"{prefix}: maxPages must be between 1 and {SourceRuleDslValidator.MaxPaginationPages}.");
        }

        if (!Enum.IsDefined(pagination.Mode))
        {
            errors.Add($"{prefix}: unknown pagination mode '{pagination.Mode}'.");
            return;
        }

        switch (pagination.Mode)
        {
            case RulePaginationMode.NextLink:
                ValidatePaginationSelector(
                    pagination.NextPageSelector,
                    $"{prefix} nextPageSelector",
                    errors,
                    required: true);
                ValidatePaginationAttribute(
                    pagination.NextPageSelector,
                    pagination.NextPageAttribute,
                    $"{prefix} nextPageAttribute",
                    errors,
                    requiredForCss: true,
                    cssLabel: "next-page selector");
                break;

            case RulePaginationMode.PageNumber:
                ValidatePaginationSelector(
                    pagination.NextPageSelector,
                    $"{prefix} nextPageSelector",
                    errors,
                    required: true);
                ValidatePaginationAttribute(
                    pagination.NextPageSelector,
                    pagination.NextPageAttribute,
                    $"{prefix} nextPageAttribute",
                    errors,
                    requiredForCss: true,
                    cssLabel: "next-page selector");
                ValidateContinuationParameter(rule, pagination, prefix, errors);
                ValidatePageNumber(pagination, prefix, errors);
                if (pagination.CursorSelector is not null || pagination.CursorAttribute is not null)
                {
                    errors.Add($"{prefix}: cursor fields are only valid for cursor mode.");
                }

                break;

            case RulePaginationMode.Cursor:
                ValidatePaginationSelector(
                    pagination.CursorSelector,
                    $"{prefix} cursorSelector",
                    errors,
                    required: true);
                ValidatePaginationAttribute(
                    pagination.CursorSelector,
                    pagination.CursorAttribute,
                    $"{prefix} cursorAttribute",
                    errors,
                    requiredForCss: true,
                    cssLabel: "cursor selector");
                ValidateContinuationParameter(rule, pagination, prefix, errors);
                if (pagination.StartPage != 1 || pagination.PageStep != 1)
                {
                    errors.Add($"{prefix}: startPage/pageStep are only valid for page-number mode.");
                }

                break;
        }
    }

    private static void ValidatePaginationSelector(
        RuleSelector? selector,
        string prefix,
        List<string> errors,
        bool required)
    {
        if (selector is null)
        {
            if (required)
            {
                errors.Add($"{prefix}: selector must be an object.");
            }

            return;
        }

        if (!Enum.IsDefined(selector.Kind))
        {
            errors.Add($"{prefix}: unknown selector kind '{selector.Kind}'.");
        }

        if (string.IsNullOrWhiteSpace(selector.Expression))
        {
            errors.Add($"{prefix}: expression must not be empty.");
        }

        SourceRuleDslValidator.ValidateMaxLength(
            selector.Expression,
            SourceRuleDslValidator.MaxSelectorExpressionLength,
            $"{prefix} expression",
            errors);

        var expression = selector.Expression?.TrimStart() ?? string.Empty;
        if (selector.Kind == SelectorKind.JsonPath && !expression.StartsWith('$'))
        {
            errors.Add($"{prefix}: JSONPath selector must start with '$'.");
        }

        if (selector.Kind == SelectorKind.XPath &&
            !expression.StartsWith('/') && !expression.StartsWith('.'))
        {
            errors.Add($"{prefix}: XPath selector must start with '/' or '.'.");
        }
    }

    private static void ValidatePaginationAttribute(
        RuleSelector? selector,
        string? attribute,
        string prefix,
        List<string> errors,
        bool requiredForCss,
        string cssLabel = "selector")
    {
        if (requiredForCss && selector?.Kind == SelectorKind.Css && string.IsNullOrWhiteSpace(attribute))
        {
            errors.Add($"{prefix}: CSS {cssLabel} requires a non-empty attribute.");
        }

        if (attribute is not null && string.IsNullOrWhiteSpace(attribute))
        {
            errors.Add($"{prefix}: attribute must not be blank when specified.");
        }

        if (attribute?.Any(char.IsControl) == true)
        {
            errors.Add($"{prefix}: attribute must not contain control characters.");
        }

        SourceRuleDslValidator.ValidateMaxLength(
            attribute,
            SourceRuleDslValidator.MaxAttributeNameLength,
            prefix,
            errors);
    }

    private static void ValidateContinuationParameter(
        CapabilityRule rule,
        RulePagination pagination,
        string prefix,
        List<string> errors)
    {
        var parameterName = pagination.ParameterName;
        if (string.IsNullOrWhiteSpace(parameterName) || parameterName.Any(char.IsControl))
        {
            errors.Add($"{prefix}: parameterName must be non-empty and contain no control characters.");
            return;
        }

        SourceRuleDslValidator.ValidateMaxLength(
            parameterName,
            SourceRuleDslValidator.MaxPaginationParameterNameLength,
            $"{prefix} parameterName",
            errors);

        if (rule.Request is null)
        {
            return;
        }

        var inQuery = rule.Request.Query?.Keys.Any(
            key => string.Equals(key, parameterName, StringComparison.Ordinal)) == true;
        var inForm = rule.Request.Form?.Keys.Any(
            key => string.Equals(key, parameterName, StringComparison.Ordinal)) == true;
        if (inQuery == inForm)
        {
            errors.Add(
                $"{prefix}: continuation parameter must be declared exactly once in request query or form.");
        }

        if (rule.Request.Method == RuleHttpMethod.Get && !inQuery)
        {
            errors.Add($"{prefix}: GET continuation requires a query parameter.");
        }
    }

    private static void ValidatePageNumber(
        RulePagination pagination,
        string prefix,
        List<string> errors)
    {
        if (pagination.StartPage < 0 || pagination.StartPage > SourceRuleDslValidator.MaxPaginationPageValue)
        {
            errors.Add(
                $"{prefix}: startPage must be between 0 and {SourceRuleDslValidator.MaxPaginationPageValue}.");
        }

        if (pagination.PageStep < 1 || pagination.PageStep > SourceRuleDslValidator.MaxPaginationPageValue)
        {
            errors.Add(
                $"{prefix}: pageStep must be between 1 and {SourceRuleDslValidator.MaxPaginationPageValue}.");
        }

        if (pagination.StartPage >= 0 &&
            pagination.PageStep > 0 &&
            pagination.MaxPages >= 1 &&
            (long)pagination.StartPage +
                ((long)pagination.MaxPages - 1) * pagination.PageStep > SourceRuleDslValidator.MaxPaginationPageValue)
        {
            errors.Add(
                $"{prefix}: startPage/pageStep exceed the maximum generated page value.");
        }
    }
}
