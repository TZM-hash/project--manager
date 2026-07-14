# 项目协作记录附件与重要记录实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**目标：** 在现有项目协作记录时间线中增加重要记录与附件能力，并与系统审计记录保持独立展示；不实现项目评论和 `@` 提醒。

**架构：** 协作记录继续使用 `ProjectCollaborationRecord`，新增 `IsImportant` 标记和 `ProjectCollaborationAttachment` 子实体。附件保存到独立的 `App_Data/collaboration-attachments` 目录，下载必须经过项目访问权限和记录归属校验。项目详情页保留 Audit 与 Collaboration 两个独立 Tab。

**技术栈：** ASP.NET Core Razor Pages、EF Core SQL Server、现有项目权限模型、SQL Server rowversion、现有 Playwright 门禁。

---

### Task 1：数据模型与迁移

**文件：**

- 新增 `src/ProjectManager.Web/Models/ProjectCollaborationAttachment.cs`
- 修改 `src/ProjectManager.Web/Models/ProjectCollaborationRecord.cs`
- 修改 `src/ProjectManager.Web/Models/ApplicationUser.cs`
- 修改 `src/ProjectManager.Web/Data/ApplicationDbContext.cs`
- 新增 EF Migration
- 测试 `tests/ProjectManager.Tests/Data/ApplicationDbContextTests.cs`

- [ ] 为协作记录增加 `IsImportant` 与附件导航。
- [ ] 新增附件的原始名称、存储相对路径、内容类型、长度、创建者、创建时间和外键索引。
- [ ] 生成 Migration 并确认无待处理模型变更。

### Task 2：服务、文件存储与权限

**文件：**

- 修改 `src/ProjectManager.Web/Services/ProjectCollaborationService.cs`
- 新增协作附件存储服务
- 修改项目详情 PageModel 与下载 Handler
- 测试 `tests/ProjectManager.Tests/Services/ProjectCollaborationServiceTests.cs`

- [ ] 新增重要筛选、附件上传、删除和下载权限检查。
- [ ] 限制附件扩展名、大小和文件名，阻止目录穿越。
- [ ] 保存和删除记录时维持并发保护，不把协作动态写入 `AuditLogs`。

### Task 3：项目详情 UI

**文件：**

- 修改 `src/ProjectManager.Web/Pages/Shared/_ProjectCollaborationPanel.cshtml`
- 修改 `src/ProjectManager.Web/Pages/Workbench/Projects/Details.cshtml.cs`
- 修改必要的 CSS 与 JS

- [ ] 增加重要标记、重要筛选、附件选择和附件列表。
- [ ] 协作记录与系统审计分别显示标题、说明和时间线。
- [ ] 空状态明确说明下一步；不显示评论或 `@` 功能入口。

### Task 4：验证与文档

- [ ] 增加服务权限、附件路径、重要筛选与并发测试。
- [ ] 增加协作附件 Playwright 流程和审计分离断言。
- [ ] 更新架构、数据字典、维护说明。
- [ ] 执行 Debug/Release 测试、JS 检查、Playwright、Migration 检查并提交本地 Git。
