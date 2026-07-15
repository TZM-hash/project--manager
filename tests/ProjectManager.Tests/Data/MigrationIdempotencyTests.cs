using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using ProjectManager.Web.Migrations;

namespace ProjectManager.Tests.Data;

public sealed class MigrationIdempotencyTests
{
    [Fact]
    public void Maintenance_template_expansion_uses_idempotent_sql_for_existing_columns()
    {
        var migration = new ExpandMaintenanceOrderTemplateFields();

        migration.UpOperations
            .OfType<AddColumnOperation>()
            .Where(operation => operation.Table == "MaintenanceOrders")
            .Select(operation => operation.Name)
            .Should()
            .BeEmpty();

        var sqlOperations = migration.UpOperations
            .OfType<SqlOperation>()
            .Select(operation => operation.Sql)
            .ToList();

        foreach (var columnName in new[]
        {
            "ContractNumber",
            "MaintenanceDescription",
            "OnSiteHardwareFrequency",
            "OnSiteSoftwareFrequency",
            "ProgressPercent",
            "SiteName"
        })
        {
            sqlOperations.Should().Contain(sql =>
                sql.Contains($"COL_LENGTH(N'dbo.MaintenanceOrders', N'{columnName}') IS NULL"));
        }
    }
}
