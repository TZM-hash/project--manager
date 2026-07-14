using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectManager.Web.Migrations
{
    /// <inheritdoc />
    public partial class ExpandMaintenanceOrderTemplateFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContractNumber",
                table: "MaintenanceOrders",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MaintenanceDescription",
                table: "MaintenanceOrders",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OnSiteHardwareFrequency",
                table: "MaintenanceOrders",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OnSiteSoftwareFrequency",
                table: "MaintenanceOrders",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "ProgressPercent",
                table: "MaintenanceOrders",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "SiteName",
                table: "MaintenanceOrders",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                """
                UPDATE dbo.MaintenanceOrders
                SET ContractNumber = CONCAT('MO-', [Year], '-', RIGHT(CONCAT('0000', Id), 4)),
                    SiteName = N'主厂区',
                    OnSiteSoftwareFrequency = CASE
                        WHEN OnSiteAnnualCount <= 0 THEN N'无'
                        WHEN OnSiteAnnualCount >= 12 THEN N'每月/次'
                        WHEN OnSiteAnnualCount >= 6 THEN N'两月/次'
                        WHEN OnSiteAnnualCount >= 4 THEN N'季度/次'
                        WHEN OnSiteAnnualCount >= 2 THEN N'半年/次'
                        ELSE N'一年/次'
                    END,
                    OnSiteHardwareFrequency = CASE
                        WHEN OnSiteAnnualCount <= 0 THEN N'无'
                        WHEN OnSiteAnnualCount >= 4 THEN N'半年/次'
                        ELSE N'一年/次'
                    END,
                    ProgressPercent = HandoverPercent,
                    MaintenanceDescription = CASE MaintenanceMethod
                        WHEN 1 THEN N'现场软件与硬件巡检、预防保养及异常处理。'
                        WHEN 2 THEN N'远程监控、问题诊断、软件支持与运行建议。'
                        ELSE N'远程支持结合现场软硬件巡检、预防保养及异常处理。'
                    END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContractNumber",
                table: "MaintenanceOrders");

            migrationBuilder.DropColumn(
                name: "MaintenanceDescription",
                table: "MaintenanceOrders");

            migrationBuilder.DropColumn(
                name: "OnSiteHardwareFrequency",
                table: "MaintenanceOrders");

            migrationBuilder.DropColumn(
                name: "OnSiteSoftwareFrequency",
                table: "MaintenanceOrders");

            migrationBuilder.DropColumn(
                name: "ProgressPercent",
                table: "MaintenanceOrders");

            migrationBuilder.DropColumn(
                name: "SiteName",
                table: "MaintenanceOrders");
        }
    }
}
