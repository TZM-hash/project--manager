using FluentAssertions;
using ProjectManager.Web.Models;
using ProjectManager.Web.Services;

namespace ProjectManager.Tests.Services;

public sealed class ProjectAuditChangeBuilderTests
{
    [Fact]
    public void BuildCreateChanges_returns_initial_project_and_purchase_details()
    {
        var snapshot = ProjectAuditChangeBuilder.CreateSnapshot(new Project
        {
            Id = 9,
            ProjectNumber = "P-009",
            Name = "Created Project",
            ProgressPercent = 10,
            ProjectAmount = 5000,
            CollectionPercent = 5,
            PurchaseRequests =
            {
                new PurchaseRequest
                {
                    Id = 11,
                    RequestNumber = "PR-011",
                    PurchaseType = PurchaseType.ExternalPurchase,
                    PurchaseAmount = 1200,
                    PaymentPercent = 25,
                    ActualPaidAmount = 300
                }
            }
        });

        var changes = ProjectAuditChangeBuilder.BuildCreateChanges(snapshot);

        changes.Should().Contain(change =>
            change.Category == "Field" &&
            change.Label == "项目名称" &&
            change.Before == null &&
            change.After == "Created Project");
        changes.Should().Contain(change =>
            change.Category == "PurchaseAdded" &&
            change.Scope == "PR-011" &&
            change.After!.Contains("1,200.00"));
    }

    [Fact]
    public void BuildDeleteChanges_returns_project_delete_detail()
    {
        var snapshot = ProjectAuditChangeBuilder.CreateSnapshot(new Project
        {
            Id = 10,
            ProjectNumber = "P-010",
            Name = "Deleted Project",
            ProgressPercent = 80,
            ProjectAmount = 9000
        });

        var changes = ProjectAuditChangeBuilder.BuildDeleteChanges(snapshot);

        changes.Should().ContainSingle(change =>
            change.Category == "ProjectDeleted" &&
            change.Label == "删除项目" &&
            change.Before!.Contains("P-010") &&
            change.After == null);
    }

    [Fact]
    public void BuildUpdateChanges_returns_project_field_differences()
    {
        var before = ProjectAuditChangeBuilder.CreateSnapshot(new Project
        {
            Id = 7,
            ProjectNumber = "P-007",
            Name = "Old name",
            ProgressPercent = 30,
            CollectionPercent = 10,
            ProjectAmount = 1000,
            ProgressDescription = "old"
        });
        var after = ProjectAuditChangeBuilder.CreateSnapshot(new Project
        {
            Id = 7,
            ProjectNumber = "P-007",
            Name = "New name",
            ProgressPercent = 45,
            CollectionPercent = 10,
            ProjectAmount = 1000,
            ProgressDescription = "new"
        });

        var changes = ProjectAuditChangeBuilder.BuildUpdateChanges(before, after);

        changes.Should().Contain(change =>
            change.Category == "Field" &&
            change.Label == "项目名称" &&
            change.Before == "Old name" &&
            change.After == "New name");
        changes.Should().Contain(change =>
            change.Category == "Field" &&
            change.Label == "项目进度" &&
            change.Before == "30%" &&
            change.After == "45%");
        changes.Should().Contain(change =>
            change.Category == "Field" &&
            change.Label == "进度说明" &&
            change.Before == "old" &&
            change.After == "new");
    }

    [Fact]
    public void BuildUpdateChanges_returns_purchase_add_update_and_delete_details()
    {
        var before = ProjectAuditChangeBuilder.CreateSnapshot(new Project
        {
            Id = 8,
            ProjectNumber = "P-008",
            Name = "Audit Project",
            PurchaseRequests =
            {
                new PurchaseRequest
                {
                    Id = 1,
                    RequestNumber = "PR-001",
                    PurchaseType = PurchaseType.InternalPurchase,
                    PurchaseAmount = 1000,
                    PaymentPercent = 20,
                    ActualPaidAmount = 100,
                    Notes = "old"
                },
                new PurchaseRequest
                {
                    Id = 2,
                    RequestNumber = "PR-002",
                    PurchaseType = PurchaseType.ExternalPurchase,
                    PurchaseAmount = 2000,
                    PaymentPercent = 30,
                    ActualPaidAmount = 200,
                    Notes = "remove"
                }
            }
        });
        var after = ProjectAuditChangeBuilder.CreateSnapshot(new Project
        {
            Id = 8,
            ProjectNumber = "P-008",
            Name = "Audit Project",
            PurchaseRequests =
            {
                new PurchaseRequest
                {
                    Id = 1,
                    RequestNumber = "PR-001",
                    PurchaseType = PurchaseType.InternalPurchase,
                    PurchaseAmount = 1500,
                    PaymentPercent = 50,
                    ActualPaidAmount = 300,
                    Notes = "new"
                },
                new PurchaseRequest
                {
                    Id = 3,
                    RequestNumber = "PR-003",
                    PurchaseType = PurchaseType.ExternalPurchase,
                    PurchaseAmount = 3500,
                    PaymentPercent = 10,
                    ActualPaidAmount = 0,
                    Notes = "added"
                }
            }
        });

        var changes = ProjectAuditChangeBuilder.BuildUpdateChanges(before, after);

        changes.Should().Contain(change =>
            change.Category == "PurchaseUpdated" &&
            change.Scope == "PR-001" &&
            change.Label == "请购金额" &&
            change.Before == "1,000.00" &&
            change.After == "1,500.00");
        changes.Should().Contain(change =>
            change.Category == "PurchaseUpdated" &&
            change.Scope == "PR-001" &&
            change.Label == "实际已付款" &&
            change.Before == "100.00" &&
            change.After == "300.00");
        changes.Should().Contain(change =>
            change.Category == "PurchaseDeleted" &&
            change.Scope == "PR-002" &&
            change.Label == "删除请购" &&
            change.Before!.Contains("2,000.00") &&
            change.After == null);
        changes.Should().Contain(change =>
            change.Category == "PurchaseAdded" &&
            change.Scope == "PR-003" &&
            change.Label == "新增请购" &&
            change.Before == null &&
            change.After!.Contains("3,500.00"));
    }
}
