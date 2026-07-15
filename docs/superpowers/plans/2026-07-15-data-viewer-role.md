# 資料查看員角色實施計劃

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 新增可查看全部業務資料但完全不可寫入的「資料查看員」，並將舊 `Viewer` 帳號遷移到目前角色體系。

**Architecture:** 在角色層新增 `DataViewer` 主角色，將「查看全部業務資料」與「管理全部業務資料」拆成兩個 ClaimsPrincipal 能力。所有資料查詢改用查看能力，所有 UI 操作與 POST handler 保持管理能力或一般使用者既有的指派編輯規則；啟動種子流程負責移除舊 `Viewer` 成員關係及角色定義。

**Tech Stack:** ASP.NET Core Razor Pages、ASP.NET Core Identity、Entity Framework Core、xUnit、FluentAssertions、WebApplicationFactory。

---

### Task 1: 角色定義、能力拆分與舊角色遷移

**Files:**
- Modify: `src/ProjectManager.Web/Security/RoleNames.cs`
- Modify: `src/ProjectManager.Web/Security/PermissionExtensions.cs`
- Modify: `src/ProjectManager.Web/Security/RoleSelection.cs`
- Modify: `src/ProjectManager.Web/Data/SeedData.cs`
- Modify: `tests/ProjectManager.Tests/Security/PermissionExtensionsTests.cs`
- Modify: `tests/ProjectManager.Tests/Security/RoleSelectionTests.cs`
- Modify: `tests/ProjectManager.Tests/Data/SeedDataTests.cs`

- [ ] **Step 1: 寫入失敗測試**

新增断言：

```csharp
RoleNames.All.Should().Contain(RoleNames.DataViewer);
RoleNames.Assignable.Should().Contain(RoleNames.DataViewer);
RoleNames.PrimaryRoles.Should().Contain(RoleNames.DataViewer);
RoleNames.GetDisplayName(RoleNames.DataViewer).Should().Be("資料查看員");

CreatePrincipal(RoleNames.DataViewer).CanViewAllBusinessData().Should().BeTrue();
CreatePrincipal(RoleNames.DataViewer).CanManageAllBusinessData().Should().BeFalse();

RoleSelection.Normalize([RoleNames.DataViewer]).Succeeded.Should().BeTrue();
RoleSelection.Normalize([RoleNames.DataViewer, RoleNames.ProjectStaff]).Succeeded.Should().BeFalse();
```

在种子整合测试中建立旧 `Viewer` 角色和用户，重新执行 `SeedData.EnsureSeededAsync` 后断言：

```csharp
(await userManager.IsInRoleAsync(user, RoleNames.ProjectStaff)).Should().BeTrue();
(await userManager.IsInRoleAsync(user, RoleNames.LegacyViewer)).Should().BeFalse();
(await roleManager.RoleExistsAsync(RoleNames.LegacyViewer)).Should().BeFalse();
```

- [ ] **Step 2: 运行测试并确认失败**

Run:

```powershell
$ErrorActionPreference = 'Stop'
dotnet test tests/ProjectManager.Tests/ProjectManager.Tests.csproj --filter "FullyQualifiedName~PermissionExtensionsTests|FullyQualifiedName~RoleSelectionTests|FullyQualifiedName~SeedDataTests" --no-restore
```

Expected: FAIL，因为 `DataViewer`、`CanViewAllBusinessData` 和旧角色迁移尚不存在。

- [ ] **Step 3: 实现最小角色与迁移代码**

在 `RoleNames` 中加入：

```csharp
public const string DataViewer = "DataViewer";
public const string LegacyViewer = "Viewer";
public const string BusinessManagerRoles = Administrator + "," + Leader;
public const string FullBusinessReadRoles = Administrator + "," + Leader + "," + DataViewer;
public const string BusinessDataRoles = Administrator + "," + Leader + "," + DataViewer + "," + ProjectStaff + "," + SubCaseContact;
```

`DataViewer` 加入 `All`、`Assignable`、`PrimaryRoles`，`LegacyViewer` 不加入这些集合。`PermissionExtensions` 新增：

```csharp
public static bool CanViewAllBusinessData(this ClaimsPrincipal user)
{
    return user.CanManageAllBusinessData() || user.IsInRole(RoleNames.DataViewer);
}
```

种子流程在补齐 `SubCaseContact` 主角色后执行旧 Viewer 迁移：没有主角色的旧 Viewer 加入 `ProjectStaff`，随后从所有用户移除 `Viewer` 并删除旧角色。

- [ ] **Step 4: 运行定向测试确认通过**

Run: 与 Step 2 相同。

Expected: PASS。

### Task 2: 全量查看范围与只读页面授权

**Files:**
- Modify: `src/ProjectManager.Web/Pages/Index.cshtml.cs`
- Modify: `src/ProjectManager.Web/Pages/Workbench/Projects/Index.cshtml.cs`
- Modify: `src/ProjectManager.Web/Pages/Workbench/Projects/Index.cshtml`
- Modify: `src/ProjectManager.Web/Pages/Workbench/Projects/Details.cshtml.cs`
- Modify: `src/ProjectManager.Web/Pages/Workbench/PlanningProjects/Index.cshtml.cs`
- Modify: `src/ProjectManager.Web/Pages/Workbench/PlanningProjects/Index.cshtml`
- Modify: `src/ProjectManager.Web/Pages/Workbench/PlanningProjects/Print.cshtml.cs`
- Modify: `src/ProjectManager.Web/Pages/Workbench/PlanningProjects/PrintList.cshtml.cs`
- Modify: `src/ProjectManager.Web/Pages/Workbench/Projects/GanttPrint.cshtml.cs`
- Modify: `src/ProjectManager.Web/Pages/Reports/OpenProjects/Index.cshtml.cs`
- Modify: `src/ProjectManager.Web/Pages/Reports/OpenProjects/Print.cshtml.cs`
- Modify: `src/ProjectManager.Web/Pages/Reports/OpenProjects/Statistics.cshtml.cs`
- Modify: `tests/ProjectManager.Tests/Web/PermissionHierarchySmokeTests.cs`
- Modify: `tests/ProjectManager.Tests/Web/OpenProjectReportSmokeTests.cs`

- [ ] **Step 1: 写入全量只读失败测试**

新增 `DataViewer` 客户端测试：

```csharp
var client = CreateClient(factory, RoleNames.DataViewer);
var response = await client.GetAsync("/Workbench/Projects");
var html = await response.Content.ReadAsStringAsync();
html.Should().Contain("MY-SCOPED-PROJECT");
html.Should().Contain("OTHER-SCOPED-PROJECT");
```

同样覆盖规划中专案、专案明细、甘特列印和未结案报表，并断言编辑链接不存在。

- [ ] **Step 2: 运行测试并确认失败**

Run:

```powershell
$ErrorActionPreference = 'Stop'
dotnet test tests/ProjectManager.Tests/ProjectManager.Tests.csproj --filter "FullyQualifiedName~PermissionHierarchySmokeTests|FullyQualifiedName~OpenProjectReportSmokeTests" --no-restore
```

Expected: FAIL，资料查看员尚未获授权或仍被限制为当前用户资料。

- [ ] **Step 3: 拆分查看和编辑判断**

所有读取范围使用：

```csharp
var canViewAll = User.CanViewAllBusinessData();
CanEditAll = User.CanManageAllBusinessData();
```

一般使用者的指派编辑另以 `User.IsInRole(RoleNames.ProjectStaff)` 控制，避免资料查看员即使出现在指派资料中也获得编辑按钮。只读页面的 `[Authorize]` 改用包含 `DataViewer`、不包含旧 `Viewer` 的角色常数。

- [ ] **Step 4: 运行定向测试确认通过**

Run: 与 Step 2 相同。

Expected: PASS。

### Task 3: 保养订单、月结、归档与导航只读入口

**Files:**
- Modify: `src/ProjectManager.Web/Pages/Shared/_SidebarNavigation.cshtml`
- Modify: `src/ProjectManager.Web/Pages/Admin/MaintenanceOrders/Index.cshtml.cs`
- Modify: `src/ProjectManager.Web/Pages/Admin/MaintenanceOrders/Index.cshtml`
- Modify: `src/ProjectManager.Web/Pages/Settlements/Index.cshtml.cs`
- Modify: `src/ProjectManager.Web/Pages/Settlements/Index.cshtml`
- Modify: `src/ProjectManager.Web/Pages/Settlements/Details.cshtml.cs`
- Modify: `src/ProjectManager.Web/Pages/Settlements/Print.cshtml.cs`
- Modify: `src/ProjectManager.Web/Pages/Admin/Archives/Index.cshtml.cs`
- Modify: `tests/ProjectManager.Tests/Web/PermissionHierarchySmokeTests.cs`
- Modify: `tests/ProjectManager.Tests/Web/SettlementPageSmokeTests.cs`

- [ ] **Step 1: 写入业务入口与写入拒绝测试**

测试资料查看员可 GET：

```text
/Admin/MaintenanceOrders
/Settlements
/Admin/Archives
```

并断言页面不含新增、编辑、删除、生成月结等操作。对保养订单批次删除或月结写入 handler 提交有效 antiforgery 请求，断言 `HttpStatusCode.Forbidden`。

同时断言以下系统页面返回 Forbidden：

```text
/Admin/Users
/Admin/Statuses
/Admin/Vendors
/Admin/DataExchange
/Admin/Operations
/Admin/Settings
```

- [ ] **Step 2: 运行测试并确认失败**

Run:

```powershell
$ErrorActionPreference = 'Stop'
dotnet test tests/ProjectManager.Tests/ProjectManager.Tests.csproj --filter "FullyQualifiedName~PermissionHierarchySmokeTests|FullyQualifiedName~SettlementPageSmokeTests" --no-restore
```

Expected: FAIL，因为只读业务入口尚未包含 `DataViewer`。

- [ ] **Step 3: 实现只读入口和 handler 保护**

维护页模型加入明确能力：

```csharp
public bool CanManageBusinessData { get; private set; }

CanManageBusinessData = User.CanManageAllBusinessData();
```

页面操作区以该属性包裹。所有 Index 页面写入 handler 首行加入：

```csharp
if (!User.CanManageAllBusinessData())
{
    return Forbid();
}
```

侧栏使用 `canViewAllBusinessData` 显示保养订单、月结与归档，继续只用 `canManageBusinessData` 显示专案分配及系统管理入口。

- [ ] **Step 4: 运行定向测试确认通过**

Run: 与 Step 2 相同。

Expected: PASS。

### Task 4: 使用者管理角色选项与完整回归

**Files:**
- Modify: `src/ProjectManager.Web/Pages/Admin/Users/Create.cshtml`
- Modify: `src/ProjectManager.Web/Pages/Admin/Users/Edit.cshtml`
- Modify: `tests/ProjectManager.Tests/Web/ChineseInterfaceSmokeTests.cs`
- Modify: `tests/ProjectManager.Tests/Web/ProjectUiRegressionTests.cs`

- [ ] **Step 1: 写入角色显示失败测试**

断言新增及编辑使用者页面包含「資料查看員」，主角色验证文字包含四个主角色，并且不再显示「旧查询角色」。

- [ ] **Step 2: 运行测试并确认失败**

Run:

```powershell
$ErrorActionPreference = 'Stop'
dotnet test tests/ProjectManager.Tests/ProjectManager.Tests.csproj --filter "FullyQualifiedName~ChineseInterfaceSmokeTests|FullyQualifiedName~ProjectUiRegressionTests" --no-restore
```

Expected: FAIL，因为新角色尚未出现在界面规则中。

- [ ] **Step 3: 完成界面文字并运行完整验证**

角色页面沿用 `RoleNames.Assignable` 与 `GetDisplayName`，更新说明文字为四个主角色。运行：

```powershell
$ErrorActionPreference = 'Stop'
dotnet test tests/ProjectManager.Tests/ProjectManager.Tests.csproj --no-restore
git diff --check
```

Expected: 全部测试通过，`git diff --check` 退出码为 0。

- [ ] **Step 4: 浏览器验收并保持服务运行**

启动服务后以资料查看员帐号验证：

1. 可看到所有专案、规划中专案、保养订单、月结、报表及归档。
2. 筛选条件可正常使用。
3. 页面无新增、编辑、删除或导入按钮。
4. 手动访问写入页面返回禁止访问。
5. 系统管理入口不可见且直接访问被拒绝。

本计划不执行 Git 提交或推送；只有用户另行明确要求时才进行 Git 历史或远端操作。
