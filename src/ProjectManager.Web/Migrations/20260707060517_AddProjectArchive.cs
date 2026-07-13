using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectManager.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectArchive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectArchives",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OriginalProjectId = table.Column<int>(type: "int", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    ProjectType = table.Column<int>(type: "int", nullable: false),
                    ParentCaseNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ProjectNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ProgressPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    ProjectAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CollectionPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    ProgressDescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StatusName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    StatusIsClosed = table.Column<bool>(type: "bit", nullable: false),
                    ClosedYearMonth = table.Column<DateOnly>(type: "date", nullable: true),
                    ArchivedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    ArchivedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    OriginalCreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    OriginalUpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AssignmentSummary = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectArchives", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectArchives_AspNetUsers_ArchivedByUserId",
                        column: x => x.ArchivedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectArchives_ArchivedAt",
                table: "ProjectArchives",
                column: "ArchivedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectArchives_ArchivedByUserId",
                table: "ProjectArchives",
                column: "ArchivedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectArchives_OriginalProjectId",
                table: "ProjectArchives",
                column: "OriginalProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectArchives_ParentCaseNumber",
                table: "ProjectArchives",
                column: "ParentCaseNumber");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectArchives_ProjectNumber",
                table: "ProjectArchives",
                column: "ProjectNumber");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectArchives_Year",
                table: "ProjectArchives",
                column: "Year");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectArchives");
        }
    }
}
