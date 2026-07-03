using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectManager.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectSkippedStatuses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectSkippedStatuses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    StatusId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectSkippedStatuses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectSkippedStatuses_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectSkippedStatuses_ProjectStatuses_StatusId",
                        column: x => x.StatusId,
                        principalTable: "ProjectStatuses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectSkippedStatuses_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectSkippedStatuses_CreatedByUserId",
                table: "ProjectSkippedStatuses",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectSkippedStatuses_ProjectId_StatusId",
                table: "ProjectSkippedStatuses",
                columns: new[] { "ProjectId", "StatusId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectSkippedStatuses_StatusId",
                table: "ProjectSkippedStatuses",
                column: "StatusId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectSkippedStatuses");
        }
    }
}
