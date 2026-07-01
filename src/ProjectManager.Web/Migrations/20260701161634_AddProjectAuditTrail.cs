using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectManager.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectAuditTrail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChangeDetailsJson",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChangeSummary",
                table: "AuditLogs",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProjectId",
                table: "AuditLogs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProjectNumber",
                table: "AuditLogs",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_ProjectId",
                table: "AuditLogs",
                column: "ProjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_ProjectId",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "ChangeDetailsJson",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "ChangeSummary",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "ProjectNumber",
                table: "AuditLogs");
        }
    }
}
