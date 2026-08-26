using InkFlow.BuildingBlocks.Security;

namespace InkFlow.Modules.Sources.Domain;

/// <summary>
/// 内容来源聚合。
/// 规则变更必须先通过 DSL 校验（结构 + 安全约束）才能写入；
/// BaseUrl 只允许 https/http 且端口受限——它是所有抓取请求的根。
/// </summary>
public sealed class Source
{
    public string Id { get; private set; } = null!;
    public string DisplayName { get; private set; } = null!;
    public string BaseUrl { get; private set; } = null!;
    public SourceRuleDsl? RuleDsl { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private Source() { }

    public static Source Create(string id, string displayName, string baseUrl, DateTimeOffset now)
    {
        ValidateId(id);
        ValidateBaseUrl(baseUrl, "baseUrl");

        return new Source
        {
            Id = id,
            DisplayName = displayName.Trim(),
            BaseUrl = baseUrl,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    public static Source Rehydrate(
        string id, string displayName, string baseUrl, SourceRuleDsl? ruleDsl,
        DateTimeOffset createdAt, DateTimeOffset updatedAt) =>
        new()
        {
            Id = id,
            DisplayName = displayName,
            BaseUrl = baseUrl,
            RuleDsl = ruleDsl,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
        };

    /// <summary>安装/更新规则文档。校验失败的文档绝不进入聚合。</summary>
    public void UpdateRuleDsl(SourceRuleDsl dsl, DateTimeOffset now)
    {
        var violations = SourceRuleDslValidator.Validate(dsl);
        if (violations.Count > 0)
        {
            throw new InvalidOperationException(
                $"rule DSL rejected for source '{Id}': {string.Join(" | ", violations)}");
        }

        RuleDsl = dsl;
        UpdatedAt = now;
    }

    public void UpdateMetadata(string displayName, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("display name must not be empty.", nameof(displayName));
        }

        DisplayName = displayName.Trim();
        UpdatedAt = now;
    }

    /// <summary>获取指定能力的规则；来源未安装规则或能力缺失时返回 null。</summary>
    public CapabilityRule? FindRule(SourceCapability capability) =>
        RuleDsl?.Rules.FirstOrDefault(r => r.Capability == capability);

    private static void ValidateId(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException("source id must be non-empty without whitespace.", nameof(id));
        }
    }

    private static void ValidateBaseUrl(string baseUrl, string paramName)
    {
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
        {
            throw new ArgumentException($"{paramName} must be an absolute URL.", nameof(baseUrl));
        }

        var errors = SsrfGuard.InspectLiteral(uri);
        if (errors.Count > 0)
        {
            throw new ArgumentException(
                $"{paramName} failed SSRF inspection: {string.Join("; ", errors)}", nameof(baseUrl));
        }
    }
}
