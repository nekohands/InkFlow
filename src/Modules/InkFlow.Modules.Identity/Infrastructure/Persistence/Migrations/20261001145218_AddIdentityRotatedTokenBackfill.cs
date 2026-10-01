using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InkFlow.Modules.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIdentityRotatedTokenBackfill : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 访问令牌的有效性已改为以所属会话活跃为前提（轮换/吊销会话即拒绝其令牌）。
            // 此前 RotateRefreshSessionAsync 只吊销会话本身、不吊销旧访问令牌，
            // 这里把被轮换/吊销会话下仍处于未吊销状态的令牌回填为“随会话吊销”，
            // 使存量数据与新验证语义一致，也让保留清理可以安全判定令牌终态。
            // 幂等：仅回填 RevokedAt 为空的行，重复执行无二次效果。
            migrationBuilder.Sql(
                """
                UPDATE "identity"."access_tokens" AS t
                   SET "RevokedAt" = s."RevokedAt"
                  FROM "identity"."sessions" AS s
                 WHERE s."Id" = t."SessionId"
                   AND t."RevokedAt" IS NULL
                   AND s."RevokedAt" IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 单向数据回填：无法区分“随会话回填的吊销”与“真实吊销”，不提供逆操作。
        }
    }
}
