# Project Audit Trail Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add viewable project operation records with field-level and purchase-request-level change details.

**Architecture:** Extend the existing `AuditLog` model with project-specific columns and JSON detail storage. Add focused audit change models and snapshot/diff helpers, then call the audit service from existing admin and workbench save paths. Render the resulting audit entries in both project detail pages.

**Tech Stack:** ASP.NET Core Razor Pages, EF Core, SQL Server migrations, SQLite-backed tests, xUnit, FluentAssertions.

---

### Task 1: Structured Audit Persistence

**Files:**
- Modify: `src/ProjectManager.Web/Models/AuditLog.cs`
- Modify: `src/ProjectManager.Web/Data/ApplicationDbContext.cs`
- Modify: `src/ProjectManager.Web/Services/AuditLogService.cs`
- Test: `tests/ProjectManager.Tests/Services/AuditLogServiceTests.cs`

- [ ] **Step 1: Write failing tests**

Add tests that call a new `LogProjectChangeAsync` API and assert `ProjectId`, `ProjectNumber`, `ChangeSummary`, and `ChangeDetailsJson` are saved.

- [ ] **Step 2: Run targeted tests and verify red**

Run: `.\scripts\dotnet.ps1 test tests\ProjectManager.Tests\ProjectManager.Tests.csproj --filter AuditLogServiceTests`

Expected: fail because the new API and columns do not exist.

- [ ] **Step 3: Implement model and service**

Add nullable project audit fields and serialize detail objects through `System.Text.Json`.

- [ ] **Step 4: Run targeted tests and verify green**

Run the same filtered test command and expect success.

### Task 2: Project Change Diff Builder

**Files:**
- Create: `src/ProjectManager.Web/Services/ProjectAuditChangeBuilder.cs`
- Test: `tests/ProjectManager.Tests/Services/ProjectAuditChangeBuilderTests.cs`

- [ ] **Step 1: Write failing tests**

Cover project field changes, purchase request addition, purchase request update, and purchase request deletion.

- [ ] **Step 2: Run targeted tests and verify red**

Run: `.\scripts\dotnet.ps1 test tests\ProjectManager.Tests\ProjectManager.Tests.csproj --filter ProjectAuditChangeBuilderTests`

Expected: fail because the builder does not exist.

- [ ] **Step 3: Implement builder**

Create snapshot records and diff output records with display labels and before/after values.

- [ ] **Step 4: Run targeted tests and verify green**

Run the same filtered test command and expect success.

### Task 3: Save-Path Audit Logging

**Files:**
- Modify: `src/ProjectManager.Web/Pages/Admin/Projects/Create.cshtml.cs`
- Modify: `src/ProjectManager.Web/Pages/Admin/Projects/Edit.cshtml.cs`
- Modify: `src/ProjectManager.Web/Services/WorkbenchProjectService.cs`
- Test: `tests/ProjectManager.Tests/Services/WorkbenchProjectServiceTests.cs`

- [ ] **Step 1: Write failing workbench test**

Assert updating progress creates an audit log with progress percent and progress description changes.

- [ ] **Step 2: Run targeted test and verify red**

Run: `.\scripts\dotnet.ps1 test tests\ProjectManager.Tests\ProjectManager.Tests.csproj --filter WorkbenchProjectServiceTests`

Expected: fail because workbench updates do not log audit records.

- [ ] **Step 3: Wire audit logging**

Admin create logs initial project fields and added purchases. Admin edit snapshots before mutation and logs generated changes. Admin delete logs deletion. Workbench progress update logs only changed progress fields.

- [ ] **Step 4: Run targeted tests and verify green**

Run the same filtered command and expect success.

### Task 4: Details Page Audit History

**Files:**
- Modify: `src/ProjectManager.Web/Pages/Admin/Projects/Details.cshtml.cs`
- Modify: `src/ProjectManager.Web/Pages/Admin/Projects/Details.cshtml`
- Modify: `src/ProjectManager.Web/Pages/Workbench/Projects/Details.cshtml.cs`
- Modify: `src/ProjectManager.Web/Pages/Workbench/Projects/Details.cshtml`
- Test: existing web smoke tests

- [ ] **Step 1: Add audit query and rendering**

Load project audit logs ordered newest-first and render time, actor, action, summary, and readable detail lines.

- [ ] **Step 2: Run web smoke tests**

Run: `.\scripts\dotnet.ps1 test tests\ProjectManager.Tests\ProjectManager.Tests.csproj --filter FullyQualifiedName~SmokeTests`

Expected: existing pages continue to render.

### Task 5: EF Migration and Full Verification

**Files:**
- Create: `src/ProjectManager.Web/Migrations/<timestamp>_AddProjectAuditTrail.cs`
- Modify: `src/ProjectManager.Web/Migrations/ApplicationDbContextModelSnapshot.cs`

- [ ] **Step 1: Generate migration**

Run: `.\scripts\dotnet.ps1 ef migrations add AddProjectAuditTrail --project src\ProjectManager.Web\ProjectManager.Web.csproj --startup-project src\ProjectManager.Web\ProjectManager.Web.csproj`

- [ ] **Step 2: Run full tests and build**

Run: `.\scripts\dotnet.ps1 test ProjectManager.sln`

Run: `.\scripts\dotnet.ps1 build ProjectManager.sln`

Expected: both commands finish with exit code 0.
