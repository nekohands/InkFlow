using System.Text.RegularExpressions;
using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Sources.Application;

/// <summary>Extracts and transforms one bounded value from a rule response.</summary>
internal sealed class RuleResponseValueExtractor
{
    private readonly ISelectorEvaluator _selectorEvaluator;
    private readonly SourceRuleExecutionLimits _limits;

    public RuleResponseValueExtractor(
        ISelectorEvaluator selectorEvaluator,
        SourceRuleExecutionLimits limits)
    {
        _selectorEvaluator = selectorEvaluator;
        _limits = limits;
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

    public bool TryExtractAndTransform(
        string body,
        RuleSelector? selector,
        string? attribute,
        RuleRegex? regex,
        string errorPrefix,
        IReadOnlyList<RuleTransform> transforms,
        CancellationToken executionCancellationToken,
        List<string> errors,
        out string transformed)
    {
        transformed = string.Empty;
        if (!TryExtractValue(
                body,
                selector,
                attribute,
                regex,
                errorPrefix,
                executionCancellationToken,
                errors,
                out var extracted))
        {
            return false;
        }

        if (extracted is null)
        {
            errors.Add($"{errorPrefix}: no match found in response.");
            return false;
        }

        transformed = ApplyTransforms(extracted, transforms);
        executionCancellationToken.ThrowIfCancellationRequested();
        return true;
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
