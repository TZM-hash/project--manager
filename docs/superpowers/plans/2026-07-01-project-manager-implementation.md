# Project Manager Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the first usable intranet ASP.NET Core project management system described in `docs/superpowers/specs/2026-07-01-project-manager-design.zh-CN.md`.

**Architecture:** Use one ASP.NET Core Razor Pages application with two logical entrances: admin pages and web workbench pages. Keep business rules in focused domain/services classes, persist with EF Core SQL Server, and verify core behavior with xUnit tests before UI wiring.

**Tech Stack:** C#, ASP.NET Core Razor Pages, ASP.NET Core Identity, EF Core SQL Server, SQL Server, ClosedXML, xUnit, EF Core SQLite provider for fast service tests.

---

## Scope Check

The specification contains authentication, project maintenance, purchase records, monthly settlement, reporting, Excel export, print views, status chart, and style settings. These features share the same domain model and permissions, so this is one integrated implementation plan instead of separate subsystem plans. The tasks below still produce incremental, testable software with frequent commits.

The current machine reports only .NET SDK `3.0.101`, which is too old for a new internal system. Task 1 installs a project-local .NET SDK under `.dotnet` and uses `scripts/dotnet.ps1`, so implementation does not change the global system SDK.

## File Structure

Create or modify these paths:

- `scripts/dotnet.ps1`: wrapper for the project-local .NET SDK.
- `ProjectManager.sln`: solution file.
- `Directory.Build.props`: shared C# build settings.
- `src/ProjectManager.Web/ProjectManager.Web.csproj`: Razor Pages application.
- `src/ProjectManager.Web/Program.cs`: middleware, Identity, EF Core, services, seed startup.
- `src/ProjectManager.Web/appsettings.json`: SQL Server connection string and admin seed settings.
- `src/ProjectManager.Web/Data/ApplicationDbContext.cs`: EF Core DbContext and model configuration.
- `src/ProjectManager.Web/Data/SeedData.cs`: role, admin user, status, and style seed data.
- `src/ProjectManager.Web/Models/ApplicationUser.cs`: Identity user extension.
- `src/ProjectManager.Web/Models/Project.cs`: project master record.
- `src/ProjectManager.Web/Models/ProjectAssignment.cs`: project personnel assignment.
- `src/ProjectManager.Web/Models/ProjectStatus.cs`: configurable project status.
- `src/ProjectManager.Web/Models/ProjectStatusStyle.cs`: status color and bold settings.
- `src/ProjectManager.Web/Models/PurchaseRequest.cs`: child purchase request records.
- `src/ProjectManager.Web/Models/MonthlySettlementBatch.cs`: settlement batch header.
- `src/ProjectManager.Web/Models/MonthlySettlementItem.cs`: settlement snapshot row.
- `src/ProjectManager.Web/Models/AuditLog.cs`: simple operation log.
- `src/ProjectManager.Web/Security/RoleNames.cs`: role constants.
- `src/ProjectManager.Web/Services/ProjectRules.cs`: validation and closed-date rules.
- `src/ProjectManager.Web/Services/ProjectQueryService.cs`: filtered project queries and report summaries.
- `src/ProjectManager.Web/Services/SettlementService.cs`: monthly settlement creation and snapshot logic.
- `src/ProjectManager.Web/Services/ExcelReportService.cs`: ClosedXML export logic.
- `src/ProjectManager.Web/Services/AuditLogService.cs`: audit log writer.
- `src/ProjectManager.Web/Pages/Admin/Users/`: admin user management pages.
- `src/ProjectManager.Web/Pages/Admin/Projects/`: admin project and purchase request pages.
- `src/ProjectManager.Web/Pages/Admin/Statuses/`: status and status style settings pages.
- `src/ProjectManager.Web/Pages/Workbench/Projects/`: project staff and leader workbench pages.
- `src/ProjectManager.Web/Pages/Settlements/`: settlement creation, detail, export, and print pages.
- `src/ProjectManager.Web/Pages/Reports/OpenProjects/`: open-project report, statistics, export, and print pages.
- `src/ProjectManager.Web/Pages/Shared/_StatusBadge.cshtml`: reusable status badge.
- `src/ProjectManager.Web/Pages/Shared/_StatusTimeline.cshtml`: reusable dynamic status chart.
- `src/ProjectManager.Web/wwwroot/css/site.css`: compact intranet UI and print CSS.
- `tests/ProjectManager.Tests/ProjectManager.Tests.csproj`: xUnit test project.
- `tests/ProjectManager.Tests/TestSupport/TestDbFactory.cs`: SQLite-backed DbContext test helper.
- `tests/ProjectManager.Tests/Domain/ProjectRulesTests.cs`: validation tests.
- `tests/ProjectManager.Tests/Services/SettlementServiceTests.cs`: monthly settlement tests.
- `tests/ProjectManager.Tests/Services/ExcelReportServiceTests.cs`: Excel header and row tests.
- `tests/ProjectManager.Tests/Services/ProjectQueryServiceTests.cs`: open project and summary tests.
- `tests/ProjectManager.Tests/Web/AuthSmokeTests.cs`: authorization smoke tests.
- `README.md`: local run, SQL Server connection, default admin instructions.

## Task 1: Local SDK, Solution, and Project Scaffold

**Files:**
- Create: `scripts/dotnet.ps1`
- Create: `Directory.Build.props`
- Create: `ProjectManager.sln`
- Create: `src/ProjectManager.Web/ProjectManager.Web.csproj`
- Create: `tests/ProjectManager.Tests/ProjectManager.Tests.csproj`

- [ ] **Step 1: Install project-local .NET SDK**

Run in PowerShell from `D:\AI\project- manager`:

```powershell
Invoke-WebRequest -Uri https://dot.net/v1/dotnet-install.ps1 -OutFile .dotnet-install.ps1
powershell -ExecutionPolicy Bypass -File .\.dotnet-install.ps1 -Channel 10.0 -InstallDir .\.dotnet
.\.dotnet\dotnet.exe --info
```

Expected: SDK output shows a `10.0.x` SDK under `D:\AI\project- manager\.dotnet`.

- [ ] **Step 2: Create the SDK wrapper**

Create `scripts/dotnet.ps1`:

```powershell
$root = Split-Path -Parent $PSScriptRoot
$dotnet = Join-Path $root ".dotnet\dotnet.exe"
& $dotnet @args
exit $LASTEXITCODE
```

Run:

```powershell
.\scripts\dotnet.ps1 --version
```

Expected: prints a `10.0.x` SDK version.

- [ ] **Step 3: Scaffold solution and projects**

Run:

```powershell
.\scripts\dotnet.ps1 new sln -n ProjectManager
.\scripts\dotnet.ps1 new webapp --auth Individual -f net10.0 -o src/ProjectManager.Web
.\scripts\dotnet.ps1 new xunit -f net10.0 -o tests/ProjectManager.Tests
.\scripts\dotnet.ps1 sln ProjectManager.sln add src/ProjectManager.Web/ProjectManager.Web.csproj
.\scripts\dotnet.ps1 sln ProjectManager.sln add tests/ProjectManager.Tests/ProjectManager.Tests.csproj
.\scripts\dotnet.ps1 add tests/ProjectManager.Tests/ProjectManager.Tests.csproj reference src/ProjectManager.Web/ProjectManager.Web.csproj
```

Expected: solution contains both projects.

- [ ] **Step 4: Add required packages**

Run:

```powershell
.\scripts\dotnet.ps1 add src/ProjectManager.Web/ProjectManager.Web.csproj package Microsoft.EntityFrameworkCore.SqlServer
.\scripts\dotnet.ps1 add src/ProjectManager.Web/ProjectManager.Web.csproj package Microsoft.EntityFrameworkCore.Design
.\scripts\dotnet.ps1 add src/ProjectManager.Web/ProjectManager.Web.csproj package ClosedXML
.\scripts\dotnet.ps1 add tests/ProjectManager.Tests/ProjectManager.Tests.csproj package Microsoft.EntityFrameworkCore.Sqlite
.\scripts\dotnet.ps1 add tests/ProjectManager.Tests/ProjectManager.Tests.csproj package Microsoft.AspNetCore.Mvc.Testing
.\scripts\dotnet.ps1 add tests/ProjectManager.Tests/ProjectManager.Tests.csproj package FluentAssertions
```

Expected: restore completes with no package errors.

- [ ] **Step 5: Add shared build settings**

Create `Directory.Build.props`:

```xml
<Project>
  <PropertyGroup>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
</Project>
```

- [ ] **Step 6: Build and commit scaffold**

Run:

```powershell
.\scripts\dotnet.ps1 build ProjectManager.sln
git add scripts/dotnet.ps1 Directory.Build.props ProjectManager.sln src tests .gitignore
git commit -m "chore: scaffold ASP.NET project manager solution"
```

Expected: build succeeds and commit is created.

## Task 2: Domain Models, Roles, and Validation Rules

**Files:**
- Create: `src/ProjectManager.Web/Models/ApplicationUser.cs`
- Create: `src/ProjectManager.Web/Models/Project.cs`
- Create: `src/ProjectManager.Web/Models/ProjectAssignment.cs`
- Create: `src/ProjectManager.Web/Models/ProjectStatus.cs`
- Create: `src/ProjectManager.Web/Models/ProjectStatusStyle.cs`
- Create: `src/ProjectManager.Web/Models/PurchaseRequest.cs`
- Create: `src/ProjectManager.Web/Models/MonthlySettlementBatch.cs`
- Create: `src/ProjectManager.Web/Models/MonthlySettlementItem.cs`
- Create: `src/ProjectManager.Web/Models/AuditLog.cs`
- Create: `src/ProjectManager.Web/Security/RoleNames.cs`
- Create: `src/ProjectManager.Web/Services/ProjectRules.cs`
- Create: `tests/ProjectManager.Tests/Domain/ProjectRulesTests.cs`

- [ ] **Step 1: Write failing validation tests**

Create `tests/ProjectManager.Tests/Domain/ProjectRulesTests.cs`:

```csharp
using FluentAssertions;
using ProjectManager.Web.Models;
using ProjectManager.Web.Services;

namespace ProjectManager.Tests.Domain;

public sealed class ProjectRulesTests
{
    [Fact]
    public void ValidateProject_rejects_negative_amounts_and_invalid_percentages()
    {
        var project = new Project
        {
            Year = 2026,
            ProjectNumber = "P-001",
            Name = "Test Project",
            ProjectAmount = -1,
            ProgressPercent = 101,
            CollectionPercent = -1
        };

        var errors = ProjectRules.ValidateProject(project, statusIsClosed: false);

        errors.Should().Contain("Project amount cannot be negative.");
        errors.Should().Contain("Progress percent must be between 0 and 100.");
        errors.Should().Contain("Collection percent must be between 0 and 100.");
    }

    [Fact]
    public void ValidateProject_requires_closed_year_month_when_status_is_closed()
    {
        var project = new Project
        {
            Year = 2026,
            ProjectNumber = "P-002",
            Name = "Closed Project",
            ProjectAmount = 1000,
            ProgressPercent = 100,
            CollectionPercent = 100
        };

        var errors = ProjectRules.ValidateProject(project, statusIsClosed: true);

        errors.Should().Contain("Closed year/month is required when project status is closed.");
    }

    [Fact]
    public void NormalizeClosedYearMonth_sets_day_to_first_day_of_month()
    {
        var normalized = ProjectRules.NormalizeClosedYearMonth(new DateOnly(2026, 7, 18));

        normalized.Should().Be(new DateOnly(2026, 7, 1));
    }
}
```

Run:

```powershell
.\scripts\dotnet.ps1 test tests/ProjectManager.Tests/ProjectManager.Tests.csproj --filter ProjectRulesTests
```

Expected: tests fail because `Project`, `ProjectRules`, and related types do not exist yet.

- [ ] **Step 2: Add role constants**

Create `src/ProjectManager.Web/Security/RoleNames.cs`:

```csharp
namespace ProjectManager.Web.Security;

public static class RoleNames
{
    public const string Administrator = "Administrator";
    public const string ProjectStaff = "ProjectStaff";
    public const string Leader = "Leader";
    public const string Viewer = "Viewer";

    public static readonly string[] All =
    [
        Administrator,
        ProjectStaff,
        Leader,
        Viewer
    ];
}
```

- [ ] **Step 3: Add domain model classes**

Create the model files with these required properties:

```csharp
namespace ProjectManager.Web.Models;

public enum PurchaseType
{
    InternalPurchase = 1,
    ExternalPurchase = 2
}
```

`Project` must include `Year`, `ParentCaseNumber`, `ProjectNumber`, `Name`, `ProgressPercent`, `ProjectAmount`, `CollectionPercent`, `ProgressDescription`, `StatusId`, `UpdatedByUserId`, `ClosedYearMonth`, `UpdatedAt`, `CreatedAt`, `IsDeleted`, assignments, and purchase requests.

`PurchaseRequest` must include `RequestNumber`, `PurchaseType`, `PurchaseStaffUserId`, `PurchaseAmount`, `SubCaseContactUserId`, `PaymentPercent`, `ActualPaidAmount`, `Notes`, `CreatedAt`, and `UpdatedAt`.

`MonthlySettlementItem` must include snapshot fields for `ParentCaseNumber`, `ClosedYearMonth`, and `SubCaseContactSummary`.

- [ ] **Step 4: Add validation rules**

Create `src/ProjectManager.Web/Services/ProjectRules.cs`:

```csharp
using ProjectManager.Web.Models;

namespace ProjectManager.Web.Services;

public static class ProjectRules
{
    public static IReadOnlyList<string> ValidateProject(Project project, bool statusIsClosed)
    {
        var errors = new List<string>();

        if (project.Year < 2000 || project.Year > 2100)
        {
            errors.Add("Year must be between 2000 and 2100.");
        }

        if (string.IsNullOrWhiteSpace(project.ProjectNumber))
        {
            errors.Add("Project number is required.");
        }

        if (string.IsNullOrWhiteSpace(project.Name))
        {
            errors.Add("Project name is required.");
        }

        if (project.ProjectAmount < 0)
        {
            errors.Add("Project amount cannot be negative.");
        }

        ValidatePercent(project.ProgressPercent, "Progress percent", errors);
        ValidatePercent(project.CollectionPercent, "Collection percent", errors);

        if (statusIsClosed && project.ClosedYearMonth is null)
        {
            errors.Add("Closed year/month is required when project status is closed.");
        }

        return errors;
    }

    public static IReadOnlyList<string> ValidatePurchaseRequest(PurchaseRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.RequestNumber))
        {
            errors.Add("Purchase request number is required.");
        }

        if (request.PurchaseAmount < 0)
        {
            errors.Add("Purchase amount cannot be negative.");
        }

        if (request.ActualPaidAmount < 0)
        {
            errors.Add("Actual paid amount cannot be negative.");
        }

        ValidatePercent(request.PaymentPercent, "Payment percent", errors);
        return errors;
    }

    public static DateOnly? NormalizeClosedYearMonth(DateOnly? value)
    {
        return value is null ? null : new DateOnly(value.Value.Year, value.Value.Month, 1);
    }

    private static void ValidatePercent(decimal value, string label, List<string> errors)
    {
        if (value < 0 || value > 100)
        {
            errors.Add($"{label} must be between 0 and 100.");
        }
    }
}
```

- [ ] **Step 5: Run tests and commit**

Run:

```powershell
.\scripts\dotnet.ps1 test tests/ProjectManager.Tests/ProjectManager.Tests.csproj --filter ProjectRulesTests
git add src/ProjectManager.Web/Models src/ProjectManager.Web/Security src/ProjectManager.Web/Services/ProjectRules.cs tests/ProjectManager.Tests/Domain
git commit -m "feat: add project domain model and validation rules"
```

Expected: validation tests pass and commit is created.

## Task 3: EF Core SQL Server DbContext and Seed Data

**Files:**
- Create: `src/ProjectManager.Web/Data/ApplicationDbContext.cs`
- Create: `src/ProjectManager.Web/Data/SeedData.cs`
- Modify: `src/ProjectManager.Web/Program.cs`
- Modify: `src/ProjectManager.Web/appsettings.json`
- Create: `tests/ProjectManager.Tests/TestSupport/TestDbFactory.cs`

- [ ] **Step 1: Add SQLite test DbContext helper**

Create `tests/ProjectManager.Tests/TestSupport/TestDbFactory.cs`:

```csharp
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ProjectManager.Web.Data;

namespace ProjectManager.Tests.TestSupport;

public static class TestDbFactory
{
    public static async Task<(ApplicationDbContext Db, SqliteConnection Connection)> CreateAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();
        return (db, connection);
    }
}
```

Run:

```powershell
.\scripts\dotnet.ps1 test tests/ProjectManager.Tests/ProjectManager.Tests.csproj --filter TestDbFactory
```

Expected: build fails because `ApplicationDbContext` does not exist yet.

- [ ] **Step 2: Implement DbContext**

Create `src/ProjectManager.Web/Data/ApplicationDbContext.cs` using `IdentityDbContext<ApplicationUser>` and configure:

- unique index on `Project.Year + Project.ProjectNumber` where `IsDeleted = false`;
- decimal precision `18,2` for money;
- decimal precision `5,2` for percentages;
- `ProjectStatus` one-to-one `ProjectStatusStyle`;
- one project to many assignments;
- one project to many purchase requests;
- one settlement batch to many settlement items.

Key configuration snippet:

```csharp
builder.Entity<Project>()
    .HasIndex(x => new { x.Year, x.ProjectNumber })
    .IsUnique()
    .HasFilter("[IsDeleted] = 0");

builder.Entity<Project>()
    .Property(x => x.ProjectAmount)
    .HasPrecision(18, 2);

builder.Entity<Project>()
    .Property(x => x.ProgressPercent)
    .HasPrecision(5, 2);
```

- [ ] **Step 3: Configure SQL Server and Identity in Program.cs**

Modify `Program.cs` so the application uses:

```csharp
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.Password.RequireDigit = true;
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();
```

`appsettings.json` must include:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=ProjectManager;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
  },
  "AdminSeed": {
    "UserName": "admin",
    "Email": "admin@example.local",
    "Password": "ChangeMe123!"
  }
}
```

- [ ] **Step 4: Seed roles, statuses, styles, and admin user**

Create `src/ProjectManager.Web/Data/SeedData.cs` with:

- roles from `RoleNames.All`;
- statuses in order: 已立案, 已请购, 代码中, 试车中, 待收款, 已结案;
- `Closed` status with `IsClosed = true`;
- style for `Closed` with red text and bold;
- admin user from configuration and `Administrator` role.

- [ ] **Step 5: Run migrations, tests, and commit**

Run:

```powershell
.\scripts\dotnet.ps1 ef migrations add InitialCreate --project src/ProjectManager.Web/ProjectManager.Web.csproj
.\scripts\dotnet.ps1 test tests/ProjectManager.Tests/ProjectManager.Tests.csproj
.\scripts\dotnet.ps1 build ProjectManager.sln
git add src/ProjectManager.Web/Data src/ProjectManager.Web/Program.cs src/ProjectManager.Web/appsettings.json src/ProjectManager.Web/Migrations tests/ProjectManager.Tests/TestSupport
git commit -m "feat: configure EF Core identity and seed data"
```

Expected: migration is created, tests pass, solution builds, and commit is created.

## Task 4: Project Query Service and Open-Project Rules

**Files:**
- Create: `src/ProjectManager.Web/Services/ProjectQueryService.cs`
- Create: `tests/ProjectManager.Tests/Services/ProjectQueryServiceTests.cs`

- [ ] **Step 1: Write failing query tests**

Create tests proving:

- closed projects are excluded from open-project queries by `ProjectStatus.IsClosed`;
- filters include year, parent case number, project number, project name, personnel, and status;
- open-project summary groups by status and totals project amount, purchase amount, and actual paid amount.

Use test data with one open project and one closed project.

- [ ] **Step 2: Implement query request and result records**

In `ProjectQueryService.cs`, define:

```csharp
public sealed record ProjectFilter(
    int? Year,
    string? ParentCaseNumber,
    string? ProjectNumber,
    string? ProjectName,
    string? PersonnelUserId,
    int? StatusId,
    bool OpenOnly);

public sealed record OpenProjectSummaryRow(
    string StatusName,
    int Count,
    decimal ProjectAmountTotal,
    decimal PurchaseAmountTotal,
    decimal ActualPaidAmountTotal);
```

- [ ] **Step 3: Implement service methods**

Implement:

- `GetProjectsAsync(ProjectFilter filter, CancellationToken cancellationToken)`;
- `GetOpenProjectSummaryAsync(ProjectFilter filter, CancellationToken cancellationToken)`.

Both methods must use `AsNoTracking()` and include `Status`, `Assignments`, and `PurchaseRequests`.

- [ ] **Step 4: Run tests and commit**

Run:

```powershell
.\scripts\dotnet.ps1 test tests/ProjectManager.Tests/ProjectManager.Tests.csproj --filter ProjectQueryServiceTests
git add src/ProjectManager.Web/Services/ProjectQueryService.cs tests/ProjectManager.Tests/Services/ProjectQueryServiceTests.cs
git commit -m "feat: add project query service"
```

Expected: query tests pass and commit is created.

## Task 5: Monthly Settlement Service

**Files:**
- Create: `src/ProjectManager.Web/Services/SettlementService.cs`
- Create: `tests/ProjectManager.Tests/Services/SettlementServiceTests.cs`

- [ ] **Step 1: Write failing settlement tests**

Create tests proving:

- creating settlement for the same year/month twice produces batch numbers `1` and `2`;
- snapshot rows include `ParentCaseNumber`, `ClosedYearMonth`, `PurchaseRequestSummary`, `SubCaseContactSummary`, totals, progress description, updater, and source update time;
- settlement creation excludes soft-deleted projects;
- settlement month outside 1 to 12 returns validation error.

- [ ] **Step 2: Implement request and result records**

In `SettlementService.cs`, define:

```csharp
public sealed record CreateSettlementRequest(
    int Year,
    int Month,
    string CreatedByUserId,
    string? Notes);

public sealed record CreateSettlementResult(
    bool Success,
    int? BatchId,
    IReadOnlyList<string> Errors);
```

- [ ] **Step 3: Implement transactional settlement creation**

Implement `CreateAsync(CreateSettlementRequest request, CancellationToken cancellationToken)`.

Rules:

- reject months outside 1 to 12;
- calculate next `BatchNumber` for the same year/month;
- create one batch and snapshot rows in a transaction;
- copy text values into snapshot fields so later project edits do not change the historical batch;
- summarize request numbers with `"; "` separator;
- summarize sub-case contact display names with `"; "` separator;
- total purchase amount and actual paid amount.

- [ ] **Step 4: Run tests and commit**

Run:

```powershell
.\scripts\dotnet.ps1 test tests/ProjectManager.Tests/ProjectManager.Tests.csproj --filter SettlementServiceTests
git add src/ProjectManager.Web/Services/SettlementService.cs tests/ProjectManager.Tests/Services/SettlementServiceTests.cs
git commit -m "feat: add monthly settlement service"
```

Expected: settlement tests pass and commit is created.

## Task 6: Excel Report Service

**Files:**
- Create: `src/ProjectManager.Web/Services/ExcelReportService.cs`
- Create: `tests/ProjectManager.Tests/Services/ExcelReportServiceTests.cs`

- [ ] **Step 1: Write failing Excel tests**

Create tests that call:

- `ExportSettlementAsync(int batchId, CancellationToken cancellationToken)`;
- `ExportOpenProjectsAsync(ProjectFilter filter, CancellationToken cancellationToken)`.

Expected settlement headers:

```text
年, 月, 批次号, 母案案号, 项目工号, 项目名称, 专案人员, 项目进度百分比, 项目金额, 收款比例, 状态, 结案日期, 请购号汇总, 请购金额合计, 子案对接人员汇总, 付款比例汇总, 实际已付款合计, 进度说明, 更新人员, 来源更新时间
```

Expected open-project headers:

```text
年, 母案案号, 项目工号, 项目名称, 专案人员, 项目进度百分比, 项目金额, 收款比例, 状态, 结案日期, 请购金额合计, 子案对接人员汇总, 实际已付款合计, 进度说明, 更新人员, 最后更新时间
```

- [ ] **Step 2: Implement ClosedXML export service**

Implement `ExcelReportService` returning:

```csharp
public sealed record ExportFile(string FileName, string ContentType, byte[] Contents);
```

Use content type:

```text
application/vnd.openxmlformats-officedocument.spreadsheetml.sheet
```

Format:

- freeze first row;
- bold header row;
- set money columns to `#,##0.00`;
- set percent columns to `0.00`;
- set date/time columns to `yyyy-mm-dd hh:mm`;
- set closed year/month columns to `yyyy-mm`.

- [ ] **Step 3: Run tests and commit**

Run:

```powershell
.\scripts\dotnet.ps1 test tests/ProjectManager.Tests/ProjectManager.Tests.csproj --filter ExcelReportServiceTests
git add src/ProjectManager.Web/Services/ExcelReportService.cs tests/ProjectManager.Tests/Services/ExcelReportServiceTests.cs
git commit -m "feat: add Excel report exports"
```

Expected: Excel tests pass and commit is created.

## Task 7: Authentication, Admin User Management, and Menus

**Files:**
- Modify: `src/ProjectManager.Web/Program.cs`
- Create: `src/ProjectManager.Web/Pages/Admin/Users/Index.cshtml`
- Create: `src/ProjectManager.Web/Pages/Admin/Users/Index.cshtml.cs`
- Create: `src/ProjectManager.Web/Pages/Admin/Users/Create.cshtml`
- Create: `src/ProjectManager.Web/Pages/Admin/Users/Create.cshtml.cs`
- Create: `src/ProjectManager.Web/Pages/Admin/Users/Edit.cshtml`
- Create: `src/ProjectManager.Web/Pages/Admin/Users/Edit.cshtml.cs`
- Create: `src/ProjectManager.Web/Pages/Admin/Users/ResetPassword.cshtml`
- Create: `src/ProjectManager.Web/Pages/Admin/Users/ResetPassword.cshtml.cs`
- Modify: `src/ProjectManager.Web/Pages/Shared/_Layout.cshtml`
- Create: `tests/ProjectManager.Tests/Web/AuthSmokeTests.cs`

- [ ] **Step 1: Write authorization smoke tests**

Create tests using `WebApplicationFactory<Program>` proving:

- anonymous user redirected from `/Admin/Users`;
- admin-only pages require `Administrator`;
- authenticated non-admin user cannot access `/Admin/Users`;
- login page is reachable.

- [ ] **Step 2: Add role-based menus**

Update `_Layout.cshtml`:

- Administrator sees Admin, Projects, Settlements, Reports.
- ProjectStaff sees Workbench.
- Leader sees Workbench, Settlements, Reports.
- Viewer sees Workbench and Reports.

- [ ] **Step 3: Implement admin user pages**

Admin user pages must support:

- list users with active flag and roles;
- create user with username, display name, email, password, roles;
- edit display name, email, active flag, roles;
- reset password.

Do not expose password hashes or security stamps in UI.

- [ ] **Step 4: Run tests and commit**

Run:

```powershell
.\scripts\dotnet.ps1 test tests/ProjectManager.Tests/ProjectManager.Tests.csproj --filter AuthSmokeTests
.\scripts\dotnet.ps1 build ProjectManager.sln
git add src/ProjectManager.Web/Pages/Admin/Users src/ProjectManager.Web/Pages/Shared/_Layout.cshtml tests/ProjectManager.Tests/Web/AuthSmokeTests.cs
git commit -m "feat: add identity user administration"
```

Expected: smoke tests pass, build succeeds, and commit is created.

## Task 8: Admin Project, Purchase Request, Status, and Style Pages

**Files:**
- Create: `src/ProjectManager.Web/Pages/Admin/Projects/Index.cshtml`
- Create: `src/ProjectManager.Web/Pages/Admin/Projects/Index.cshtml.cs`
- Create: `src/ProjectManager.Web/Pages/Admin/Projects/Create.cshtml`
- Create: `src/ProjectManager.Web/Pages/Admin/Projects/Create.cshtml.cs`
- Create: `src/ProjectManager.Web/Pages/Admin/Projects/Edit.cshtml`
- Create: `src/ProjectManager.Web/Pages/Admin/Projects/Edit.cshtml.cs`
- Create: `src/ProjectManager.Web/Pages/Admin/Projects/Details.cshtml`
- Create: `src/ProjectManager.Web/Pages/Admin/Projects/Details.cshtml.cs`
- Create: `src/ProjectManager.Web/Pages/Admin/Statuses/Index.cshtml`
- Create: `src/ProjectManager.Web/Pages/Admin/Statuses/Index.cshtml.cs`
- Create: `src/ProjectManager.Web/Pages/Admin/Statuses/Edit.cshtml`
- Create: `src/ProjectManager.Web/Pages/Admin/Statuses/Edit.cshtml.cs`

- [ ] **Step 1: Add page model tests for admin project validation**

Create tests proving:

- duplicate `Year + ProjectNumber` is rejected;
- closed status requires `ClosedYearMonth`;
- purchase request requires `RequestNumber`;
- negative purchase amount is rejected.

- [ ] **Step 2: Implement admin project CRUD**

Pages must include:

- filters: year, parent case number, project number, project name, personnel, status, open/closed;
- fields from the spec: year, parent case number, project number, name, personnel, progress percent, amount, collection percent, progress description, updater, closed year/month, status;
- purchase request child rows with request number, type, purchase staff, amount, sub-case contact, payment percent, actual paid, notes;
- soft delete for projects and purchase requests.

- [ ] **Step 3: Implement status and style settings**

Status pages must support:

- status name and code;
- display order;
- `IsClosed`;
- active flag;
- text color;
- background color;
- bold flag.

Keep seeded statuses editable but prevent deleting a status currently used by projects.

- [ ] **Step 4: Run tests and commit**

Run:

```powershell
.\scripts\dotnet.ps1 test tests/ProjectManager.Tests/ProjectManager.Tests.csproj --filter AdminProject
.\scripts\dotnet.ps1 build ProjectManager.sln
git add src/ProjectManager.Web/Pages/Admin/Projects src/ProjectManager.Web/Pages/Admin/Statuses tests/ProjectManager.Tests
git commit -m "feat: add admin project and status management"
```

Expected: validation tests pass, build succeeds, and commit is created.

## Task 9: Web Workbench and Status Visualization

**Files:**
- Create: `src/ProjectManager.Web/Pages/Workbench/Projects/Index.cshtml`
- Create: `src/ProjectManager.Web/Pages/Workbench/Projects/Index.cshtml.cs`
- Create: `src/ProjectManager.Web/Pages/Workbench/Projects/Details.cshtml`
- Create: `src/ProjectManager.Web/Pages/Workbench/Projects/Details.cshtml.cs`
- Create: `src/ProjectManager.Web/Pages/Workbench/Projects/EditProgress.cshtml`
- Create: `src/ProjectManager.Web/Pages/Workbench/Projects/EditProgress.cshtml.cs`
- Create: `src/ProjectManager.Web/Pages/Shared/_StatusBadge.cshtml`
- Create: `src/ProjectManager.Web/Pages/Shared/_StatusTimeline.cshtml`
- Modify: `src/ProjectManager.Web/wwwroot/css/site.css`

- [ ] **Step 1: Add workbench authorization tests**

Create tests proving:

- ProjectStaff sees assigned projects only;
- Leader sees all non-deleted projects;
- ProjectStaff can update progress percent and progress description only;
- ProjectStaff cannot edit project amount, collection percent, purchase amount, or actual paid amount.

- [ ] **Step 2: Implement workbench project list and detail pages**

Workbench pages must show:

- parent case number;
- project number and name;
- personnel;
- status badge using configured colors and bold;
- progress percent;
- amount and collection percent;
- closed year/month;
- purchase request summaries.

- [ ] **Step 3: Implement status timeline partial**

`_StatusTimeline.cshtml` renders active statuses ordered by `SortOrder`.

Rules:

- statuses before current status are completed;
- current status is highlighted;
- closed status uses configured red/bold default until administrator changes it;
- inactive statuses do not render.

- [ ] **Step 4: Run tests and commit**

Run:

```powershell
.\scripts\dotnet.ps1 test tests/ProjectManager.Tests/ProjectManager.Tests.csproj --filter Workbench
.\scripts\dotnet.ps1 build ProjectManager.sln
git add src/ProjectManager.Web/Pages/Workbench src/ProjectManager.Web/Pages/Shared/_StatusBadge.cshtml src/ProjectManager.Web/Pages/Shared/_StatusTimeline.cshtml src/ProjectManager.Web/wwwroot/css/site.css tests/ProjectManager.Tests
git commit -m "feat: add web workbench and status timeline"
```

Expected: workbench tests pass, build succeeds, and commit is created.

## Task 10: Settlement Pages, Print Views, and Export Actions

**Files:**
- Create: `src/ProjectManager.Web/Pages/Settlements/Index.cshtml`
- Create: `src/ProjectManager.Web/Pages/Settlements/Index.cshtml.cs`
- Create: `src/ProjectManager.Web/Pages/Settlements/Create.cshtml`
- Create: `src/ProjectManager.Web/Pages/Settlements/Create.cshtml.cs`
- Create: `src/ProjectManager.Web/Pages/Settlements/Details.cshtml`
- Create: `src/ProjectManager.Web/Pages/Settlements/Details.cshtml.cs`
- Create: `src/ProjectManager.Web/Pages/Settlements/Print.cshtml`
- Create: `src/ProjectManager.Web/Pages/Settlements/Print.cshtml.cs`

- [ ] **Step 1: Add settlement page smoke tests**

Create tests proving:

- Administrator can open settlement creation page;
- Leader can view settlement details and print page;
- ProjectStaff cannot create settlement;
- export action returns Excel content type.

- [ ] **Step 2: Implement settlement pages**

Pages must support:

- list batches filtered by year/month;
- preview current open and closed projects before creating settlement;
- create settlement using `SettlementService`;
- view batch details;
- export batch to Excel using `ExcelReportService`;
- print page with table columns matching settlement Excel headers.

- [ ] **Step 3: Add print CSS**

Add to `wwwroot/css/site.css`:

```css
@media print {
  .no-print,
  nav,
  footer {
    display: none !important;
  }

  body {
    background: #fff;
    color: #000;
    font-size: 11px;
  }

  table {
    width: 100%;
    border-collapse: collapse;
  }

  th,
  td {
    border: 1px solid #999;
    padding: 4px 6px;
  }
}
```

- [ ] **Step 4: Run tests and commit**

Run:

```powershell
.\scripts\dotnet.ps1 test tests/ProjectManager.Tests/ProjectManager.Tests.csproj --filter Settlement
.\scripts\dotnet.ps1 build ProjectManager.sln
git add src/ProjectManager.Web/Pages/Settlements src/ProjectManager.Web/wwwroot/css/site.css tests/ProjectManager.Tests
git commit -m "feat: add settlement pages and print view"
```

Expected: settlement page tests pass, build succeeds, and commit is created.

## Task 11: Open-Project Reports and Statistics

**Files:**
- Create: `src/ProjectManager.Web/Pages/Reports/OpenProjects/Index.cshtml`
- Create: `src/ProjectManager.Web/Pages/Reports/OpenProjects/Index.cshtml.cs`
- Create: `src/ProjectManager.Web/Pages/Reports/OpenProjects/Print.cshtml`
- Create: `src/ProjectManager.Web/Pages/Reports/OpenProjects/Print.cshtml.cs`
- Create: `src/ProjectManager.Web/Pages/Reports/OpenProjects/Statistics.cshtml`
- Create: `src/ProjectManager.Web/Pages/Reports/OpenProjects/Statistics.cshtml.cs`

- [ ] **Step 1: Add report smoke tests**

Create tests proving:

- Leader can access open-project report;
- Viewer can access open-project report;
- ProjectStaff can access report only for assigned projects;
- exported open-project Excel has the configured headers.

- [ ] **Step 2: Implement open-project report pages**

Report pages must include filters:

- year;
- month;
- parent case number;
- project number;
- project name;
- personnel;
- status;
- open/closed state.

Open-project default uses `ProjectStatus.IsClosed == false`.

- [ ] **Step 3: Implement statistics page**

Statistics page must show grouped rows by status:

- project count;
- project amount total;
- purchase amount total;
- actual paid total;
- average collection percent.

- [ ] **Step 4: Run tests and commit**

Run:

```powershell
.\scripts\dotnet.ps1 test tests/ProjectManager.Tests/ProjectManager.Tests.csproj --filter OpenProject
.\scripts\dotnet.ps1 build ProjectManager.sln
git add src/ProjectManager.Web/Pages/Reports tests/ProjectManager.Tests
git commit -m "feat: add open project reports"
```

Expected: report tests pass, build succeeds, and commit is created.

## Task 12: Audit Logging, UI Polish, README, and Final Verification

**Files:**
- Create: `src/ProjectManager.Web/Services/AuditLogService.cs`
- Modify: `src/ProjectManager.Web/wwwroot/css/site.css`
- Create: `README.md`

- [ ] **Step 1: Add audit log service**

Implement `AuditLogService` with:

```csharp
public Task LogAsync(string userId, string action, string entityName, string entityId, string description, CancellationToken cancellationToken)
```

Call it from:

- admin project create/edit/soft delete;
- purchase request create/edit/soft delete;
- status style edits;
- settlement batch creation.

- [ ] **Step 2: Add compact intranet UI styles**

`site.css` must use:

- compact table spacing;
- restrained neutral page background;
- clear status badges;
- print-friendly table styling;
- no decorative landing page.

- [ ] **Step 3: Add README**

Create `README.md` with:

```markdown
# Project Manager

Internal ASP.NET Core Razor Pages project management system.

## Run Locally

1. Download and install the project-local SDK:
   `Invoke-WebRequest -Uri https://dot.net/v1/dotnet-install.ps1 -OutFile .dotnet-install.ps1`
   `powershell -ExecutionPolicy Bypass -File .\.dotnet-install.ps1 -Channel 10.0 -InstallDir .\.dotnet`
2. Build:
   `.\scripts\dotnet.ps1 build ProjectManager.sln`
3. Update SQL Server connection string in `src/ProjectManager.Web/appsettings.json`.
4. Apply migrations:
   `.\scripts\dotnet.ps1 ef database update --project src/ProjectManager.Web/ProjectManager.Web.csproj`
5. Run:
   `.\scripts\dotnet.ps1 run --project src/ProjectManager.Web/ProjectManager.Web.csproj`

## Default Admin

Username: `admin`
Password: `ChangeMe123!`

Change the password after first login.
```

- [ ] **Step 4: Run full verification**

Run:

```powershell
.\scripts\dotnet.ps1 test ProjectManager.sln
.\scripts\dotnet.ps1 build ProjectManager.sln
git status --short
```

Expected:

- all tests pass;
- solution builds;
- `git status --short` shows only intended modifications before final commit.

- [ ] **Step 5: Final commit**

Run:

```powershell
git add src tests README.md
git commit -m "chore: finalize project manager v1"
```

Expected: final verification commit is created.

## Self-Review

Spec coverage:

- Login, roles, password change, and admin user management are covered in Tasks 3 and 7.
- SQL Server EF Core configuration is covered in Task 3.
- Project fields, including parent case number and closed year/month, are covered in Tasks 2, 3, 8, 9, 10, and 11.
- Multiple purchase requests, internal/external type, purchase staff, sub-case contact, payment percent, and paid amount are covered in Tasks 2, 5, 6, and 8.
- Manual monthly settlement with repeat runs and snapshot rows is covered in Tasks 5 and 10.
- Excel export is covered in Task 6 and wired in Tasks 10 and 11.
- Printable reports are covered in Tasks 10, 11, and 12.
- Dynamic status chart and configurable status colors/bold are covered in Tasks 8 and 9.
- Open-project report and statistics are covered in Tasks 4 and 11.
- Administrator-controlled frontend status settings are covered in Task 8.

Plan consistency:

- The domain field names are consistent across model, DbContext, settlement snapshot, Excel export, and report tasks.
- `ClosedYearMonth` represents a month-precision date by storing the first day of the selected month.
- `ProjectStatus.IsClosed` is the source of truth for closed/open logic.
- ProjectStaff permissions are limited to progress and progress description updates.
