using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orchestrator.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmbeddingProviderConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Remove dimension constraint so vectors from any provider fit
            migrationBuilder.Sql(@"ALTER TABLE ""BusinessContexts"" ALTER COLUMN ""Embedding"" TYPE vector USING ""Embedding""::vector;");

            migrationBuilder.CreateTable(
                name: "EmbeddingProviderConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProviderType = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ModelId = table.Column<string>(type: "text", nullable: false),
                    ApiKey = table.Column<string>(type: "text", nullable: true),
                    Endpoint = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmbeddingProviderConfigs", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "EmbeddingProviderConfigs");

            // Restore dimension constraint (best-effort — data must still be 1536-dim)
            migrationBuilder.Sql(@"ALTER TABLE ""BusinessContexts"" ALTER COLUMN ""Embedding"" TYPE vector(1536) USING ""Embedding""::vector(1536);");
        }
    }
}
