# Gantt Collaboration and Concurrency Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add milestone, owner, dependency, planned/actual dates and overdue behavior to Gantt while protecting project, Gantt and collaboration edits with row-version concurrency.

**Architecture:** Extend the existing Gantt aggregate, add a collaboration timeline entity/service, and carry Base64 row versions through Razor forms. Services catch concurrency exceptions and return explicit conflict results without overwriting database state.

**Tech Stack:** ASP.NET Core Razor Pages, EF Core 9 SQL Server rowversion, xUnit, FluentAssertions, Playwright.

---

## Task 1: Schema and Migration

**Files:**
- Modify: `Models/Project.cs`, `Models/ProjectGanttPlan.cs`, `Models/ProjectGanttTask.cs`, `Models/ApplicationUser.cs`
- Create: `Models/ProjectCollaborationRecord.cs`
- Modify: `Data/ApplicationDbContext.cs`
- Modify: `tests/ProjectManager.Tests/Data/ApplicationDbContextTests.cs`
- Create: `Migrations/*_EnhanceGanttCollaborationConcurrency.cs`

- [ ] Write failing model tests for rowversion mappings, collaboration indexes/relationships and Gantt fields.
- [ ] Implement model and Fluent API mappings.
- [ ] Generate Migration and assert it only adds approved columns/table/indexes.
- [ ] Run data and migration regression tests.

## Task 2: Project Concurrency

**Files:**
- Modify project edit/progress input models, services and Razor forms.
- Modify: `tests/ProjectManager.Tests/Services/ProjectMaintenanceServiceTests.cs`
- Modify: `tests/ProjectManager.Tests/Services/WorkbenchProjectServiceTests.cs`

- [ ] Write failing tests simulating two contexts editing the same project.
- [ ] Add Base64 RowVersion to requests and attach original concurrency value before save.
- [ ] Catch `DbUpdateConcurrencyException` and return a localized conflict result.
- [ ] Preserve user input and render reload links on conflict.
- [ ] Run project service and page tests.

## Task 3: Enhanced Gantt Aggregate

**Files:**
- Modify: `Services/ProjectGanttService.cs`
- Modify: `Pages/Shared/ProjectGanttPanelModel.cs`
- Modify: `Pages/Shared/_ProjectGanttPanel.cshtml`
- Modify: `wwwroot/js/components/gantt-editor.js`
- Modify: `tests/ProjectManager.Tests/Services/ProjectGanttServiceTests.cs`
- Modify: `tests/ProjectManager.Tests/Web/GanttUiAssetTests.cs`

- [ ] Write failing tests for milestone date equality, owner, dependency validation, cycles, actual dates, overdue and stale RowVersion conflict.
- [ ] Extend input models and update-in-place save logic protected by plan RowVersion.
- [ ] Add owner selector, milestone toggle, predecessor selector and actual date controls.
- [ ] Render milestone diamonds, dependency labels, owner, overdue badges and planned/actual variance.
- [ ] Update Excel/print output and run Gantt tests.

## Task 4: Collaboration Timeline

**Files:**
- Create: `Services/ProjectCollaborationService.cs`
- Create: `Pages/Shared/_ProjectCollaborationPanel.cshtml`
- Modify project detail PageModels/pages.
- Create: `tests/ProjectManager.Tests/Services/ProjectCollaborationServiceTests.cs`

- [ ] Write failing tests for add/edit/delete permissions, user isolation and stale RowVersion conflicts.
- [ ] Implement safe content normalization, paging and conflict results.
- [ ] Add project-detail handlers and accessible timeline controls.
- [ ] Run collaboration and details tests.

## Task 5: Verification and Commit

**Files:**
- Modify visual spec and architecture/data dictionary docs.
- Create: `docs/2026-07-15-第四批甘特并发说明.md`

- [ ] Run migration script generation, JS checks, Release build, complete tests and Playwright discovery/execution where environment permits.
- [ ] Inspect migration and `git diff --check`.
- [ ] Commit with `feat: 增强甘特协作与并发保护`.
