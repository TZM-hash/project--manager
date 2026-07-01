# 项目管理 ASP.NET 系统设计

## 1. 目标

建设一个精简、高效的 C# ASP.NET Core 内部项目管理系统。系统支持多人登录、角色权限、项目跟踪、请购记录、每月手动结算快照、Excel 导出、打印报表、项目状态可视化，以及由管理员配置前台显示样式。

第一版面向公司内网部署，数据库使用 SQL Server 或 SQL Server Express。

## 2. 推荐架构

使用一个 ASP.NET Core Web 应用，内部分成两个逻辑入口：

- 后台管理入口：管理员维护用户、角色、项目主档、状态定义和显示样式设置。
- Web 工作台入口：项目人员更新自己负责的项目资料；领导和查询人员查看项目、状态图、月结报表和未结案统计。

技术栈：

- ASP.NET Core Razor Pages：用于服务端渲染页面。
- ASP.NET Core Identity：用于登录、密码加密、修改密码和角色管理。
- Entity Framework Core：用于 SQL Server 数据访问。
- SQL Server / SQL Server Express：作为数据库。
- ClosedXML：用于 Excel 导出。
- 浏览器打印样式：用于打印报表。
- 少量 JavaScript 和 CSS：用于动态状态图和可配置状态颜色。

选择这套技术栈的原因是：内网部署简单，不需要单独的前端构建流程，同时仍然保留后续扩展空间。

## 3. 角色与权限

初始角色：

- Administrator：管理所有用户、角色、项目、状态定义、月结、报表和样式设置。
- ProjectStaff：查看分配给自己的项目，并更新项目进度、进度说明和非财务备注。第一版中，项目人员不编辑财务金额。
- Leader：查看项目列表、项目详情、状态图、月结报表和未结案统计。
- Viewer：只读查看授权范围内的项目和报表页面。

密码规则：

- 用户使用用户名和密码登录。
- 所有用户可以修改自己的密码。
- 管理员可以创建用户、重置密码、启用或停用账号、分配角色。

## 4. 主要功能区域

### 4.1 项目主档

每个项目保存以下信息：

- 年
- 项目工号
- 项目名称
- 专案人员
- 专案进度百分比
- 专案金额
- 专案收款比例
- 进度说明
- 更新人员
- 最后更新时间
- 专案状态 / 结案状态

项目状态必须支持扩展。初始状态包括：

- Created / 已立案
- PurchaseRequested / 已请购
- Coding / 代码中
- TrialRun / 试车中
- WaitingCollection / 待收款
- Closed / 已结案

`已结案` 是特殊的终态标记，默认显示为红色。数据库必须允许以后新增状态，不需要修改代码。

### 4.2 请购记录

每个项目可以有多笔请购记录。

每笔请购记录保存以下信息：

- 请购号
- 请购类型：内购或外购
- 请购人员
- 请购金额
- 付款比例
- 实际已付款
- 备注

请购记录作为项目的子记录保存，因此一个项目可以没有请购记录，也可以有一笔或多笔请购记录。

### 4.3 每月手动结算

系统支持对项目进度和财务数据进行每月手动结算。

要求：

- 用户选择年、月，然后触发结算。
- 同一个月份允许多次结算。
- 每次结算创建一个新的结算批次，不覆盖以前的批次。
- 每个批次保存当时项目数据的快照。
- 快照数据包括：项目进度、项目金额、收款比例、状态、请购汇总、付款比例、实际已付款、进度说明和更新人员。
- 批次保存创建人员和创建时间。

这样可以保留可追溯的月末历史，同时允许后续多次修正和重结。

### 4.4 报表

必需报表：

- 按年、月、结算批次查看月结报表。
- 查看未标记为 `已结案` 的未结案项目报表。
- 按状态、金额、收款比例、请购金额、已付款金额统计未结案项目。
- 项目详情报表，包含请购记录明细。

导出和打印：

- 月结报表和未结案项目报表支持 Excel 导出。
- 报表页面支持浏览器打印，并提供打印专用样式。
- 报表筛选条件包括：年、月、项目工号、项目名称、专案人员、状态、已结案 / 未结案。

默认月结 Excel 栏位：

- 年
- 月
- 批次号
- 项目工号
- 项目名称
- 专案人员
- 项目进度百分比
- 项目金额
- 收款比例
- 状态
- 请购号汇总
- 请购金额合计
- 付款比例汇总
- 实际已付款合计
- 进度说明
- 更新人员
- 来源更新时间

默认未结案 Excel 栏位：

- 年
- 项目工号
- 项目名称
- 专案人员
- 项目进度百分比
- 项目金额
- 收款比例
- 状态
- 请购金额合计
- 实际已付款合计
- 进度说明
- 更新人员
- 最后更新时间

### 4.5 项目动态状态图

每个项目详情页显示一个可视化状态图。

设计规则：

- 按管理员配置的状态顺序显示状态节点。
- 根据项目当前状态高亮已完成状态和当前状态。
- `已结案` 默认显示为红色。
- 后续新增状态从状态定义表读取，不写死在代码中。
- 第一版优先使用服务端渲染，必要时只加入少量 JavaScript 增强。

### 4.6 显示样式设置

管理员可以配置前台状态显示样式：

- 状态文字颜色。
- 状态背景颜色。
- 是否加粗。
- 显示顺序。
- 启用 / 停用。

这些设置会统一用于项目列表、项目详情、状态图和报表。

## 5. 数据模型

核心表：

- AspNetUsers 和 ASP.NET Identity 相关表。
- Projects：项目主档。
- ProjectAssignments：项目人员分配。
- ProjectStatuses：项目状态定义。
- ProjectStatusStyles：项目状态样式。
- PurchaseRequests：请购记录。
- MonthlySettlementBatches：月结批次。
- MonthlySettlementItems：月结快照明细。
- AuditLogs：操作日志。

建议关键字段：

Projects：

- Id
- Year
- ProjectNumber
- Name
- ProgressPercent
- ProjectAmount
- CollectionPercent
- ProgressDescription
- StatusId
- UpdatedByUserId
- UpdatedAt
- CreatedAt
- IsDeleted

ProjectAssignments：

- Id
- ProjectId
- UserId
- RoleInProject

ProjectStatuses：

- Id
- Code
- Name
- SortOrder
- IsClosed
- IsActive

ProjectStatusStyles：

- Id
- StatusId
- TextColor
- BackgroundColor
- IsBold

PurchaseRequests：

- Id
- ProjectId
- RequestNumber
- PurchaseType
- PurchaseStaffUserId
- PurchaseAmount
- PaymentPercent
- ActualPaidAmount
- Notes
- CreatedAt
- UpdatedAt

MonthlySettlementBatches：

- Id
- Year
- Month
- BatchNumber
- CreatedByUserId
- CreatedAt
- Notes

MonthlySettlementItems：

- Id
- BatchId
- ProjectId
- ProjectNumber
- ProjectName
- ProjectPersonnelText
- ProgressPercent
- ProjectAmount
- CollectionPercent
- StatusName
- IsClosed
- PurchaseRequestSummary
- PurchaseAmountTotal
- PaymentPercentSummary
- ActualPaidAmountTotal
- ProgressDescription
- UpdatedByUserName
- SourceUpdatedAt

AuditLogs：

- Id
- UserId
- Action
- EntityName
- EntityId
- Description
- CreatedAt

## 6. 关键流程

登录和密码：

1. 用户登录系统。
2. 系统根据角色显示菜单和页面权限。
3. 用户可以修改自己的密码。
4. 管理员可以重置用户密码并停用账号。

项目维护：

1. 管理员创建或编辑项目。
2. 管理员分配项目人员。
3. 项目人员更新自己负责项目的进度和进度说明。
4. 系统记录更新人员和更新时间。

月度结算：

1. 管理员选择年 / 月。
2. 系统预览本次结算包含的项目。
3. 管理员确认结算。
4. 系统创建新的结算批次和快照明细。
5. 用户从批次页面导出 Excel 或打开打印页面。

未结案统计：

1. 用户选择年 / 月或当前日期。
2. 系统筛选状态不是 `已结案` 的项目。
3. 系统按状态和财务字段进行分组汇总。
4. 用户导出或打印报表。

## 7. 错误处理与校验

校验规则：

- 项目工号必填，并且在同一年内唯一。
- 项目金额、请购金额、实际已付款、项目进度百分比、收款比例、付款比例不能为负数。
- 百分比字段范围为 0 到 100。
- 每笔请购记录必须填写请购号。
- 结算月份必须在 1 到 12 之间。
- 是否结案由 `ProjectStatuses.IsClosed` 控制，不能依赖写死的状态文字。

错误处理：

- 表单页面显示友好的校验提示。
- 未预期错误写入日志。
- 月结创建必须使用事务，确保一个批次要么完整创建成功，要么完全不创建。

## 8. 测试策略

初始自动化测试覆盖：

- 项目创建和校验。
- 一个项目下多笔请购记录。
- 状态样式渲染规则。
- 已结案项目过滤。
- 同一月份多次创建月结批次。
- Excel 导出包含预期的表头和数据行。
- Administrator、ProjectStaff、Leader、Viewer 的角色权限。

手动验证覆盖：

- 登录和修改密码。
- 管理员创建用户和分配角色。
- 项目列表筛选。
- 项目详情状态图。
- 月结 Excel 导出和浏览器打印页面。

## 9. 第一版实施范围

第一版包含：

- ASP.NET Core 项目脚手架。
- SQL Server EF Core 配置。
- Identity 登录和密码管理。
- 初始化角色和初始状态定义。
- 后台项目增删改查。
- 项目下的请购记录子表。
- Web 工作台项目列表和详情。
- 项目状态图。
- 月结批次创建、列表和详情。
- 月结报表和未结案报表 Excel 导出。
- 可打印报表页面。
- 管理员状态样式设置。

第一版不包含：

- 外部客户入口。
- 手机 App。
- 复杂审批流程。
- 实时通知。
- 文件附件管理。
- 独立前端 SPA。

## 10. 实施默认规则

第一版采用以下默认规则：

- 使用克制的内网业务系统风格：中性背景、紧凑表格、可配置状态颜色。
- 使用报表章节列出的默认 Excel 栏位顺序。
- ProjectStaff 只能更新项目进度和进度说明；管理员维护财务字段和请购金额。
- 项目和请购记录使用软删除。
- 默认初始化 `Closed / 已结案` 状态为红色文字并启用加粗。
