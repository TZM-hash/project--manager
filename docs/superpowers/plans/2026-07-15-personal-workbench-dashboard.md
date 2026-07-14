# Personal Workbench Dashboard Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the statistics-heavy home page with a permission-aware personal workbench that highlights one conclusion, one action, and four actionable project queues.

**Architecture:** Add a focused query service returning immutable projections. The Razor Page obtains identity context and renders a single hero conclusion, compact counters, bounded lists, and role-safe links.

**Tech Stack:** ASP.NET Core Razor Pages, EF Core 9, xUnit, FluentAssertions, Playwright.

---

## Task 1: Personal Workbench Query Contract

**Files:**
- Create: `src/ProjectManager.Web/Services/Workbench/PersonalWorkbenchService.cs`
- Create: `tests/ProjectManager.Tests/Services/PersonalWorkbenchServiceTests.cs`
- Modify: `src/ProjectManager.Web/Program.cs`

- [ ] Write failing tests for project-staff isolation, overdue, pending, next-14-day nodes, stale-30-day projects and conclusion priority.
- [ ] Run `dotnet test ... --filter PersonalWorkbenchServiceTests` and verify failures are caused by missing service.
- [ ] Implement `PersonalWorkbenchSnapshot`, `WorkbenchProjectItem`, `WorkbenchNodeItem` and `PersonalWorkbenchService` using `TimeProvider`, `AsNoTracking`, projection and `Take(5)`.
- [ ] Register the service and rerun tests to green.

## Task 2: Replace Home Page UI

**Files:**
- Modify: `src/ProjectManager.Web/Pages/Index.cshtml.cs`
- Modify: `src/ProjectManager.Web/Pages/Index.cshtml`
- Modify: `src/ProjectManager.Web/wwwroot/css/pages.css`
- Modify: `tests/ProjectManager.Tests/Web/DashboardSmokeTests.cs`
- Modify: `tests/ProjectManager.Tests/Web/ProjectUiRegressionTests.cs`

- [ ] Add failing smoke and asset assertions for one hero conclusion, one primary action, compact four-counter row and four list sections.
- [ ] Replace global aggregate queries with `PersonalWorkbenchService`; keep anonymous login state.
- [ ] Render actionable empty states and role-safe links without consecutive metric-card groups.
- [ ] Add responsive workbench CSS and visible focus states.
- [ ] Run dashboard and UI regression tests.

## Task 3: Accessibility and Batch Documentation

**Files:**
- Modify: `tests/visual/project-ui.spec.js`
- Create: `docs/2026-07-15-第三批个人工作台说明.md`

- [ ] Add keyboard, 720px effective width and primary-action contrast tests.
- [ ] Document metric definitions, permissions, empty states and rollback.
- [ ] Run JavaScript checks, Release build and complete tests.
- [ ] Commit with `feat: 升级个人工作台首页`.
