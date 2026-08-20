using InkFlow.BuildingBlocks.Observability;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(TimeProvider.System);
builder.AddInkFlowObservability("InkFlow.Api");

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "InkFlow.Api" }));
app.MapGet("/api/v1", () => Results.Ok(new { name = "墨流", product = "InkFlow", version = "v1" }));

app.Run();

public partial class Program;
