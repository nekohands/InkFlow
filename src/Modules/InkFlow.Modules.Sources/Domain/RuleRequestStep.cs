namespace InkFlow.Modules.Sources.Domain;

/// <summary>
/// 一次能力执行中的有界前置请求。步骤按声明顺序执行，响应变量只存在于本次
/// RuleAdapter 执行中，并可供后续步骤或主请求的模板使用。
/// </summary>
public sealed record RuleRequestStep(
    string Name,
    RuleRequest Request,
    IReadOnlyList<RuleResponseVariable>? ResponseVariables = null);
