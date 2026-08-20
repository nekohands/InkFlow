var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "InkFlow.Api" }));
app.MapGet("/api/v1", () => Results.Ok(new { name = "墨流", product = "InkFlow", version = "v1" }));

app.Run();
