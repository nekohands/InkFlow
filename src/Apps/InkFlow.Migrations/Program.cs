using InkFlow.BuildingBlocks.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("Database")
    ?? "Host=localhost;Port=5432;Database=inkflow;Username=inkflow;Password=inkflow";

builder.Services.AddInkFlowPersistence(connectionString);

using var host = builder.Build();
await host.Services.MigrateInkFlowAsync();

Console.WriteLine("InkFlow database migrations completed successfully.");
