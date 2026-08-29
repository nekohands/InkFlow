using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InkFlow.Modules.Developers.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDeveloperApplications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "developers");

            migrationBuilder.CreateTable(
                name: "applications",
                schema: "developers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Environment = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_applications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "api_keys",
                schema: "developers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Prefix = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SecretHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Scope = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Environment = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastUsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_api_keys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_api_keys_applications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalSchema: "developers",
                        principalTable: "applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_api_keys_ApplicationId_RevokedAt",
                schema: "developers",
                table: "api_keys",
                columns: new[] { "ApplicationId", "RevokedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_api_keys_SecretHash",
                schema: "developers",
                table: "api_keys",
                column: "SecretHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_api_keys_UserId_ApplicationId_CreatedAt",
                schema: "developers",
                table: "api_keys",
                columns: new[] { "UserId", "ApplicationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_applications_UserId_CreatedAt",
                schema: "developers",
                table: "applications",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_applications_UserId_RevokedAt",
                schema: "developers",
                table: "applications",
                columns: new[] { "UserId", "RevokedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "api_keys",
                schema: "developers");

            migrationBuilder.DropTable(
                name: "applications",
                schema: "developers");
        }
    }
}
