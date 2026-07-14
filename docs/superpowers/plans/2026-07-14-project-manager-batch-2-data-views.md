# Project Manager Batch 2 Data Workspace and Saved Views Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make wide business tables readable and efficient, and persist each authenticated user's filters, column layout, row density, and default view across browsers.

**Architecture:** Add a generic `SavedDataView` entity and a whitelist-driven service, expose reusable Razor handlers and shared view controls, and extend the Batch 1 data-table module to initialize from server state and save user changes through antiforgery-protected POST requests. System presets remain code-defined and immutable; personal views are database records isolated by user and page key.

**Tech Stack:** ASP.NET Core Razor Pages, .NET 9, EF Core 9, SQL Server, System.Text.Json, vanilla ES modules, xUnit, FluentAssertions, SQLite integration tests, Playwright visual regression.

---

## Scope Check

This batch changes persistence by adding one new table. It does not alter existing project, report, maintenance, settlement, Identity, or audit tables. It adds synchronous operation feedback only; background queues and a real-time progress center remain Batch 6.

## File Structure

- Create `src/ProjectManager.Web/Models/SavedDataView.cs`: persisted personal view.
- Modify `src/ProjectManager.Web/Models/ApplicationUser.cs`: navigation collection.
- Modify `src/ProjectManager.Web/Data/ApplicationDbContext.cs`: mapping and indexes.
- Create an EF Core migration named `AddSavedDataViews`.
- Create `src/ProjectManager.Web/Services/DataViews/DataViewDefinition.cs`: page whitelist and preset definitions.
- Create `src/ProjectManager.Web/Services/DataViews/SavedDataViewService.cs`: persistence and validation.
- Create `src/ProjectManager.Web/Services/DataViews/DataViewRegistry.cs`: supported page definitions.
- Register services in `src/ProjectManager.Web/Program.cs`.
- Create `src/ProjectManager.Web/Pages/Shared/SavedDataViewBarViewModel.cs` and `_SavedDataViewBar.cshtml`.
- Extend `src/ProjectManager.Web/wwwroot/js/components/data-table.js`.
- Create `src/ProjectManager.Web/wwwroot/js/components/saved-views.js`.
- Modify project, report, workbench, and maintenance PageModels/pages.
- Add service, data, web, asset, and visual regression tests.
- Update SQL dictionary and operational documentation.

## Task 1: Add the SavedDataView Persistence Model

**Files:**
- Create: `src/ProjectManager.Web/Models/SavedDataView.cs`
- Modify: `src/ProjectManager.Web/Models/ApplicationUser.cs`
- Modify: `src/ProjectManager.Web/Data/ApplicationDbContext.cs`
- Modify: `tests/ProjectManager.Tests/Data/ApplicationDbContextTests.cs`

- [ ] **Step 1: Write failing data-model tests**

Add tests that require a `SavedDataViews` DbSet, a unique `(UserId, PageKey, Name)` index, bounded strings, and cascade deletion from `ApplicationUser`.

```csharp
[Fact]
public void Saved_data_view_has_unique_user_page_name_key()
{
    using var db = TestDbFactory.CreateContext();
    var entity = db.Model.FindEntityType(typeof(SavedDataView));
    var index = entity!.GetIndexes().Single(x => x.IsUnique);
    index.Properties.Select(x => x.Name)
        .Should().Equal(nameof(SavedDataView.UserId), nameof(SavedDataView.PageKey), nameof(SavedDataView.Name));
}
```

- [ ] **Step 2: Run and verify failure**

```powershell
$ErrorActionPreference = 'Stop'
.\scripts\dotnet.ps1 test tests\ProjectManager.Tests\ProjectManager.Tests.csproj -c Release --filter ApplicationDbContextTests
```

Expected: FAIL because the entity is missing.

- [ ] **Step 3: Create the model**

```csharp
namespace ProjectManager.Web.Models;

public enum DataViewRowDensity
{
    Compact = 1,
    Normal = 2,
    Spacious = 3
}

public sealed class SavedDataView
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }
    public string PageKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string FilterJson { get; set; } = "{}";
    public string ColumnJson { get; set; } = "{}";
    public DataViewRowDensity RowDensity { get; set; } = DataViewRowDensity.Normal;
    public bool IsDefault { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
```

Add `ICollection<SavedDataView> SavedDataViews` to `ApplicationUser`.

- [ ] **Step 4: Configure the entity**

Add the DbSet and mapping:

```csharp
builder.Entity<SavedDataView>(entity =>
{
    entity.HasIndex(x => new { x.UserId, x.PageKey, x.Name }).IsUnique();
    entity.HasIndex(x => new { x.UserId, x.PageKey, x.IsDefault });
    entity.Property(x => x.UserId).HasMaxLength(450).IsRequired();
    entity.Property(x => x.PageKey).HasMaxLength(80).IsRequired();
    entity.Property(x => x.Name).HasMaxLength(80).IsRequired();
    entity.Property(x => x.FilterJson).HasMaxLength(8000).IsRequired();
    entity.Property(x => x.ColumnJson).HasMaxLength(8000).IsRequired();
    entity.HasOne(x => x.User)
        .WithMany(x => x.SavedDataViews)
        .HasForeignKey(x => x.UserId)
        .OnDelete(DeleteBehavior.Cascade);
});
```

- [ ] **Step 5: Run model tests**

```powershell
$ErrorActionPreference = 'Stop'
.\scripts\dotnet.ps1 test tests\ProjectManager.Tests\ProjectManager.Tests.csproj -c Release --filter ApplicationDbContextTests
```

Expected: PASS.

## Task 2: Implement Whitelist Definitions and SavedDataViewService

**Files:**
- Create: `src/ProjectManager.Web/Services/DataViews/DataViewDefinition.cs`
- Create: `src/ProjectManager.Web/Services/DataViews/DataViewRegistry.cs`
- Create: `src/ProjectManager.Web/Services/DataViews/SavedDataViewService.cs`
- Create: `tests/ProjectManager.Tests/Services/SavedDataViewServiceTests.cs`
- Modify: `src/ProjectManager.Web/Program.cs`

- [ ] **Step 1: Write failing service tests**

Cover:

- create and reload a personal view;
- reject unknown page keys;
- discard unknown filter/column keys;
- unique default per user/page;
- same view name allowed for different users;
- update and delete only by owner;
- malformed JSON falls back safely.

```csharp
[Fact]
public async Task Setting_default_clears_the_previous_default_for_the_same_user_and_page()
{
    await service.SaveAsync(userId, commandA with { IsDefault = true }, CancellationToken.None);
    await service.SaveAsync(userId, commandB with { IsDefault = true }, CancellationToken.None);

    var views = await service.ListAsync(userId, "admin-projects", CancellationToken.None);
    views.Count(x => x.IsDefault).Should().Be(1);
    views.Single(x => x.IsDefault).Name.Should().Be(commandB.Name);
}
```

- [ ] **Step 2: Run and verify failure**

```powershell
$ErrorActionPreference = 'Stop'
.\scripts\dotnet.ps1 test tests\ProjectManager.Tests\ProjectManager.Tests.csproj -c Release --filter SavedDataViewServiceTests
```

Expected: FAIL because the service is absent.

- [ ] **Step 3: Define immutable view metadata**

```csharp
public sealed record DataViewColumnDefinition(
    string Key,
    string Label,
    bool DefaultVisible = true,
    bool Fixed = false);

public sealed record DataViewPreset(
    string Key,
    string Name,
    IReadOnlyDictionary<string, string?> Filters,
    IReadOnlyList<string> VisibleColumns,
    DataViewRowDensity RowDensity = DataViewRowDensity.Normal);

public sealed record DataViewDefinition(
    string PageKey,
    IReadOnlySet<string> FilterKeys,
    IReadOnlyList<DataViewColumnDefinition> Columns,
    IReadOnlyList<DataViewPreset> Presets);
```

- [ ] **Step 4: Register the approved page definitions**

Implement definitions for:

- `admin-projects`;
- `open-project-report`;
- `workbench-projects`;
- `maintenance-orders`.

The registry throws a controlled `ArgumentException` for unsupported keys. Preset keys are stable lowercase identifiers.

- [ ] **Step 5: Implement the service**

Use `System.Text.Json` to deserialize dictionaries, filter keys against the registry, normalize visible-column order, enforce maximum JSON length, and execute default changes in a transaction. Use `TimeProvider` for timestamps.

- [ ] **Step 6: Register dependencies**

```csharp
builder.Services.AddSingleton<DataViewRegistry>();
builder.Services.AddScoped<SavedDataViewService>();
```

- [ ] **Step 7: Run service tests**

```powershell
$ErrorActionPreference = 'Stop'
.\scripts\dotnet.ps1 test tests\ProjectManager.Tests\ProjectManager.Tests.csproj -c Release --filter SavedDataViewServiceTests
```

Expected: PASS.

## Task 3: Generate and Validate the EF Core Migration

**Files:**
- Create: `src/ProjectManager.Web/Migrations/*_AddSavedDataViews.cs`
- Create: matching designer file
- Modify: `src/ProjectManager.Web/Migrations/ApplicationDbContextModelSnapshot.cs`
- Modify: migration regression tests

- [ ] **Step 1: Generate the migration**

```powershell
$ErrorActionPreference = 'Stop'
.\scripts\dotnet.ps1 ef migrations add AddSavedDataViews --project src\ProjectManager.Web\ProjectManager.Web.csproj
```

Expected: a migration containing only creation of `SavedDataViews`, its foreign key, and indexes.

- [ ] **Step 2: Inspect the migration**

```powershell
$ErrorActionPreference = 'Stop'
$migration = Get-ChildItem -LiteralPath 'src\ProjectManager.Web\Migrations' -Filter '*_AddSavedDataViews.cs' |
    Where-Object { $_.Name -notlike '*.Designer.cs' } |
    Select-Object -Single
Get-Content -LiteralPath $migration.FullName -Raw -Encoding UTF8
```

Expected: `Up` creates one table; `Down` drops only that table; no existing table or column is modified.

- [ ] **Step 3: Add a migration regression test**

Assert the migration contains `CreateTable(name: "SavedDataViews")`, the unique index, and no `DropColumn`/`AlterColumn` in `Up`.

- [ ] **Step 4: Run migration and data tests**

```powershell
$ErrorActionPreference = 'Stop'
.\scripts\dotnet.ps1 test tests\ProjectManager.Tests\ProjectManager.Tests.csproj -c Release --filter "ApplicationDbContextTests|SavedDataViewServiceTests|ProjectUiRegressionTests"
```

Expected: PASS.

## Task 4: Add Shared Saved-View Controls and Handlers

**Files:**
- Create: `src/ProjectManager.Web/Pages/Shared/SavedDataViewBarViewModel.cs`
- Create: `src/ProjectManager.Web/Pages/Shared/_SavedDataViewBar.cshtml`
- Create: `src/ProjectManager.Web/Pages/Shared/SavedDataViewPageSupport.cs`
- Create: `src/ProjectManager.Web/wwwroot/js/components/saved-views.js`
- Modify: `src/ProjectManager.Web/wwwroot/js/site.js`
- Modify: `src/ProjectManager.Web/wwwroot/css/components.css`
- Create/Modify: shared UI tests

- [ ] **Step 1: Write failing shared-control tests**

Require preset buttons, personal view select, save/update/delete/default actions, antiforgery form posts, specific accessible labels, and a `data-saved-view-bar` hook.

- [ ] **Step 2: Run and verify failure**

```powershell
$ErrorActionPreference = 'Stop'
.\scripts\dotnet.ps1 test tests\ProjectManager.Tests\ProjectManager.Tests.csproj -c Release --filter ProjectUiRegressionTests
```

Expected: FAIL on new assertions.

- [ ] **Step 3: Implement the shared ViewModel**

Include page key, selected view, system presets, personal views, current serialized filters/columns/density, and capability flags. Do not pass the authenticated user ID from the browser.

- [ ] **Step 4: Implement shared PageModel support**

Provide methods for:

```csharp
Task<IActionResult> SaveViewAsync(string userId, SaveDataViewInput input, CancellationToken cancellationToken);
Task<IActionResult> DeleteViewAsync(string userId, int id, string returnUrl, CancellationToken cancellationToken);
Task<IActionResult> SetDefaultAsync(string userId, int id, string returnUrl, CancellationToken cancellationToken);
```

Validate `returnUrl` with `Url.IsLocalUrl`; otherwise redirect to the current page.

- [ ] **Step 5: Implement saved-view JavaScript**

The module gathers current filter fields and data-table state, writes them to hidden inputs, disables the submitted button, and shows a Traditional Chinese processing label. Delete uses the shared confirmation UI, not raw `window.confirm`.

- [ ] **Step 6: Add conditional module loading**

`site.js` imports `saved-views.js` only when `[data-saved-view-bar]` exists.

- [ ] **Step 7: Run shared tests and JavaScript checks**

```powershell
$ErrorActionPreference = 'Stop'
Get-ChildItem -LiteralPath 'src\ProjectManager.Web\wwwroot\js' -Recurse -Filter '*.js' |
    ForEach-Object { node --check $_.FullName }
.\scripts\dotnet.ps1 test tests\ProjectManager.Tests\ProjectManager.Tests.csproj -c Release --filter "ProjectUiRegressionTests|SavedDataViewServiceTests"
```

Expected: PASS.

## Task 5: Upgrade the Admin Project Data Workspace

**Files:**
- Modify: `src/ProjectManager.Web/Pages/Admin/Projects/Index.cshtml`
- Modify: `src/ProjectManager.Web/Pages/Admin/Projects/Index.cshtml.cs`
- Modify: `src/ProjectManager.Web/wwwroot/js/components/data-table.js`
- Modify: `src/ProjectManager.Web/wwwroot/css/components.css`
- Modify/Create: project list tests and visual test

- [ ] **Step 1: Write failing page tests**

Require:

- `PageKey = "admin-projects"`;
- system preset definitions rendered by `_SavedDataViewBar`;
- stable `data-column` on every configurable header/cell;
- project number/name fixed;
- long descriptions use truncation;
- server output includes selected row density and visible columns.

- [ ] **Step 2: Run and verify failure**

```powershell
$ErrorActionPreference = 'Stop'
.\scripts\dotnet.ps1 test tests\ProjectManager.Tests\ProjectManager.Tests.csproj -c Release --filter ProjectUiRegressionTests
```

Expected: FAIL.

- [ ] **Step 3: Load saved-view state in the PageModel**

Use the authenticated `ClaimTypes.NameIdentifier`. Selection precedence:

1. explicit query-string filters;
2. explicitly selected preset/personal view;
3. user's default view;
4. system default.

Never silently overwrite explicit query parameters with a default view.

- [ ] **Step 4: Add save/delete/default handlers**

Handlers obtain the current user ID server-side, call `SavedDataViewPageSupport`, preserve local return URLs, and use TempData success/error messages.

- [ ] **Step 5: Normalize column metadata**

Fixed identity columns receive explicit widths and sticky offsets. The action column remains visible without overlapping the prior cell. Column manager must refuse to hide all non-fixed data columns; at least one configurable data column stays visible.

- [ ] **Step 6: Add two-line truncation**

Render long descriptions inside:

```html
<span class="table-text-clamp" data-full-text="@project.ProgressDescription">@project.ProgressDescription</span>
```

The full value is available through an accessible details trigger; it is not inserted as unsanitized HTML.

- [ ] **Step 7: Run project tests and visual regression**

```powershell
$ErrorActionPreference = 'Stop'
.\scripts\dotnet.ps1 test tests\ProjectManager.Tests\ProjectManager.Tests.csproj -c Release --filter "ProjectUiRegressionTests|ProjectQueryServiceTests|DashboardSmokeTests"
```

Expected: PASS; visual capture shows readable fixed identity columns and no action overlap.

## Task 6: Upgrade the Open-Project Report

**Files:**
- Modify: `src/ProjectManager.Web/Pages/Reports/OpenProjects/Index.cshtml`
- Modify: `src/ProjectManager.Web/Pages/Reports/OpenProjects/Index.cshtml.cs`
- Modify: `src/ProjectManager.Web/Pages/Reports/OpenProjects/_OpenProjectsTable.cshtml`
- Modify: report tests and visual spec

- [ ] **Step 1: Write failing report tests**

Require the default visible columns exactly as approved:

```text
projectNumber, name, assignments, progress, collectionPercent,
status, risk, projectAmount, updatedAt
```

Require system presets `risk`, `progress`, `finance`, and `full`.

- [ ] **Step 2: Run and verify failure**

```powershell
$ErrorActionPreference = 'Stop'
.\scripts\dotnet.ps1 test tests\ProjectManager.Tests\ProjectManager.Tests.csproj -c Release --filter OpenProjectReportSmokeTests
```

Expected: FAIL.

- [ ] **Step 3: Add complete stable column metadata**

Every one of the 17 columns gets a stable key and label. The empty-state colspan is generated from the full column count rather than a magic number.

- [ ] **Step 4: Render default and saved visibility server-side**

Hidden optional columns must start hidden before JavaScript runs, preventing layout flash. JavaScript enhances column changes but is not required for the initial readable layout.

- [ ] **Step 5: Apply fixed-column and truncation rules**

Freeze project number and name on desktop. Ensure names and personnel have usable minimum widths. Progress description uses the shared clamp/details behavior.

- [ ] **Step 6: Extend visual tests**

At 1440×900 assert:

- project identity cells do not wrap one character per line;
- page width does not overflow;
- optional columns are hidden in the default view;
- selecting `full` exposes all columns inside the scroll wrapper;
- pagination remains usable.

- [ ] **Step 7: Run report service, web, and visual tests**

```powershell
$ErrorActionPreference = 'Stop'
.\scripts\dotnet.ps1 test tests\ProjectManager.Tests\ProjectManager.Tests.csproj -c Release --filter "OpenProjectReportSmokeTests|ExcelReportServiceTests|SavedDataViewServiceTests"
```

Expected: PASS.

## Task 7: Apply Saved Views to Workbench Projects and Maintenance Orders

**Files:**
- Modify: `src/ProjectManager.Web/Pages/Workbench/Projects/Index.cshtml`
- Modify: `src/ProjectManager.Web/Pages/Workbench/Projects/Index.cshtml.cs`
- Modify: `src/ProjectManager.Web/Pages/Admin/MaintenanceOrders/Index.cshtml`
- Modify: `src/ProjectManager.Web/Pages/Admin/MaintenanceOrders/Index.cshtml.cs`
- Modify: related services/tests/visual tests

- [ ] **Step 1: Add failing page integration tests**

Require `workbench-projects` and `maintenance-orders` page keys, their approved presets, owner-isolated personal views, and server-rendered density/column state.

- [ ] **Step 2: Run and verify failure**

```powershell
$ErrorActionPreference = 'Stop'
.\scripts\dotnet.ps1 test tests\ProjectManager.Tests\ProjectManager.Tests.csproj -c Release --filter "WorkbenchProjectServiceTests|MaintenanceOrderServiceTests|ProjectUiRegressionTests"
```

Expected: FAIL.

- [ ] **Step 3: Integrate Workbench Projects**

Presets: `my-progress`, `recent-updates`, `full`. Preserve existing project visibility authorization; a saved view only narrows a user's already-authorized result set.

- [ ] **Step 4: Integrate Maintenance Orders**

Presets: `execution`, `scope`, `full`. Preserve grouped header colspan recalculation when columns are hidden. Existing localStorage settings are imported only when the user has no server preference, then saved on the next explicit update.

- [ ] **Step 5: Run service and visual tests**

```powershell
$ErrorActionPreference = 'Stop'
.\scripts\dotnet.ps1 test tests\ProjectManager.Tests\ProjectManager.Tests.csproj -c Release --filter "WorkbenchProjectServiceTests|MaintenanceOrderServiceTests|ProjectUiRegressionTests"
```

Expected: PASS.

## Task 8: Add Synchronous Operation Processing and Result Feedback

**Files:**
- Modify: `src/ProjectManager.Web/wwwroot/js/core/feedback.js`
- Modify: bulk/import/export forms and PageModels
- Modify: relevant service result types
- Modify/Create: web and service tests

- [ ] **Step 1: Write failing operation-result tests**

Require batch deletion results to expose attempted, succeeded, and failed counts; import results to expose total, imported, skipped, and error rows; forms to contain `data-processing-label`.

- [ ] **Step 2: Run and verify failure**

```powershell
$ErrorActionPreference = 'Stop'
.\scripts\dotnet.ps1 test tests\ProjectManager.Tests\ProjectManager.Tests.csproj -c Release --filter "ImportPageModelTests|ProjectMaintenanceServiceTests|MaintenanceOrderServiceTests|SettlementServiceTests"
```

Expected: FAIL on new result expectations.

- [ ] **Step 3: Introduce explicit result records**

Use focused immutable records such as:

```csharp
public sealed record BatchOperationResult(
    int Attempted,
    int Succeeded,
    IReadOnlyList<string> Errors)
{
    public int Failed => Attempted - Succeeded;
}
```

Do not hide partial failures behind a single Boolean.

- [ ] **Step 4: Add browser processing states**

On valid submit, disable submit controls, set `aria-busy="true"`, replace the label with the form's `data-processing-label`, and restore state if client validation prevents submission or the page remains active after a failed fetch.

- [ ] **Step 5: Render Traditional Chinese completion summaries**

Use TempData/alerts for synchronous redirects:

```text
已處理 20 筆：成功 18 筆，失敗 2 筆。請查看下方錯誤明細。
```

Errors remain bounded and actionable. Export buttons show “正在準備匯出…” until navigation/download starts; no fake percentage is shown.

- [ ] **Step 6: Run operation tests**

```powershell
$ErrorActionPreference = 'Stop'
.\scripts\dotnet.ps1 test tests\ProjectManager.Tests\ProjectManager.Tests.csproj -c Release --filter "ImportPageModelTests|ProjectMaintenanceServiceTests|MaintenanceOrderServiceTests|SettlementServiceTests|ProjectUiRegressionTests"
```

Expected: PASS.

## Task 9: Add Keyboard, 200% Zoom, and Color-Contrast Coverage for Changed Components

**Files:**
- Modify: `tests/visual/project-ui.spec.js`
- Modify/Create: accessibility asset tests

- [ ] **Step 1: Add keyboard tests**

Cover:

- Tab reaches preset and personal-view controls;
- Enter applies a view;
- Escape closes menus/details;
- Space toggles column checkboxes;
- focus remains visible in both themes.

- [ ] **Step 2: Add 200% zoom checks**

Use a 720px effective viewport or browser zoom emulation. Assert the view toolbar wraps, controls remain clickable, and the table scroll wrapper contains overflow without widening the page.

- [ ] **Step 3: Add contrast checks**

For semantic status badges, primary buttons, muted text, focus rings, and empty-state actions, calculate foreground/background contrast from computed styles. Require WCAG AA for normal text and visible non-text focus indicators.

- [ ] **Step 4: Run visual tests**

```powershell
$ErrorActionPreference = 'Stop'
pwsh -NoProfile -File '.\scripts\visual-regression.ps1' -BaseUrl 'http://localhost:62383'
```

Expected: all Playwright tests pass. If the running app uses another port, pass its actual local URL.

## Task 10: Batch 2 Documentation and Full Verification

**Files:**
- Modify: `docs/架构说明书.md`
- Modify: `docs/设计文档.md`
- Modify: `docs/维护手册.md`
- Modify: `docs/sql-data-dictionary.md`
- Modify: `db/sql-data-dictionary.md`
- Modify: `db/sql-data-dictionary.sql`
- Modify: this plan checkbox state during execution

- [ ] **Step 1: Document SavedDataViews**

Document columns, indexes, ownership, cascade deletion, JSON whitelist rules, preset behavior, and recovery/rollback consequences.

- [ ] **Step 2: Document user workflows**

Explain how to apply presets, save/update/delete/set-default personal views, reset columns, and interpret synchronous operation summaries.

- [ ] **Step 3: Verify the migration can script successfully**

```powershell
$ErrorActionPreference = 'Stop'
.\scripts\dotnet.ps1 ef migrations script --project src\ProjectManager.Web\ProjectManager.Web.csproj --idempotent --output artifacts\AddSavedDataViews.sql
```

Expected: SQL script is generated and contains `SavedDataViews` without destructive changes to existing tables. The generated artifact is for verification and is not staged automatically.

- [ ] **Step 4: Run JavaScript syntax checks**

```powershell
$ErrorActionPreference = 'Stop'
Get-ChildItem -LiteralPath 'src\ProjectManager.Web\wwwroot\js' -Recurse -Filter '*.js' |
    ForEach-Object { node --check $_.FullName }
```

Expected: exit 0.

- [ ] **Step 5: Run Release build and full tests**

```powershell
$ErrorActionPreference = 'Stop'
.\scripts\dotnet.ps1 build ProjectManager.sln -c Release --no-restore
.\scripts\dotnet.ps1 test tests\ProjectManager.Tests\ProjectManager.Tests.csproj -c Release --no-build --logger 'console;verbosity=minimal'
```

Expected: build succeeds with 0 errors and all tests pass.

- [ ] **Step 6: Inspect final changes**

```powershell
$ErrorActionPreference = 'Stop'
git diff --check
git diff --stat
git status --short
```

Expected: no whitespace errors; only approved Batch 1/2 code, tests, migration, and documentation are changed; the pre-existing zip remains untouched.

- [ ] **Step 7: Stop before Git history operations**

Report exact verification evidence, migration name, changed-file summary, and any remaining follow-up. Do not stage, commit, push, or create a PR without separate user authorization.

## Self-Review

- Spec coverage: wide tables, presets, saved filters, personal columns/density/default view, operation feedback, keyboard/zoom/contrast, migration, and docs all have tasks.
- Security: user ID is always sourced server-side; page/filter/column keys are whitelist validated; local return URLs are validated.
- Data isolation: service tests cover ownership and cross-user separation.
- Schema boundary: one additive table only; no existing business tables change.
- Async boundary: no queue, SignalR, or fake progress percentage is introduced.
- Git boundary: no staging or history mutation is authorized.
