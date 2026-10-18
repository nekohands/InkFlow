using System.Text;
using System.Text.RegularExpressions;
using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Sources.Application;

/// <summary>Validates the bounded variable context shared by a rule execution.</summary>
internal static class RuleVariableContextValidator
{
    private static readonly Regex VariableNamePattern =
        new(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

    public static List<string> Validate(
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
}
