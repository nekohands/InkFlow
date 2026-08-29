using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace InkFlow.Modules.Billing.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCommercialFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "billing");

            migrationBuilder.CreateTable(
                name: "plans",
                schema: "billing",
                columns: table => new
                {
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    MonthlyQuotaUnits = table.Column<long>(type: "bigint", nullable: false),
                    QuotaAlgorithmVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EntitlementsJson = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plans", x => new { x.Code, x.Version });
                });

            migrationBuilder.CreateTable(
                name: "usage_ledger",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApiKeyId = table.Column<Guid>(type: "uuid", nullable: false),
                    PeriodStart = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Operation = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Units = table.Column<long>(type: "bigint", nullable: false),
                    AlgorithmVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TraceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usage_ledger", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "usage_periods",
                schema: "billing",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PeriodStart = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UsedUnits = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usage_periods", x => new { x.UserId, x.PeriodStart });
                });

            migrationBuilder.CreateTable(
                name: "entitlement_assignments",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PlanVersion = table.Column<int>(type: "integer", nullable: false),
                    AssignedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_entitlement_assignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_entitlement_assignments_plans_PlanCode_PlanVersion",
                        columns: x => new { x.PlanCode, x.PlanVersion },
                        principalSchema: "billing",
                        principalTable: "plans",
                        principalColumns: new[] { "Code", "Version" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "billing",
                table: "plans",
                columns: new[] { "Code", "Version", "EntitlementsJson", "MonthlyQuotaUnits", "Name", "QuotaAlgorithmVersion" },
                values: new object[,]
                {
                    { "developer", 1, "[\"developer.catalog.read\"]", 1000000L, "Developer", "quota-v1" },
                    { "free", 1, "[\"developer.catalog.read\"]", 1000L, "Free", "quota-v1" },
                    { "pro", 1, "[\"developer.catalog.read\"]", 100000L, "Pro", "quota-v1" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_entitlement_assignments_PlanCode_PlanVersion",
                schema: "billing",
                table: "entitlement_assignments",
                columns: new[] { "PlanCode", "PlanVersion" });

            migrationBuilder.CreateIndex(
                name: "IX_entitlement_assignments_UserId_CreatedAt_Id",
                schema: "billing",
                table: "entitlement_assignments",
                columns: new[] { "UserId", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_usage_ledger_ApplicationId_ApiKeyId_PeriodStart",
                schema: "billing",
                table: "usage_ledger",
                columns: new[] { "ApplicationId", "ApiKeyId", "PeriodStart" });

            migrationBuilder.CreateIndex(
                name: "IX_usage_ledger_UserId_PeriodStart_OccurredAt",
                schema: "billing",
                table: "usage_ledger",
                columns: new[] { "UserId", "PeriodStart", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "entitlement_assignments",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "usage_ledger",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "usage_periods",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "plans",
                schema: "billing");
        }
    }
}
