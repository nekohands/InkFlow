using InkFlow.Modules.Operations.Application;

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
        return cursor is null ? null : ApiKeysetCursor.Encode(cursor.OccurredAt, cursor.Id);
    }

    private static bool TryParseCursor(
        string? raw,
        out OperationsAlertHistoryCursor? cursor)
    {
        if (!ApiKeysetCursor.TryDecode(raw, out var occurredAt, out var id))
        {
            cursor = null;
            return false;
        }

        cursor = occurredAt is { } value
            ? new OperationsAlertHistoryCursor(value, id)
            : null;
        return true;
    }
}
