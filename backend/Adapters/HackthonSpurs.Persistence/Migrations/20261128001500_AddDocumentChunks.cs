using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace HackthonSpurs.Documents.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentChunks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.CreateTable(
                name: "document_chunks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChunkIndex = table.Column<int>(type: "integer", nullable: false),
                    SourceName = table.Column<string>(type: "text", nullable: true),
                    Locator = table.Column<string>(type: "text", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Model = table.Column<string>(type: "text", nullable: false),
                    Embedding = table.Column<Vector>(type: "vector", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_chunks", x => x.Id);
                    table.CheckConstraint("ck_chunk_index", "\"ChunkIndex\" >= 0");
                    table.ForeignKey(
                        name: "FK_document_chunks_document_jobs_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "document_jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_document_chunk",
                table: "document_chunks",
                columns: new[] { "DocumentId", "ChunkIndex" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "document_chunks");
        }
    }
}
