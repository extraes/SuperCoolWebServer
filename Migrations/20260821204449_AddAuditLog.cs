using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SuperCoolWebServer.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditLogEntries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Action = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    EntityType = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    EntityId = table.Column<long>(type: "INTEGER", nullable: true),
                    UserId = table.Column<long>(type: "INTEGER", nullable: true),
                    DetailsJson = table.Column<string>(type: "TEXT", maxLength: 16384, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogEntries", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogEntries");
        }
    }
}
