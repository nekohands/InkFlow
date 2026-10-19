using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace InkFlow.Api;

internal static class ApiKeysetCursor
{
    private const int MaxEncodedLength = 256;

    public static string Encode(DateTimeOffset timestamp, Guid id)
    {
        var payload = string.Create(
            CultureInfo.InvariantCulture,
            $"{timestamp.ToUniversalTime():O}|{id:D}");
        return WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(payload));
    }

    public static bool TryDecode(
        string? raw,
        out DateTimeOffset? timestamp,
        out Guid id)
    {
        timestamp = null;
        id = Guid.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        if (raw.Length > MaxEncodedLength)
        {
            return false;
        }

        try
        {
            var payload = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(raw.Trim()));
            var separator = payload.IndexOf('|');
            if (separator <= 0 ||
                separator == payload.Length - 1 ||
                payload.IndexOf('|', separator + 1) >= 0)
            {
                return false;
            }

            if (!DateTimeOffset.TryParseExact(
                    payload[..separator],
                    "O",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out var parsedTimestamp) ||
                !Guid.TryParseExact(payload[(separator + 1)..], "D", out var parsedId) ||
                parsedId == Guid.Empty)
            {
                return false;
            }

            timestamp = parsedTimestamp.ToUniversalTime();
            id = parsedId;
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
