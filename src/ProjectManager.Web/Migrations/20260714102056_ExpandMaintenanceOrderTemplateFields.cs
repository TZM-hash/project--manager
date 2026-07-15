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
            migrationBuilder.Sql(
                """
                IF COL_LENGTH(N'dbo.MaintenanceOrders', N'ContractNumber') IS NULL
                    ALTER TABLE [dbo].[MaintenanceOrders] ADD [ContractNumber] nvarchar(50) NOT NULL DEFAULT N'';

                IF COL_LENGTH(N'dbo.MaintenanceOrders', N'MaintenanceDescription') IS NULL
                    ALTER TABLE [dbo].[MaintenanceOrders] ADD [MaintenanceDescription] nvarchar(1000) NOT NULL DEFAULT N'';

                IF COL_LENGTH(N'dbo.MaintenanceOrders', N'OnSiteHardwareFrequency') IS NULL
                    ALTER TABLE [dbo].[MaintenanceOrders] ADD [OnSiteHardwareFrequency] nvarchar(50) NOT NULL DEFAULT N'';

                IF COL_LENGTH(N'dbo.MaintenanceOrders', N'OnSiteSoftwareFrequency') IS NULL
                    ALTER TABLE [dbo].[MaintenanceOrders] ADD [OnSiteSoftwareFrequency] nvarchar(50) NOT NULL DEFAULT N'';

                IF COL_LENGTH(N'dbo.MaintenanceOrders', N'ProgressPercent') IS NULL
                    ALTER TABLE [dbo].[MaintenanceOrders] ADD [ProgressPercent] decimal(5,2) NOT NULL DEFAULT 0;

                IF COL_LENGTH(N'dbo.MaintenanceOrders', N'SiteName') IS NULL
                    ALTER TABLE [dbo].[MaintenanceOrders] ADD [SiteName] nvarchar(100) NOT NULL DEFAULT N'';
                """);

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
