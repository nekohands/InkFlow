// 定时调度宿主骨架：当前仅暴露 /health 探针供编排健康检查，
// 追更轮询等定时作业将在 Sources/Crawling 阶段接入。
using InkFlow.BuildingBlocks.Observability;

var builder = WebApplication.CreateBuilder(args);
builder.AddInkFlowObservability("InkFlow.Scheduler");

var app = builder.Build();
app.MapGet("/health", () => Results.Json(new { status = "healthy", service = "InkFlow.Scheduler" }));

app.Run();
