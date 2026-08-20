using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace InkFlow.BuildingBlocks.Persistence.Migrations.Schema;

[DbContext(typeof(SchemaDbContext))]
[Migration("20260820150000_InitialSchemas")]
public sealed class InitialSchemas : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        foreach (var schema in DatabaseSchemas.All)
        {
            migrationBuilder.EnsureSchema(schema);
        }
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        foreach (var schema in DatabaseSchemas.All.Reverse())
        {
            migrationBuilder.Sql($"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE;");
        }
    }
}
