# Project Manager Batch 1 UI Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Establish a Traditional-Chinese-first UI foundation with modular frontend assets, clearer navigation, restrained data-page styling, semantic business colors, reusable empty states, and accessible collapsed navigation.

**Architecture:** Keep Razor Pages, Bootstrap, existing routes, and the current database unchanged. Convert `site.js` into an ES module loader, split CSS into four ordered layers, introduce small reusable UI helpers, and preserve `site.js`/`site.css` as compatibility entry points while tests migrate to the new asset map.

**Tech Stack:** ASP.NET Core Razor Pages, .NET 9, Bootstrap, vanilla ES modules, CSS custom properties, xUnit, FluentAssertions, Playwright visual regression.

---

## Scope Check

This batch is one frontend-foundation release. It must not create EF Core migrations or alter existing entity schemas. Saved views and database-backed table preferences belong to Batch 2.

## File Structure

- Create `tests/ProjectManager.Tests/Web/TraditionalChineseTerminologyTests.cs`: canonical terminology guard.
- Create `tests/ProjectManager.Tests/Web/FrontendAssetStructureTests.cs`: asset loading and module-boundary guard.
- Create `src/ProjectManager.Web/wwwroot/css/base.css`: tokens, typography, focus, reduced motion, application shell primitives.
- Create `src/ProjectManager.Web/wwwroot/css/components.css`: reusable controls, cards, tables, badges, tooltip, toast, empty state.
- Create `src/ProjectManager.Web/wwwroot/css/pages.css`: page-specific layouts and print rules.
- Create `src/ProjectManager.Web/wwwroot/css/themes.css`: theme, font, motion, and effect-level variants.
- Modify `src/ProjectManager.Web/wwwroot/css/site.css`: compatibility notice only after rules move.
- Create `src/ProjectManager.Web/wwwroot/js/core/shell.js`: navigation and sidebar behavior.
- Create `src/ProjectManager.Web/wwwroot/js/core/feedback.js`: processing state and toast primitives.
- Create component/page/effect modules under `src/ProjectManager.Web/wwwroot/js/`.
- Modify `src/ProjectManager.Web/wwwroot/js/site.js`: selector-driven ES module loader.
- Create `src/ProjectManager.Web/Services/ProjectVisualStateResolver.cs`: pure business-semantic visual state resolver.
- Create `tests/ProjectManager.Tests/Services/ProjectVisualStateResolverTests.cs`: semantic state tests.
- Create `src/ProjectManager.Web/Pages/Shared/EmptyStateViewModel.cs` and `_EmptyState.cshtml`.
- Modify shared layout, percentage, status, risk, navigation, and priority list pages.

## Task 1: Add Traditional Chinese Terminology Guard

**Files:**
- Create: `tests/ProjectManager.Tests/Web/TraditionalChineseTerminologyTests.cs`
- Modify: affected `.cshtml`, `.cs`, and `.js` UI strings found by the test

- [ ] **Step 1: Write the failing terminology test**

Create a test that scans user-facing source files, excludes migrations/generated output, and rejects the agreed mixed Simplified terms:

```csharp
using FluentAssertions;

namespace ProjectManager.Tests.Web;

public sealed class TraditionalChineseTerminologyTests
{
    private static readonly string[] ForbiddenTerms =
    [
        "当前", "确定", "推进", "颜色", "系统", "用户", "数据", "项目", "导入", "导出", "打印"
    ];

    [Fact]
    public void User_facing_source_uses_traditional_chinese_as_the_canonical_text()
    {
        var root = RepositoryRoot();
        var files = Directory.EnumerateFiles(
                Path.Combine(root, "src", "ProjectManager.Web"),
                "*.*",
                SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase)
                           || path.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}lib{Path.DirectorySeparatorChar}"));

        var violations = files
            .SelectMany(path => ForbiddenTerms
                .Where(term => File.ReadAllText(path).Contains(term, StringComparison.Ordinal))
                .Select(term => $"{Path.GetRelativePath(root, path)}: {term}"))
            .ToArray();

        violations.Should().BeEmpty();
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ProjectManager.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Cannot locate ProjectManager.sln.");
    }
}
```

- [ ] **Step 2: Run the test and confirm the current mixed terminology fails**

```powershell
$ErrorActionPreference = 'Stop'
.\scripts\dotnet.ps1 test tests\ProjectManager.Tests\ProjectManager.Tests.csproj -c Release --filter TraditionalChineseTerminologyTests
```

Expected: FAIL with paths containing one or more forbidden mixed terms.

- [ ] **Step 3: Replace hard-coded interface strings with the canonical terms**

Use the terminology table from the approved design. Do not change dynamic project/customer/vendor/person names. Add `data-language-preserve` to dynamic values rendered inside pages processed by the language middleware.

- [ ] **Step 4: Extend language conversion regression coverage**

Modify `HtmlLanguageConverterTests.cs` and `DisplayLanguageMiddlewareTests.cs` with a fixture containing a Traditional UI label plus a preserved dynamic name:

```csharp
const string html = "<button title=\"儲存專案\">儲存</button><span data-language-preserve>測試项目 A</span>";
var converted = converter.ToSimplified(html);
converted.Should().Contain("保存项目");
converted.Should().Contain("測試项目 A");
```

- [ ] **Step 5: Run terminology and language tests**

```powershell
$ErrorActionPreference = 'Stop'
.\scripts\dotnet.ps1 test tests\ProjectManager.Tests\ProjectManager.Tests.csproj -c Release --filter "TraditionalChineseTerminologyTests|HtmlLanguageConverterTests|DisplayLanguageMiddlewareTests|ChineseInterfaceSmokeTests"
```

Expected: PASS.

- [ ] **Step 6: Record a no-commit checkpoint**

```powershell
$ErrorActionPreference = 'Stop'
git diff --check
git status --short
```

Expected: only terminology-related files and the plan/spec are modified. Do not commit unless the user separately authorizes Git history changes.

## Task 2: Establish the Four-Layer CSS Structure

**Files:**
- Create: `src/ProjectManager.Web/wwwroot/css/base.css`
- Create: `src/ProjectManager.Web/wwwroot/css/components.css`
- Create: `src/ProjectManager.Web/wwwroot/css/pages.css`
- Create: `src/ProjectManager.Web/wwwroot/css/themes.css`
- Modify: `src/ProjectManager.Web/wwwroot/css/site.css`
- Modify: `src/ProjectManager.Web/Pages/Shared/_Layout.cshtml`
- Create: `tests/ProjectManager.Tests/Web/FrontendAssetStructureTests.cs`

- [ ] **Step 1: Write a failing asset-order test**

The test must assert that `_Layout.cshtml` loads the four layers in order and no longer loads `site.css` as the active stylesheet:

```csharp
[Fact]
public void Layout_loads_four_css_layers_in_dependency_order()
{
    var layout = ReadRepositoryFile("src", "ProjectManager.Web", "Pages", "Shared", "_Layout.cshtml");
    var baseIndex = layout.IndexOf("~/css/base.css", StringComparison.Ordinal);
    var componentsIndex = layout.IndexOf("~/css/components.css", StringComparison.Ordinal);
    var pagesIndex = layout.IndexOf("~/css/pages.css", StringComparison.Ordinal);
    var themesIndex = layout.IndexOf("~/css/themes.css", StringComparison.Ordinal);

    baseIndex.Should().BeGreaterThan(-1);
    baseIndex.Should().BeLessThan(componentsIndex);
    componentsIndex.Should().BeLessThan(pagesIndex);
    pagesIndex.Should().BeLessThan(themesIndex);
    layout.Should().NotContain("href=\"~/css/site.css\"");
}
```

- [ ] **Step 2: Run the asset test and verify failure**

```powershell
$ErrorActionPreference = 'Stop'
.\scripts\dotnet.ps1 test tests\ProjectManager.Tests\ProjectManager.Tests.csproj -c Release --filter FrontendAssetStructureTests
```

Expected: FAIL because the four files are not loaded.

- [ ] **Step 3: Move CSS rules by responsibility**

Use these exact top-level file headers. Do not wrap the files in CSS cascade `@layer`: Bootstrap is currently unlayered, so layered application rules would lose precedence to Bootstrap and change existing behavior.

```css
/* base.css: tokens, document defaults, application shell, and global controls. */
```

```css
/* components.css: reusable cards, filters, tables, forms, badges, and shared controls. */
```

```css
/* pages.css: route-specific layouts, responsive rules, Gantt print, and report print styles. */
```

```css
/* themes.css: visual themes, effect levels, motion styles, and decorative enhancements. */
```

Move existing selectors without duplicating them. Keep `site.css` with only a compatibility comment explaining the new files; do not delete it.

- [ ] **Step 4: Update the shared layout links**

Replace the single custom stylesheet link with:

```html
<link rel="stylesheet" href="~/css/base.css" asp-append-version="true" />
<link rel="stylesheet" href="~/css/components.css" asp-append-version="true" />
<link rel="stylesheet" href="~/css/pages.css" asp-append-version="true" />
<link rel="stylesheet" href="~/css/themes.css" asp-append-version="true" />
```

- [ ] **Step 5: Update CSS-based tests to read the correct layer**

Replace direct `site.css` assumptions in `UiEffectsAssetTests.cs`, `GanttUiAssetTests.cs`, and `ProjectUiRegressionTests.cs` with a helper that concatenates the four new files in layout order.

- [ ] **Step 6: Run CSS structure and UI asset tests**

```powershell
$ErrorActionPreference = 'Stop'
.\scripts\dotnet.ps1 test tests\ProjectManager.Tests\ProjectManager.Tests.csproj -c Release --filter "FrontendAssetStructureTests|UiEffectsAssetTests|GanttUiAssetTests|ProjectUiRegressionTests"
```

Expected: PASS.

## Task 3: Split JavaScript and Add Selector-Driven Loading

**Files:**
- Modify: `src/ProjectManager.Web/wwwroot/js/site.js`
- Create: JavaScript modules listed in the File Structure
- Modify: `src/ProjectManager.Web/Pages/Shared/_Layout.cshtml`
- Modify: `tests/ProjectManager.Tests/Web/FrontendAssetStructureTests.cs`
- Modify: asset tests that currently read only `site.js`

- [ ] **Step 1: Add a failing module-loader test**

Assert that `site.js` is a module loader, imports Shell unconditionally, and conditionally imports data, settings, Gantt, rich-text, and effect modules only when matching selectors/classes exist.

```csharp
[Fact]
public void Site_script_loads_page_components_on_demand()
{
    var script = ReadRepositoryFile("src", "ProjectManager.Web", "wwwroot", "js", "site.js");
    script.Should().Contain("import { initShell }");
    script.Should().Contain("document.querySelector(\"[data-column-manager]\")");
    script.Should().Contain("import(\"./components/data-table.js\")");
    script.Should().Contain("document.querySelector(\"[data-gantt-editor]\")");
    script.Should().Contain("import(\"./components/gantt-editor.js\")");
}
```

- [ ] **Step 2: Run and verify the new test fails**

```powershell
$ErrorActionPreference = 'Stop'
.\scripts\dotnet.ps1 test tests\ProjectManager.Tests\ProjectManager.Tests.csproj -c Release --filter FrontendAssetStructureTests
```

Expected: FAIL.

- [ ] **Step 3: Move each current initializer into its target module**

Each file exports one idempotent initializer:

```javascript
let initialized = false;

export function initDataTables() {
  if (initialized) return;
  initialized = true;
  document.querySelectorAll("[data-column-manager]").forEach(initializeColumnManager);
  document.querySelectorAll("[data-row-spacing]").forEach(initializeRowSpacing);
}
```

Move the current column-manager body into the private `initializeColumnManager(manager)` function and the current row-spacing body into `initializeRowSpacing(manager)` without changing storage keys or DOM behavior.

Do not change observable behavior while moving functions. Preserve `prefers-reduced-motion`, pointer capability checks, ARIA updates, and localStorage compatibility until Batch 2 replaces table persistence.

- [ ] **Step 4: Replace `site.js` with the loader**

Implement the entry pattern:

```javascript
import { initShell } from "./core/shell.js";

initShell();

const jobs = [];
if (document.querySelector("[data-bulk-form], [data-bulk-checkbox]")) {
  jobs.push(import("./components/bulk-actions.js").then((module) => module.initBulkActions()));
}
if (document.querySelector("[data-column-manager], [data-row-spacing]")) {
  jobs.push(import("./components/data-table.js").then((module) => module.initDataTables()));
}
if (document.querySelector("[data-gantt-editor]")) {
  jobs.push(import("./components/gantt-editor.js").then((module) => module.initGanttEditor()));
}
if (document.body.matches(".ui-effects-medium, .ui-effects-high")) {
  jobs.push(import("./effects/ui-effects.js").then((module) => module.initUiEffects()));
}

await Promise.all(jobs);
```

Also conditionally load `filter-drawer.js` for `[data-filter-drawer]`, `detail-tabs.js` for `[data-detail-tabs]`, `rich-text.js` for `[data-rich-text]`, `settings.js` for `[data-theme-option], [data-motion-option], [data-global-font-picker]`, and `feedback.js` for `[data-processing-label], [data-toast]`.

- [ ] **Step 5: Load the entry as an ES module**

Update `_Layout.cshtml`:

```html
<script type="module" src="~/js/site.js" asp-append-version="true"></script>
```

Keep jQuery and Bootstrap before it for existing validation and Bootstrap components.

- [ ] **Step 6: Run JavaScript syntax checks for every project script**

```powershell
$ErrorActionPreference = 'Stop'
Get-ChildItem -LiteralPath 'src\ProjectManager.Web\wwwroot\js' -Recurse -Filter '*.js' |
    ForEach-Object { node --check $_.FullName }
```

Expected: all commands exit 0.

- [ ] **Step 7: Run affected asset and smoke tests**

```powershell
$ErrorActionPreference = 'Stop'
.\scripts\dotnet.ps1 test tests\ProjectManager.Tests\ProjectManager.Tests.csproj -c Release --filter "FrontendAssetStructureTests|UiEffectsAssetTests|ProjectUiRegressionTests|DashboardSmokeTests|SystemSettingsPageTests"
```

Expected: PASS.

## Task 4: Reorganize Navigation and Add Accessible Tooltips

**Files:**
- Modify: `src/ProjectManager.Web/Pages/Shared/_Layout.cshtml`
- Modify: `src/ProjectManager.Web/wwwroot/js/core/shell.js`
- Modify: `src/ProjectManager.Web/wwwroot/css/base.css`
- Modify: `src/ProjectManager.Web/wwwroot/css/components.css`
- Create/Modify: navigation regression tests

- [ ] **Step 1: Write failing navigation structure tests**

Assert that the layout contains the three group labels, the sidebar button exposes `aria-expanded`, and each navigation link has `data-nav-label`.

- [ ] **Step 2: Run the tests and verify failure**

```powershell
$ErrorActionPreference = 'Stop'
.\scripts\dotnet.ps1 test tests\ProjectManager.Tests\ProjectManager.Tests.csproj -c Release --filter "ProjectUiRegressionTests|ChineseInterfaceSmokeTests"
```

Expected: FAIL on the new assertions.

- [ ] **Step 3: Group links without changing authorization rules**

Use this semantic structure around existing role checks:

```html
<section class="nav-section" aria-labelledby="nav-work-label">
    <div class="nav-section-label" id="nav-work-label">業務工作</div>
    <ul class="nav flex-column">
        <li class="nav-item">
            <a class="nav-link" asp-page="/Index" data-nav-label="首頁"><span>首頁</span></a>
        </li>
        <li class="nav-item">
            <a class="nav-link" asp-page="/Workbench/Projects/Index" data-nav-label="我的專案"><span>我的專案</span></a>
        </li>
    </ul>
</section>
```

Repeat for `報表分析` and `系統管理`. Do not expose a group when the current role has no visible items.

- [ ] **Step 4: Replace inline sidebar events with data hooks**

```html
<button class="app-sidebar-toggle" type="button"
        data-sidebar-toggle aria-label="收合側邊導覽" aria-expanded="true">
```

`shell.js` must update the label and expanded state after each toggle and expose compatibility wrappers only for remaining legacy markup.

- [ ] **Step 5: Implement focusable collapsed tooltips**

Each link uses `data-nav-label="專案總覽"`. CSS renders the tooltip for collapsed sidebar Hover/Focus; JavaScript closes it on Escape and ensures only one tooltip is active.

- [ ] **Step 6: Run navigation tests and a visual regression capture**

```powershell
$ErrorActionPreference = 'Stop'
.\scripts\dotnet.ps1 test tests\ProjectManager.Tests\ProjectManager.Tests.csproj -c Release --filter "ProjectUiRegressionTests|ChineseInterfaceSmokeTests|AuthSmokeTests"
```

Expected: PASS.

## Task 5: Introduce Business-Semantic Status Colors

**Files:**
- Create: `src/ProjectManager.Web/Services/ProjectVisualStateResolver.cs`
- Create: `tests/ProjectManager.Tests/Services/ProjectVisualStateResolverTests.cs`
- Modify: `src/ProjectManager.Web/Pages/Shared/_ProgressRiskBadge.cshtml`
- Modify: `src/ProjectManager.Web/Pages/Shared/_PercentBar.cshtml`
- Modify: `src/ProjectManager.Web/Pages/Shared/_StatusBadge.cshtml`
- Modify: `src/ProjectManager.Web/wwwroot/css/components.css`

- [ ] **Step 1: Write failing resolver tests**

Cover these rules:

```csharp
[Theory]
[InlineData("closed", true, "complete")]
[InlineData("blocked", false, "blocked")]
[InlineData("waiting", false, "waiting")]
[InlineData("in-progress", false, "active")]
[InlineData("custom-state", false, "neutral")]
public void Status_semantics_are_derived_from_business_state(
    string code, bool isClosed, string expected)
{
    ProjectVisualStateResolver.ResolveStatus(code, isClosed).CssKey.Should().Be(expected);
}
```

Also cover collection lag and stale updates. Do not classify a project as dangerous only because progress is below 30%.

- [ ] **Step 2: Run and verify failure**

```powershell
$ErrorActionPreference = 'Stop'
.\scripts\dotnet.ps1 test tests\ProjectManager.Tests\ProjectManager.Tests.csproj -c Release --filter ProjectVisualStateResolverTests
```

Expected: FAIL because the resolver does not exist.

- [ ] **Step 3: Implement the pure resolver**

Define immutable results:

```csharp
public sealed record ProjectVisualState(string CssKey, string Label, string Description);
```

Normalize status codes with `Trim().ToLowerInvariant()`. Map known closed/completed, blocked, waiting/pending, and active codes; unknown codes return neutral. Risk resolution considers closed state, collection lag of at least 25 points, and configurable stale-update age.

- [ ] **Step 4: Update partials**

Progress bars use primary color for 0–99 and success for 100. Status badges use semantic classes plus the configured color as a small accent. Risk badge text comes from the resolver.

- [ ] **Step 5: Run resolver and UI tests**

```powershell
$ErrorActionPreference = 'Stop'
.\scripts\dotnet.ps1 test tests\ProjectManager.Tests\ProjectManager.Tests.csproj -c Release --filter "ProjectVisualStateResolverTests|ProjectUiRegressionTests|DashboardSmokeTests|OpenProjectReportSmokeTests"
```

Expected: PASS.

## Task 6: Add the Shared Empty-State Component

**Files:**
- Create: `src/ProjectManager.Web/Pages/Shared/EmptyStateViewModel.cs`
- Create: `src/ProjectManager.Web/Pages/Shared/_EmptyState.cshtml`
- Modify: priority list pages and shared table partials
- Modify: `src/ProjectManager.Web/wwwroot/css/components.css`
- Create/Modify: web smoke tests

- [ ] **Step 1: Write failing component and page tests**

Test that `_EmptyState.cshtml` supports a title, description, optional primary action, and clear-filter action. Test at least project list and open-project report markup for use of the shared partial.

- [ ] **Step 2: Run tests and verify failure**

```powershell
$ErrorActionPreference = 'Stop'
.\scripts\dotnet.ps1 test tests\ProjectManager.Tests\ProjectManager.Tests.csproj -c Release --filter "ProjectUiRegressionTests|OpenProjectReportSmokeTests"
```

Expected: FAIL.

- [ ] **Step 3: Implement the ViewModel**

```csharp
public sealed record EmptyStateViewModel(
    string Title,
    string Description,
    string Icon = "inbox",
    string? PrimaryActionText = null,
    string? PrimaryActionPage = null,
    IReadOnlyDictionary<string, string?>? PrimaryActionRouteValues = null,
    string? ClearActionText = null,
    string? ClearActionPage = null,
    IReadOnlyDictionary<string, string?>? ClearActionRouteValues = null);
```

- [ ] **Step 4: Implement the partial with accessible actions**

Use an `<section class="empty-state" aria-live="polite">`, a decorative icon with `aria-hidden="true"`, an `h2`, descriptive text, and only render actions when their text and target page are both present.

- [ ] **Step 5: Replace bespoke empty rows**

Apply to projects, workbench projects, planning projects, open-project report, settlements, maintenance orders, archives, and users. Use “清除篩選” when filters are active; use “新增…” only when the role already has create permission.

- [ ] **Step 6: Run smoke tests**

```powershell
$ErrorActionPreference = 'Stop'
.\scripts\dotnet.ps1 test tests\ProjectManager.Tests\ProjectManager.Tests.csproj -c Release --filter "DashboardSmokeTests|OpenProjectReportSmokeTests|SettlementPageSmokeTests|AuthSmokeTests|ProjectUiRegressionTests"
```

Expected: PASS.

## Task 7: Restrain Dense-Data Visual Effects

**Files:**
- Modify: `src/ProjectManager.Web/wwwroot/css/components.css`
- Modify: `src/ProjectManager.Web/wwwroot/css/pages.css`
- Modify: `src/ProjectManager.Web/wwwroot/css/themes.css`
- Modify: `src/ProjectManager.Web/wwwroot/js/effects/ui-effects.js`
- Modify: visual regression tests

- [ ] **Step 1: Add regression assertions for dense surfaces**

Assert that `.data-work-surface`, `.data-list-card`, and `.data-table-wrap` use restrained shadows and opaque-enough backgrounds in both themes, and that table rows are not tilt targets.

- [ ] **Step 2: Run and verify the new assertions fail**

```powershell
$ErrorActionPreference = 'Stop'
.\scripts\dotnet.ps1 test tests\ProjectManager.Tests\ProjectManager.Tests.csproj -c Release --filter "UiEffectsAssetTests|ProjectUiRegressionTests"
```

Expected: FAIL.

- [ ] **Step 3: Reduce visual competition**

Use one subtle elevation token for dense work areas, remove strong gradient borders from tables, and restrict 3D tilt to dashboard/module cards. Table controls retain short Hover/Focus transitions only.

- [ ] **Step 4: Capture primary pages**

Run the existing visual regression suite against the local app and inspect admin projects, open-project report, project details, and maintenance orders. Confirm table text remains the strongest visual element.

- [ ] **Step 5: Run asset and visual tests**

```powershell
$ErrorActionPreference = 'Stop'
.\scripts\dotnet.ps1 test tests\ProjectManager.Tests\ProjectManager.Tests.csproj -c Release --filter "UiEffectsAssetTests|ProjectUiRegressionTests|GanttUiAssetTests"
```

Expected: PASS.

## Task 8: Batch 1 Documentation and Full Verification

**Files:**
- Modify: `docs/架构说明书.md`
- Modify: `docs/设计文档.md`
- Modify: `docs/维护手册.md`
- Modify: this plan checkbox state during execution

- [ ] **Step 1: Document the new frontend asset map and terminology rule**

Record the four CSS layers, JavaScript loader/module responsibilities, Traditional-Chinese canonical source rule, dynamic data preservation rule, and no-database-change statement.

- [ ] **Step 2: Run every JavaScript syntax check**

```powershell
$ErrorActionPreference = 'Stop'
Get-ChildItem -LiteralPath 'src\ProjectManager.Web\wwwroot\js' -Recurse -Filter '*.js' |
    ForEach-Object { node --check $_.FullName }
```

Expected: exit 0.

- [ ] **Step 3: Run Release build and full test suite**

```powershell
$ErrorActionPreference = 'Stop'
.\scripts\dotnet.ps1 build ProjectManager.sln -c Release --no-restore
.\scripts\dotnet.ps1 test tests\ProjectManager.Tests\ProjectManager.Tests.csproj -c Release --no-build --logger 'console;verbosity=minimal'
```

Expected: build succeeds with 0 errors and all tests pass.

- [ ] **Step 4: Run diff checks**

```powershell
$ErrorActionPreference = 'Stop'
git diff --check
git status --short
```

Expected: no whitespace errors; no migration/model/database changes; the pre-existing `project- manager.zip` remains untouched.

- [ ] **Step 5: Stop at the Batch 1 checkpoint**

Report build/test/visual evidence and the exact changed files before starting Batch 2. Do not commit, stage, or push without separate user authorization.

## Self-Review

- Spec coverage: terminology, modular JS, four-layer CSS, navigation groups, Tooltip, semantic colors, empty states, restrained effects, and verification all have tasks.
- Database boundary: no model, DbContext, migration, or SQL dictionary task exists in Batch 1.
- Compatibility: `site.js` and `site.css` remain present.
- Execution boundary: Batch 2 starts only after the Batch 1 verification checkpoint.
- Git boundary: commands inspect changes only; no commit or staging step is authorized.
