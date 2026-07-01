using FluentAssertions;
using ProjectManager.Tests.TestSupport;
using ProjectManager.Web.Models;
using ProjectManager.Web.Services;

namespace ProjectManager.Tests.Services;

public sealed class ProjectMaintenanceServiceTests
{
    [Fact]
    public async Task ValidateForSaveAsync_rejects_duplicate_project_number_in_same_year()
    {
        var (db, connection) = await TestDbFactory.CreateAsync();
        await using var disposeDb = db;
        await using var disposeConnection = connection;
        var status = new ProjectStatus { Code = "Created", Name = "已立案", SortOrder = 10 };
        db.Projects.Add(new Project
        {
            Year = 2026,
            ProjectNumber = "P-001",
            Name = "Existing",
            Status = status
        });
        await db.SaveChangesAsync();
        var service = new ProjectMaintenanceService(db);

        var errors = await service.ValidateForSaveAsync(
            new Project
            {
                Year = 2026,
                ProjectNumber = "P-001",
                Name = "New",
                StatusId = status.Id
            },
            [],
            existingProjectId: null,
            statusIsClosed: false,
            CancellationToken.None);

        errors.Should().Contain("Project number must be unique within the same year.");
    }

    [Fact]
    public async Task ValidateForSaveAsync_rejects_missing_closed_year_month_for_closed_status()
    {
        var (db, connection) = await TestDbFactory.CreateAsync();
        await using var disposeDb = db;
        await using var disposeConnection = connection;
        var service = new ProjectMaintenanceService(db);

        var errors = await service.ValidateForSaveAsync(
            new Project
            {
                Year = 2026,
                ProjectNumber = "P-002",
                Name = "Closed Missing Date",
                ProjectAmount = 1,
                ProgressPercent = 100,
                CollectionPercent = 100
            },
            [],
            existingProjectId: null,
            statusIsClosed: true,
            CancellationToken.None);

        errors.Should().Contain("Closed year/month is required when project status is closed.");
    }

    [Fact]
    public async Task ValidateForSaveAsync_rejects_invalid_purchase_request_fields()
    {
        var (db, connection) = await TestDbFactory.CreateAsync();
        await using var disposeDb = db;
        await using var disposeConnection = connection;
        var service = new ProjectMaintenanceService(db);

        var errors = await service.ValidateForSaveAsync(
            new Project
            {
                Year = 2026,
                ProjectNumber = "P-003",
                Name = "Purchase Invalid",
                ProjectAmount = 1
            },
            [
                new PurchaseRequest
                {
                    PurchaseType = PurchaseType.InternalPurchase,
                    PurchaseAmount = -1,
                    PaymentPercent = 101,
                    ActualPaidAmount = -1
                }
            ],
            existingProjectId: null,
            statusIsClosed: false,
            CancellationToken.None);

        errors.Should().Contain("Purchase request number is required.");
        errors.Should().Contain("Purchase amount cannot be negative.");
        errors.Should().Contain("Payment percent must be between 0 and 100.");
        errors.Should().Contain("Actual paid amount cannot be negative.");
    }
}
