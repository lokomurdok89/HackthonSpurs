using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HackthonSpurs.Documents.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DocumentOcrRouting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "SendToOcr",
                table: "document_jobs",
                type: "boolean",
                nullable: false,
                defaultValue: false);
            // Previous versions admitted images only through the explicit SPUR lane.
            migrationBuilder.Sql("""UPDATE document_jobs SET "SendToOcr" = true WHERE "MediaType" IN ('image/png','image/jpeg')""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SendToOcr",
                table: "document_jobs");
        }
    }
}
