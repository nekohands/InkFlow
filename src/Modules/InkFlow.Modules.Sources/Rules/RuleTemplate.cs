using System.Text.RegularExpressions;

namespace InkFlow.Modules.Sources.Rules;

public static partial class RuleTemplate
{
    public static string Expand(string template, IReadOnlyDictionary<string, string> variables)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(variables);

        return VariableRegex().Replace(template, match =>
        {
            var key = match.Groups[1].Value;
            return variables.TryGetValue(key, out var value)
                ? value
                : throw new KeyNotFoundException($"Template variable '{key}' is not defined.");
        });
    }

    [GeneratedRegex(@"\{\{\s*([A-Za-z0-9_.-]+)\s*\}\}", RegexOptions.CultureInvariant)]
    private static partial Regex VariableRegex();
}
