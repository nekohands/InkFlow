using System.Collections.Frozen;

namespace InkFlow.Modules.Legado.Application;

/// <summary>
/// 已发布的 Legado 兼容性边界。变更外部书源字段或路由前必须提升/新增 Profile。
/// </summary>
public sealed record LegadoCompatibilityProfile(
    string SchemaVersion,
    string MinSupportedVersion,
    string TestedVersion,
    IReadOnlySet<string> Capabilities,
    DateTimeOffset? DeprecatedAt)
{
    public static LegadoCompatibilityProfile Current { get; } = new(
        SchemaVersion: "legado-book-source-v1",
        MinSupportedVersion: "3.0",
        TestedVersion: "3.0",
        Capabilities: new[]
        {
            "search",
            "book-info",
            "toc",
            "content",
            "personal-token",
        }.ToFrozenSet(StringComparer.Ordinal),
        DeprecatedAt: null);

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(SchemaVersion) ||
            string.IsNullOrWhiteSpace(MinSupportedVersion) ||
            string.IsNullOrWhiteSpace(TestedVersion) ||
            Capabilities.Count == 0)
        {
            throw new InvalidOperationException("Legado compatibility profile is incomplete.");
        }

        if (Capabilities.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException("Legado compatibility capabilities must be non-empty.");
        }
    }
}

/// <summary>Legado 规则生成器 seam。API 只依赖此接口，避免散落手工 JSON。</summary>
public interface ILegadoRuleGenerator
{
    LegadoCompatibilityProfile Profile { get; }

    string Generate(string baseUrl, string? legadoToken = null);
}

public sealed class LegadoRuleGenerator : ILegadoRuleGenerator
{
    public LegadoCompatibilityProfile Profile => LegadoCompatibilityProfile.Current;

    public string Generate(string baseUrl, string? legadoToken = null)
    {
        Profile.Validate();
        return LegadoBookSourceManifest.Generate(baseUrl, legadoToken);
    }
}
