using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orchestrator.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDepartmentsAndArtifacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Add ChatModelId to EmbeddingProviderConfigs
            migrationBuilder.AddColumn<string>(
                name: "ChatModelId",
                table: "EmbeddingProviderConfigs",
                type: "text",
                nullable: true);

            // 2. Create Departments table
            migrationBuilder.CreateTable(
                name: "Departments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    EstimatedSize = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Departments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Departments_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // 3. Create Artifacts table
            migrationBuilder.CreateTable(
                name: "Artifacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    IsShared = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Artifacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Artifacts_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Artifacts_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            // 4. Add ArtifactId to BusinessContexts
            migrationBuilder.AddColumn<Guid>(
                name: "ArtifactId",
                table: "BusinessContexts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_BusinessContexts_Artifacts_ArtifactId",
                table: "BusinessContexts",
                column: "ArtifactId",
                principalTable: "Artifacts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // 5. Create indexes
            migrationBuilder.CreateIndex(
                name: "IX_Departments_TenantId",
                table: "Departments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Artifacts_TenantId",
                table: "Artifacts",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Artifacts_DepartmentId",
                table: "Artifacts",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessContexts_ArtifactId",
                table: "BusinessContexts",
                column: "ArtifactId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverse in reverse order

            migrationBuilder.DropIndex(
                name: "IX_BusinessContexts_ArtifactId",
                table: "BusinessContexts");

            migrationBuilder.DropIndex(
                name: "IX_Artifacts_DepartmentId",
                table: "Artifacts");

            migrationBuilder.DropIndex(
                name: "IX_Artifacts_TenantId",
                table: "Artifacts");

            migrationBuilder.DropIndex(
                name: "IX_Departments_TenantId",
                table: "Departments");

            migrationBuilder.DropForeignKey(
                name: "FK_BusinessContexts_Artifacts_ArtifactId",
                table: "BusinessContexts");

            migrationBuilder.DropColumn(
                name: "ArtifactId",
                table: "BusinessContexts");

            migrationBuilder.DropTable(name: "Artifacts");

            migrationBuilder.DropTable(name: "Departments");

            migrationBuilder.DropColumn(
                name: "ChatModelId",
                table: "EmbeddingProviderConfigs");
        }
    }
}
