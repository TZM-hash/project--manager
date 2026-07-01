# Project Manager SaaS UI Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restyle the existing ASP.NET Core Razor Pages project manager into a bright, professional SaaS-style interface without changing business behavior.

**Architecture:** Keep the current Razor Pages app and Bootstrap dependency. Implement the redesign with shared CSS design tokens and targeted markup updates in the existing pages and partials, so services, models, routes, and database migrations remain unchanged.

**Tech Stack:** ASP.NET Core Razor Pages, Bootstrap, CSS custom properties, existing xUnit smoke tests.

---

## Scope Check

The approved spec is one UI redesign across one existing application shell. It does not introduce independent subsystems, new persistence, or new services, so it can be implemented as one focused plan.

## File Structure

- Modify `src/ProjectManager.Web/Pages/Shared/_Layout.cshtml`: product-style app shell and navigation classes.
- Modify `src/ProjectManager.Web/wwwroot/css/site.css`: design tokens, nav, page headers, cards, filters, tables, forms, details, status components, responsive and print styles.
- Modify `src/ProjectManager.Web/Pages/Index.cshtml`: SaaS dashboard header, metrics, and role entrance modules.
- Modify `src/ProjectManager.Web/Pages/Admin/Projects/Index.cshtml`: page header, filter panel, status badges, and data table classes.
- Modify `src/ProjectManager.Web/Pages/Admin/Projects/_ProjectForm.cshtml`: sectioned form layout and editable purchase table styling.
- Modify `src/ProjectManager.Web/Pages/Admin/Projects/Details.cshtml`: detail page structure and shared detail/table classes.
- Modify `src/ProjectManager.Web/Pages/Workbench/Projects/Index.cshtml`: workbench list with shared filter/table patterns.
- Modify `src/ProjectManager.Web/Pages/Workbench/Projects/Details.cshtml`: project summary, status timeline, and purchase table structure.
- Modify `src/ProjectManager.Web/Pages/Shared/_StatusBadge.cshtml`: ensure badge class supports configured color while matching new style.
- Modify `src/ProjectManager.Web/Pages/Shared/_StatusTimeline.cshtml`: add polished timeline structure while keeping status logic unchanged.
- Modify report and settlement `.cshtml` pages only where needed to apply shared page/filter/table classes.
- Modify account pages only lightly if the shared account shell already supports the new design.

## Task 1: Shared App Shell And Design Tokens

**Files:**
- Modify `src/ProjectManager.Web/Pages/Shared/_Layout.cshtml`
- Modify `src/ProjectManager.Web/wwwroot/css/site.css`

- [ ] **Step 1: Update layout classes**

Change the body and nav markup to use `app-shell`, `app-navbar`, `app-brand`, `app-nav`, and `app-main` classes while preserving existing role checks and links.

- [ ] **Step 2: Replace global CSS foundation**

Define CSS custom properties for background, surface, text, border, accent, status colors, radius, and shadow. Restyle Bootstrap buttons, inputs, navbar, focus states, containers, and page-level spacing through these tokens.

- [ ] **Step 3: Build check**

Run:

```powershell
.\scripts\dotnet.ps1 build ProjectManager.sln
```

Expected: build succeeds with no errors.

## Task 2: Dashboard And Navigation Surface

**Files:**
- Modify `src/ProjectManager.Web/Pages/Index.cshtml`
- Modify `src/ProjectManager.Web/wwwroot/css/site.css`

- [ ] **Step 1: Restyle dashboard markup**

Convert the current dashboard into `page-header`, `metric-grid`, `metric-card`, `module-grid`, and `module-card` sections. Keep all model properties and role checks unchanged.

- [ ] **Step 2: Add dashboard CSS**

Style metric cards, module cards, dashboard metadata, and action rows with the approved bright SaaS system.

- [ ] **Step 3: Run dashboard smoke tests**

Run:

```powershell
.\scripts\dotnet.ps1 test ProjectManager.sln --filter DashboardSmokeTests
```

Expected: dashboard smoke tests pass.

## Task 3: Project Data Workspace Screens

**Files:**
- Modify `src/ProjectManager.Web/Pages/Admin/Projects/Index.cshtml`
- Modify `src/ProjectManager.Web/Pages/Admin/Projects/Details.cshtml`
- Modify `src/ProjectManager.Web/Pages/Workbench/Projects/Index.cshtml`
- Modify `src/ProjectManager.Web/Pages/Workbench/Projects/Details.cshtml`
- Modify `src/ProjectManager.Web/Pages/Shared/_StatusBadge.cshtml`
- Modify `src/ProjectManager.Web/Pages/Shared/_StatusTimeline.cshtml`
- Modify `src/ProjectManager.Web/wwwroot/css/site.css`

- [ ] **Step 1: Apply page headers and filter panels**

Update project and workbench lists to use shared page header and filter panel classes. Preserve existing form fields, model bindings, and routes.

- [ ] **Step 2: Apply data table style**

Wrap wide tables in `table-responsive data-table-wrap`, add `data-table` to tables, align numeric columns with `numeric`, and keep actions compact.

- [ ] **Step 3: Improve detail pages**

Use `detail-panel`, `detail-grid`, and `summary-strip` classes for project details. Keep status timeline and purchase data unchanged.

- [ ] **Step 4: Polish status components**

Keep configured status colors but improve badge padding, border, text weight, and timeline anatomy.

- [ ] **Step 5: Run project/workbench tests**

Run:

```powershell
.\scripts\dotnet.ps1 test ProjectManager.sln --filter "DashboardSmokeTests|ChineseInterfaceSmokeTests"
```

Expected: smoke tests pass.

## Task 4: Project Form And Dense Editable Tables

**Files:**
- Modify `src/ProjectManager.Web/Pages/Admin/Projects/_ProjectForm.cshtml`
- Modify `src/ProjectManager.Web/wwwroot/css/site.css`

- [ ] **Step 1: Split form into sections**

Group fields into `form-section` blocks for basic information, status/progress/finance, progress description, and purchase requests. Preserve every `asp-for`, select list, hidden input, and validation summary.

- [ ] **Step 2: Style editable purchase table**

Use compact controls, stable column spacing, and horizontal overflow for the purchase request table.

- [ ] **Step 3: Build check**

Run:

```powershell
.\scripts\dotnet.ps1 build ProjectManager.sln
```

Expected: build succeeds with no errors.

## Task 5: Reports, Settlements, And Account Surfaces

**Files:**
- Modify `src/ProjectManager.Web/Pages/Settlements/Index.cshtml`
- Modify `src/ProjectManager.Web/Pages/Settlements/Create.cshtml`
- Modify `src/ProjectManager.Web/Pages/Settlements/Details.cshtml`
- Modify `src/ProjectManager.Web/Pages/Reports/OpenProjects/Index.cshtml`
- Modify `src/ProjectManager.Web/Pages/Reports/OpenProjects/Statistics.cshtml`
- Modify shared settlement/report table partials if needed.
- Modify `src/ProjectManager.Web/wwwroot/css/site.css`

- [ ] **Step 1: Apply shared page and table classes**

Update settlement and report pages to use the shared page header, filter panel, action group, and data table classes while preserving export and print links.

- [ ] **Step 2: Preserve print CSS**

Keep print styles plain, high contrast, and not dependent on SaaS card styling.

- [ ] **Step 3: Run report and settlement tests**

Run:

```powershell
.\scripts\dotnet.ps1 test ProjectManager.sln --filter "SettlementPageSmokeTests|OpenProjectReportSmokeTests"
```

Expected: report and settlement smoke tests pass.

## Task 6: Final Verification

**Files:**
- All modified UI files.

- [ ] **Step 1: Run full test suite**

Run:

```powershell
.\scripts\dotnet.ps1 test ProjectManager.sln
```

Expected: all tests pass.

- [ ] **Step 2: Run build**

Run:

```powershell
.\scripts\dotnet.ps1 build ProjectManager.sln
```

Expected: build succeeds with no errors.

- [ ] **Step 3: Run app for visual smoke check**

Run:

```powershell
.\scripts\dotnet.ps1 run --project src/ProjectManager.Web/ProjectManager.Web.csproj --urls http://localhost:5277
```

Expected: app starts and primary pages render with the new SaaS UI.

- [ ] **Step 4: Inspect git diff**

Run:

```powershell
git diff --stat
git status --short
```

Expected: only intended UI files and this plan are modified.

## Self-Review

Spec coverage:

- Shared layout and navigation are covered by Task 1.
- Home dashboard is covered by Task 2.
- Project list, detail, workbench, status badge, and timeline are covered by Task 3.
- Project form and purchase table are covered by Task 4.
- Report and settlement surfaces are covered by Task 5.
- Build, tests, and visual smoke check are covered by Task 6.

Placeholder scan:

- The plan contains no TBD, TODO, or undefined future work.

Type and route consistency:

- The plan uses existing Razor Page routes, `asp-for` bindings, partials, and model properties. No new backend methods or data types are introduced.
