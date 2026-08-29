using System.Globalization;
using System.Text;
using InkFlow.Modules.Operations.Application;
using Microsoft.AspNetCore.WebUtilities;

namespace InkFlow.Api;

public sealed record OperationsAlertHistoryPageResponse(
    IReadOnlyList<OperationsAlertHistoryEntry> Entries,
    string? NextCursor);

/// <summary>Operations 告警历史查询的有界输入和不透明游标映射。</summary>
public static class OperationsAlertHistoryEndpointResults
{
    public const int DefaultLimit = 50;
    public const int MaxLimit = 100;

    public static bool TryCreateQuery(
        int? limitRaw,
        string? cursorRaw,
        out int limit,
        out OperationsAlertHistoryCursor? before,
        out string error)
    {
        limit = limitRaw ?? DefaultLimit;
        before = null;
        error = "invalid_operations_alert_history_query";

        if (limit is < 1 or > MaxLimit || !TryParseCursor(cursorRaw, out before))
        {
            return false;
        }

        return true;
    }

    public static OperationsAlertHistoryPageResponse ToResponse(
        OperationsAlertHistoryPage page) =>
        new(page.Entries, EncodeCursor(page.NextCursor));

    public static string? EncodeCursor(OperationsAlertHistoryCursor? cursor)
    {
        if (cursor is null)
        {
            return null;
        }

        var payload = string.Create(
            CultureInfo.InvariantCulture,
            $"{cursor.OccurredAt.ToUniversalTime():O}|{cursor.Id:D}");
        return WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(payload));
    }

    private static bool TryParseCursor(
        string? raw,
        out OperationsAlertHistoryCursor? cursor)
    {
        cursor = null;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        if (raw.Length > 256)
        {
            return false;
        }

        try
        {
            var payload = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(raw.Trim()));
            var separator = payload.IndexOf('|');
            if (separator <= 0 || separator == payload.Length - 1 ||
                payload.IndexOf('|', separator + 1) >= 0)
            {
                return false;
            }

            if (!DateTimeOffset.TryParseExact(
                    payload[..separator],
                    "O",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out var occurredAt) ||
                !Guid.TryParseExact(payload[(separator + 1)..], "D", out var id) ||
                id == Guid.Empty)
            {
                return false;
            }

            cursor = new OperationsAlertHistoryCursor(occurredAt.ToUniversalTime(), id);
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
