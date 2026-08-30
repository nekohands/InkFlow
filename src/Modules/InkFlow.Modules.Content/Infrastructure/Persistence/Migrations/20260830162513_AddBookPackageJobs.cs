using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InkFlow.Modules.Content.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBookPackageJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "package_jobs",
                schema: "content",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CanonicalBookId = table.Column<Guid>(type: "uuid", nullable: false),
                    Format = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    MaxAttempts = table.Column<int>(type: "integer", nullable: false),
                    ScheduledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LeaseOwner = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    LeaseExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TotalChapterCount = table.Column<int>(type: "integer", nullable: false),
                    CompletedChapterCount = table.Column<int>(type: "integer", nullable: false),
                    ArtifactFileName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ArtifactSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ArtifactLength = table.Column<long>(type: "bigint", nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_package_jobs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_package_jobs_CanonicalBookId_CreatedAt",
                schema: "content",
                table: "package_jobs",
                columns: new[] { "CanonicalBookId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_package_jobs_Status_ExpiresAt",
                schema: "content",
                table: "package_jobs",
                columns: new[] { "Status", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_package_jobs_Status_ScheduledAt_CreatedAt",
                schema: "content",
                table: "package_jobs",
                columns: new[] { "Status", "ScheduledAt", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "package_jobs",
                schema: "content");
        }
    }
}
