using InkFlow.Api;
using InkFlow.BuildingBlocks.Observability;
using InkFlow.Modules.Developers.Infrastructure.Authentication;
using Microsoft.AspNetCore.Authentication;

var builder = WebApplication.CreateBuilder(args);
builder.AddInkFlowApiServices();

var app = builder.Build();

// 全局兜底放在最外层：未捕获异常由 RequestAuditMiddleware/SLO 中间件先观察并审计，
// 到达这里后统一转为不含堆栈与异常细节的 ProblemDetails，任何环境都不走默认错误页。
app.UseInkFlowExceptionHandler();

// 认证先于审计/限流，使审计 actor 与认证主体分桶均可用；health 不进入业务审计。
app.UseAuthentication();
app.UseMiddleware<RequestAuditMiddleware>();
app.Use(async (context, next) =>
{
    // Developer API has a dedicated key scheme. Authenticate it before the
    // rate-limit partition is selected so valid keys are bucketed by key ID;
    // missing/invalid keys remain IP-bucketed and are then rejected by policy.
    if (context.Request.Path.StartsWithSegments("/api/developer/v1"))
    {
        var result = await context
            .AuthenticateAsync(DeveloperAuthenticationDefaults.Scheme)
            .ConfigureAwait(false);
        if (result.Succeeded && result.Principal is not null)
        {
            context.User = result.Principal;
        }
    }

    await next().ConfigureAwait(false);
});
app.UseMiddleware<CoreSloMetricsMiddleware>();
app.UseRateLimiter();
app.UseAuthorization();

app.MapInkFlowEndpoints();
app.Run();

public partial class Program;
