using Microsoft.Extensions.DependencyInjection;

namespace InkFlow.Api;

/// <summary>
/// 全局兜底错误响应：未捕获异常统一转为 <c>application/problem+json</c>。
/// 任何环境都不内联异常类型、消息、堆栈或物理路径——错误细节只进结构化日志
/// 与审计（RequestAuditMiddleware 已记录 unhandled-exception），不进响应体。
/// 既有端点的稳定错误体（如 auth 的 <c>{ "error": code }</c>）不受影响：
/// 兜底只处理到达中间件的未捕获异常，不重写已完成的状态码响应。
/// </summary>
public static class ApiErrorHandlingExtensions
{
    public static IServiceCollection AddInkFlowProblemDetails(this IServiceCollection services) =>
        services.AddProblemDetails(options =>
        {
            // 无论哪个组件（如开发环境的异常内联）预先填入异常细节，写出前一律剥除。
            options.CustomizeProblemDetails = context =>
            {
                context.ProblemDetails.Extensions.Remove("exceptionDetails");
            };
        });

    /// <summary>必须注册为最外层中间件，使其余中间件仍能观察并审计原始异常。</summary>
    public static IApplicationBuilder UseInkFlowExceptionHandler(this IApplicationBuilder app) =>
        app.UseExceptionHandler();
}
