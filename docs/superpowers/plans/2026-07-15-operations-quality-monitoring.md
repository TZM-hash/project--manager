# Operations Quality Monitoring and Job Center Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add repeatable accessibility gates, operational monitoring and a durable progress center for imports, exports and bulk tasks.

**Architecture:** Persist `OperationJob` records in SQL Server, process them through a hosted worker and operation handlers, store files under a constrained `App_Data` root, and expose health/operations pages restricted by role. Monitoring degrades to Unknown when infrastructure permissions are unavailable.

**Tech Stack:** ASP.NET Core Health Checks, BackgroundService, EF Core 9, System.Threading.Channels/database polling, PowerShell, Playwright, xUnit.

---

## Task 1: OperationJob Schema

**Files:**
- Create: `Models/OperationJob.cs`
- Modify: `Models/ApplicationUser.cs`, `Data/ApplicationDbContext.cs`
- Create: `Migrations/*_AddOperationJobs.cs`
- Modify data-model tests.

- [ ] Write failing tests for fields, status enum, rowversion, user relationship and status/created indexes.
- [ ] Implement the model/mapping, generate Migration and verify additive-only SQL.
- [ ] Run data tests.

## Task 2: Job Queue and Worker

**Files:**
- Create: `Services/Operations/OperationJobService.cs`
- Create: `Services/Operations/OperationJobWorker.cs`
- Create: `Services/Operations/OperationHandlers.cs`
- Create: `Services/Operations/OperationFileStore.cs`
- Modify: `Program.cs`
- Create operation service tests.

- [ ] Write failing tests for queue, claim, progress, success, failure, recovery, ownership and safe file paths.
- [ ] Implement persisted state transitions and worker heartbeat.
- [ ] Implement handlers for full import/export and project/maintenance bulk delete.
- [ ] Register hosted service and scoped handlers; rerun tests.

## Task 3: Progress Center UI

**Files:**
- Create: `Pages/Operations/Index.cshtml(.cs)`, `Pages/Operations/Status.cshtml.cs`, `Pages/Operations/Download.cshtml.cs`
- Create: `wwwroot/js/pages/operation-center.js`
- Modify source operation pages and navigation.
- Add page/asset tests.

- [ ] Write failing tests for role/owner isolation, queue redirects, polling hooks, progress semantics and download authorization.
- [ ] Replace synchronous full import/export and two bulk delete handlers with queued jobs.
- [ ] Render status, percentage, timestamps, summary, bounded errors and download action.
- [ ] Add conditional JS loading and keyboard-safe live updates.

## Task 4: Monitoring and Exception Logs

**Files:**
- Create: `Services/Operations/OperationalHealthService.cs`
- Create: `Services/Operations/ExceptionLogStore.cs`
- Create: `Middleware/ExceptionLogMiddleware.cs`
- Create: `Pages/Admin/Operations/Index.cshtml(.cs)`
- Modify: `Program.cs`, navigation and tests.

- [ ] Write failing tests for database, backup Unknown fallback, disk thresholds, exception counts and worker heartbeat.
- [ ] Add `/health/live` and `/health/ready` endpoints.
- [ ] Implement UTF-8 JSONL exception storage and admin monitoring page.
- [ ] Run monitoring tests.

## Task 5: Quality Gate

**Files:**
- Create: `scripts/quality-gate.ps1`
- Modify: `tests/visual/project-ui.spec.js`
- Create: `docs/2026-07-15-第五批运维任务中心说明.md`

- [ ] Add keyboard, 720px, contrast and progress-live-region Playwright cases.
- [ ] Implement PowerShell gate with `$ErrorActionPreference = 'Stop'`, UTF-8-safe paths and existing local package cache.
- [ ] Document backup permissions, health endpoints, log/file cleanup and job recovery.
- [ ] Run JS checks, Release build, full tests, health probes and Playwright discovery/execution where environment permits.
- [ ] Commit with `feat: 增加运维监控与任务进度中心`.
