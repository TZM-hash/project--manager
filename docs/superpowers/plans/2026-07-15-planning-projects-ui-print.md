# Planning Projects UI and Print Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rename planning-project provisional fields, add consistent table display controls and optional information columns, and redesign the single/batch print layouts.

**Architecture:** Keep the existing `PlanningProject` schema unchanged. Reuse the current client-side column manager and row-density persistence, derive extra display values from already-loaded history records, and isolate print styling under planning-specific CSS classes so other reports are unaffected.

**Tech Stack:** ASP.NET Core Razor Pages, C#, Bootstrap, existing vanilla JavaScript data-table controls, xUnit/FluentAssertions, scoped CSS.

---

### Task 1: Lock the requested labels and table contracts

**Files:**
- Modify: `tests/ProjectManager.Tests/Web/ProjectUiRegressionTests.cs`
- Test: `tests/ProjectManager.Tests/Web/ProjectUiRegressionTests.cs`

- [ ] **Step 1: Add failing regression tests for provisional labels**

Assert that all planning-project list, create, edit, import, single-print, and batch-print UI surfaces use `暫定負責人` and `暫定廠商`, and no longer render the old headings in those surfaces.

- [ ] **Step 2: Add failing regression tests for display controls**

Assert that the planning index contains `data-column-manager`, `data-row-spacing`, a stable `planning-projects-table` ID, and optional column keys `latestPeriod`, `historyCount`, and `createdAt`.

- [ ] **Step 3: Add a control-order regression test**

Read all four existing configurable data pages plus the planning page and assert `data-column-manager-toggle` appears before `data-row-spacing-toggle`.

- [ ] **Step 4: Add failing print-structure tests**

Assert that single and batch print pages contain planning-scoped classes such as `planning-print-report`, `planning-print-summary`, `planning-print-description`, and `planning-print-list-table`.

- [ ] **Step 5: Run the targeted tests and verify they fail for the missing labels, controls, columns, order, and print structure**

Run:

```powershell
$ErrorActionPreference = 'Stop'
dotnet test 'tests/ProjectManager.Tests/ProjectManager.Tests.csproj' --no-restore --filter 'FullyQualifiedName~Planning_project'
```

Expected: FAIL because the requested UI contract is not implemented yet.

### Task 2: Rename provisional planning-project fields consistently

**Files:**
- Modify: `src/ProjectManager.Web/Pages/Workbench/PlanningProjects/Index.cshtml`
- Modify: `src/ProjectManager.Web/Pages/Workbench/PlanningProjects/Index.cshtml.cs`
- Modify: `src/ProjectManager.Web/Pages/Workbench/PlanningProjects/Create.cshtml`
- Modify: `src/ProjectManager.Web/Pages/Workbench/PlanningProjects/Create.cshtml.cs`
- Modify: `src/ProjectManager.Web/Pages/Workbench/PlanningProjects/Edit.cshtml`
- Modify: `src/ProjectManager.Web/Pages/Workbench/PlanningProjects/Edit.cshtml.cs`
- Modify: `src/ProjectManager.Web/Pages/Workbench/PlanningProjects/Import.cshtml`
- Modify: `src/ProjectManager.Web/Pages/Workbench/PlanningProjects/Import.cshtml.cs`
- Modify: `src/ProjectManager.Web/Pages/Workbench/PlanningProjects/Print.cshtml`
- Modify: `src/ProjectManager.Web/Pages/Workbench/PlanningProjects/PrintList.cshtml`

- [ ] **Step 1: Rename leader-facing labels**

Change visible `專案負責人` labels, filter summaries, chart titles, form display names, print headings, and import-template headings to `暫定負責人`. Keep internal property names and authorization behavior unchanged.

- [ ] **Step 2: Rename vendor-facing labels**

Change visible `廠商` labels, placeholders, filter summaries, metric/chart headings, form display names, print headings, and import-template headings to `暫定廠商`. Keep the `Vendor` database property unchanged.

- [ ] **Step 3: Preserve import compatibility**

Keep import parsing positional so older spreadsheets remain usable while newly downloaded templates show the provisional wording.

### Task 3: Add planning list display controls and optional columns

**Files:**
- Modify: `src/ProjectManager.Web/Pages/Workbench/PlanningProjects/Index.cshtml`
- Modify: `src/ProjectManager.Web/Pages/Workbench/PlanningProjects/Index.cshtml.cs`

- [ ] **Step 1: Add the standard display-control toolbar**

Place `列管理` before `行間距`, matching the majority convention used by project and report tables.

- [ ] **Step 2: Register a stable configurable table**

Add:

```html
id="planning-projects-table"
data-column-manager-table
data-initial-visible-columns='["item","name","leader","vendor","latestDescription","updatedAt","actions"]'
data-initial-row-density="Normal"
```

- [ ] **Step 3: Mark all headers and cells with column keys**

Fixed columns: `item`, `name`, `actions`. Configurable columns: `leader`, `vendor`, `latestDescription`, `latestPeriod`, `historyCount`, `createdAt`, `updatedAt`.

- [ ] **Step 4: Add derived display helpers**

Add `GetLatestRecordPeriod(PlanningProject project)` that orders history by year/month and returns `yyyy-MM` or `-`. Render `project.HistoryRecords.Count` without schema changes.

- [ ] **Step 5: Keep the empty-state colspan synchronized with the expanded table**

Use the total rendered column count so the empty state spans the complete table.

### Task 4: Standardize display-control order on existing pages

**Files:**
- Modify: `src/ProjectManager.Web/Pages/Admin/MaintenanceOrders/Index.cshtml`

- [ ] **Step 1: Move the maintenance-order column manager before row spacing**

Do not change button behavior, labels, icons, or table data; only normalize left-to-right order to `列管理 → 行間距`.

### Task 5: Redesign the single planning-project print page

**Files:**
- Modify: `src/ProjectManager.Web/Pages/Workbench/PlanningProjects/Print.cshtml`
- Modify: `src/ProjectManager.Web/wwwroot/css/pages.css`

- [ ] **Step 1: Build a report header and metadata hierarchy**

Use a planning-specific report wrapper, a compact title/print-date header, and a summary grid for project name, provisional leader, provisional vendor, created time, and updated time.

- [ ] **Step 2: Promote the latest description to the primary content block**

Render it in a dedicated bordered description card with readable typography and safe rich-text wrapping.

- [ ] **Step 3: Restyle history as a readable report section**

Use a scoped table with a strong header, alternating rows, stable column widths, page-break protection, and rich-text wrapping.

- [ ] **Step 4: Add print-safe color and pagination rules**

Use `print-color-adjust: exact`, avoid breaking metadata cards and rows, and keep the report usable both onscreen and on paper.

### Task 6: Redesign the batch planning-project print page

**Files:**
- Modify: `src/ProjectManager.Web/Pages/Workbench/PlanningProjects/PrintList.cshtml`
- Modify: `src/ProjectManager.Web/wwwroot/css/pages.css`

- [ ] **Step 1: Switch to the wide planning print wrapper**

Render a branded heading, print date, selected-project count, and a concise explanatory subtitle.

- [ ] **Step 2: Expand useful printed information**

Include item number, project name, provisional leader, provisional vendor, latest record period, latest description, and last update time.

- [ ] **Step 3: Improve table readability**

Use a restrained teal header inspired by the reference image, clear borders, alternating pale rows, sensible width constraints, and top-aligned wrapped content.

### Task 7: Verify behavior and visuals

**Files:**
- Test: `tests/ProjectManager.Tests/Web/ProjectUiRegressionTests.cs`
- Test: `tests/ProjectManager.Tests/Web/ImportPageModelTests.cs`
- Test: `tests/visual/project-ui.spec.js` when the local environment supports it

- [ ] **Step 1: Run targeted tests**

```powershell
$ErrorActionPreference = 'Stop'
dotnet test 'tests/ProjectManager.Tests/ProjectManager.Tests.csproj' --no-restore --filter 'FullyQualifiedName~Planning_project|FullyQualifiedName~Import'
```

- [ ] **Step 2: Run the complete test suite**

```powershell
$ErrorActionPreference = 'Stop'
dotnet test 'tests/ProjectManager.Tests/ProjectManager.Tests.csproj' --no-restore
```

- [ ] **Step 3: Verify the running page in the local browser**

Reload `/Workbench/PlanningProjects`, confirm the new labels, toggle an optional column, change row density, and verify the control order.

- [ ] **Step 4: Verify single and batch print DOM/layout**

Open a real planning project print page and a selected-project batch print page, inspect at desktop width, and confirm no horizontal clipping or unreadable rich text.

- [ ] **Step 5: Run final diff checks**

```powershell
$ErrorActionPreference = 'Stop'
git diff --check
git status --short
```

Expected: only planning-project UI/print files, the maintenance toolbar order, scoped CSS, tests, and this execution plan are changed.
