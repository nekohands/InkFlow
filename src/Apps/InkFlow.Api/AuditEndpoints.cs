using System.Globalization;
using System.Text;
using InkFlow.BuildingBlocks.Persistence;
using InkFlow.BuildingBlocks.Security;
using Microsoft.AspNetCore.WebUtilities;

namespace InkFlow.Api;

public sealed record AuditEventResponse(
    Guid Id,
    DateTimeOffset OccurredAt,
    string ActorType,
    string? ActorId,
    string Action,
    string Resource,
    string Outcome,
    int StatusCode,
    string? Reason,
    string? TraceId,
    string? Reference);

public sealed record AuditEventPageResponse(
    IReadOnlyList<AuditEventResponse> Events,
    string? NextCursor);

/// <summary>Admin 审计读端的输入解析、游标编码和稳定响应映射。</summary>
public static class AuditEndpointResults
{
    public const int DefaultLimit = AuditEventQuery.DefaultLimit;
    public const int MaxLimit = AuditEventQuery.MaxLimit;

    public static bool TryCreateQuery(
        string? fromRaw,
        string? toRaw,
        string? actionRaw,
        string? outcomeRaw,
        string? actorIdRaw,
        string? cursorRaw,
        int? limitRaw,
        out AuditEventQuery? query,
        out string error)
    {
        query = null;
        error = "invalid_audit_query";

        if (!TryParseDate(fromRaw, out var from) ||
            !TryParseDate(toRaw, out var to) ||
            !TryParseFilter(actionRaw, 128, out var action) ||
            !TryParseFilter(outcomeRaw, 64, out var outcome) ||
            !TryParseFilter(actorIdRaw, 256, out var actorId) ||
            !TryParseCursor(cursorRaw, out var before))
        {
            return false;
        }

        var limit = limitRaw ?? DefaultLimit;
        if (limit is < 1 or > MaxLimit || (from is not null && to is not null && from > to))
        {
            return false;
        }

        query = new AuditEventQuery(from, to, action, outcome, actorId, before, limit);
        return true;
    }

    public static AuditEventPageResponse ToResponse(AuditEventPage page) =>
        new(
            page.Events.Select(ToResponse).ToList(),
            EncodeCursor(page.NextCursor));

    public static string? EncodeCursor(AuditEventCursor? cursor)
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

    private static AuditEventResponse ToResponse(AuditEvent auditEvent) =>
        new(
            auditEvent.Id,
            auditEvent.OccurredAt,
            auditEvent.ActorType,
            auditEvent.ActorId,
            auditEvent.Action,
            auditEvent.Resource,
            auditEvent.Outcome,
            auditEvent.StatusCode,
            auditEvent.Reason,
            auditEvent.TraceId,
            auditEvent.Reference);

    private static bool TryParseDate(
        string? raw,
        out DateTimeOffset? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        if (!DateTimeOffset.TryParse(
                raw.Trim(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.RoundtripKind,
                out var parsed))
        {
            return false;
        }

        value = parsed.ToUniversalTime();
        return true;
    }

    private static bool TryParseFilter(
        string? raw,
        int maxLength,
        out string? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        var normalized = raw.Trim();
        if (normalized.Length > maxLength || normalized.Any(char.IsControl))
        {
            return false;
        }

        value = normalized;
        return true;
    }

    private static bool TryParseCursor(
        string? raw,
        out AuditEventCursor? cursor)
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

            cursor = new AuditEventCursor(occurredAt.ToUniversalTime(), id);
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
