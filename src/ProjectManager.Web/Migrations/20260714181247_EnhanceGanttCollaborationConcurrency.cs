using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectManager.Web.Migrations
{
    /// <inheritdoc />
    public partial class EnhanceGanttCollaborationConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Projects",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ActualFinishDate",
                table: "ProjectGanttTasks",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ActualStartDate",
                table: "ProjectGanttTasks",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsMilestone",
                table: "ProjectGanttTasks",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "OwnerUserId",
                table: "ProjectGanttTasks",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PredecessorTaskId",
                table: "ProjectGanttTasks",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "ProjectGanttPlans",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.CreateTable(
                name: "ProjectCollaborationRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectCollaborationRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectCollaborationRecords_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectCollaborationRecords_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectGanttTasks_OwnerUserId",
                table: "ProjectGanttTasks",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectGanttTasks_PredecessorTaskId",
                table: "ProjectGanttTasks",
                column: "PredecessorTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCollaborationRecords_CreatedByUserId",
                table: "ProjectCollaborationRecords",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectCollaborationRecords_ProjectId_CreatedAt",
                table: "ProjectCollaborationRecords",
                columns: new[] { "ProjectId", "CreatedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectGanttTasks_AspNetUsers_OwnerUserId",
                table: "ProjectGanttTasks",
                column: "OwnerUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectGanttTasks_ProjectGanttTasks_PredecessorTaskId",
                table: "ProjectGanttTasks",
                column: "PredecessorTaskId",
                principalTable: "ProjectGanttTasks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProjectGanttTasks_AspNetUsers_OwnerUserId",
                table: "ProjectGanttTasks");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectGanttTasks_ProjectGanttTasks_PredecessorTaskId",
                table: "ProjectGanttTasks");

            migrationBuilder.DropTable(
                name: "ProjectCollaborationRecords");

            migrationBuilder.DropIndex(
                name: "IX_ProjectGanttTasks_OwnerUserId",
                table: "ProjectGanttTasks");

            migrationBuilder.DropIndex(
                name: "IX_ProjectGanttTasks_PredecessorTaskId",
                table: "ProjectGanttTasks");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "ActualFinishDate",
                table: "ProjectGanttTasks");

            migrationBuilder.DropColumn(
                name: "ActualStartDate",
                table: "ProjectGanttTasks");

            migrationBuilder.DropColumn(
                name: "IsMilestone",
                table: "ProjectGanttTasks");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "ProjectGanttTasks");

            migrationBuilder.DropColumn(
                name: "PredecessorTaskId",
                table: "ProjectGanttTasks");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "ProjectGanttPlans");
        }
    }
}
