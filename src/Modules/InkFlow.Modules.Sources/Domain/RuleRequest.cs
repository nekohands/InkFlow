namespace InkFlow.Modules.Sources.Domain;

public enum RuleHttpMethod
{
    Get,
    Post,
}

/// <summary>
/// 能力对应的 HTTP 请求描述。<paramref name="pathTemplate"/> 支持 <c>{name}</c> 占位符，
/// 由执行器以查询参数或路径变量填充；禁止携带绝对 URL（目标主机由来源配置决定）。
/// </summary>
public sealed record RuleRequest(
    RuleHttpMethod Method,
    string PathTemplate,
    IReadOnlyDictionary<string, string> Headers,
    IReadOnlyDictionary<string, string> Query,
    IReadOnlyDictionary<string, string> Form)
{
    public static RuleRequest Get(string pathTemplate) => new(
        RuleHttpMethod.Get,
        pathTemplate,
        new Dictionary<string, string>(),
        new Dictionary<string, string>(),
        new Dictionary<string, string>());
}
