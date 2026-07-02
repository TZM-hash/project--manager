using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectManager.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddMaintenanceOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MaintenanceOrders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Year = table.Column<int>(type: "int", nullable: false),
                    CustomerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    MaintenanceStartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    MaintenanceEndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    MaintenanceMethod = table.Column<int>(type: "int", nullable: false),
                    OnSiteAnnualCount = table.Column<int>(type: "int", nullable: false),
                    RemoteAnnualCount = table.Column<int>(type: "int", nullable: false),
                    ExecutorUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    HandoverPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaintenanceOrders_AspNetUsers_ExecutorUserId",
                        column: x => x.ExecutorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenanceOrders_AspNetUsers_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceOrders_ExecutorUserId",
                table: "MaintenanceOrders",
                column: "ExecutorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceOrders_UpdatedByUserId",
                table: "MaintenanceOrders",
                column: "UpdatedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MaintenanceOrders");
        }
    }
}
