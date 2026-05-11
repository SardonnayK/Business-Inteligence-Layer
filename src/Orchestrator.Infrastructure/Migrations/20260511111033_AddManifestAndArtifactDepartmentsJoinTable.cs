using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orchestrator.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddManifestAndArtifactDepartmentsJoinTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Artifacts_Departments_DepartmentId",
                table: "Artifacts");

            migrationBuilder.DropIndex(
                name: "IX_Artifacts_DepartmentId",
                table: "Artifacts");

            migrationBuilder.CreateTable(
                name: "ArtifactDepartments",
                columns: table => new
                {
                    ArtifactId = table.Column<Guid>(type: "uuid", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArtifactDepartments", x => new { x.ArtifactId, x.DepartmentId });
                    table.ForeignKey(
                        name: "FK_ArtifactDepartments_Artifacts_ArtifactId",
                        column: x => x.ArtifactId,
                        principalTable: "Artifacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ArtifactDepartments_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql(
                """
                INSERT INTO "ArtifactDepartments" ("ArtifactId", "DepartmentId", "CreatedAt")
                SELECT "Id", "DepartmentId", NOW()
                FROM "Artifacts"
                WHERE "DepartmentId" IS NOT NULL;
                """);

            migrationBuilder.DropColumn(
                name: "DepartmentId",
                table: "Artifacts");

            migrationBuilder.CreateTable(
                name: "DepartmentManifests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DepartmentManifests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DepartmentManifests_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArtifactDepartments_DepartmentId",
                table: "ArtifactDepartments",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_DepartmentManifests_TenantId",
                table: "DepartmentManifests",
                column: "TenantId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArtifactDepartments");

            migrationBuilder.DropTable(
                name: "DepartmentManifests");

            migrationBuilder.AddColumn<Guid>(
                name: "DepartmentId",
                table: "Artifacts",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Artifacts_DepartmentId",
                table: "Artifacts",
                column: "DepartmentId");

            migrationBuilder.AddForeignKey(
                name: "FK_Artifacts_Departments_DepartmentId",
                table: "Artifacts",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id");
        }
    }
}
