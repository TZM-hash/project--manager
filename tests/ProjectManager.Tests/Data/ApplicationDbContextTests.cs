using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ProjectManager.Tests.TestSupport;
using ProjectManager.Web.Models;

namespace ProjectManager.Tests.Data;

public sealed class ApplicationDbContextTests
{
    [Fact]
    public async Task Operation_job_maps_status_owner_indexes_and_row_version()
    {
        var (db, connection) = await TestDbFactory.CreateAsync();
        await using var disposeDb = db;
        await using var disposeConnection = connection;

        var entity = db.Model.FindEntityType(typeof(OperationJob));
        entity.Should().NotBeNull();
        entity!.FindProperty(nameof(OperationJob.Status)).Should().NotBeNull();
        entity.FindProperty(nameof(OperationJob.Type)).Should().NotBeNull();
        var rowVersion = entity.FindProperty(nameof(OperationJob.RowVersion));
        rowVersion!.IsConcurrencyToken.Should().BeTrue();
        rowVersion.ValueGenerated.Should().Be(Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.OnAddOrUpdate);
        entity.GetIndexes().Should().Contain(index =>
            index.Properties.Select(property => property.Name).SequenceEqual(
                new[] { nameof(OperationJob.Status), nameof(OperationJob.CreatedAt) }));
        entity.GetIndexes().Should().Contain(index =>
            index.Properties.Select(property => property.Name).SequenceEqual(
                new[] { nameof(OperationJob.RequestedByUserId), nameof(OperationJob.CreatedAt) }));
        entity.GetForeignKeys().Should().Contain(foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(ApplicationUser));
    }

    [Fact]
    public async Task Project_gantt_and_collaboration_models_define_concurrency_and_relationships()
    {
        var (db, connection) = await TestDbFactory.CreateAsync();
        await using var disposeDb = db;
        await using var disposeConnection = connection;

        foreach (var entityType in new[] { typeof(Project), typeof(ProjectGanttPlan), typeof(ProjectCollaborationRecord) })
        {
            var rowVersion = db.Model.FindEntityType(entityType)?.FindProperty("RowVersion");
            rowVersion.Should().NotBeNull($"{entityType.Name} must protect concurrent edits");
            rowVersion!.IsConcurrencyToken.Should().BeTrue();
            rowVersion.ValueGenerated.Should().Be(Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.OnAddOrUpdate);
        }

        var ganttTask = db.Model.FindEntityType(typeof(ProjectGanttTask));
        ganttTask!.FindProperty(nameof(ProjectGanttTask.IsMilestone)).Should().NotBeNull();
        ganttTask.FindProperty(nameof(ProjectGanttTask.OwnerUserId)).Should().NotBeNull();
        ganttTask.FindProperty(nameof(ProjectGanttTask.PredecessorTaskId)).Should().NotBeNull();
        ganttTask.FindProperty(nameof(ProjectGanttTask.ActualStartDate)).Should().NotBeNull();
        ganttTask.FindProperty(nameof(ProjectGanttTask.ActualFinishDate)).Should().NotBeNull();

        var collaboration = db.Model.FindEntityType(typeof(ProjectCollaborationRecord));
        collaboration.Should().NotBeNull();
        collaboration!.GetIndexes().Should().Contain(index =>
            index.Properties.Select(property => property.Name).SequenceEqual(
                new[] { nameof(ProjectCollaborationRecord.ProjectId), nameof(ProjectCollaborationRecord.CreatedAt) }));
        collaboration.GetForeignKeys().Should().Contain(foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(Project));
        collaboration.GetForeignKeys().Should().Contain(foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(ApplicationUser));
    }

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
