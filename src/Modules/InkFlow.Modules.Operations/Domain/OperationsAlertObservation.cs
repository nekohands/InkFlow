using System.Security.Cryptography;
using System.Text;

namespace InkFlow.Modules.Operations.Domain;

/// <summary>
/// 告警身份只由稳定 code 和资源坐标构成；动态 message 不进入 fingerprint，避免
/// 计数变化制造新的告警，也避免把潜在敏感文本写入历史。
/// </summary>
public sealed record OperationsAlertObservation
{
    public const int MaxCodeLength = 128;
    public const int MaxSeverityLength = 32;
    public const int MaxResourceTypeLength = 64;
    public const int MaxResourceIdLength = 256;

    private OperationsAlertObservation(
        string code,
        string severity,
        string resourceType,
        string resourceId)
    {
        Code = code;
        Severity = severity;
        ResourceType = resourceType;
        ResourceId = resourceId;
        Fingerprint = CreateFingerprint(code, resourceType, resourceId);
    }

    public string Fingerprint { get; }

    public string Code { get; }

    public string Severity { get; }

    public string ResourceType { get; }

    public string ResourceId { get; }

    public static OperationsAlertObservation Create(
        string code,
        string severity,
        string resourceType,
        string resourceId) =>
        new(
            NormalizeRequired(code, nameof(code), MaxCodeLength),
            NormalizeRequired(severity, nameof(severity), MaxSeverityLength),
            NormalizeRequired(resourceType, nameof(resourceType), MaxResourceTypeLength),
            NormalizeRequired(resourceId, nameof(resourceId), MaxResourceIdLength));

    private static string NormalizeRequired(string value, string name, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("alert identity must not be empty.", name);
        }

        var normalized = value.Trim();
        if (normalized.Length > maxLength || normalized.Any(char.IsControl))
        {
            throw new ArgumentException("alert identity is too long or contains control characters.", name);
        }

        return normalized;
    }

    private static string CreateFingerprint(
        string code,
        string resourceType,
        string resourceId)
    {
        var canonical = string.Join('\u001f', code, resourceType, resourceId);
        return Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))
            .ToLowerInvariant();
    }
}
