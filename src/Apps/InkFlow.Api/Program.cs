// dev 分支基础设施骨架：仅提供 /health 探针与 v1 信息端点，
// 模块 API 将随各 Phase 的垂直切片逐步接入。
using InkFlow.BuildingBlocks.Observability;

var builder = WebApplication.CreateBuilder(args);
builder.AddInkFlowObservability("InkFlow.Api");

var app = builder.Build();

app.MapGet("/health", () => Results.Json(new { status = "healthy", service = "InkFlow.Api" }));
app.MapGet("/api/v1", () => Results.Json(new { product = "InkFlow", name = "墨流" }));

app.Run();
