using InkFlow.Api;
using InkFlow.BuildingBlocks.Observability;
using InkFlow.BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(TimeProvider.System);
builder.AddInkFlowObservability("InkFlow.Api");

var connectionString = builder.Configuration.GetConnectionString("Database")
    ?? "Host=localhost;Port=5432;Database=inkflow;Username=inkflow;Password=inkflow";
builder.Services.AddInkFlowPersistence(connectionString);

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "InkFlow.Api" }));
app.MapGet("/ready", async (LibraryDbContext database, CancellationToken cancellationToken) =>
{
    if (!await database.Database.CanConnectAsync(cancellationToken))
    {
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }

    return Results.Ok(new { status = "ready", service = "InkFlow.Api" });
});
app.MapGet("/api/v1", () => Results.Ok(new { name = "墨流", product = "InkFlow", version = "v1" }));
app.MapCatalogEndpoints();

app.Run();

public partial class Program;
