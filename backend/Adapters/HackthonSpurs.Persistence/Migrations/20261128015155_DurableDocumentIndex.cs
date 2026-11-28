using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HackthonSpurs.Documents.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DurableDocumentIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(name: "Page", table: "document_chunks", type: "integer", nullable: true);
            migrationBuilder.AddCheckConstraint(name: "ck_chunk_page", table: "document_chunks",
                sql: " \"Page\" IS NULL OR \"Page\" BETWEEN 1 AND 20 ");

            migrationBuilder.CreateTable(
                name: "document_index_jobs",
                columns: table => new
                {
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    Attempt = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LeaseToken = table.Column<Guid>(type: "uuid", nullable: true),
                    LeaseUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailureCode = table.Column<string>(type: "text", nullable: true),
                    EmbeddingVersion = table.Column<string>(type: "text", nullable: true),
                    Dimensions = table.Column<int>(type: "integer", nullable: true),
                    ChunkCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_index_jobs", x => x.DocumentId);
                    table.CheckConstraint("ck_index_attempt", " \"Attempt\" BETWEEN 0 AND 3 ");
                    table.CheckConstraint("ck_index_chunks", "(\"State\" = 'Completed' AND \"ChunkCount\" > 0 AND \"Dimensions\" IS NOT NULL AND \"EmbeddingVersion\" IS NOT NULL)\nOR (\"State\" <> 'Completed' AND \"ChunkCount\" = 0 AND \"Dimensions\" IS NULL AND \"EmbeddingVersion\" IS NULL)");
                    table.CheckConstraint("ck_index_failure", " (\"State\" = 'Failed') = (\"FailureCode\" IS NOT NULL) ");
                    table.CheckConstraint("ck_index_lease", "((\"State\" = 'Processing') = (\"LeaseToken\" IS NOT NULL)) AND ((\"State\" = 'Processing') = (\"LeaseUntil\" IS NOT NULL))");
                    table.CheckConstraint("ck_index_state", " \"State\" IN ('Queued','Processing','Completed','Skipped','Failed') ");
                    table.ForeignKey(
                        name: "FK_document_index_jobs_document_jobs_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "document_jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_document_index_queue",
                table: "document_index_jobs",
                columns: new[] { "State", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "document_index_jobs");
            migrationBuilder.DropCheckConstraint(name: "ck_chunk_page", table: "document_chunks");
            migrationBuilder.DropColumn(name: "Page", table: "document_chunks");
        }
    }
}
