using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectManager.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddPlanningProjectsAndWeakManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsWeakManaged",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "PlanningProjects",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LeaderUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    LatestDescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanningProjects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanningProjects_AspNetUsers_LeaderUserId",
                        column: x => x.LeaderUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PlanningProjectHistoryRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PlanningProjectId = table.Column<int>(type: "int", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    PreviousDescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CurrentRecord = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanningProjectHistoryRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanningProjectHistoryRecords_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PlanningProjectHistoryRecords_PlanningProjects_PlanningProjectId",
                        column: x => x.PlanningProjectId,
                        principalTable: "PlanningProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlanningProjectHistoryRecords_CreatedByUserId",
                table: "PlanningProjectHistoryRecords",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanningProjectHistoryRecords_PlanningProjectId_Year_Month",
                table: "PlanningProjectHistoryRecords",
                columns: new[] { "PlanningProjectId", "Year", "Month" });

            migrationBuilder.CreateIndex(
                name: "IX_PlanningProjects_LeaderUserId",
                table: "PlanningProjects",
                column: "LeaderUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlanningProjectHistoryRecords");

            migrationBuilder.DropTable(
                name: "PlanningProjects");

            migrationBuilder.DropColumn(
                name: "IsWeakManaged",
                table: "AspNetUsers");
        }
    }
}
