// 后台任务宿主骨架：当前仅暴露 /health 探针供编排健康检查，
// 抓取执行等后台作业将在 Crawling 阶段接入。
using InkFlow.BuildingBlocks.Observability;

var builder = WebApplication.CreateBuilder(args);
builder.AddInkFlowObservability("InkFlow.Worker");

var app = builder.Build();
app.MapGet("/health", () => Results.Json(new { status = "healthy", service = "InkFlow.Worker" }));

app.Run();
