using ClosedXML.Excel;
using FluentAssertions;
using ProjectManager.Tests.TestSupport;
using ProjectManager.Web.Models;
using ProjectManager.Web.Services;

namespace ProjectManager.Tests.Services;

public sealed class ExcelReportServiceTests
{
    private static readonly string[] SettlementHeaders =
    [
        "年",
        "月",
        "批次號",
        "母案案號",
        "專案工號",
        "專案名稱",
        "專案人員",
        "專案進度百分比",
        "專案金額",
        "收款比例",
        "狀態",
        "結案日期",
        "請購號彙總",
        "請購金額合計",
        "子案對接人員彙總",
        "付款比例彙總",
        "實際已付款合計",
        "進度說明",
        "更新人員",
        "來源更新時間"
    ];

    private static readonly string[] OpenProjectHeaders =
    [
        "年",
        "母案案號",
        "專案工號",
        "專案名稱",
        "專案人員",
        "專案進度百分比",
        "專案金額",
        "收款比例",
        "狀態",
        "結案日期",
        "請購金額合計",
        "子案對接人員彙總",
        "實際已付款合計",
        "進度說明",
        "更新人員",
        "最後更新時間"
    ];

    [Fact]
    public async Task ExportSettlementAsync_writes_expected_headers_and_rows()
    {
        var (db, connection) = await TestDbFactory.CreateAsync();
        await using var disposeDb = db;
        await using var disposeConnection = connection;
        var batch = new MonthlySettlementBatch
        {
            Year = 2026,
            Month = 7,
            BatchNumber = 1,
            CreatedByUserId = "admin-1",
            Items =
            {
                new MonthlySettlementItem
                {
                    ProjectId = 10,
                    ParentCaseNumber = "M-001",
                    ProjectNumber = "P-001",
                    ProjectName = "Settlement Project",
                    ProjectPersonnelText = "Staff User",
                    ProgressPercent = 100,
                    ProjectAmount = 10000,
                    CollectionPercent = 80,
                    StatusName = "已结案",
                    IsClosed = true,
                    ClosedYearMonth = new DateOnly(2026, 7, 1),
                    PurchaseRequestSummary = "PR-001",
                    PurchaseAmountTotal = 3000,
                    SubCaseContactSummary = "Contact User",
                    PaymentPercentSummary = "50%",
                    ActualPaidAmountTotal = 1500,
                    ProgressDescription = "Ready",
                    UpdatedByUserName = "Staff User",
                    SourceUpdatedAt = new DateTimeOffset(2026, 7, 10, 8, 30, 0, TimeSpan.Zero)
                }
            }
        };
        db.Users.Add(new ApplicationUser { Id = "admin-1", UserName = "admin", DisplayName = "Admin User" });
        db.MonthlySettlementBatches.Add(batch);
        await db.SaveChangesAsync();
        var service = new ExcelReportService(db, new ProjectQueryService(db));

        var export = await service.ExportSettlementAsync(batch.Id, CancellationToken.None);

        export.ContentType.Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        using var workbook = LoadWorkbook(export);
        var sheet = workbook.Worksheet(1);
        ReadHeaders(sheet, SettlementHeaders.Length).Should().Equal(SettlementHeaders);
        sheet.Cell(2, 4).GetString().Should().Be("M-001");
        sheet.Cell(2, 5).GetString().Should().Be("P-001");
        sheet.Cell(2, 15).GetString().Should().Be("Contact User");
    }

    [Fact]
    public async Task ExportOpenProjectsAsync_writes_expected_headers_and_open_project_rows()
    {
        var (db, connection) = await TestDbFactory.CreateAsync();
        await using var disposeDb = db;
        await using var disposeConnection = connection;
        await SeedOpenProjectAsync(db);
        var service = new ExcelReportService(db, new ProjectQueryService(db));

        var export = await service.ExportOpenProjectsAsync(
            new ProjectFilter(2026, null, null, null, null, null, OpenOnly: true),
            CancellationToken.None);

        export.FileName.Should().Contain("open-projects");
        using var workbook = LoadWorkbook(export);
        var sheet = workbook.Worksheet(1);
        ReadHeaders(sheet, OpenProjectHeaders.Length).Should().Equal(OpenProjectHeaders);
        sheet.Cell(2, 2).GetString().Should().Be("M-001");
        sheet.Cell(2, 3).GetString().Should().Be("P-OPEN");
        sheet.Cell(2, 12).GetString().Should().Be("Contact User");
    }

    private static XLWorkbook LoadWorkbook(ExportFile export)
    {
        return new XLWorkbook(new MemoryStream(export.Contents));
    }

    private static string[] ReadHeaders(IXLWorksheet sheet, int count)
    {
        return Enumerable.Range(1, count)
            .Select(index => sheet.Cell(1, index).GetString())
            .ToArray();
    }

    private static async Task SeedOpenProjectAsync(ProjectManager.Web.Data.ApplicationDbContext db)
    {
        var staff = new ApplicationUser
        {
            Id = "staff-1",
            UserName = "staff",
            DisplayName = "Staff User"
        };
        var contact = new ApplicationUser
        {
            Id = "contact-1",
            UserName = "contact",
            DisplayName = "Contact User"
        };
        var status = new ProjectStatus
        {
            Code = "Created",
            Name = "已立案",
            SortOrder = 10,
            IsClosed = false
        };

        db.Projects.Add(new Project
        {
            Year = 2026,
            ParentCaseNumber = "M-001",
            ProjectNumber = "P-OPEN",
            Name = "Open Project",
            Status = status,
            ProjectAmount = 10000,
            ProgressPercent = 30,
            CollectionPercent = 20,
            ProgressDescription = "Open progress",
            UpdatedByUser = staff,
            UpdatedAt = new DateTimeOffset(2026, 7, 10, 8, 30, 0, TimeSpan.Zero),
            Assignments =
            {
                new ProjectAssignment
                {
                    User = staff,
                    RoleInProject = "Owner"
                }
            },
            PurchaseRequests =
            {
                new PurchaseRequest
                {
                    RequestNumber = "PR-001",
                    PurchaseType = PurchaseType.InternalPurchase,
                    PurchaseStaff = staff,
                    SubCaseContact = contact,
                    PurchaseAmount = 1500,
                    PaymentPercent = 50,
                    ActualPaidAmount = 750
                }
            }
        });
        await db.SaveChangesAsync();
    }
}
