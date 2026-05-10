using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orchestrator.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUsersAndPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Artifacts_Departments_DepartmentId",
                table: "Artifacts");

            migrationBuilder.DropForeignKey(
                name: "FK_BusinessContexts_Artifacts_ArtifactId",
                table: "BusinessContexts");

            migrationBuilder.CreateTable(
                name: "TenantUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Username = table.Column<string>(type: "text", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantUsers_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ArtifactPermissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArtifactId = table.Column<Guid>(type: "uuid", nullable: false),
                    CanRead = table.Column<bool>(type: "boolean", nullable: false),
                    CanWrite = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArtifactPermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArtifactPermissions_Artifacts_ArtifactId",
                        column: x => x.ArtifactId,
                        principalTable: "Artifacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ArtifactPermissions_TenantUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "TenantUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArtifactPermissions_ArtifactId",
                table: "ArtifactPermissions",
                column: "ArtifactId");

            migrationBuilder.CreateIndex(
                name: "IX_ArtifactPermissions_UserId",
                table: "ArtifactPermissions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ArtifactPermissions_UserId_ArtifactId",
                table: "ArtifactPermissions",
                columns: new[] { "UserId", "ArtifactId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantUsers_TenantId",
                table: "TenantUsers",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantUsers_TenantId_Username",
                table: "TenantUsers",
                columns: new[] { "TenantId", "Username" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Artifacts_Departments_DepartmentId",
                table: "Artifacts",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_BusinessContexts_Artifacts_ArtifactId",
                table: "BusinessContexts",
                column: "ArtifactId",
                principalTable: "Artifacts",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Artifacts_Departments_DepartmentId",
                table: "Artifacts");

            migrationBuilder.DropForeignKey(
                name: "FK_BusinessContexts_Artifacts_ArtifactId",
                table: "BusinessContexts");

            migrationBuilder.DropTable(
                name: "ArtifactPermissions");

            migrationBuilder.DropTable(
                name: "TenantUsers");

            migrationBuilder.AddForeignKey(
                name: "FK_Artifacts_Departments_DepartmentId",
                table: "Artifacts",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_BusinessContexts_Artifacts_ArtifactId",
                table: "BusinessContexts",
                column: "ArtifactId",
                principalTable: "Artifacts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
