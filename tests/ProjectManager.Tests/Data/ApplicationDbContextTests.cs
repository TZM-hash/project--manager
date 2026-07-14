using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ProjectManager.Tests.TestSupport;
using ProjectManager.Web.Models;

namespace ProjectManager.Tests.Data;

public sealed class ApplicationDbContextTests
{
    [Fact]
    public async Task Saved_data_view_has_unique_user_page_name_key_and_cascades_with_user()
    {
        var (db, connection) = await TestDbFactory.CreateAsync();
        await using var disposeDb = db;
        await using var disposeConnection = connection;

        var entity = db.Model.FindEntityType(typeof(SavedDataView));
        entity.Should().NotBeNull();
        entity!.GetIndexes()
            .Single(x => x.IsUnique)
            .Properties.Select(x => x.Name)
            .Should().Equal(
                nameof(SavedDataView.UserId),
                nameof(SavedDataView.PageKey),
                nameof(SavedDataView.Name));

        var foreignKey = entity.GetForeignKeys().Single(x => x.PrincipalEntityType.ClrType == typeof(ApplicationUser));
        foreignKey.DeleteBehavior.Should().Be(DeleteBehavior.Cascade);
    }

    [Fact]
    public async Task Can_persist_project_with_status_assignment_and_purchase_request()
    {
        var (db, connection) = await TestDbFactory.CreateAsync();
        await using var disposeDb = db;
        await using var disposeConnection = connection;

        var status = new ProjectStatus
        {
            Code = "Created",
            Name = "已立案",
            SortOrder = 10
        };
        var user = new ApplicationUser
        {
            Id = "user-1",
            UserName = "staff",
            DisplayName = "Staff User"
        };
        var project = new Project
        {
            Year = 2026,
            ParentCaseNumber = "M-001",
            ProjectNumber = "P-001",
            Name = "Demo Project",
            Status = status,
            ProjectAmount = 10000,
            ProgressPercent = 25,
            CollectionPercent = 10,
            UpdatedByUser = user,
            Assignments =
            {
                new ProjectAssignment
                {
                    User = user,
                    RoleInProject = "Owner"
                }
            },
            PurchaseRequests =
            {
                new PurchaseRequest
                {
                    RequestNumber = "PR-001",
                    PurchaseType = PurchaseType.InternalPurchase,
                    PurchaseStaff = user,
                    SubCaseContact = user,
                    PurchaseAmount = 1500,
                    PaymentPercent = 50,
                    ActualPaidAmount = 750
                }
            }
        };

        db.Projects.Add(project);
        await db.SaveChangesAsync();

        var saved = await db.Projects
            .Include(x => x.Status)
            .Include(x => x.Assignments)
            .Include(x => x.PurchaseRequests)
            .SingleAsync();

        saved.ParentCaseNumber.Should().Be("M-001");
        saved.Status!.Name.Should().Be("已立案");
        saved.Assignments.Should().ContainSingle();
        saved.PurchaseRequests.Should().ContainSingle(x => x.SubCaseContactUserId == "user-1");
    }
}
