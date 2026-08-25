namespace InkFlow.Modules.Sources.Application;

/// <summary>
/// 一次能力规则执行的输出：成功时携带全部字段值；
/// 失败时 <see cref="Errors"/> 给出可分类的原因（模板变量缺失、SSRF 拒绝、上游状态码、字段抽取失败等）。
/// </summary>
public sealed record RuleExecutionResult(
    bool IsSuccess,
    IReadOnlyDictionary<string, string> Values,
    IReadOnlyList<string> Errors)
{
    public static RuleExecutionResult Ok(IReadOnlyDictionary<string, string> values) =>
        new(true, values, []);

    public static RuleExecutionResult Fail(IReadOnlyList<string> errors) =>
        new(false, new Dictionary<string, string>(), errors);
}
