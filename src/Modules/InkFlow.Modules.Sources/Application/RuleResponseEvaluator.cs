using System.Text;
using System.Text.RegularExpressions;
using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Sources.Application;

/// <summary>Extracts bounded fields and transient response variables from source responses.</summary>
internal sealed class RuleResponseEvaluator
{
    private readonly ISelectorEvaluator _selectorEvaluator;
    private readonly SourceRuleExecutionLimits _limits;

    public RuleResponseEvaluator(
        ISelectorEvaluator selectorEvaluator,
        SourceRuleExecutionLimits limits)
    {
        _selectorEvaluator = selectorEvaluator;
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
            if (!TryExtractValue(
                body,
                field.Selector,
                field.Attribute,
                field.Regex,
                $"rules[{rule.Capability}]['{field.Name}']",
                executionCancellationToken,
                errors,
                out var extracted))
            {
                continue;
            }
            if (extracted is null)
            {
                errors.Add($"rules[{rule.Capability}]['{field.Name}']: no match found in response.");
                continue;
            }

            extracted = ApplyTransforms(extracted, field.Transforms);
            executionCancellationToken.ThrowIfCancellationRequested();

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
            if (!TryExtractValue(
                body,
                variable.Selector,
                variable.Attribute,
                variable.Regex,
                errorPrefix,
                executionCancellationToken,
                errors,
                out var extracted))
            {
                continue;
            }
            if (extracted is null)
            {
                errors.Add($"{errorPrefix}: no match found in response.");
                continue;
            }

            candidate[variable.Name] = ApplyTransforms(extracted, variable.Transforms);
            executionCancellationToken.ThrowIfCancellationRequested();
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

    public string? EvaluateFirst(
        string body,
        RuleSelector selector,
        string? attributeName,
        CancellationToken executionCancellationToken)
    {
        executionCancellationToken.ThrowIfCancellationRequested();
        var value = _selectorEvaluator.EvaluateFirst(body, selector, attributeName);
        executionCancellationToken.ThrowIfCancellationRequested();
        return value;
    }

    private bool TryExtractValue(
        string body,
        RuleSelector? selector,
        string? attribute,
        RuleRegex? regex,
        string errorPrefix,
        CancellationToken executionCancellationToken,
        List<string> errors,
        out string? extracted)
    {
        if (selector is { } selected)
        {
            extracted = EvaluateFirst(body, selected, attribute, executionCancellationToken);
            return true;
        }

        if (regex is { } regexSpec)
        {
            executionCancellationToken.ThrowIfCancellationRequested();
            extracted = EvaluateRegex(
                body,
                regexSpec,
                errorPrefix,
                errors);
            executionCancellationToken.ThrowIfCancellationRequested();
            return true;
        }

        errors.Add($"{errorPrefix}: no extraction source defined.");
        extracted = null;
        return false;
    }

    private string? EvaluateRegex(
        string body,
        RuleRegex regexSpec,
        string errorPrefix,
        List<string> errors)
    {
        try
        {
            var executionTimeoutMilliseconds = (int)Math.Min(
                regexSpec.TimeoutMilliseconds,
                _limits.MaxRegexTime.TotalMilliseconds);
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
        catch (ArgumentException)
        {
            errors.Add($"{errorPrefix}: invalid regex pattern.");
            return null;
        }
        catch (RegexMatchTimeoutException)
        {
            errors.Add($"{errorPrefix}: regex evaluation timed out.");
            return null;
        }
    }

    private static string ApplyTransforms(
        string value,
        IReadOnlyList<RuleTransform> transforms)
    {
        foreach (var transform in transforms)
        {
            value = transform switch
            {
                TrimTransform => value.Trim(),
                ReplaceTransform replace => value.Replace(replace.From, replace.To),
                _ => value,
            };
        }

        return value;
    }
}
