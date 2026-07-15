# Permissions And Create Flow Fixes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make every manual create flow provide reliable save feedback and enforce a downward-compatible three-level permission hierarchy where system administrators manage everything, information administrators manage all business data, and regular users remain limited to their assigned scope.

**Architecture:** Keep the existing persisted role names to avoid a database schema migration: `Administrator` is the system administrator, `Leader` is the information administrator, and `ProjectStaff` is the regular user role. Add one claims-principal permission helper as the single hierarchy source, treat `Viewer` as a legacy regular user, and keep `SubCaseContact` as a supplemental business identity. Reuse the helper in page authorization/data-scope checks and add shared flash/validation feedback for create forms.

**Tech Stack:** ASP.NET Core Razor Pages, ASP.NET Core Identity, Entity Framework Core, xUnit, FluentAssertions, WebApplicationFactory, JavaScript modules.

---

### Task 1: Permission Hierarchy

**Files:**
- Create: `src/ProjectManager.Web/Security/PermissionExtensions.cs`
- Modify: `src/ProjectManager.Web/Security/RoleNames.cs`
- Modify: `src/ProjectManager.Web/Data/SeedData.cs`
- Test: `tests/ProjectManager.Tests/Security/PermissionExtensionsTests.cs`
- Test: `tests/ProjectManager.Tests/Data/SeedDataTests.cs`

- [ ] Write failing tests proving system administrators inherit information-manager and regular-user capabilities, information administrators inherit regular-user capabilities, viewers do not receive global access, and sub-case contact remains supplemental.
- [ ] Run the targeted tests and confirm they fail because the hierarchy helper and updated role labels do not exist.
- [ ] Add `IsSystemAdministrator`, `IsInformationAdministrator`, `CanManageAllBusinessData`, and `IsRegularUser` helpers; update role display names and seed compatibility memberships for legacy viewer/sub-case-contact accounts.
- [ ] Run the targeted tests and confirm they pass.

### Task 2: Page Authorization And Data Scope

**Files:**
- Modify: business-data pages under `Pages/Admin`, `Pages/Workbench`, `Pages/Reports`, `Pages/Settlements`, and `Pages/Index.cshtml.cs`
- Test: `tests/ProjectManager.Tests/Web/PermissionHierarchySmokeTests.cs`

- [ ] Write failing web tests proving `Leader` can access and edit vendors, statuses, maintenance orders, assignments, archives, data exchange, projects, planning projects, and settlements while remaining blocked from users, system settings, and operational administration.
- [ ] Write failing tests proving administrators see all planning projects and regular/viewer users only see assigned planning/project data.
- [ ] Run the tests and confirm the current administrator-only attributes and viewer global-scope checks fail the required behavior.
- [ ] Replace repeated role checks with the hierarchy helper, include information administrators on business-data pages, remove viewer global visibility, and preserve system-only pages.
- [ ] Run the targeted tests and confirm they pass.

### Task 3: Reliable Manual Create Flows

**Files:**
- Modify: create handlers/views for projects, planning projects, users, vendors, statuses, maintenance orders, and settlements
- Modify: `src/ProjectManager.Web/Pages/Shared/_Layout.cshtml`
- Create: `src/ProjectManager.Web/Pages/Shared/_FlashMessages.cshtml`
- Modify: `src/ProjectManager.Web/wwwroot/js/core/feedback.js`
- Test: `tests/ProjectManager.Tests/Web/CreateFlowRegressionTests.cs`
- Test: `tests/ProjectManager.Tests/Web/ProjectUiRegressionTests.cs`

- [ ] Write failing tests proving every create form has an all-errors summary and every successful handler sets a success message before redirecting.
- [ ] Write failing tests proving project required/range errors bind to their fields and planning-project list scope does not hide newly created records from administrators.
- [ ] Run the tests and confirm the current model-only summaries, missing flash messages, project model-level validation, and planning default filter fail.
- [ ] Add shared flash rendering, validation-error focus/scroll behavior, field-specific project validation, create success messages, and role-aware planning defaults.
- [ ] Run the targeted tests and confirm they pass.

### Task 4: Verification

**Files:**
- Verify all modified source and test files.

- [ ] Run focused permission and create-flow tests.
- [ ] Run the full `ProjectManager.Tests` suite in Release mode.
- [ ] Build `ProjectManager.sln` in Release mode.
- [ ] Inspect `git diff` and confirm no unrelated files or generated application data are included.
