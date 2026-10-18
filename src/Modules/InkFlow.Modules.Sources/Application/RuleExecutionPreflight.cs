using System.Globalization;
using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Sources.Application;

internal sealed record RuleExecutionPlan(
    string BaseUrl,
    Uri SourceOrigin,
    RulePagination? Pagination,
    IReadOnlyList<RuleRequestStep> PreRequests,
    IReadOnlyDictionary<string, string> InitialVariables,
    SourceHttpRequest FirstRequest,
    string? InitialParameterName,
    string? InitialParameterValue);

/// <summary>
/// Performs the no-I/O admission checks for one rule execution and builds its first request.
/// Secret resolution and HTTP execution start only after this boundary succeeds.
/// </summary>
internal sealed class RuleExecutionPreflight(SourceRuleExecutionLimits limits)
{
    public bool TryPrepare(
        CapabilityRule rule,
        string baseUrl,
        IReadOnlyDictionary<string, string>? variables,
        out RuleExecutionPlan? plan,
        out IReadOnlyList<string> errors)
    {
        plan = null;
        variables ??= new Dictionary<string, string>();

        var variableErrors = RuleRequestBuilder.ValidateVariables(limits, variables);
        if (variableErrors.Count > 0)
        {
            errors = variableErrors;
            return false;
        }

        var pagination = rule.Pagination;
        if (pagination is not null)
        {
            var paginationErrors = SourceRuleDslValidator.ValidatePagination(rule);
            if (paginationErrors.Count > 0)
            {
                errors = paginationErrors;
                return false;
            }
        }

        if (rule.Session is not null)
        {
            var sessionErrors = SourceRuleDslValidator.ValidateSession(rule);
            if (sessionErrors.Count > 0)
            {
                errors = sessionErrors;
                return false;
            }
        }

        var responseVariableDeclarationErrors =
            SourceRuleDslValidator.ValidateResponseVariables(rule);
        if (responseVariableDeclarationErrors.Count > 0)
        {
            errors = responseVariableDeclarationErrors;
            return false;
        }

        var preRequestDeclarationErrors = SourceRuleDslValidator.ValidatePreRequests(rule);
        if (preRequestDeclarationErrors.Count > 0)
        {
            errors = preRequestDeclarationErrors;
            return false;
        }

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var sourceOrigin))
        {
            errors = ["request: source base URL must be absolute."];
            return false;
        }

        var preRequests = rule.PreRequests ?? [];
        var initialVariables = new Dictionary<string, string>(
            variables,
            StringComparer.Ordinal);
        var initialParameterName = pagination?.Mode == RulePaginationMode.PageNumber
            ? pagination.ParameterName
            : null;
        var initialParameterValue = pagination?.Mode == RulePaginationMode.PageNumber
            ? pagination.StartPage.ToString(CultureInfo.InvariantCulture)
            : null;

        // Inspect the first request before resolving credentials. With a pre-request chain this
        // is the first step; otherwise it is the capability's main request.
        var firstRequestDefinition = preRequests.Count > 0
            ? preRequests[0].Request
            : rule.Request;
        var firstBuildErrors = RuleRequestBuilder.TryBuildRequest(
            firstRequestDefinition,
            rule.Capability,
            baseUrl,
            initialVariables,
            preRequests.Count == 0 ? initialParameterName : null,
            preRequests.Count == 0 ? initialParameterValue : null,
            out var firstRequest);
        if (firstBuildErrors.Count > 0)
        {
            errors = firstBuildErrors;
            return false;
        }

        // 出网前的最后一道闸：字面量 SSRF 校验失败绝不发起请求。
        if (!RuleRequestUrlPolicy.TryValidateInitialRequest(
                firstRequest!,
                sourceOrigin,
                out var firstRequestError))
        {
            errors = [firstRequestError];
            return false;
        }

        // A zero request budget is an explicit fail-closed switch and must be checked before
        // entering the HTTP seam, including when pagination is declared.
        if (limits.MaxRequests < 1)
        {
            errors = ["execution: request budget exceeded."];
            return false;
        }

        plan = new RuleExecutionPlan(
            baseUrl,
            sourceOrigin,
            pagination,
            preRequests,
            initialVariables,
            firstRequest!,
            initialParameterName,
            initialParameterValue);
        errors = [];
        return true;
    }
}
