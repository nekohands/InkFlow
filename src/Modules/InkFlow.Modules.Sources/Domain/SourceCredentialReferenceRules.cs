namespace InkFlow.Modules.Sources.Domain;

/// <summary>
/// Shared domain validation for non-sensitive credential reference identifiers.
/// The identifier may be persisted or transported, but it never contains credential material.
/// </summary>
public static class SourceCredentialReferenceRules
{
    public const int MaxLength = 256;

    public static bool IsValid(string? referenceId)
    {
        if (string.IsNullOrEmpty(referenceId) || referenceId.Length > MaxLength)
        {
            return false;
        }

        for (var index = 0; index < referenceId.Length; index++)
        {
            var character = referenceId[index];
            var isAlphaNumeric = character is >= 'a' and <= 'z' or
                >= 'A' and <= 'Z' or
                >= '0' and <= '9';
            if (index == 0 && !isAlphaNumeric)
            {
                return false;
            }

            if (!isAlphaNumeric && character is not ('.' or '_' or '-'))
            {
                return false;
            }
        }

        return true;
    }
}
