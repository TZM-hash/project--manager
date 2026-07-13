using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectManager.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectVendorAndTrialRun : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "TrialRunYearMonth",
                table: "Projects",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VendorName",
                table: "Projects",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TrialRunYearMonth",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "VendorName",
                table: "Projects");
        }
    }
}
