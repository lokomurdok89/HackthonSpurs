using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HackthonSpurs.Documents.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WorkspaceAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "workspace_users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Username = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    NormalizedUsername = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workspace_users", x => x.Id);
                    table.CheckConstraint("ck_workspace_role", "\"Role\" IN ('admin', 'reader')");
                });

            migrationBuilder.CreateTable(
                name: "workspace_sessions",
                columns: table => new
                {
                    Digest = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workspace_sessions", x => x.Digest);
                    table.ForeignKey(
                        name: "FK_workspace_sessions_workspace_users_UserId",
                        column: x => x.UserId,
                        principalTable: "workspace_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_workspace_sessions_ExpiresAt",
                table: "workspace_sessions",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_workspace_sessions_UserId",
                table: "workspace_sessions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_workspace_users_NormalizedUsername",
                table: "workspace_users",
                column: "NormalizedUsername",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "workspace_sessions");

            migrationBuilder.DropTable(
                name: "workspace_users");
        }
    }
}
