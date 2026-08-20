using System.Text;
using System.Text.RegularExpressions;

namespace InkFlow.Modules.Library;

public static partial class TextIdentityNormalizer
{
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Normalize(NormalizationForm.FormKC).Trim().ToLowerInvariant();
        return IdentityNoiseRegex().Replace(normalized, string.Empty);
    }

    [GeneratedRegex(@"[^\p{L}\p{Nd}]", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex IdentityNoiseRegex();
}
