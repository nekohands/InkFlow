using System.Text;
using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Sources.Application;

/// <summary>Extracts bounded fields and transient response variables from source responses.</summary>
internal sealed class RuleResponseEvaluator
{
    private readonly RuleResponseValueExtractor _valueExtractor;
    private readonly SourceRuleExecutionLimits _limits;

    public RuleResponseEvaluator(
        RuleResponseValueExtractor valueExtractor,
        SourceRuleExecutionLimits limits)
    {
        _valueExtractor = valueExtractor;
        _limits = limits;
    }

    public RuleExecutionResult ExtractFields(
        CapabilityRule rule,
        string body,
        CancellationToken executionCancellationToken)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var errors = new List<string>();
        long resultBytes = 0;

        foreach (var field in rule.Fields)
        {
            executionCancellationToken.ThrowIfCancellationRequested();
            if (!_valueExtractor.TryExtractAndTransform(
                body,
                field.Selector,
                field.Attribute,
                field.Regex,
                $"rules[{rule.Capability}]['{field.Name}']",
                field.Transforms,
                executionCancellationToken,
                errors,
                out var extracted))
            {
                continue;
            }

            resultBytes += Encoding.UTF8.GetByteCount(extracted);
            if (resultBytes > _limits.MaxResultSize)
            {
                errors.Add("execution: result exceeded size budget.");
                continue;
            }

            values[field.Name] = extracted;
        }

        executionCancellationToken.ThrowIfCancellationRequested();

        return errors.Count > 0 ? RuleExecutionResult.Fail(errors) : RuleExecutionResult.Ok(values, body);
    }

    public List<string> TryBuildResponseVariables(
        IReadOnlyList<RuleResponseVariable>? responseVariables,
        string prefix,
        string body,
        IReadOnlyDictionary<string, string> currentVariables,
        CancellationToken executionCancellationToken,
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
            executionCancellationToken.ThrowIfCancellationRequested();
            var errorPrefix = $"{prefix}['{variable.Name}']";
            if (!_valueExtractor.TryExtractAndTransform(
                body,
                variable.Selector,
                variable.Attribute,
                variable.Regex,
                errorPrefix,
                variable.Transforms,
                executionCancellationToken,
                errors,
                out var extracted))
            {
                continue;
            }

            candidate[variable.Name] = extracted;
        }

        executionCancellationToken.ThrowIfCancellationRequested();

        if (errors.Count > 0)
        {
            return errors;
        }

        var variableErrors = RuleRequestBuilder.ValidateVariables(_limits, candidate);
        if (variableErrors.Count > 0)
        {
            return variableErrors;
        }

        updatedVariables = candidate;
        return [];
    }

}
