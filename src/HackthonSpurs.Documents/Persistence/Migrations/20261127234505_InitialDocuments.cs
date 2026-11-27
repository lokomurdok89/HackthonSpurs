using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HackthonSpurs.Documents.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "document_jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationKey = table.Column<Guid>(type: "uuid", nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    MediaType = table.Column<string>(type: "text", nullable: false),
                    ByteCount = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    Attempt = table.Column<int>(type: "integer", nullable: false),
                    FailureCode = table.Column<string>(type: "text", nullable: true),
                    ResultJson = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_jobs", x => x.Id);
                    table.CheckConstraint("ck_attempt", "\"Attempt\" >= 0");
                    table.CheckConstraint("ck_failure", "(\"State\" = 'Failed') = (\"FailureCode\" IS NOT NULL)");
                    table.CheckConstraint("ck_result", "(\"State\" = 'Completed') = (\"ResultJson\" IS NOT NULL)");
                    table.CheckConstraint("ck_state", "\"State\" IN ('Queued','Processing','Completed','Failed')");
                });

            migrationBuilder.CreateIndex(
                name: "ux_document_operation",
                table: "document_jobs",
                column: "OperationKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "document_jobs");
        }
    }
}
