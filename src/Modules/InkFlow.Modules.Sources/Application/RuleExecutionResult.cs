namespace InkFlow.Modules.Sources.Application;

/// <summary>
/// 一次能力规则执行的输出：成功时携带全部字段值；
/// 失败时 <see cref="Errors"/> 给出可分类的原因（模板变量缺失、SSRF 拒绝、上游状态码、字段抽取失败等）。
/// Body 为原始响应文本，供列表绑定(List)在规则适配器中做条目集抽取。
/// </summary>
public sealed record RuleExecutionResult(
    bool IsSuccess,
    IReadOnlyDictionary<string, string> Values,
    IReadOnlyList<string> Errors,
    string? Body = null)
{
    public static RuleExecutionResult Ok(IReadOnlyDictionary<string, string> values, string? body = null) =>
        new(true, values, [], body);

    public static RuleExecutionResult Fail(IReadOnlyList<string> errors) =>
        new(false, new Dictionary<string, string>(), errors);
}